using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class Warehouse
    {
        const string BuildingResource = "MscDuo.galpao_jogo.obj";
        const string PlatformResource = "MscDuo.plataforma_jogo.obj";
        const string TexturePrefix = "MscDuo.galpao.";
        const string GravelPath = "MAP/MESH/TERRAIN_OBJ/Gravel";
        const string GrassPath = "MAP/MESH/TERRAIN_OBJ/Grass1";
        const float TreeMargin = 2f;
        const string SwitchSource = "switch_kitchen";
        const string SwitchTag = "MscDuoGalpaoLuz";
        static readonly Vector3 Origin = new Vector3(-30f, -0.497f, 13f);

        static readonly Vector4[] Lamps =
        {
            new Vector4(6f, 3f, 6f, 14f), new Vector4(12f, 3f, 7f, 14f), new Vector4(18f, 3f, 6f, 14f),
            new Vector4(6f, 7.5f, 6f, 14f), new Vector4(12f, 7.5f, 7f, 14f), new Vector4(18f, 7.5f, 6f, 14f),
            new Vector4(3f, 12.9f, 2.8f, 7f), new Vector4(9f, 12.9f, 2.8f, 7f), new Vector4(15f, 12.9f, 2.8f, 7f),
            new Vector4(4f, 13f, 6f, 9f), new Vector4(12f, 13f, 7f, 9f), new Vector4(20f, 13f, 6f, 9f),
        };

        static readonly string[] Solid = { "Piso", "Paredes", "Mezanino", "Escada", "Salas", "GuardaCorpo", "Estrutura", "Telhado", "Janelas" };
        static readonly string[] Gates = { "Portao1", "Portao2" };

        static GameObject root;

        internal static GameObject Root => root;

        internal static void Spawn()
        {
            if (root != null) Object.Destroy(root);
            var building = Parse(Resource(BuildingResource));
            var platform = Parse(Resource(PlatformResource));
            if (building == null || platform == null)
            {
                ModConsole.Warning("[MSC Duo] galpão não encontrado na DLL.");
                return;
            }
            root = new GameObject("MscDuo_Galpao");
            var template = Template();
            var textures = new Dictionary<string, Material>();
            int parts = 0, colliders = 0;
            var bounds = new Bounds();
            var first = true;

            foreach (var part in building)
            {
                var materials = new List<Material>();
                foreach (var name in part.Materials) materials.Add(BuildingMaterial(name, template, textures));
                var go = Create(part, materials.ToArray(), root.transform);
                parts++;
                if (System.Array.IndexOf(Gates, part.Name) >= 0) go.SetActive(false);
                else if (System.Array.IndexOf(Solid, part.Name) >= 0)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    colliders++;
                }
                var b = go.GetComponent<Renderer>().bounds;
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }

            var gravel = GameObject.Find(GravelPath);
            var grass = GameObject.Find(GrassPath);
            foreach (var part in platform)
            {
                var materials = new Material[part.Materials.Count];
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = part.Materials[i] == "cascalho" ? gravel : grass;
                    var renderer = source != null ? source.GetComponent<Renderer>() : null;
                    materials[i] = renderer != null ? renderer.sharedMaterial : template;
                }
                var go = Create(part, materials, root.transform);
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                if (gravel != null)
                {
                    go.layer = gravel.layer;
                    go.tag = gravel.tag;
                    var surface = gravel.GetComponent<Collider>();
                    if (surface != null) collider.sharedMaterial = surface.sharedMaterial;
                }
                parts++;
            }

            var lights = BuildLights(root.transform);
            var lamp = root.transform.Find("Galpao_Luzes");
            if (lamp != null) lamp.gameObject.AddComponent<LampState>().Lights = lights;
            var light = Switch(root.transform, lights);

            var cleared = TreeClearer.ClearArea(bounds.min - new Vector3(TreeMargin, 0f, TreeMargin), bounds.max + new Vector3(TreeMargin + 12f, 0f, TreeMargin));
            ModConsole.Print($"<color=orange>[MSC Duo]</color> galpão: {parts} peças, {colliders} com colisão, {textures.Count} materiais, {cleared} árvores removidas, interruptor {(light ? "ok" : "não encontrado")}; centro {HouseCloner.Format(bounds.center)}.");
        }

        static Vector3 Local(float x, float y, float z) => new Vector3(Origin.x - y, Origin.y + z, Origin.z + x);

        static GameObject BuildLights(Transform parent)
        {
            var group = new GameObject("Galpao_Iluminacao");
            group.transform.parent = parent;
            foreach (var lamp in Lamps)
            {
                var go = new GameObject("Lampada");
                go.transform.parent = group.transform;
                go.transform.position = Local(lamp.x, lamp.y, lamp.z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = lamp.w;
                light.intensity = lamp.w > 10f ? 1.4f : 1.0f;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.shadows = LightShadows.None;
            }
            group.SetActive(false);
            return group;
        }

        static bool Switch(Transform parent, GameObject lights)
        {
            PlayMakerFSM source = null;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (fsm != null && fsm.gameObject.name == SwitchSource && fsm.FsmName == "Use" && fsm.gameObject.hideFlags == HideFlags.None && fsm.transform.root.name == "YARD")
                {
                    source = fsm;
                    break;
                }
            if (source == null) return false;

            var original = source.gameObject;
            var wallBehind = WallDirection(original.transform);
            var copy = (GameObject)Object.Instantiate(original, Local(0.21f, 11.9f, 1.3f), original.transform.rotation);
            copy.name = "Galpao_Interruptor";
            copy.transform.parent = parent;
            if (wallBehind.HasValue)
                copy.transform.rotation = Quaternion.FromToRotation(wallBehind.Value, Vector3.back) * original.transform.rotation;
            var map = new Dictionary<Object, Object>();
            SatsumaCloner.MapHierarchy(original.transform, copy.transform, map);
            foreach (var fsm in copy.GetComponents<PlayMakerFSM>())
            {
                FsmTools.EnsureLoaded(fsm);
                SatsumaCloner.Remap(fsm, map);
                var tag = fsm.FsmVariables.FindFsmString("UniqueTagSwitch");
                if (tag != null) tag.Value = SwitchTag;
                var target = fsm.FsmVariables.FindFsmGameObject("Lights");
                if (target != null) target.Value = lights;
                var on = fsm.FsmVariables.FindFsmBool("Switch");
                if (on != null) on.Value = false;
            }
            copy.SetActive(true);
            return true;
        }

        static Vector3? WallDirection(Transform t)
        {
            Vector3? best = null;
            var nearest = 0.6f;
            foreach (var axis in new[] { t.forward, -t.forward, t.right, -t.right })
            {
                var flat = new Vector3(axis.x, 0f, axis.z);
                if (flat.sqrMagnitude < 0.25f) continue;
                flat.Normalize();
                RaycastHit hit;
                if (Physics.Raycast(t.position - flat * 0.05f, flat, out hit, nearest) && !hit.collider.isTrigger)
                {
                    nearest = hit.distance;
                    best = flat;
                }
            }
            return best;
        }

        class LampState : MonoBehaviour
        {
            internal GameObject Lights;
            Renderer lampRenderer;
            Material[] lit;
            Material[] dark;
            bool shown = true;

            void Start()
            {
                lampRenderer = GetComponent<Renderer>();
                lit = lampRenderer.sharedMaterials;
                dark = new Material[lit.Length];
                for (int i = 0; i < lit.Length; i++)
                {
                    if (lit[i] != null && lit[i].name.EndsWith("_luz"))
                    {
                        var shader = Shader.Find("Diffuse") ?? Shader.Find("Legacy Shaders/Diffuse") ?? lit[i].shader;
                        dark[i] = new Material(shader) { name = lit[i].name + "_apagada", color = new Color(0.55f, 0.55f, 0.57f), mainTexture = lit[i].mainTexture };
                    }
                    else dark[i] = lit[i];
                }
            }

            void Update()
            {
                var on = Lights != null && Lights.activeSelf;
                if (on == shown || lampRenderer == null) return;
                shown = on;
                lampRenderer.sharedMaterials = on ? lit : dark;
            }
        }

        static Material Template()
        {
            var concrete = ApronExtension.ConcreteRenderer;
            if (concrete != null && concrete.sharedMaterial != null) return concrete.sharedMaterial;
            return new Material(Shader.Find("Diffuse"));
        }

        static Material BuildingMaterial(string name, Material template, Dictionary<string, Material> cache)
        {
            Material material;
            if (cache.TryGetValue(name, out material)) return material;
            Shader shader = null;
            if (name == "vidro") shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Transparent/Diffuse");
            else if (name == "luz") shader = Shader.Find("Unlit/Texture");
            material = shader != null ? new Material(shader) : new Material(template);
            material.name = "MscDuo_galpao_" + name;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.color = name == "vidro" ? new Color(0.75f, 0.85f, 0.9f, 0.35f) : Color.white;
            var bytes = ResourceBytes(TexturePrefix + name + ".png");
            if (bytes != null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "MscDuo_galpao_" + name, wrapMode = TextureWrapMode.Repeat };
                texture.LoadImage(bytes);
                material.mainTexture = texture;
            }
            cache[name] = material;
            return material;
        }

        static GameObject Create(Part part, Material[] materials, Transform parent)
        {
            var mesh = new Mesh { name = "MscDuo_" + part.Name };
            mesh.vertices = part.Vertices.ToArray();
            mesh.uv = part.Uvs.ToArray();
            mesh.subMeshCount = part.Triangles.Count;
            for (int i = 0; i < part.Triangles.Count; i++) mesh.SetTriangles(part.Triangles[i].ToArray(), i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Galpao_" + part.Name);
            go.transform.parent = parent;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        class Part
        {
            internal string Name;
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector2> Uvs = new List<Vector2>();
            internal readonly List<string> Materials = new List<string>();
            internal readonly List<List<int>> Triangles = new List<List<int>>();
            internal readonly Dictionary<long, int> Index = new Dictionary<long, int>();
        }

        static List<Part> Parse(string text)
        {
            if (text == null) return null;
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var parts = new List<Part>();
            Part current = null;
            List<int> triangles = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(' ');
                switch (p[0])
                {
                    case "v":
                        positions.Add(new Vector3(F(p[1]), F(p[3]), F(p[2])));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(F(p[1]), F(p[2])));
                        break;
                    case "o":
                        current = new Part { Name = line.Substring(2).Trim() };
                        parts.Add(current);
                        triangles = null;
                        break;
                    case "usemtl":
                        if (current == null) break;
                        current.Materials.Add(p[1]);
                        triangles = new List<int>();
                        current.Triangles.Add(triangles);
                        break;
                    case "f":
                        if (current == null || triangles == null) break;
                        var corner = new int[3];
                        for (int k = 0; k < 3; k++)
                        {
                            var ids = p[k + 1].Split('/');
                            int v = int.Parse(ids[0]) - 1, t = ids.Length > 1 && ids[1].Length > 0 ? int.Parse(ids[1]) - 1 : -1;
                            long key = ((long)v << 32) | (uint)t;
                            int index;
                            if (!current.Index.TryGetValue(key, out index))
                            {
                                index = current.Vertices.Count;
                                current.Index[key] = index;
                                current.Vertices.Add(positions[v]);
                                current.Uvs.Add(t >= 0 ? uvs[t] : Vector2.zero);
                            }
                            corner[k] = index;
                        }
                        triangles.Add(corner[0]);
                        triangles.Add(corner[2]);
                        triangles.Add(corner[1]);
                        break;
                }
            }
            return parts;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static string Resource(string name)
        {
            var bytes = ResourceBytes(name);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        static byte[] ResourceBytes(string name)
        {
            using (var stream = typeof(Warehouse).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                return bytes;
            }
        }
    }
}
