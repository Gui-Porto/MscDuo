using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class HouseCloner
    {
        const string OriginalPath = "YARD/Building";
        const string PoseFile = "house2.txt";

        internal static GameObject Original { get; private set; }
        internal static GameObject Clone { get; private set; }

        internal static Vector3 Position { get; private set; }
        internal static float Yaw { get; private set; }

        static readonly Type[] StripOrder =
        {
            typeof(Joint),
            typeof(MonoBehaviour),
            typeof(Rigidbody),
            typeof(AudioSource),
            typeof(Animation),
            typeof(Camera),
            typeof(Light),
            typeof(ParticleRenderer),
            typeof(ParticleAnimator),
            typeof(ParticleEmitter),
            typeof(TextMesh),
        };

        internal static bool PlannedMove(out Matrix4x4 move, out Quaternion turn)
        {
            move = Matrix4x4.identity;
            turn = Quaternion.identity;
            var original = GameObject.Find(OriginalPath);
            if (original == null) return false;
            Original = original;
            LoadPose();
            var rotation = RotationFor(Yaw);
            move = Matrix4x4.TRS(Position, rotation, original.transform.lossyScale) * original.transform.worldToLocalMatrix;
            turn = rotation * Quaternion.Inverse(original.transform.rotation);
            return true;
        }

        internal static void Spawn()
        {
            Original = GameObject.Find(OriginalPath);
            if (Original == null)
            {
                ModConsole.Error($"[MSC Duo] '{OriginalPath}' não encontrado; casa 2 não foi criada.");
                return;
            }

            LoadPose();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var stats = new CloneStats();
            var root = new GameObject("MSCDUO");
            Clone = (GameObject)UnityEngine.Object.Instantiate(Original, Position, RotationFor(Yaw));
            Clone.name = "Building_P2";
            Clone.transform.parent = root.transform;

            Strip(Clone, stats);
            FixStaticBatching(Original, Clone, stats);
            Step("sistemas", () => KeptSystems.Prepare(Clone));

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2 criada em {Format(Position)} yaw {Yaw:0.#} em {clock.ElapsedMilliseconds} ms. {stats}");
            Step("árvores", () => TreeClearer.Apply(Clone));
            Step("terreno", () => TerrainFlattener.Apply(Clone));
            Step("rampa", () => ApronExtension.Apply(Clone));
            Step("contas", () => House2Bills.Setup(Clone));
            Step("satsuma", () => SatsumaCloner.Spawn(Original, Clone));
            Step("veículos", () => VehicleCloner.Spawn(Original, Clone, SatsumaCloner.Clone));
        }

        static void Step(string name, System.Action action)
        {
            try { action(); }
            catch (Exception e) { ModConsole.Error($"[MSC Duo] casa 2, etapa '{name}' falhou: {e}"); }
        }

        internal static void Move(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
            SavePose();
            if (Clone == null) return;
            Clone.transform.position = position;
            Clone.transform.rotation = RotationFor(yaw);
            Step("árvores", () => TreeClearer.Apply(Clone));
            Step("terreno", () => TerrainFlattener.Apply(Clone));
            Step("rampa", () => ApronExtension.Apply(Clone));
        }

        internal static bool FaceTowards(Vector3 target)
        {
            if (Clone == null) return false;
            var door = FindDeep(Clone.transform, "DoorFront");
            if (door == null) return false;

            var yaw = Yaw;
            for (int i = 0; i < 4; i++)
            {
                Clone.transform.rotation = RotationFor(yaw);
                var center = Center(Clone);
                var delta = Mathf.DeltaAngle(Heading(door.position - center), Heading(target - center));
                if (Mathf.Abs(delta) < 0.1f) break;
                yaw += delta;
            }
            Move(Position, Mathf.Repeat(yaw, 360f));
            return true;
        }

        static float Heading(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        static Vector3 Center(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return go.transform.position;
            var b = colliders[0].bounds;
            foreach (var c in colliders) b.Encapsulate(c.bounds);
            return b.center;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        internal static Quaternion RotationFor(float yaw) =>
            Quaternion.Euler(0f, yaw, 0f) * Original.transform.rotation;

        internal static Matrix4x4 LocalFrame(GameObject house)
        {
            var right = house.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            var forward = Vector3.Cross(right.normalized, Vector3.up);
            return Matrix4x4.TRS(house.transform.position, Quaternion.LookRotation(forward, Vector3.up), Vector3.one);
        }

        internal static float OriginalYaw => Original != null ? Original.transform.eulerAngles.y : 0f;

        static void Strip(GameObject clone, CloneStats stats)
        {
            foreach (var type in StripOrder)
            {
                foreach (var c in clone.GetComponentsInChildren(type, true))
                {
                    if (c == null) continue;
                    if (KeptSystems.Keeps(c.transform)) { stats.Kept++; continue; }
                    var textRenderer = c is TextMesh ? c.GetComponent<MeshRenderer>() : null;
                    UnityEngine.Object.DestroyImmediate(c);
                    if (textRenderer != null) UnityEngine.Object.DestroyImmediate(textRenderer);
                    stats.Removed++;
                }
            }

            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                if (!col.isTrigger || KeptSystems.Keeps(col.transform)) continue;
                UnityEngine.Object.DestroyImmediate(col);
                stats.Removed++;
            }
        }

        internal static void FixStaticBatching(GameObject original, GameObject clone) => FixStaticBatching(original, clone, new CloneStats());

        static void FixStaticBatching(GameObject original, GameObject clone, CloneStats stats)
        {
            var originals = original.GetComponentsInChildren<MeshFilter>(true);
            var clones = clone.GetComponentsInChildren<MeshFilter>(true);
            if (originals.Length != clones.Length)
            {
                ModConsole.Warning($"[MSC Duo] hierarquias diferentes ({originals.Length} x {clones.Length}); static batching não verificado.");
                return;
            }

            using (var extractor = new StaticBatchExtractor())
            {
                for (int i = 0; i < originals.Length; i++)
                {
                    var renderer = originals[i].GetComponent<Renderer>();
                    if (renderer == null || !renderer.isPartOfStaticBatch) continue;
                    stats.Batched++;

                    var cloneRenderer = clones[i].GetComponent<Renderer>();
                    if (cloneRenderer != null) cloneRenderer.enabled = false;

                    var mesh = extractor.Extract(renderer, originals[i].sharedMesh);
                    if (mesh != null)
                    {
                        stats.Extracted++;
                    }
                    else
                    {
                        var collider = originals[i].GetComponent<MeshCollider>();
                        if (collider != null && collider.sharedMesh != null)
                        {
                            mesh = collider.sharedMesh;
                            stats.FromCollider++;
                        }
                    }

                    if (mesh == null)
                    {
                        stats.Hidden++;
                        stats.HiddenNames.Add(originals[i].name);
                        continue;
                    }
                    AttachFreshRenderer(clones[i].gameObject, mesh, renderer);
                }

                if (extractor.Unmatched > 0)
                    ModConsole.Warning($"[MSC Duo] {extractor.Unmatched} peças não encontradas dentro das malhas combinadas.");
            }

            if (stats.Hidden > 0)
                ModConsole.Warning($"[MSC Duo] malhas combinadas sem malha original disponível (ocultas): {string.Join(", ", Distinct(stats.HiddenNames))}");
        }

        static void AttachFreshRenderer(GameObject host, Mesh mesh, Renderer source)
        {
            var go = new GameObject(host.name + "_mesh");
            go.layer = host.layer;
            go.transform.parent = host.transform;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = source.sharedMaterials;
            r.shadowCastingMode = source.shadowCastingMode;
            r.receiveShadows = source.receiveShadows;
            r.lightmapIndex = source.lightmapIndex;
            r.lightmapScaleOffset = source.lightmapScaleOffset;
            r.enabled = source.enabled;
        }

        static string[] Distinct(List<string> names)
        {
            var set = new List<string>();
            foreach (var n in names) if (!set.Contains(n)) set.Add(n);
            return set.ToArray();
        }

        static string PosePath => Path.Combine(MscDuo.Instance.SettingsFolder, PoseFile);

        static void LoadPose()
        {
            Position = new Vector3(-7.46f, 0.38f, 45.43f);
            Yaw = 241.1472f;
            try
            {
                if (!File.Exists(PosePath)) return;
                var parts = File.ReadAllText(PosePath).Trim().Split(' ');
                var inv = CultureInfo.InvariantCulture;
                Position = new Vector3(float.Parse(parts[0], inv), float.Parse(parts[1], inv), float.Parse(parts[2], inv));
                Yaw = float.Parse(parts[3], inv);
            }
            catch (Exception e)
            {
                ModConsole.Warning($"[MSC Duo] {PoseFile} inválido, usando posição padrão: {e.Message}");
            }
        }

        static void SavePose()
        {
            var inv = CultureInfo.InvariantCulture;
            File.WriteAllText(PosePath, string.Format(inv, "{0} {1} {2} {3}", Position.x, Position.y, Position.z, Yaw));
        }

        internal static string Format(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);

        class CloneStats
        {
            internal int Removed;
            internal int Kept;
            internal int Batched;
            internal int Extracted;
            internal int FromCollider;
            internal int Hidden;
            internal readonly List<string> HiddenNames = new List<string>();

            public override string ToString() =>
                $"{Removed} componentes removidos, {Kept} mantidos (sistemas ativos); static batching: {Batched} malhas combinadas, {Extracted} recuperadas do level2, {FromCollider} pelo collider, {Hidden} ocultas.";
        }
    }
}
