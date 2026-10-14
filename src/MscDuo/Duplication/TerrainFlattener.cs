using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class TerrainFlattener
    {
        static readonly string[] TerrainPaths =
        {
            "MAP/MESH/TERRAIN_OBJ/Grass1",
            "MAP/MESH/TERRAIN_OBJ/Grass2",
        };

        const float PivotAboveGround = 1.19f;
        const float Sink = 0.08f;
        const float FlatMargin = 3f;
        const float BlendWidth = 12f;

        static readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> originalColliders = new Dictionary<MeshCollider, Mesh>();

        internal static void Apply(GameObject house)
        {
            Restore();

            var toHouse = FlatFrame(house.transform);
            var local = LocalFootprint(house, toHouse);
            var groundY = house.transform.position.y - PivotAboveGround - Sink;
            int moved = 0, flat = 0, meshes = 0, nearRoad = 0;

            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                if (go == null) { ModConsole.Warning($"[MSC Duo] terreno '{path}' não encontrado"); continue; }
                var filter = go.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                var source = filter.sharedMesh;
                Vector3[] vertices;
                try { vertices = source.vertices; }
                catch { ModConsole.Warning($"[MSC Duo] terreno '{path}' sem leitura"); continue; }

                var toWorld = go.transform.localToWorldMatrix;
                var toMesh = go.transform.worldToLocalMatrix;
                var changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var world = toWorld.MultiplyPoint3x4(vertices[i]);
                    var h = toHouse.MultiplyPoint3x4(world);
                    var dx = Mathf.Max(0f, Mathf.Max(local.min.x - FlatMargin - h.x, h.x - local.max.x - FlatMargin));
                    var dz = Mathf.Max(0f, Mathf.Max(local.min.z - FlatMargin - h.z, h.z - local.max.z - FlatMargin));
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance >= BlendWidth) continue;

                    if (NearRoad(world)) { nearRoad++; continue; }
                    var t = distance <= 0f ? 1f : Smooth(1f - distance / BlendWidth);
                    var y = Mathf.Lerp(world.y, groundY, t);
                    if (Mathf.Abs(y - world.y) < 0.001f) continue;
                    world.y = y;
                    vertices[i] = toMesh.MultiplyPoint3x4(world);
                    moved++;
                    if (distance <= 0f) flat++;
                    changed = true;
                }
                if (!changed) continue;

                var mesh = Object.Instantiate(source);
                mesh.name = source.name + "_flat";
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                originalMeshes[filter] = source;
                filter.sharedMesh = mesh;

                var collider = go.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    originalColliders[collider] = collider.sharedMesh;
                    collider.sharedMesh = null;
                    collider.sharedMesh = mesh;
                }
                meshes++;
            }

            ModConsole.Print($"<color=orange>[MSC Duo]</color> terreno: {moved} vértices ajustados ({flat} no platô) em {meshes} malhas, {nearRoad} preservados perto de estrada, platô {local.size.x:0.#} x {local.size.z:0.#} m, nível {groundY:0.##}.");
        }

        static void Restore()
        {
            foreach (var pair in originalMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var pair in originalColliders)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMesh = null;
                pair.Key.sharedMesh = pair.Value;
            }
            originalMeshes.Clear();
            originalColliders.Clear();
        }

        static readonly string[] StructurePrefixes = { "house_wall", "house_base", "house_floor" };
        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };
        const float RoadClearance = 3f;

        static Matrix4x4 FlatFrame(Transform house)
        {
            var right = house.right;
            right.y = 0f;
            var heading = Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg - 90f;
            return Matrix4x4.TRS(house.position, Quaternion.Euler(0f, heading, 0f), Vector3.one).inverse;
        }

        static Bounds LocalFootprint(GameObject house, Matrix4x4 toLocal)
        {
            var first = true;
            var bounds = new Bounds();
            foreach (var filter in house.GetComponentsInChildren<MeshFilter>())
            {
                var r = filter.GetComponent<Renderer>();
                if (r == null || !r.enabled || filter.sharedMesh == null || !IsStructure(filter.transform)) continue;
                var b = filter.sharedMesh.bounds;
                var toHouse = toLocal * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = toHouse.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        static bool IsStructure(Transform t)
        {
            var name = t.name.EndsWith("_mesh") && t.parent != null ? t.parent.name : t.name;
            foreach (var prefix in StructurePrefixes)
                if (name.StartsWith(prefix)) return true;
            return false;
        }

        static bool NearRoad(Vector3 world)
        {
            for (int i = 0; i < 5; i++)
            {
                var offset = i == 0 ? Vector3.zero : Quaternion.Euler(0f, 90f * i, 0f) * new Vector3(RoadClearance, 0f, 0f);
                foreach (var hit in Physics.RaycastAll(new Vector3(world.x, world.y + 50f, world.z) + offset, Vector3.down, 100f))
                    foreach (var road in RoadNames)
                        if (hit.collider.name == road) return true;
            }
            return false;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
