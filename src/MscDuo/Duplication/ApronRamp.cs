using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class ApronRamp
    {
        const float Slope = 0.15f;
        const float MaxRun = 8f;
        const float ApronCell = 0.25f;
        const float RampCell = 0.25f;
        const float SlabTolerance = 0.08f;
        const float Sink = 0.05f;
        const float SeamReach = 0.6f;
        const float ClearHeight = 2.5f;
        const float CorridorReach = 8f;
        const float CorridorSlack = 3f;
        const float MaxCorridor = 14f;

        static readonly string[] KeptRoots = { "MSCDUO", "PLAYER", "SATSUMA", "CARPARTS", "Database", "ITEMS", "STORE", "Systems", "Sheets", "GUI" };
        static readonly string[] KeptPaths = { "MAP/MESH/TERRAIN_OBJ", "MAP/MESH/FOLIAGE" };

        static readonly string[] TerrainPaths = { "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2" };

        static GameObject ramp;
        static readonly Dictionary<MeshFilter, Mesh> cutMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> cutColliders = new Dictionary<MeshCollider, Mesh>();
        static readonly List<Collider> disabledColliders = new List<Collider>();

        internal static void Apply(GameObject house)
        {
            Restore();

            float slabY;
            Collider slab;
            var apron = FindApron(house, out slabY, out slab);
            if (apron.Count == 0)
            {
                ModConsole.Warning("[MSC Duo] casa 2: laje externa não encontrada; rampa não criada.");
                return;
            }

            var area = ApronBounds(apron);
            area.Expand(new Vector3((MaxRun + CorridorReach) * 2f, 0f, (MaxRun + CorridorReach) * 2f));
            int cutTriangles, removed;
            var unreadable = ClearArea(house, apron, slabY, out cutTriangles, out removed);

            var painted = RepaintGround(apron);
            var mesh = BuildRamp(house, apron, area, slabY);
            if (mesh == null)
            {
                ModConsole.Print("<color=orange>[MSC Duo]</color> casa 2: laje já está no nível do chão; rampa não precisa.");
                return;
            }

            ramp = new GameObject("ApronRamp_P2");
            ramp.transform.parent = house.transform.parent;
            ramp.layer = slab != null ? slab.gameObject.layer : house.layer;
            ramp.AddComponent<MeshFilter>().sharedMesh = mesh;
            ramp.AddComponent<MeshRenderer>().sharedMaterial = roadMaterial ?? ConcreteMaterial();
            var collider = ramp.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            if (slab != null)
            {
                collider.sharedMaterial = slab.sharedMaterial;
                ramp.tag = slab.gameObject.tag;
            }

            var extra = unreadable.Count > 0 ? $" Sem leitura (ficaram): {string.Join(", ", unreadable.ToArray())}." : "";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: rampa em volta da laje ({apron.Count} pontos de laje, {mesh.vertexCount} vértices), {cutTriangles} triângulos cortados do caminho, {removed} objetos removidos, material {(roadMaterial != null ? roadMaterial.name : "concreto")}, {painted} pontos de terra viraram grama, superfície copiada de '{(slab != null ? slab.name : "-")}' (material {(slab != null && slab.sharedMaterial != null ? slab.sharedMaterial.name : "nenhum")}, tag {ramp.tag}).{extra}");
        }

        static List<Vector2> FindApron(GameObject house, out float slabY, out Collider slab)
        {
            slabY = 0f;
            slab = null;
            var bounds = HouseBounds(house);
            var top = bounds.max.y + 1f;
            var samples = new List<KeyValuePair<Vector2, RaycastHit>>();
            for (var x = bounds.min.x - 1f; x <= bounds.max.x + 1f; x += ApronCell)
                for (var z = bounds.min.z - 1f; z <= bounds.max.z + 1f; z += ApronCell)
                {
                    RaycastHit hit;
                    if (TopHit(house, x, z, top, out hit)) samples.Add(new KeyValuePair<Vector2, RaycastHit>(new Vector2(x, z), hit));
                }

            var heights = new Dictionary<int, int>();
            var flat = new List<KeyValuePair<Vector2, RaycastHit>>();
            int steep = 0, high = 0;
            foreach (var s in samples)
            {
                if (s.Value.normal.y < 0.95f) { steep++; continue; }
                float ground;
                if (!Ground.TryHeight(new Vector3(s.Key.x, s.Value.point.y, s.Key.y), 0.5f, out ground, house)) continue;
                var lift = s.Value.point.y - ground;
                if (lift < -0.3f || lift > 1.5f) { high++; continue; }
                flat.Add(s);
                var key = Mathf.RoundToInt(s.Value.point.y * 20f);
                heights[key] = heights.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            samples = flat;
            if (flat.Count == 0) ModConsole.Warning($"[MSC Duo] casa 2: laje: {flat.Count + steep + high} pontos da casa vistos de cima, {steep} inclinados, {high} longe do chão.");
            var best = int.MinValue;
            var bestCount = 0;
            foreach (var pair in heights)
                if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            var apron = new List<Vector2>();
            if (bestCount == 0) return apron;
            slabY = best / 20f;

            var sum = 0f;
            foreach (var s in samples)
            {
                if (Mathf.Abs(s.Value.point.y - slabY) > SlabTolerance) continue;
                apron.Add(s.Key);
                sum += s.Value.point.y;
                if (slab == null) slab = s.Value.collider;
            }
            slabY = sum / apron.Count;
            return LargestPatch(apron);
        }

        static List<Vector2> LargestPatch(List<Vector2> cells)
        {
            var grid = new HashSet<long>();
            var origin = cells[0];
            foreach (var c in cells) grid.Add(Key(c, origin));

            var core = new HashSet<long>();
            foreach (var k in grid)
            {
                int x = (int)(k >> 32), z = (int)(k & 0xffffffff);
                var solid = true;
                for (int dx = -1; dx <= 1 && solid; dx++)
                    for (int dz = -1; dz <= 1 && solid; dz++)
                        if (!grid.Contains(Pack(x + dx, z + dz))) solid = false;
                if (solid) core.Add(k);
            }

            var best = new List<long>();
            var seen = new HashSet<long>();
            foreach (var start in core)
            {
                if (seen.Contains(start)) continue;
                var patch = new List<long>();
                var queue = new Queue<long>();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    var k = queue.Dequeue();
                    patch.Add(k);
                    int x = (int)(k >> 32), z = (int)(k & 0xffffffff);
                    foreach (var n in new[] { Pack(x + 1, z), Pack(x - 1, z), Pack(x, z + 1), Pack(x, z - 1) })
                        if (core.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                }
                if (patch.Count > best.Count) best = patch;
            }

            var result = new HashSet<long>();
            foreach (var k in best)
            {
                int x = (int)(k >> 32), z = (int)(k & 0xffffffff);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var n = Pack(x + dx, z + dz);
                        if (grid.Contains(n)) result.Add(n);
                    }
            }
            var list = new List<Vector2>();
            foreach (var k in result)
                list.Add(new Vector2(origin.x + (int)(k >> 32) * ApronCell, origin.y + (int)(k & 0xffffffff) * ApronCell));
            return list;
        }

        static long Key(Vector2 c, Vector2 origin) =>
            Pack(Mathf.RoundToInt((c.x - origin.x) / ApronCell), Mathf.RoundToInt((c.y - origin.y) / ApronCell));

        static long Pack(int x, int z) => ((long)x << 32) | (uint)z;

        static int RepaintGround(List<Vector2> apron)
        {
            var area = ApronBounds(apron);
            area.Expand(new Vector3(MaxRun * 2f + 8f, 0f, MaxRun * 2f + 8f));
            var painted = 0;
            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                var filter = go != null ? go.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.EndsWith("_flat")) continue;
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var colors = mesh.colors;
                if (colors == null || colors.Length != vertices.Length) continue;

                var toWorld = go.transform.localToWorldMatrix;
                var distances = new float[vertices.Length];
                var counts = new Dictionary<int, int>();
                var samples = new Dictionary<int, Color>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    distances[i] = float.MaxValue;
                    var w = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (w.x < area.min.x || w.x > area.max.x || w.z < area.min.z || w.z > area.max.z) continue;
                    distances[i] = Distance(apron, w.x, w.z);
                    if (distances[i] < MaxRun + 4f || distances[i] > MaxRun + 12f) continue;
                    var c = colors[i];
                    var key = Mathf.RoundToInt(c.r * 10f) * 10000 + Mathf.RoundToInt(c.g * 10f) * 100 + Mathf.RoundToInt(c.b * 10f);
                    counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                    samples[key] = c;
                }
                var bestKey = -1;
                var bestCount = 0;
                foreach (var pair in counts) if (pair.Value > bestCount) { bestKey = pair.Key; bestCount = pair.Value; }
                if (bestCount == 0) continue;
                var grass = samples[bestKey];

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (distances[i] > MaxRun + 3f) continue;
                    var weight = distances[i] <= MaxRun ? 1f : 1f - (distances[i] - MaxRun) / 3f;
                    colors[i] = Color.Lerp(colors[i], grass, weight);
                    painted++;
                }
                mesh.colors = colors;
            }
            return painted;
        }

        static Material roadMaterial;

        static Mesh BuildRamp(GameObject house, List<Vector2> apron, Bounds area, float slabY)
        {
            var nx = Mathf.CeilToInt(area.size.x / RampCell) + 1;
            var nz = Mathf.CeilToInt(area.size.z / RampCell) + 1;
            var heights = new float[nx, nz];
            var above = new bool[nx, nz];
            var valid = new bool[nx, nz];
            var apronDistance = new float[nx, nz];
            var grounds = new float[nx, nz];
            var top = slabY + 6f;
            var roads = new List<Vector2>();
            roadMaterial = null;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var x = area.min.x + i * RampCell;
                    var z = area.min.z + j * RampCell;
                    apronDistance[i, j] = Distance(apron, x, z);
                    if (apronDistance[i, j] > MaxRun + CorridorReach) continue;
                    RaycastHit hit;
                    if (TopHit(house, x, z, top, out hit) && apronDistance[i, j] > SeamReach) continue;
                    if (!SurfaceAt(x, z, slabY, out grounds[i, j])) grounds[i, j] = slabY - 2f;
                    valid[i, j] = true;
                    Collider road;
                    if (RoadAt(x, z, slabY, out road))
                    {
                        roads.Add(new Vector2(x, z));
                        if (roadMaterial == null) roadMaterial = MaterialOf(road);
                    }
                }

            Smooth(grounds, valid, nx, nz, 3);

            var roadDistance = new float[nx, nz];
            var corridor = float.MaxValue;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!valid[i, j]) continue;
                    roadDistance[i, j] = roads.Count == 0 ? float.MaxValue : Nearest(roads, area.min.x + i * RampCell, area.min.z + j * RampCell);
                    corridor = Mathf.Min(corridor, apronDistance[i, j] + roadDistance[i, j]);
                }
            var fillLimit = corridor <= MaxCorridor ? corridor + CorridorSlack : -1f;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!valid[i, j]) continue;
                    var ground = grounds[i, j];
                    var distance = apronDistance[i, j];
                    var rampY = distance <= SeamReach && distance < 0.01f ? slabY - 0.02f : slabY - distance * Slope;
                    var fill = apronDistance[i, j] + roadDistance[i, j] <= fillLimit;
                    var raised = distance <= MaxRun && rampY > ground + 0.01f;
                    above[i, j] = raised || fill;
                    heights[i, j] = raised ? rampY : fill ? ground + 0.03f : ground - Sink;
                }

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var index = new int[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    index[i, j] = -1;

            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    if (!valid[i, j] || !valid[i + 1, j] || !valid[i, j + 1] || !valid[i + 1, j + 1]) continue;
                    if (!above[i, j] && !above[i + 1, j] && !above[i, j + 1] && !above[i + 1, j + 1]) continue;
                    var a = Vertex(i, j, area, heights, index, vertices, uv);
                    var b = Vertex(i, j + 1, area, heights, index, vertices, uv);
                    var c = Vertex(i + 1, j + 1, area, heights, index, vertices, uv);
                    var d = Vertex(i + 1, j, area, heights, index, vertices, uv);
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(a); triangles.Add(c); triangles.Add(d);
                }

            if (triangles.Count == 0) return null;
            if (vertices.Count > 64000)
            {
                ModConsole.Warning($"[MSC Duo] casa 2: rampa grande demais ({vertices.Count} vértices).");
                return null;
            }
            var mesh = new Mesh { name = "ApronRamp_P2" };
            mesh.vertices = vertices.ToArray();
            mesh.uv = uv.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Vertex(int i, int j, Bounds area, float[,] heights, int[,] index, List<Vector3> vertices, List<Vector2> uv)
        {
            if (index[i, j] >= 0) return index[i, j];
            var p = new Vector3(area.min.x + i * RampCell, heights[i, j], area.min.z + j * RampCell);
            index[i, j] = vertices.Count;
            vertices.Add(p);
            uv.Add(new Vector2(p.x * 0.25f, p.z * 0.25f));
            return index[i, j];
        }

        static List<string> ClearArea(GameObject house, List<Vector2> apron, float slabY, out int cutTriangles, out int removed)
        {
            cutTriangles = 0;
            removed = 0;
            var unreadable = new List<string>();
            var area = ApronBounds(apron);
            area.Expand(new Vector3(MaxRun * 2f, 0f, MaxRun * 2f));
            var floor = slabY - 3f;
            var ceiling = slabY + ClearHeight;

            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null || Kept(filter.transform, house)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !Overlaps(renderer.bounds, area, floor, ceiling)) continue;

                Vector3[] vertices;
                int[] triangles;
                try
                {
                    vertices = filter.sharedMesh.vertices;
                    triangles = filter.sharedMesh.triangles;
                }
                catch
                {
                    if (!unreadable.Contains(filter.name)) unreadable.Add(filter.name);
                    continue;
                }

                var toWorld = filter.transform.localToWorldMatrix;
                var original = filter.sharedMesh;
                var subMeshes = new int[original.subMeshCount][];
                var cut = 0;
                var kept = 0;
                for (int s = 0; s < subMeshes.Length; s++)
                {
                    var source = original.GetTriangles(s);
                    var list = new List<int>(source.Length);
                    for (int t = 0; t < source.Length; t += 3)
                    {
                        var center = (toWorld.MultiplyPoint3x4(vertices[source[t]]) + toWorld.MultiplyPoint3x4(vertices[source[t + 1]]) + toWorld.MultiplyPoint3x4(vertices[source[t + 2]])) / 3f;
                        if (center.y > floor && center.y < ceiling && Distance(apron, center.x, center.z) <= MaxRun)
                        {
                            cut++;
                            continue;
                        }
                        list.Add(source[t]); list.Add(source[t + 1]); list.Add(source[t + 2]);
                    }
                    subMeshes[s] = list.ToArray();
                    kept += list.Count / 3;
                }
                if (cut == 0) continue;

                var mesh = Object.Instantiate(original);
                mesh.name = original.name + "_ramp";
                for (int s = 0; s < subMeshes.Length; s++) mesh.SetTriangles(subMeshes[s], s);
                cutMeshes[filter] = original;
                filter.sharedMesh = mesh;
                cutTriangles += cut;

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
                if (b.size.x > 4f || b.size.z > 4f || !Overlaps(b, area, floor, ceiling)) continue;
                if (Distance(apron, b.center.x, b.center.z) > MaxRun) continue;
                collider.enabled = false;
                disabledColliders.Add(collider);
                removed++;
            }
            return unreadable;
        }

        static bool Kept(Transform t, GameObject house)
        {
            if (t.IsChildOf(house.transform)) return true;
            if (ramp != null && t.IsChildOf(ramp.transform)) return true;
            var root = t.root.name;
            foreach (var kept in KeptRoots)
                if (root.StartsWith(kept)) return true;
            var path = Path(t);
            foreach (var kept in KeptPaths)
                if (path.StartsWith(kept)) return true;
            return false;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        static bool Overlaps(Bounds b, Bounds area, float floor, float ceiling) =>
            b.max.x >= area.min.x && b.min.x <= area.max.x && b.max.z >= area.min.z && b.min.z <= area.max.z && b.max.y > floor && b.min.y < ceiling;

        static void Restore()
        {
            if (ramp != null) Object.DestroyImmediate(ramp);
            ramp = null;
            foreach (var pair in cutMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var pair in cutColliders)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMesh = null;
                pair.Key.sharedMesh = pair.Value;
            }
            foreach (var collider in disabledColliders) if (collider != null) collider.enabled = true;
            cutMeshes.Clear();
            cutColliders.Clear();
            disabledColliders.Clear();
        }

        static float Distance(List<Vector2> apron, float x, float z)
        {
            var best = float.MaxValue;
            foreach (var p in apron)
            {
                var dx = p.x - x;
                var dz = p.y - z;
                var d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Max(0f, Mathf.Sqrt(best) - ApronCell * 0.5f);
        }

        static Bounds ApronBounds(List<Vector2> apron)
        {
            var b = new Bounds(new Vector3(apron[0].x, 0f, apron[0].y), Vector3.zero);
            foreach (var p in apron) b.Encapsulate(new Vector3(p.x, 0f, p.y));
            return b;
        }

        static bool TopHit(GameObject house, float x, float z, float top, out RaycastHit best)
        {
            best = new RaycastHit();
            var found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, 30f))
            {
                if (hit.collider.isTrigger || !hit.collider.transform.IsChildOf(house.transform)) continue;
                if (found && hit.point.y <= best.point.y) continue;
                best = hit;
                found = true;
            }
            return found;
        }

        static Bounds HouseBounds(GameObject house)
        {
            var found = false;
            var bounds = new Bounds(house.transform.position, Vector3.zero);
            foreach (var c in house.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return bounds;
        }

        internal static bool SurfaceAt(float x, float z, float y, out float height)
        {
            height = float.MinValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, y + 20f, z), Vector3.down, 40f))
            {
                if (hit.collider.isTrigger || !IsDrivable(hit.collider)) continue;
                if (hit.point.y > height) height = hit.point.y;
            }
            return height > float.MinValue;
        }

        internal static bool IsDrivable(Collider c)
        {
            foreach (var name in TerrainFlattener.RoadNames)
                if (c.name == name) return true;
            return Path(c.transform).StartsWith("MAP/MESH/TERRAIN_OBJ");
        }

        static void Smooth(float[,] heights, bool[,] valid, int nx, int nz, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var next = (float[,])heights.Clone();
                for (int i = 1; i < nx - 1; i++)
                    for (int j = 1; j < nz - 1; j++)
                    {
                        if (!valid[i, j]) continue;
                        float sum = 0f;
                        var count = 0;
                        for (int di = -1; di <= 1; di++)
                            for (int dj = -1; dj <= 1; dj++)
                            {
                                if (!valid[i + di, j + dj]) continue;
                                sum += heights[i + di, j + dj];
                                count++;
                            }
                        next[i, j] = sum / count;
                    }
                System.Array.Copy(next, heights, heights.Length);
            }
        }

        static bool RoadAt(float x, float z, float y, out Collider road)
        {
            road = null;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, y + 20f, z), Vector3.down, 40f))
                foreach (var name in TerrainFlattener.RoadNames)
                    if (hit.collider.name == name) { road = hit.collider; return true; }
            return false;
        }

        static Material MaterialOf(Collider road)
        {
            var renderer = road.GetComponent<Renderer>() ?? road.GetComponentInChildren<Renderer>() ?? road.GetComponentInParent<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        static float Nearest(List<Vector2> points, float x, float z)
        {
            var best = float.MaxValue;
            foreach (var p in points)
            {
                var dx = p.x - x;
                var dz = p.y - z;
                var d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        static Material concrete;

        static Material ConcreteMaterial()
        {
            if (concrete != null) return concrete;
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            concrete = new Material(shader) { name = "MscDuoConcrete", color = new Color(0.56f, 0.55f, 0.52f) };
            if (concrete.HasProperty("_Glossiness")) concrete.SetFloat("_Glossiness", 0.05f);
            return concrete;
        }
    }
}
