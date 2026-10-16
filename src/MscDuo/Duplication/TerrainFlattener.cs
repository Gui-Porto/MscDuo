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

        const string RoadPath = "MAP/MESH/TERRAIN_OBJ/Road";
        const string TerrainRoot = "MAP/MESH/TERRAIN_OBJ";
        const float RoadClear = 0.5f;
        const float HedgeSink = 0.05f;

        static readonly Dictionary<Transform, Vector3> hedgeOrigins = new Dictionary<Transform, Vector3>();
        static readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> originalColliders = new Dictionary<MeshCollider, Mesh>();

        internal static void Apply(GameObject house)
        {
            Restore();

            var toHouse = FlatFrame(house.transform);
            var local = LocalFootprint(house, toHouse);
            var groundY = house.transform.position.y - PivotAboveGround - Sink;
            var top = house.transform.position.y + PitProbe;
            var pit = FindPit(house, toHouse, local, groundY, top);
            int moved = 0, flat = 0, meshes = 0, nearRoad = 0, cut = 0, uncut = 0;

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
                    if (distance > 0f)
                    {
                        var road = RoadDistance(world);
                        if (road < RoadRamp)
                        {
                            t *= Smooth(road / RoadRamp);
                            nearRoad++;
                        }
                    }
                    var y = Mathf.Lerp(world.y, groundY, t);
                    if (Mathf.Abs(y - world.y) < 0.001f) continue;
                    world.y = y;
                    vertices[i] = toMesh.MultiplyPoint3x4(world);
                    moved++;
                    if (distance <= FlatMargin) flat++;
                    changed = true;
                }
                var cutter = new PitCutter(source, vertices, toWorld, pit, PitStep * 0.6f, p => HouseFloor(house, p.x, p.z, top));
                var triangles = new int[source.subMeshCount][];
                for (int s = 0; s < triangles.Length; s++) triangles[s] = cutter.Process(source.GetTriangles(s));
                cut += cutter.Cut;
                uncut += cutter.Kept;
                if (!changed && !cutter.Changed) continue;

                var mesh = cutter.Build(source.name + "_flat", triangles);
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

            var roadCut = CutRoad(house, toHouse, local);
            var hedges = SettleHedges(house);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> estradinha de terra: {roadCut} triângulos tirados de dentro da casa e da extensão; cerca viva: {hedges} pedaços assentados no chão.");
            ModConsole.Print($"<color=orange>[MSC Duo]</color> terreno: {moved} vértices ajustados ({flat} no platô) em {meshes} malhas, {nearRoad} em rampa até a estrada, platô {local.size.x:0.#} x {local.size.z:0.#} m, nível {groundY:0.##}, vala da garagem com {pit.Count} pontos, {cut} pedaços abertos ({uncut} mantidos).");
        }

        static int CutRoad(GameObject house, Matrix4x4 toHouse, Bounds local)
        {
            var go = GameObject.Find(RoadPath);
            var filter = go != null ? go.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return 0;
            var source = filter.sharedMesh;
            Vector3[] vertices;
            try { vertices = source.vertices; }
            catch { ModConsole.Warning("[MSC Duo] estradinha de terra sem leitura"); return 0; }

            var corners = ApronExtension.Corners(house);
            var toWorld = go.transform.localToWorldMatrix;
            var removed = 0;
            var triangles = new int[source.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                var all = source.GetTriangles(s);
                var kept = new List<int>(all.Length);
                for (int t = 0; t < all.Length; t += 3)
                {
                    var center = toWorld.MultiplyPoint3x4((vertices[all[t]] + vertices[all[t + 1]] + vertices[all[t + 2]]) / 3f);
                    var h = toHouse.MultiplyPoint3x4(center);
                    var inHouse = h.x > local.min.x - RoadClear && h.x < local.max.x + RoadClear && h.z > local.min.z - RoadClear && h.z < local.max.z + RoadClear;
                    if (inHouse || ApronExtension.Inside(corners, center, 0.6f)) { removed++; continue; }
                    kept.Add(all[t]); kept.Add(all[t + 1]); kept.Add(all[t + 2]);
                }
                triangles[s] = kept.ToArray();
            }
            if (removed == 0) return 0;

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_cut";
            for (int s = 0; s < triangles.Length; s++) mesh.SetTriangles(triangles[s], s);
            originalMeshes[filter] = source;
            filter.sharedMesh = mesh;
            var collider = go.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh == source)
            {
                originalColliders[collider] = source;
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
            return removed;
        }

        static int SettleHedges(GameObject house)
        {
            var fence = FindDeep(house.transform, "HedgeFence");
            var terrain = GameObject.Find(TerrainRoot);
            if (fence == null || terrain == null) return 0;
            var moved = 0;
            foreach (Transform hedge in fence)
            {
                var renderers = hedge.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                var ground = float.MaxValue;
                for (int i = 0; i < 3; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        var x = Mathf.Lerp(b.min.x, b.max.x, 0.2f + 0.3f * i);
                        var z = Mathf.Lerp(b.min.z, b.max.z, 0.2f + 0.3f * k);
                        foreach (var hit in Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 6f))
                            if (hit.collider.transform.IsChildOf(terrain.transform) && hit.point.y < ground) ground = hit.point.y;
                    }
                if (ground == float.MaxValue) continue;
                var drop = b.min.y - ground + HedgeSink;
                if (drop <= 0.01f) continue;
                hedgeOrigins[hedge] = hedge.position;
                hedge.position -= Vector3.up * drop;
                moved++;
            }
            return moved;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void Restore()
        {
            foreach (var pair in hedgeOrigins) if (pair.Key != null) pair.Key.position = pair.Value;
            hedgeOrigins.Clear();
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
        internal static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };
        const float RoadRamp = 8f;
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

        static float RoadDistance(Vector3 world)
        {
            if (OnRoad(world.x, world.z, world.y)) return 0f;
            for (var radius = RoadStep; radius <= RoadRamp; radius += RoadStep)
                for (int k = 0; k < 8; k++)
                {
                    var angle = k * Mathf.PI / 4f;
                    if (OnRoad(world.x + Mathf.Cos(angle) * radius, world.z + Mathf.Sin(angle) * radius, world.y))
                        return radius;
                }
            return RoadRamp;
        }

        static bool OnRoad(float x, float z, float y)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(x, y + 50f, z), Vector3.down, 100f))
                foreach (var road in RoadNames)
                    if (hit.collider.name == road) return true;
            return false;
        }

        const float PitProbe = 0.5f;
        const float PitStep = 0.4f;
        const float PitDepth = 0.3f;

        static List<Vector2> FindPit(GameObject house, Matrix4x4 toHouse, Bounds local, float groundY, float top)
        {
            var cells = new List<Vector2>();
            var toWorld = toHouse.inverse;
            for (var x = local.min.x; x <= local.max.x; x += PitStep)
                for (var z = local.min.z; z <= local.max.z; z += PitStep)
                {
                    var p = toWorld.MultiplyPoint3x4(new Vector3(x, 0f, z));
                    float y;
                    if (HouseFloor(house, p.x, p.z, top, out y) && y < groundY - PitDepth) cells.Add(new Vector2(p.x, p.z));
                }
            return cells;
        }

        static bool HouseFloor(GameObject house, float x, float z, float top)
        {
            float y;
            return HouseFloor(house, x, z, top, out y);
        }

        static bool HouseFloor(GameObject house, float x, float z, float top, out float y)
        {
            y = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, 10f))
            {
                if (hit.collider.isTrigger || !hit.collider.transform.IsChildOf(house.transform)) continue;
                if (hit.point.y > y) y = hit.point.y;
            }
            return y > float.MinValue;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
