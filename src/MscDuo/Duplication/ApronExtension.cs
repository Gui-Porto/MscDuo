using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class ApronExtension
    {
        static readonly Vector3 DrawnPosition = new Vector3(-7.46f, 0.38f, 45.43f);
        const float DrawnYaw = 241.1472f;

        static readonly Vector2[] Drawn =
        {
            new Vector2(-13.29f, 35.69f),
            new Vector2(-10.15f, 30.43f),
            new Vector2(-18.19f, 32.41f),
            new Vector2(-12.35f, 29.00f),
        };

        const int Steps = 24;
        const float Skirt = 0.8f;
        const float ClearHeight = 2.5f;
        const float TerrainGap = 0.08f;
        const float RoadCover = 0.03f;
        const float Berm = 3f;
        const float BermSlope = 0.25f;

        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };
        static readonly string[] KeptRoots = { "MSCDUO", "PLAYER", "SATSUMA", "CARPARTS", "Database", "ITEMS", "STORE", "Systems", "Sheets", "GUI" };
        static readonly string[] TerrainPaths = { "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2" };

        static GameObject slab;
        static readonly Dictionary<MeshFilter, Mesh> cutMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> cutColliders = new Dictionary<MeshCollider, Mesh>();
        static readonly List<Collider> disabled = new List<Collider>();

        internal static void Apply(GameObject house)
        {
            Restore();
            var corners = Corners(house);
            Collider apron;
            Material material;
            var apronY = ApronHeight(house, corners, out apron, out material);
            var roadLeft = RoadHeight(corners[2]);
            var roadRight = RoadHeight(corners[3]);
            var heights = new[] { apronY, apronY, roadLeft, roadRight };

            int cut, removed;
            var unreadable = Clear(house, corners, Mathf.Max(apronY, Mathf.Max(roadLeft, roadRight)), out cut, out removed);

            slab = new GameObject("ApronExtension_P2");
            slab.transform.parent = house.transform.parent;
            if (apron != null) slab.layer = apron.gameObject.layer;
            var mesh = Build(corners, heights);
            slab.AddComponent<MeshFilter>().sharedMesh = mesh;
            slab.AddComponent<MeshRenderer>().sharedMaterial = material ?? Fallback();
            var collider = slab.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            if (apron != null)
            {
                collider.sharedMaterial = apron.sharedMaterial;
                slab.tag = apron.gameObject.tag;
            }

            var lowered = LowerTerrain(corners, collider);
            var extra = unreadable.Count > 0 ? $" Sem leitura (ficaram): {string.Join(", ", unreadable.ToArray())}." : "";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: extensão do cimento criada (laje {apronY:0.00}, rua {roadLeft:0.00}/{roadRight:0.00}, material {(material != null ? material.name : "cinza")}), {cut} triângulos e {removed} objetos tirados do caminho, {lowered} pontos de terreno rebaixados.{extra}");
        }

        static Vector3[] Corners(GameObject house)
        {
            var drawn = Matrix4x4.TRS(DrawnPosition, HouseCloner.RotationFor(DrawnYaw), Vector3.one);
            var now = Matrix4x4.TRS(house.transform.position, house.transform.rotation, Vector3.one);
            var move = now * drawn.inverse;
            var corners = new Vector3[Drawn.Length];
            for (int i = 0; i < Drawn.Length; i++)
                corners[i] = move.MultiplyPoint3x4(new Vector3(Drawn[i].x, DrawnPosition.y, Drawn[i].y));
            return corners;
        }

        static float ApronHeight(GameObject house, Vector3[] corners, out Collider apron, out Material material)
        {
            apron = null;
            material = null;
            var heights = new List<float>();
            var inward = ((corners[0] + corners[1]) - (corners[2] + corners[3])).normalized;
            inward.y = 0f;
            for (int i = 0; i <= 10; i++)
            {
                var p = Vector3.Lerp(corners[0], corners[1], i / 10f) + inward * 0.5f;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, house.transform.position.y + 1f, p.z), Vector3.down, 6f))
                {
                    if (hit.collider.isTrigger || !hit.collider.transform.IsChildOf(house.transform)) continue;
                    heights.Add(hit.point.y);
                    if (apron == null) apron = hit.collider;
                }
            }
            if (heights.Count == 0) return house.transform.position.y - 0.4f;
            heights.Sort();
            var y = heights[heights.Count / 2];

            material = ConcreteFrom(house, corners, inward, y);
            return y;
        }

        static float RoadHeight(Vector3 p)
        {
            var road = float.MinValue;
            var ground = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, p.y + 20f, p.z), Vector3.down, 40f))
            {
                if (hit.collider.isTrigger) continue;
                if (IsRoad(hit.collider)) road = Mathf.Max(road, hit.point.y);
                else if (Path(hit.collider.transform).StartsWith("MAP/MESH/TERRAIN_OBJ")) ground = Mathf.Max(ground, hit.point.y);
            }
            return road > float.MinValue ? road : ground > float.MinValue ? ground : p.y - 1f;
        }

        static float RoadOnly(Vector3 p)
        {
            var road = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, p.y + 20f, p.z), Vector3.down, 40f))
                if (!hit.collider.isTrigger && IsRoad(hit.collider)) road = Mathf.Max(road, hit.point.y);
            return road;
        }

        static float SurfaceHeight(Vector3[] c, float[] h, float u, float v) =>
            Mathf.Lerp(Mathf.Lerp(h[0], h[1], u), Mathf.Lerp(h[2], h[3], u), v);

        static Vector3 SurfacePoint(Vector3[] c, float[] h, float u, float v)
        {
            var near = Vector3.Lerp(c[0], c[1], u);
            var far = Vector3.Lerp(c[2], c[3], u);
            var p = Vector3.Lerp(near, far, v);
            p.y = SurfaceHeight(c, h, u, v) + (v < 0.02f ? -0.01f : 0.02f);
            return p;
        }

        static Mesh Build(Vector3[] c, float[] h)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var grid = new int[Steps + 1, Steps + 1];
            for (int i = 0; i <= Steps; i++)
                for (int j = 0; j <= Steps; j++)
                {
                    var u = i / (float)Steps;
                    var v = Mathf.Lerp(-0.12f, 1.03f, j / (float)Steps);
                    var p = SurfacePoint(c, h, u, v);
                    var road = RoadOnly(p);
                    if (road > float.MinValue) p.y = Mathf.Max(p.y, road + RoadCover);
                    grid[i, j] = vertices.Count;
                    vertices.Add(p);
                    uv.Add(UvAt(p));
                }
            for (int i = 0; i < Steps; i++)
                for (int j = 0; j < Steps; j++)
                {
                    int a = grid[i, j], b = grid[i + 1, j], d = grid[i, j + 1], e = grid[i + 1, j + 1];
                    Upward(triangles, vertices, a, b, e);
                    Upward(triangles, vertices, a, e, d);
                }
            for (int k = 0; k < Steps; k++)
            {
                SkirtEdge(vertices[grid[0, k]], vertices[grid[0, k + 1]], vertices, uv, triangles);
                SkirtEdge(vertices[grid[Steps, k]], vertices[grid[Steps, k + 1]], vertices, uv, triangles);
                SkirtEdge(vertices[grid[k, Steps]], vertices[grid[k + 1, Steps]], vertices, uv, triangles);
            }

            var mesh = new Mesh { name = "ApronExtension_P2" };
            mesh.vertices = vertices.ToArray();
            mesh.uv = uv.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void SkirtEdge(Vector3 a, Vector3 b, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var c = new Vector3(b.x, b.y - Skirt, b.z);
            var d = new Vector3(a.x, a.y - Skirt, a.z);
            for (int side = 0; side < 2; side++)
            {
                var k = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                foreach (var p in new[] { a, b, c, d }) uv.Add(new Vector2((p.x + p.z) * 0.5f, p.y * 0.5f));
                if (side == 0)
                {
                    triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2);
                    triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 3);
                }
                else
                {
                    triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 1);
                    triangles.Add(k); triangles.Add(k + 3); triangles.Add(k + 2);
                }
            }
        }

        static void Upward(List<int> t, List<Vector3> v, int a, int b, int c)
        {
            var normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (normal.y >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }

        static bool Inside(Vector3[] c, Vector3 p, float margin)
        {
            var poly = new[] { c[0], c[1], c[3], c[2] };
            var center = (c[0] + c[1] + c[2] + c[3]) / 4f;
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i] + (poly[i] - center).normalized * margin;
                var b = poly[j] + (poly[j] - center).normalized * margin;
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        static List<string> Clear(GameObject house, Vector3[] corners, float top, out int cut, out int removed)
        {
            cut = 0;
            removed = 0;
            var unreadable = new List<string>();
            var min = Vector3.Min(Vector3.Min(corners[0], corners[1]), Vector3.Min(corners[2], corners[3]));
            var max = Vector3.Max(Vector3.Max(corners[0], corners[1]), Vector3.Max(corners[2], corners[3]));
            var floor = Mathf.Min(min.y, max.y) - 3f;
            var ceiling = top + ClearHeight;

            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null || Kept(filter.transform, house)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null) continue;
                var bounds = renderer.bounds;
                if (bounds.max.x < min.x - 1f || bounds.min.x > max.x + 1f || bounds.max.z < min.z - 1f || bounds.min.z > max.z + 1f) continue;
                if (bounds.max.y < floor || bounds.min.y > ceiling) continue;

                Vector3[] vertices;
                try { vertices = filter.sharedMesh.vertices; }
                catch
                {
                    if (!unreadable.Contains(filter.name)) unreadable.Add(filter.name);
                    continue;
                }
                var original = filter.sharedMesh;
                var toWorld = filter.transform.localToWorldMatrix;
                var subMeshes = new int[original.subMeshCount][];
                int removedHere = 0, kept = 0;
                for (int s = 0; s < subMeshes.Length; s++)
                {
                    var source = original.GetTriangles(s);
                    var list = new List<int>(source.Length);
                    for (int t = 0; t < source.Length; t += 3)
                    {
                        var center = (toWorld.MultiplyPoint3x4(vertices[source[t]]) + toWorld.MultiplyPoint3x4(vertices[source[t + 1]]) + toWorld.MultiplyPoint3x4(vertices[source[t + 2]])) / 3f;
                        if (center.y > floor && center.y < ceiling && Inside(corners, center, 0.3f)) { removedHere++; continue; }
                        list.Add(source[t]); list.Add(source[t + 1]); list.Add(source[t + 2]);
                    }
                    subMeshes[s] = list.ToArray();
                    kept += list.Count / 3;
                }
                if (removedHere == 0) continue;
                var mesh = Object.Instantiate(original);
                for (int s = 0; s < subMeshes.Length; s++) mesh.SetTriangles(subMeshes[s], s);
                cutMeshes[filter] = original;
                filter.sharedMesh = mesh;
                cut += removedHere;
                var collider = filter.GetComponent<MeshCollider>();
                if (collider != null && collider.sharedMesh == original)
                {
                    cutColliders[collider] = original;
                    collider.sharedMesh = null;
                    if (kept > 0) collider.sharedMesh = mesh;
                }
                if (kept == 0) removed++;
            }

            foreach (var collider in Object.FindObjectsOfType<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || collider is MeshCollider || Kept(collider.transform, house)) continue;
                var b = collider.bounds;
                if (b.size.x > 6f || b.size.z > 6f || b.max.y < floor || b.min.y > ceiling) continue;
                if (!Inside(corners, b.center, 0.5f)) continue;
                collider.enabled = false;
                disabled.Add(collider);
                removed++;
            }
            return unreadable;
        }

        static int LowerTerrain(Vector3[] corners, Collider surface)
        {
            var lowered = 0;
            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                var filter = go != null ? go.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                if (!mesh.name.EndsWith("_flat"))
                {
                    var copy = Object.Instantiate(mesh);
                    copy.name = mesh.name + "_flat";
                    cutMeshes[filter] = mesh;
                    filter.sharedMesh = mesh = copy;
                }
                var vertices = mesh.vertices;
                var toWorld = go.transform.localToWorldMatrix;
                var toMesh = go.transform.worldToLocalMatrix;
                var changed = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (!Inside(corners, w, 0.6f))
                    {
                        var edge = EdgeDistance(corners, w);
                        if (edge > Berm) continue;
                        RaycastHit rim;
                        if (!NearestSurface(surface, w, out rim)) continue;
                        var rise = rim.point.y - 0.03f - edge * BermSlope;
                        if (w.y >= rise) continue;
                        w.y = rise;
                        vertices[i] = toMesh.MultiplyPoint3x4(w);
                        lowered++;
                        changed = true;
                        continue;
                    }
                    RaycastHit hit;
                    if (!surface.Raycast(new Ray(new Vector3(w.x, w.y + 20f, w.z), Vector3.down), out hit, 40f) && !NearestSurface(surface, w, out hit)) continue;
                    var target = hit.point.y - TerrainGap;
                    if (w.y <= target) continue;
                    w.y = target;
                    vertices[i] = toMesh.MultiplyPoint3x4(w);
                    lowered++;
                    changed = true;
                }
                if (!changed) continue;
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var collider = go.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    collider.sharedMesh = null;
                    collider.sharedMesh = mesh;
                }
            }
            return lowered;
        }

        static float EdgeDistance(Vector3[] c, Vector3 p)
        {
            var poly = new[] { c[0], c[1], c[3], c[2] };
            var best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = new Vector2(poly[j].x, poly[j].z);
                var b = new Vector2(poly[i].x, poly[i].z);
                var q = new Vector2(p.x, p.z);
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            return best;
        }

        static bool NearestSurface(Collider surface, Vector3 w, out RaycastHit hit)
        {
            var b = surface.bounds;
            var p = new Vector3(Mathf.Clamp(w.x, b.min.x + 0.05f, b.max.x - 0.05f), b.max.y + 5f, Mathf.Clamp(w.z, b.min.z + 0.05f, b.max.z - 0.05f));
            return surface.Raycast(new Ray(p, Vector3.down), out hit, 20f);
        }

        static void Restore()
        {
            if (slab != null) Object.DestroyImmediate(slab);
            slab = null;
            foreach (var pair in cutMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var pair in cutColliders)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMesh = null;
                pair.Key.sharedMesh = pair.Value;
            }
            foreach (var c in disabled) if (c != null) c.enabled = true;
            cutMeshes.Clear();
            cutColliders.Clear();
            disabled.Clear();
        }

        static bool Kept(Transform t, GameObject house)
        {
            if (t.IsChildOf(house.transform) || (slab != null && t.IsChildOf(slab.transform))) return true;
            var root = t.root.name;
            foreach (var kept in KeptRoots)
                if (root.StartsWith(kept)) return true;
            var path = Path(t);
            return path.StartsWith("MAP/MESH/TERRAIN_OBJ") || path.StartsWith("MAP/MESH/FOLIAGE") || IsRoad(t.GetComponent<Collider>());
        }

        static bool IsRoad(Collider c)
        {
            if (c == null) return false;
            foreach (var name in RoadNames)
                if (c.name == name) return true;
            return false;
        }

        static Material concrete;
        static Vector3[] uvMap;

        static Vector3[] FitUv(List<KeyValuePair<Vector3, Vector2>> pairs)
        {
            var m = new float[3, 3];
            var bu = new float[3];
            var bv = new float[3];
            foreach (var pair in pairs)
            {
                var row = new[] { pair.Key.x, pair.Key.z, 1f };
                for (int a = 0; a < 3; a++)
                {
                    for (int b = 0; b < 3; b++) m[a, b] += row[a] * row[b];
                    bu[a] += row[a] * pair.Value.x;
                    bv[a] += row[a] * pair.Value.y;
                }
            }
            var u = Solve(m, bu);
            var v = Solve(m, bv);
            return u == null || v == null ? null : new[] { u.Value, v.Value };
        }

        static Vector3? Solve(float[,] m, float[] b)
        {
            var det = Det(m[0, 0], m[0, 1], m[0, 2], m[1, 0], m[1, 1], m[1, 2], m[2, 0], m[2, 1], m[2, 2]);
            if (Mathf.Abs(det) < 1e-9f) return null;
            var x = Det(b[0], m[0, 1], m[0, 2], b[1], m[1, 1], m[1, 2], b[2], m[2, 1], m[2, 2]) / det;
            var y = Det(m[0, 0], b[0], m[0, 2], m[1, 0], b[1], m[1, 2], m[2, 0], b[2], m[2, 2]) / det;
            var z = Det(m[0, 0], m[0, 1], b[0], m[1, 0], m[1, 1], b[1], m[2, 0], m[2, 1], b[2]) / det;
            return new Vector3(x, y, z);
        }

        static float Det(float a, float b, float c, float d, float e, float f, float g, float h, float i) =>
            a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

        static Vector2 UvAt(Vector3 p) =>
            uvMap == null
                ? new Vector2(p.x * 0.5f, p.z * 0.5f)
                : new Vector2(uvMap[0].x * p.x + uvMap[0].y * p.z + uvMap[0].z, uvMap[1].x * p.x + uvMap[1].y * p.z + uvMap[1].z);

        static Material ConcreteFrom(GameObject house, Vector3[] corners, Vector3 inward, float slabY)
        {
            if (concrete != null) return concrete;
            MeshRenderer bestRenderer = null;
            List<KeyValuePair<Vector3, Vector2>> bestPairs = null;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            var samples = 0;
            foreach (var renderer in house.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null) continue;
                var b = renderer.bounds;
                if (b.min.y > slabY + 0.1f || b.max.y < slabY - 0.1f) continue;
                Vector2 lo = Vector2.one * float.MaxValue, hi = Vector2.one * float.MinValue;
                var count = 0;
                var pairs = new List<KeyValuePair<Vector3, Vector2>>();
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                try
                {
                    collider.sharedMesh = filter.sharedMesh;
                    for (int i = 0; i <= 6; i++)
                        for (int k = 1; k <= 4; k++)
                        {
                            var p = Vector3.Lerp(corners[0], corners[1], i / 6f) + inward * (0.6f * k);
                            RaycastHit hit;
                            if (!collider.Raycast(new Ray(new Vector3(p.x, slabY + 2f, p.z), Vector3.down), out hit, 4f)) continue;
                            if (hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - slabY) > 0.1f) continue;
                            lo = Vector2.Min(lo, hit.textureCoord);
                            hi = Vector2.Max(hi, hit.textureCoord);
                            pairs.Add(new KeyValuePair<Vector3, Vector2>(hit.point, hit.textureCoord));
                            count++;
                        }
                }
                catch { count = 0; }
                finally { Object.DestroyImmediate(collider); }
                if (count <= samples) continue;
                samples = count;
                bestRenderer = renderer;
                bestPairs = pairs;
                min = lo;
                max = hi;
            }
            if (bestRenderer == null || samples < 3 || (max - min).magnitude < 0.0005f)
            {
                ModConsole.Warning($"[MSC Duo] casa 2: superfície de cimento da laje não encontrada ({samples} amostras).");
                return null;
            }
            var source = bestRenderer.sharedMaterial;
            var texture = source.mainTexture;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: cimento da laje é '{bestRenderer.name}' ({source.name}).");

            uvMap = FitUv(bestPairs);
            if (uvMap == null) return null;
            concrete = source;
            return concrete;
        }

        static Material fallback;

        static Material Fallback()
        {
            if (fallback != null) return fallback;
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            fallback = new Material(shader) { name = "MscDuoConcrete", color = new Color(0.56f, 0.55f, 0.52f) };
            return fallback;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
