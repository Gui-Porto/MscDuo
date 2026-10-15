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
        const float RampCell = 0.5f;
        const float SlabTolerance = 0.08f;
        const float Sink = 0.05f;
        const float SeamReach = 0.6f;
        const float ClearHeight = 2.5f;

        static readonly string[] KeptRoots = { "MSCDUO", "PLAYER", "SATSUMA", "CARPARTS", "Database", "ITEMS", "STORE", "Systems", "Sheets", "GUI" };
        static readonly string[] KeptPaths = { "MAP/MESH/TERRAIN_OBJ", "MAP/MESH/FOLIAGE" };

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
            area.Expand(new Vector3(MaxRun * 2f, 0f, MaxRun * 2f));
            int cutTriangles, removed;
            var unreadable = ClearArea(house, apron, slabY, out cutTriangles, out removed);

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
            var renderer = ramp.AddComponent<MeshRenderer>();
            var material = Concrete(house, slab);
            if (material != null) renderer.sharedMaterial = material;
            ramp.AddComponent<MeshCollider>().sharedMesh = mesh;

            var extra = unreadable.Count > 0 ? $" Sem leitura (ficaram): {string.Join(", ", unreadable.ToArray())}." : "";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: rampa em volta da laje ({apron.Count} pontos de laje, {mesh.vertexCount} vértices), {cutTriangles} triângulos cortados do caminho, {removed} objetos removidos.{extra}");
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
            var low = bounds.min.y + 1.5f;
            foreach (var s in samples)
            {
                if (s.Value.point.y > low) continue;
                var key = Mathf.RoundToInt(s.Value.point.y * 20f);
                heights[key] = heights.TryGetValue(key, out var n) ? n + 1 : 1;
            }
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
            return apron;
        }

        static Mesh BuildRamp(GameObject house, List<Vector2> apron, Bounds area, float slabY)
        {
            var nx = Mathf.CeilToInt(area.size.x / RampCell) + 1;
            var nz = Mathf.CeilToInt(area.size.z / RampCell) + 1;
            var heights = new float[nx, nz];
            var above = new bool[nx, nz];
            var valid = new bool[nx, nz];
            var top = slabY + 6f;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var x = area.min.x + i * RampCell;
                    var z = area.min.z + j * RampCell;
                    var distance = Distance(apron, x, z);
                    if (distance > MaxRun) continue;
                    RaycastHit hit;
                    var onHouse = TopHit(house, x, z, top, out hit);
                    if (onHouse && distance > SeamReach) continue;

                    float ground;
                    if (!Ground.TryHeight(new Vector3(x, slabY, z), 3f, out ground, house)) ground = slabY - 2f;
                    var rampY = onHouse ? slabY - 0.02f : slabY - distance * Slope;
                    above[i, j] = rampY > ground + 0.01f;
                    heights[i, j] = above[i, j] ? rampY : ground - Sink;
                    valid[i, j] = true;
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

            if (triangles.Count == 0 || vertices.Count > 64000) return null;
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
            uv.Add(new Vector2(p.x * 0.5f, p.z * 0.5f));
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

        static Material Concrete(GameObject house, Collider slab)
        {
            if (slab != null)
            {
                var r = slab.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
            }
            Material fallback = null;
            foreach (var r in house.GetComponentsInChildren<Renderer>(true))
            {
                var name = r.name.EndsWith("_mesh") && r.transform.parent != null ? r.transform.parent.name : r.name;
                if (name.StartsWith("house_base_concrete")) return r.sharedMaterial;
                if (fallback == null && name.StartsWith("house_base")) fallback = r.sharedMaterial;
            }
            return fallback;
        }
    }
}
