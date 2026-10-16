using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Sync
{
    // corpo de um pedestre do próprio jogo, com as animações dele e a pose sentada dos motoristas NPC
    internal class Avatar
    {
        const string WalkerPath = "Randomizer/Walkers/Kale/Pivot/Char";
        const string DriverPath = "KUSKI/LOD/Driver/skeleton";
        const string HeadPath = "skeleton/pelvis/spine_middle/spine_upper/HeadPivot/head";
        const string Walk = "fat_walk";
        const string Stand = "fat_standing";
        const float WalkAnimSpeed = 1.4f;
        const float NameHeight = 2.1f;
        const float EyesAhead = 0.08f;
        const float DefaultFeetBelowPivot = 0.28f;
        const float SkinWidth = 0.03f;

        static readonly Dictionary<int, string[]> Outfits = new Dictionary<int, string[]>
        {
            { 1, new[] { "char_shirt01", "char_pants01", "char_face01" } },
            { 2, new[] { "char_shirt13", "char_pants05", "char_face03" } },
        };

        // na moto as pernas descem até a pedaleira em vez de esticar para a frente como no carro
        internal static readonly Dictionary<string, Vector3> BikeLegs = new Dictionary<string, Vector3>
        {
            { "thig_left", new Vector3(0f, 0f, 20f) },
            { "thig_right", new Vector3(0f, 0f, 20f) },
            { "thig_left/knee_left", new Vector3(0f, 0f, 40f) },
            { "thig_right/knee_right", new Vector3(0f, 0f, 40f) },
        };

        static GameObject template;
        static Dictionary<string, KeyValuePair<Vector3, Quaternion>> seatedPose;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static float feetBelowPivot = -1f;

        readonly string name;
        GameObject root;
        TextMesh label;
        Transform body;
        Transform head;
        SkinnedMeshRenderer mesh;
        Animation legs;
        Animation[] animations;
        readonly List<KeyValuePair<Transform, string>> bones = new List<KeyValuePair<Transform, string>>();
        int skin;
        int wanted = 1;
        bool seated;
        Vector3 lastFeet;
        bool hasLast;
        float speed;

        internal Avatar(string name)
        {
            this.name = name;
        }

        void Build()
        {
            root = new GameObject("MscDuo_Avatar_" + name);
            skin = 0;
            seated = false;
            hasLast = false;

            var tag = new GameObject("Name");
            tag.transform.parent = root.transform;
            tag.transform.localPosition = new Vector3(0f, NameHeight, 0f);
            label = tag.AddComponent<TextMesh>();
            label.text = name;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.018f;
            label.fontSize = 48;
            label.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.font = font;
            tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            if (!LoadTemplate()) return;
            var copy = (GameObject)Object.Instantiate(template);
            copy.name = "Char";
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(behaviour);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            body = copy.transform;
            body.parent = root.transform;
            body.localPosition = template.transform.localPosition;
            body.localRotation = template.transform.localRotation;
            body.localScale = template.transform.localScale;
            copy.SetActive(true);

            head = body.Find(HeadPath);
            mesh = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true)[0];
            mesh.enabled = true;
            mesh.updateWhenOffscreen = true;
            var skeleton = body.Find("skeleton");
            legs = skeleton != null ? skeleton.GetComponent<Animation>() : null;
            animations = copy.GetComponentsInChildren<Animation>(true);
            foreach (var anim in animations)
            {
                anim.enabled = true;
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.wrapMode = WrapMode.Loop;
                if (anim == legs) continue;
                // respiração e mãos paradas: o primeiro clipe de cada osso é o de repouso
                foreach (AnimationState state in anim) { anim.Play(state.name); break; }
            }
            if (legs != null && legs[Stand] != null) legs.Play(Stand);

            bones.Clear();
            if (skeleton != null) Collect(skeleton, "");
        }

        void Collect(Transform t, string path)
        {
            foreach (Transform child in t)
            {
                var childPath = path.Length == 0 ? child.name : path + "/" + child.name;
                bones.Add(new KeyValuePair<Transform, string>(child, childPath));
                Collect(child, childPath);
            }
        }

        internal void SetSkin(int value)
        {
            wanted = value;
            if (root == null || mesh == null || value == skin) return;
            string[] outfit;
            if (!Outfits.TryGetValue(value, out outfit)) return;
            var current = mesh.sharedMaterials;
            for (int i = 0; i < outfit.Length && i < current.Length; i++)
            {
                var material = FindMaterial(outfit[i]);
                if (material != null) current[i] = material;
            }
            mesh.sharedMaterials = current;
            skin = value;
        }

        internal void Apply(Vector3 pivot, float yaw, float pitch)
        {
            if (!Ready()) return;
            if (seated) GetUp();
            var feet = pivot - Vector3.up * FeetBelowPivot();
            root.transform.parent = null;
            root.transform.position = feet;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var dt = Time.deltaTime;
            if (hasLast && dt > 0f)
            {
                var moved = feet - lastFeet;
                moved.y = 0f;
                speed = Mathf.Lerp(speed, moved.magnitude / dt, Mathf.Clamp01(dt * 8f));
            }
            lastFeet = feet;
            hasLast = true;

            if (legs != null && legs[Walk] != null)
            {
                if (speed > 0.3f)
                {
                    legs[Walk].speed = Mathf.Clamp(speed / WalkAnimSpeed, 0.6f, 3f);
                    legs.CrossFade(Walk, 0.2f);
                }
                else legs.CrossFade(Stand, 0.3f);
            }
            FaceCamera();
        }

        internal void Sit(Transform vehicle, Vector3 headWorld, bool bike)
        {
            if (!Ready()) return;
            if (!seated)
            {
                seated = true;
                hasLast = false;
                foreach (var anim in animations) { anim.Stop(); anim.enabled = false; }
            }
            if (seatedPose != null)
                foreach (var bone in bones)
                {
                    KeyValuePair<Vector3, Quaternion> pose;
                    if (!seatedPose.TryGetValue(bone.Value, out pose)) continue;
                    Vector3 bend;
                    bone.Key.localPosition = pose.Key;
                    bone.Key.localRotation = bike && BikeLegs.TryGetValue(bone.Value, out bend) ? pose.Value * Quaternion.Euler(bend) : pose.Value;
                }
            // preso ao veículo local: acompanha a carroceria sem tremer entre pacotes
            root.transform.parent = vehicle;
            root.transform.rotation = vehicle.rotation;
            root.transform.position = headWorld;
            if (head != null) root.transform.position += headWorld - vehicle.forward * EyesAhead - head.position;
            FaceCamera();
        }

        void GetUp()
        {
            seated = false;
            foreach (var anim in animations)
            {
                anim.enabled = true;
                if (anim == legs) continue;
                foreach (AnimationState state in anim) { anim.Play(state.name); break; }
            }
            if (legs != null && legs[Stand] != null) legs.Play(Stand);
        }

        bool Ready()
        {
            if (root == null)
            {
                Build();
                SetSkin(wanted);
            }
            return body != null;
        }

        void FaceCamera()
        {
            var camera = Camera.main;
            if (camera != null)
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position);
        }

        internal void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        static bool LoadTemplate()
        {
            if (template != null) return true;
            var humans = FindRoot("HUMANS");
            var walker = humans != null ? humans.Find(WalkerPath) : null;
            if (walker == null)
            {
                ModConsole.Warning("[MSC Duo] pedestre HUMANS/" + WalkerPath + " não encontrado; avatar sem corpo.");
                return false;
            }
            template = walker.gameObject;

            var cars = FindRoot("NPC_CARS");
            var driver = cars != null ? cars.Find(DriverPath) : null;
            seatedPose = null;
            if (driver != null)
            {
                seatedPose = new Dictionary<string, KeyValuePair<Vector3, Quaternion>>();
                Record(driver, "");
            }
            else ModConsole.Warning("[MSC Duo] motorista NPC_CARS/" + DriverPath + " não encontrado; avatar sentado fica na pose em pé.");
            return true;
        }

        static void Record(Transform t, string path)
        {
            foreach (Transform child in t)
            {
                var childPath = path.Length == 0 ? child.name : path + "/" + child.name;
                seatedPose[childPath] = new KeyValuePair<Vector3, Quaternion>(child.localPosition, child.localRotation);
                Record(child, childPath);
            }
        }

        static Transform FindRoot(string rootName)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null && t.hideFlags == HideFlags.None && t.name == rootName) return t;
            return null;
        }

        static Material FindMaterial(string materialName)
        {
            Material found;
            if (materials.TryGetValue(materialName, out found) && found != null) return found;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                if (m.name == materialName) { materials[materialName] = m; return m; }
            ModConsole.Warning("[MSC Duo] material " + materialName + " não encontrado.");
            return null;
        }

        static float FeetBelowPivot()
        {
            if (feetBelowPivot >= 0f) return feetBelowPivot;
            var player = GameObject.Find("PLAYER");
            var controller = player != null ? player.GetComponent<CharacterController>() : null;
            if (controller == null || !controller.enabled) return DefaultFeetBelowPivot;
            feetBelowPivot = player.transform.position.y - controller.bounds.min.y + SkinWidth;
            return feetBelowPivot;
        }
    }
}
