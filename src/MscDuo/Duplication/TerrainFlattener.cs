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
                    var dx = Mathf.Max(0f, Mathf.Max(local.min.x - h.x, h.x - local.max.x));
                    var dz = Mathf.Max(0f, Mathf.Max(local.min.z - h.z, h.z - local.max.z));
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance >= FlatMargin + BlendWidth) continue;

                    var t = distance <= FlatMargin ? 1f : Smooth(1f - (distance - FlatMargin) / BlendWidth);
                    var y = Mathf.Lerp(world.y, groundY, t);
                    if (distance > 0f)
                    {
                        float roadDistance, roadY;
                        if (FindRoad(world, out roadDistance, out roadY))
                        {
                            nearRoad++;
                            if (roadDistance <= 0f)
                                y = Mathf.Min(world.y, roadY - RoadDepth);
                            else
                                y = Mathf.Lerp(roadY - RoadDepth, y, Smooth(roadDistance / RoadRamp));
                        }
                    }
                    if (Mathf.Abs(y - world.y) < 0.001f) continue;
                    world.y = y;
                    vertices[i] = toMesh.MultiplyPoint3x4(world);
                    moved++;
                    if (distance <= FlatMargin) flat++;
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

            ModConsole.Print($"<color=orange>[MSC Duo]</color> terreno: {moved} vértices ajustados ({flat} no platô) em {meshes} malhas, {nearRoad} acompanhando a estrada, platô {local.size.x:0.#} x {local.size.z:0.#} m, nível {groundY:0.##}.");
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
        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Pavement", "4tie" };
        const float RoadRamp = 8f;
        const float RoadDepth = 0.04f;
        const float RoadStep = 1f;

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

        static bool FindRoad(Vector3 world, out float distance, out float surfaceY)
        {
            distance = 0f;
            if (RoadSurface(world.x, world.z, world.y, out surfaceY)) return true;
            for (var radius = RoadStep; radius < RoadRamp; radius += RoadStep)
            {
                var found = false;
                for (int k = 0; k < 8; k++)
                {
                    var angle = k * Mathf.PI / 4f;
                    float y;
                    if (!RoadSurface(world.x + Mathf.Cos(angle) * radius, world.z + Mathf.Sin(angle) * radius, world.y, out y)) continue;
                    if (!found || y > surfaceY) surfaceY = y;
                    found = true;
                }
                if (found)
                {
                    distance = radius;
                    return true;
                }
            }
            return false;
        }

        static bool RoadSurface(float x, float z, float y, out float surfaceY)
        {
            surfaceY = 0f;
            var found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, y + 50f, z), Vector3.down, 100f))
            {
                var isRoad = false;
                foreach (var road in RoadNames)
                    if (hit.collider.name == road) { isRoad = true; break; }
                if (!isRoad) continue;
                if (!found || hit.point.y > surfaceY) surfaceY = hit.point.y;
                found = true;
            }
            return found;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
