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

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2 criada em {Format(Position)} yaw {Yaw:0.#} em {clock.ElapsedMilliseconds} ms. {stats}");
        }

        internal static void Move(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
            SavePose();
            if (Clone == null) return;
            Clone.transform.position = position;
            Clone.transform.rotation = RotationFor(yaw);
        }

        internal static Quaternion RotationFor(float yaw) =>
            Quaternion.Euler(0f, yaw, 0f) * Original.transform.rotation;

        internal static float OriginalYaw => Original != null ? Original.transform.eulerAngles.y : 0f;

        static void Strip(GameObject clone, CloneStats stats)
        {
            foreach (var type in StripOrder)
            {
                foreach (var c in clone.GetComponentsInChildren(type, true))
                {
                    if (c == null) continue;
                    var textRenderer = c is TextMesh ? c.GetComponent<MeshRenderer>() : null;
                    UnityEngine.Object.DestroyImmediate(c);
                    if (textRenderer != null) UnityEngine.Object.DestroyImmediate(textRenderer);
                    stats.Removed++;
                }
            }

            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                if (!col.isTrigger) continue;
                UnityEngine.Object.DestroyImmediate(col);
                stats.Removed++;
            }
        }

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
            Position = Original.transform.position + new Vector3(0f, 0f, 40f);
            Yaw = 180f;
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
            internal int Batched;
            internal int Extracted;
            internal int FromCollider;
            internal int Hidden;
            internal readonly List<string> HiddenNames = new List<string>();

            public override string ToString() =>
                $"{Removed} componentes removidos; static batching: {Batched} malhas combinadas, {Extracted} recuperadas do level2, {FromCollider} pelo collider, {Hidden} ocultas.";
        }
    }
}
