using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class TreeClearer
    {
        const string FoliagePath = "MAP/MESH/FOLIAGE";
        const float Margin = 4f;

        static readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> originalColliders = new Dictionary<MeshCollider, Mesh>();
        static readonly List<Collider> disabledColliders = new List<Collider>();

        internal static void Apply(GameObject house)
        {
            Restore();
            var foliage = GameObject.Find(FoliagePath);
            if (foliage == null)
            {
                ModConsole.Warning($"[MSC Duo] '{FoliagePath}' não encontrado; árvores mantidas.");
                return;
            }

            var zone = Zone(house);
            int meshes = 0, pieces = 0, unreadable = 0, colliders = 0;

            foreach (var filter in foliage.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                Mesh cleared;
                int removed;
                if (!TryClear(mesh, filter.transform, zone, out cleared, out removed)) { unreadable++; continue; }
                if (removed == 0) continue;
                originalMeshes[filter] = mesh;
                filter.sharedMesh = cleared;
                meshes++;
                pieces += removed;
            }

            foreach (var col in TreeColliders())
            {
                var mc = col as MeshCollider;
                if (mc != null && mc.sharedMesh != null)
                {
                    Mesh cleared;
                    int removed;
                    if (TryClear(mc.sharedMesh, mc.transform, zone, out cleared, out removed) && removed > 0)
                    {
                        originalColliders[mc] = mc.sharedMesh;
                        mc.sharedMesh = cleared;
                        colliders += removed;
                    }
                    continue;
                }
                if (!col.enabled || !zone.Contains(col.bounds.center)) continue;
                col.enabled = false;
                disabledColliders.Add(col);
                colliders++;
            }

            ModConsole.Print($"<color=orange>[MSC Duo]</color> árvores: {pieces} removidas de {meshes} malhas, {colliders} colisões removidas, {unreadable} malhas sem leitura.");
        }

        static IEnumerable<Collider> TreeColliders()
        {
            var map = GameObject.Find("MAP");
            if (map == null) yield break;
            foreach (var col in map.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;
                if (IsTreePath(col.transform)) yield return col;
            }
        }

        static bool IsTreePath(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                var name = t.name.ToUpperInvariant();
                if (name.StartsWith("TREE") || name == "FOLIAGE" || name.StartsWith("BUSHES")) return true;
                if (name == "MAP") return false;
            }
            return false;
        }

        static void Restore()
        {
            foreach (var pair in originalMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var pair in originalColliders) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var col in disabledColliders) if (col != null) col.enabled = true;
            originalMeshes.Clear();
            originalColliders.Clear();
            disabledColliders.Clear();
        }

        static ZoneBox Zone(GameObject house)
        {
            var bounds = new Bounds(house.transform.position, Vector3.zero);
            var first = true;
            foreach (var c in house.GetComponentsInChildren<Collider>())
            {
                if (first) { bounds = c.bounds; first = false; }
                else bounds.Encapsulate(c.bounds);
            }
            return new ZoneBox(bounds.min - new Vector3(Margin, 0f, Margin), bounds.max + new Vector3(Margin, 0f, Margin));
        }

        static bool TryClear(Mesh mesh, Transform owner, ZoneBox zone, out Mesh cleared, out int removed)
        {
            cleared = null;
            removed = 0;
            Vector3[] vertices;
            try { vertices = mesh.vertices; }
            catch { return false; }
            if (vertices == null || vertices.Length == 0) return false;

            var toWorld = owner.localToWorldMatrix;
            var triangles = mesh.triangles;
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Union(parent, triangles[t], triangles[t + 1]);
                Union(parent, triangles[t], triangles[t + 2]);
            }

            var min = new Dictionary<int, Vector3>();
            var max = new Dictionary<int, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var root = Find(parent, i);
                var w = toWorld.MultiplyPoint3x4(vertices[i]);
                Vector3 lo, hi;
                if (!min.TryGetValue(root, out lo)) { min[root] = w; max[root] = w; continue; }
                hi = max[root];
                min[root] = Vector3.Min(lo, w);
                max[root] = Vector3.Max(hi, w);
            }

            var drop = new HashSet<int>();
            foreach (var root in min.Keys)
            {
                var center = (min[root] + max[root]) * 0.5f;
                if (zone.Contains(center)) drop.Add(root);
            }
            if (drop.Count == 0) return true;

            cleared = Object.Instantiate(mesh);
            cleared.name = mesh.name + "_cleared";
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var src = mesh.GetTriangles(s);
                var kept = new List<int>(src.Length);
                for (int t = 0; t < src.Length; t += 3)
                {
                    if (drop.Contains(Find(parent, src[t]))) continue;
                    kept.Add(src[t]);
                    kept.Add(src[t + 1]);
                    kept.Add(src[t + 2]);
                }
                cleared.SetTriangles(kept.ToArray(), s);
            }
            removed = drop.Count;
            return true;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[a] = b;
        }

        struct ZoneBox
        {
            readonly Vector3 min;
            readonly Vector3 max;

            internal ZoneBox(Vector3 min, Vector3 max)
            {
                this.min = min;
                this.max = max;
            }

            internal bool Contains(Vector3 p) => p.x >= min.x && p.x <= max.x && p.z >= min.z && p.z <= max.z;
        }
    }
}
