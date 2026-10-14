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

        static readonly string[] StructurePrefixes = { "house_wall", "house_base", "house_floor" };
        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };

        const float PivotAboveGround = 1.19f;
        const float Sink = 0.08f;
        const float FlatMargin = 3f;
        const float BlendWidth = 12f;
        const float RoadKeep = 2f;
        const float RoadRamp = 8f;
        const float Cell = 1f;
        const float EdgeDrop = 0.02f;

        static readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> originalColliders = new Dictionary<MeshCollider, Mesh>();
        static GameObject patch;

        internal static void Apply(GameObject house)
        {
            Restore();

            var frame = FlatFrame(house.transform);
            var toWorld = frame.inverse;
            var footprint = LocalFootprint(house, frame);
            var groundY = house.transform.position.y - PivotAboveGround - Sink;
            var reach = FlatMargin + BlendWidth;
            var min = new Vector2(footprint.min.x - reach, footprint.min.z - reach);
            var max = new Vector2(footprint.max.x + reach, footprint.max.z + reach);

            var terrains = new List<GameObject>();
            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                if (go != null && go.GetComponent<MeshFilter>() != null) terrains.Add(go);
            }
            if (terrains.Count == 0) { ModConsole.Warning("[MSC Duo] malhas de terreno não encontradas"); return; }

            var nx = Mathf.CeilToInt((max.x - min.x) / Cell) + 1;
            var nz = Mathf.CeilToInt((max.y - min.y) / Cell) + 1;
            var vertices = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            var valid = new bool[nx * nz];
            var uvMap = UvMapping(terrains[0]);
            var roads = new RoadGrid(toWorld, min, max, RoadKeep + RoadRamp);
            int ramped = 0;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    var local = new Vector3(min.x + i * Cell, 0f, min.y + j * Cell);
                    var world = toWorld.MultiplyPoint3x4(local);
                    float originalY;
                    var k = j * nx + i;
                    if (!SampleTerrain(world.x, world.z, terrains, out originalY)) continue;
                    valid[k] = true;

                    var dx = Mathf.Max(0f, Mathf.Max(footprint.min.x - local.x, local.x - footprint.max.x));
                    var dz = Mathf.Max(0f, Mathf.Max(footprint.min.z - local.z, local.z - footprint.max.z));
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    var t = distance <= FlatMargin ? 1f : Smooth(Mathf.Clamp01(1f - (distance - FlatMargin) / BlendWidth));
                    if (distance > 0f && t > 0f)
                    {
                        var road = roads.Distance(local.x, local.z);
                        if (road < RoadKeep + RoadRamp)
                        {
                            t *= Smooth(Mathf.Clamp01((road - RoadKeep) / RoadRamp));
                            ramped++;
                        }
                    }

                    var y = Mathf.Lerp(originalY, groundY, t);
                    var edge = i == 0 || j == 0 || i == nx - 1 || j == nz - 1;
                    if (t <= 0.001f || edge) y -= EdgeDrop;
                    vertices[k] = new Vector3(world.x, y, world.z);
                    uv[k] = uvMap.Map(world);
                }
            }

            var triangles = new List<int>();
            for (int j = 0; j < nz - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    if (!valid[a] || !valid[b] || !valid[c] || !valid[d]) continue;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            var cut = 0;
            foreach (var go in terrains) cut += CutHole(go, frame, min, max);

            var mesh = new Mesh { name = "MscDuo_TerrainPatch", vertices = vertices, uv = uv };
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var source = terrains[0];
            patch = new GameObject("MscDuo_TerrainPatch");
            patch.layer = source.layer;
            patch.tag = source.tag;
            patch.transform.parent = house.transform.parent;
            patch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = patch.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            renderer.receiveShadows = true;
            var collider = patch.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            var sourceCollider = source.GetComponent<MeshCollider>();
            if (sourceCollider != null) collider.sharedMaterial = sourceCollider.sharedMaterial;

            ModConsole.Print($"<color=orange>[MSC Duo]</color> terreno: remendo {nx}x{nz} pontos ({Cell} m), {cut} triângulos originais recortados, {ramped} pontos em rampa até a estrada, platô {footprint.size.x:0.#} x {footprint.size.z:0.#} m, nível {groundY:0.##}.");
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
            if (patch != null) Object.DestroyImmediate(patch);
            patch = null;
        }

        static bool SampleTerrain(float x, float z, List<GameObject> terrains, out float y)
        {
            y = 0f;
            var found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
            {
                if (!terrains.Contains(hit.collider.gameObject)) continue;
                if (!found || hit.point.y > y)
                {
                    y = hit.point.y;
                    found = true;
                }
            }
            return found;
        }

        static int CutHole(GameObject go, Matrix4x4 frame, Vector2 min, Vector2 max)
        {
            var filter = go.GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            Vector3[] vertices;
            try { vertices = source.vertices; }
            catch { return 0; }

            var toWorld = go.transform.localToWorldMatrix;
            var inside = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = frame.MultiplyPoint3x4(toWorld.MultiplyPoint3x4(vertices[i]));
                inside[i] = p.x >= min.x && p.x <= max.x && p.z >= min.y && p.z <= max.y;
            }

            var cut = 0;
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_hole";
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (inside[tris[t]] && inside[tris[t + 1]] && inside[tris[t + 2]]) { cut++; continue; }
                    kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                }
                mesh.SetTriangles(kept.ToArray(), s);
            }
            if (cut == 0) { Object.Destroy(mesh); return 0; }

            originalMeshes[filter] = source;
            filter.sharedMesh = mesh;
            var collider = go.GetComponent<MeshCollider>();
            if (collider != null)
            {
                originalColliders[collider] = collider.sharedMesh;
                collider.sharedMesh = null;
                collider.sharedMesh = mesh;
            }
            return cut;
        }

        static UvAffine UvMapping(GameObject terrain)
        {
            var mesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var toWorld = terrain.transform.localToWorldMatrix;
            double sxx = 0, sxz = 0, szz = 0, sx = 0, sz = 0, n = 0;
            double sux = 0, suz = 0, su = 0, svx = 0, svz = 0, sv = 0;
            var step = Mathf.Max(1, vertices.Length / 4000);
            for (int i = 0; i < vertices.Length && i < uvs.Length; i += step)
            {
                var w = toWorld.MultiplyPoint3x4(vertices[i]);
                double x = w.x, z = w.z, u = uvs[i].x, v = uvs[i].y;
                sxx += x * x; sxz += x * z; szz += z * z; sx += x; sz += z; n++;
                sux += u * x; suz += u * z; su += u;
                svx += v * x; svz += v * z; sv += v;
            }
            return new UvAffine(Solve(sxx, sxz, sx, szz, sz, n, sux, suz, su), Solve(sxx, sxz, sx, szz, sz, n, svx, svz, sv));
        }

        static double[] Solve(double a11, double a12, double a13, double a22, double a23, double a33, double b1, double b2, double b3)
        {
            var m = new[,] { { a11, a12, a13, b1 }, { a12, a22, a23, b2 }, { a13, a23, a33, b3 } };
            for (int c = 0; c < 3; c++)
            {
                for (int r = c + 1; r < 3; r++)
                {
                    var f = m[r, c] / m[c, c];
                    for (int k = c; k < 4; k++) m[r, k] -= f * m[c, k];
                }
            }
            var x = new double[3];
            for (int r = 2; r >= 0; r--)
            {
                var sum = m[r, 3];
                for (int k = r + 1; k < 3; k++) sum -= m[r, k] * x[k];
                x[r] = sum / m[r, r];
            }
            return x;
        }

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

        static bool OnRoad(float x, float z)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
                foreach (var road in RoadNames)
                    if (hit.collider.name == road) return true;
            return false;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        class RoadGrid
        {
            readonly bool[] road;
            readonly int nx;
            readonly int nz;
            readonly float originX;
            readonly float originZ;
            readonly float range;

            internal RoadGrid(Matrix4x4 toWorld, Vector2 min, Vector2 max, float range)
            {
                this.range = range;
                originX = min.x - range;
                originZ = min.y - range;
                nx = Mathf.CeilToInt((max.x - min.x + 2f * range) / Cell) + 1;
                nz = Mathf.CeilToInt((max.y - min.y + 2f * range) / Cell) + 1;
                road = new bool[nx * nz];
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        var w = toWorld.MultiplyPoint3x4(new Vector3(originX + i * Cell, 0f, originZ + j * Cell));
                        road[j * nx + i] = OnRoad(w.x, w.z);
                    }
            }

            internal float Distance(float x, float z)
            {
                var ci = Mathf.RoundToInt((x - originX) / Cell);
                var cj = Mathf.RoundToInt((z - originZ) / Cell);
                var r = Mathf.CeilToInt(range / Cell);
                var best = range * range;
                for (int j = Mathf.Max(0, cj - r); j <= Mathf.Min(nz - 1, cj + r); j++)
                    for (int i = Mathf.Max(0, ci - r); i <= Mathf.Min(nx - 1, ci + r); i++)
                    {
                        if (!road[j * nx + i]) continue;
                        var dx = (i - ci) * Cell;
                        var dz = (j - cj) * Cell;
                        var d = dx * dx + dz * dz;
                        if (d < best) best = d;
                    }
                return Mathf.Sqrt(best);
            }
        }

        struct UvAffine
        {
            readonly double[] u;
            readonly double[] v;

            internal UvAffine(double[] u, double[] v)
            {
                this.u = u;
                this.v = v;
            }

            internal Vector2 Map(Vector3 w) =>
                new Vector2((float)(u[0] * w.x + u[1] * w.z + u[2]), (float)(v[0] * w.x + v[1] * w.z + v[2]));
        }
    }
}
