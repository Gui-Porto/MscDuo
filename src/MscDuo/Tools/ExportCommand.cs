using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class ExportCommand : ConsoleCommand
    {
        const float Vertical = 20f;
        const float Cell = 0.25f;

        static readonly string[] Skipped = { "PLAYER", "SATSUMA", "CARPARTS", "ITEMS", "GUI", "Database", "MscDuo_Commands" };

        public override string Name => "duo_export";
        public override string Help => "duo_export [meia largura em metros = 20]  salva relevo, estrada e casa 2 em volta da extensão em relevo.obj para o Blender";

        public override void Run(string[] args)
        {
            var half = 20f;
            if (args.Length > 0 && !DevCommands.TryFloat(args[0], out half)) { ModConsole.Error("meia largura inválida"); return; }
            var house = HouseCloner.Clone;
            if (house == null) { ModConsole.Error("[MSC Duo] casa 2 não existe"); return; }

            var folder = System.IO.Path.Combine(MscDuo.Instance.SettingsFolder, "dumps");
            Directory.CreateDirectory(folder);
            var file = System.IO.Path.Combine(folder, "relevo.obj");
            ApronExtension.Restore();
            try
            {
                ModConsole.Print($"<color=orange>[MSC Duo]</color> {Export(house, half, file)} Arquivo: {file}");
            }
            finally
            {
                ApronExtension.Apply(house);
            }
        }

        static string Export(GameObject house, float half, string file)
        {
            var corners = ApronExtension.Corners(house);
            var center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
            var toLocal = Frame(house).inverse;
            var text = new StringBuilder();
            text.AppendLine("# MSC Duo relevo: coordenadas da casa 2 em eixos do Blender (x, z, y da Unity)");
            text.AppendLine("# eixos 2");
            foreach (var c in corners) text.AppendLine("# extensao " + Point(toLocal.MultiplyPoint3x4(c)));

            var done = new HashSet<Mesh>();
            var counts = new Dictionary<string, int>();
            var unreadable = 0;
            var offset = 1;
            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                var mesh = filter.sharedMesh;
                if (mesh == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy || Skip(filter.transform)) continue;
                var b = renderer.bounds;
                if (b.max.x < center.x - half || b.min.x > center.x + half || b.max.z < center.z - half || b.min.z > center.z + half) continue;
                if (b.max.y < center.y - Vertical || b.min.y > center.y + Vertical) continue;

                var batched = renderer.isPartOfStaticBatch;
                if (batched && !done.Add(mesh)) continue;
                Vector3[] vertices;
                try { vertices = mesh.vertices; }
                catch { vertices = null; }
                if (vertices == null || vertices.Length == 0)
                {
                    var collider = filter.GetComponent<MeshCollider>();
                    mesh = collider != null ? collider.sharedMesh : null;
                    try { vertices = mesh != null ? mesh.vertices : null; }
                    catch { vertices = null; }
                    if (vertices == null || vertices.Length == 0) { unreadable++; continue; }
                }

                var toWorld = batched ? Matrix4x4.identity : filter.transform.localToWorldMatrix;
                var uv = mesh.uv;
                var hasUv = uv != null && uv.Length == vertices.Length;
                var triangles = mesh.triangles;
                var used = new Dictionary<int, int>();
                var faces = new List<int>();
                var world = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) world[i] = toWorld.MultiplyPoint3x4(vertices[i]);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    if (!Near(world[triangles[t]], center, half) && !Near(world[triangles[t + 1]], center, half) && !Near(world[triangles[t + 2]], center, half)) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        var index = triangles[t + k];
                        int mapped;
                        if (!used.TryGetValue(index, out mapped)) used[index] = mapped = used.Count;
                        faces.Add(mapped);
                    }
                }
                if (faces.Count == 0) continue;

                var category = Category(filter.transform, house, batched);
                counts[category] = (counts.TryGetValue(category, out var n) ? n : 0) + 1;
                text.AppendLine($"o {category}|{Path(filter.transform)}");
                text.AppendLine($"usemtl {category}");
                var order = new int[used.Count];
                foreach (var pair in used) order[pair.Value] = pair.Key;
                foreach (var index in order) text.AppendLine("v " + Point(toLocal.MultiplyPoint3x4(world[index])));
                if (hasUv) foreach (var index in order) text.AppendLine($"vt {F(uv[index].x)} {F(uv[index].y)}");
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f] + offset, c = faces[f + 1] + offset, d = faces[f + 2] + offset;
                    text.AppendLine(hasUv ? $"f {a}/{a} {d}/{d} {c}/{c}" : $"f {a} {d} {c}");
                }
                offset += used.Count;
            }
            var cells = Scan(house, center, half, toLocal, text, ref offset);
            File.WriteAllText(file, text.ToString());

            var summary = new List<string>();
            foreach (var pair in counts) summary.Add($"{pair.Key} {pair.Value}");
            return $"relevo exportado ({string.Join(", ", summary.ToArray())}{(unreadable > 0 ? $", {unreadable} sem leitura" : "")}; chão varrido em {cells} pontos).";
        }

        static int Scan(GameObject house, Vector3 center, float half, Matrix4x4 toLocal, StringBuilder text, ref int offset)
        {
            var n = Mathf.CeilToInt(half * 2f / Cell) + 1;
            var points = new Vector3[n, n];
            var kinds = new string[n, n];
            var found = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var x = center.x - half + i * Cell;
                    var z = center.z - half + j * Cell;
                    RaycastHit best = default(RaycastHit);
                    var hasHit = false;
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, center.y + Vertical, z), Vector3.down, Vertical * 2f))
                    {
                        if (hit.collider.isTrigger || Skip(hit.collider.transform)) continue;
                        if (!hasHit || hit.point.y > best.point.y) { best = hit; hasHit = true; }
                    }
                    if (!hasHit) continue;
                    points[i, j] = toLocal.MultiplyPoint3x4(best.point);
                    kinds[i, j] = Category(best.collider.transform, house, false);
                    found++;
                }

            var groups = new Dictionary<string, List<int[]>>();
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    var kind = kinds[i, j];
                    if (kind == null || kinds[i + 1, j] == null || kinds[i, j + 1] == null || kinds[i + 1, j + 1] == null) continue;
                    List<int[]> list;
                    if (!groups.TryGetValue(kind, out list)) groups[kind] = list = new List<int[]>();
                    list.Add(new[] { i, j });
                }
            foreach (var pair in groups)
            {
                text.AppendLine($"o chao_{pair.Key}|varredura");
                text.AppendLine($"usemtl chao_{pair.Key}");
                var index = new Dictionary<int, int>();
                var faces = new List<int>();
                foreach (var cell in pair.Value)
                {
                    int i = cell[0], j = cell[1];
                    foreach (var k in new[] { i * n + j, (i + 1) * n + j, (i + 1) * n + j + 1, i * n + j + 1 })
                    {
                        int mapped;
                        if (!index.TryGetValue(k, out mapped))
                        {
                            index[k] = mapped = index.Count;
                            text.AppendLine("v " + Point(points[k / n, k % n]));
                        }
                        faces.Add(mapped + offset);
                    }
                }
                for (int f = 0; f < faces.Count; f += 4)
                    text.AppendLine($"f {faces[f]} {faces[f + 3]} {faces[f + 2]} {faces[f + 1]}");
                offset += index.Count;
            }
            return found;
        }

        internal static Matrix4x4 Frame(GameObject house)
        {
            var right = house.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            var forward = Vector3.Cross(right.normalized, Vector3.up);
            return Matrix4x4.TRS(house.transform.position, Quaternion.LookRotation(forward, Vector3.up), Vector3.one);
        }

        static bool Near(Vector3 p, Vector3 center, float half) =>
            Mathf.Abs(p.x - center.x) <= half && Mathf.Abs(p.z - center.z) <= half && Mathf.Abs(p.y - center.y) <= Vertical;

        static bool Skip(Transform t)
        {
            var root = t.root.name;
            foreach (var name in Skipped)
                if (root.StartsWith(name)) return true;
            return false;
        }

        static string Category(Transform t, GameObject house, bool batched)
        {
            if (t.IsChildOf(house.transform)) return "casa2";
            var path = Path(t);
            if (path == "MAP/MESH/TERRAIN_OBJ/Road") return "estrada";
            if (path.StartsWith("MAP/MESH/TERRAIN_OBJ")) return "terreno";
            if (path.StartsWith("MAP/MESH/FOLIAGE")) return "vegetacao";
            var collider = t.GetComponent<Collider>();
            foreach (var road in TerrainFlattener.RoadNames)
                if (t.name == road || (collider != null && collider.name == road)) return "estrada";
            if (t.name.ToLower().Contains("road")) return "estrada";
            return batched ? "lote" : "outros";
        }

        static string Point(Vector3 p) => $"{F(p.x)} {F(p.z)} {F(p.y)}";

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
