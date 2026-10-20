using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        const string ModelResource = "MscDuo.extensao.obj";
        const string TextureResource = "MscDuo.extensao.png";
        const string FillResource = "MscDuo.aterro.obj";
        const string SlabName = "house_base_concrete_mesh";
        const float ClearHeight = 2.5f;
        const float TerrainGap = 0.08f;
        const float SkirtWidth = 2.5f;
        const int SkirtRows = 8;
        const float SkirtInset = 0.03f;
        const float SkirtLift = 0.02f;
        const float SkirtTuck = 0.04f;

        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };
        static readonly string[] KeptRoots = { "MSCDUO", "PLAYER", "SATSUMA", "CARPARTS", "Database", "ITEMS", "STORE", "Systems", "Sheets", "GUI" };
        static readonly string[] TerrainPaths = { "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2" };

        static GameObject slab;
        static GameObject fill;
        static GameObject skirt;
        static Mesh fillModel;
        static Mesh model;
        static Vector2[][] outline;
        static float top;
        static Texture2D texture;
        static Material concrete;
        static readonly Dictionary<MeshFilter, Mesh> cutMeshes = new Dictionary<MeshFilter, Mesh>();
        static readonly Dictionary<MeshCollider, Mesh> cutColliders = new Dictionary<MeshCollider, Mesh>();
        static readonly List<Collider> disabled = new List<Collider>();

        internal static MeshRenderer ConcreteRenderer { get; private set; }

        internal static void Apply(GameObject house)
        {
            Restore();
            if (!LoadModel())
            {
                ModConsole.Error("[MSC Duo] casa 2: modelo da extensão não encontrado na DLL.");
                return;
            }

            var frame = HouseCloner.LocalFrame(house);
            int cut, removed;
            var unreadable = Clear(house, frame, out cut, out removed);

            slab = new GameObject("ApronExtension_P2");
            slab.transform.parent = house.transform.parent;
            slab.transform.position = frame.MultiplyPoint3x4(Vector3.zero);
            slab.transform.rotation = Quaternion.LookRotation(frame.GetColumn(2), frame.GetColumn(1));
            slab.AddComponent<MeshFilter>().sharedMesh = model;
            slab.AddComponent<MeshRenderer>().sharedMaterial = Concrete(house);
            var collider = slab.AddComponent<MeshCollider>();
            collider.sharedMesh = model;
            var apron = ConcreteRenderer != null ? ConcreteRenderer.GetComponentInParent<Collider>() : null;
            if (apron != null)
            {
                slab.layer = apron.gameObject.layer;
                slab.tag = apron.gameObject.tag;
                collider.sharedMaterial = apron.sharedMaterial;
            }

            var lowered = LowerTerrain(house, frame);
            var filled = BuildFill(house, frame);
            string skirted;
            try { skirted = BuildSkirt(house, frame); }
            catch (System.Exception e) { skirted = "rampa de grama falhou: " + e.Message; }
            filled += ", " + skirted;
            var extra = unreadable.Count > 0 ? $" Sem leitura (ficaram): {string.Join(", ", unreadable.ToArray())}." : "";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: extensão do Blender criada (topo {frame.MultiplyPoint3x4(new Vector3(0f, top, 0f)).y:0.00}, {model.vertexCount} vértices, material {(concrete != null ? concrete.name : "cinza")}), {cut} triângulos e {removed} objetos tirados do caminho, {lowered} pontos de terreno rebaixados, {filled}.{extra}");
        }

        internal static Vector3[] Corners(GameObject house)
        {
            var drawn = Matrix4x4.TRS(DrawnPosition, HouseCloner.RotationFor(DrawnYaw), Vector3.one);
            var now = Matrix4x4.TRS(house.transform.position, house.transform.rotation, Vector3.one);
            var move = now * drawn.inverse;
            var corners = new Vector3[Drawn.Length];
            for (int i = 0; i < Drawn.Length; i++)
                corners[i] = move.MultiplyPoint3x4(new Vector3(Drawn[i].x, DrawnPosition.y, Drawn[i].y));
            return corners;
        }

        internal static bool Covers(GameObject house, Vector3 world, float margin)
        {
            if (!LoadModel()) return false;
            var local = HouseCloner.LocalFrame(house).inverse.MultiplyPoint3x4(world);
            var p = new Vector2(local.x, local.z);
            foreach (var t in outline)
                if (InTriangle(p, t[0], t[1], t[2]) || EdgeDistance(p, t) <= margin) return true;
            return false;
        }

        static Mesh LoadFill()
        {
            if (fillModel != null) return fillModel;
            var text = Resource(FillResource);
            if (text == null) return null;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var parts = line.Split(' ');
                if (line.StartsWith("v ")) vertices.Add(new Vector3(F(parts[1]), F(parts[3]), F(parts[2])));
                else if (line.StartsWith("f "))
                {
                    var face = new List<int>();
                    for (int k = 1; k < parts.Length; k++)
                        if (parts[k].Length > 0) face.Add(int.Parse(parts[k].Split('/')[0]) - 1);
                    for (int k = 1; k + 1 < face.Count; k++)
                    {
                        triangles.Add(face[0]); triangles.Add(face[k + 1]); triangles.Add(face[k]);
                    }
                }
            }
            if (triangles.Count == 0) return null;
            fillModel = new Mesh { name = "ApronFill_P2" };
            fillModel.vertices = vertices.ToArray();
            fillModel.triangles = triangles.ToArray();
            fillModel.RecalculateNormals();
            fillModel.RecalculateBounds();
            return fillModel;
        }

        static string BuildFill(GameObject house, Matrix4x4 frame)
        {
            var source = LoadFill();
            if (source == null) return "sem aterro";
            var mesh = Object.Instantiate(source);
            mesh.name = source.name;
            var vertices = mesh.vertices;
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var found = new bool[vertices.Length];
            var pairs = new List<KeyValuePair<Vector3, Vector2>>();
            var votes = new Dictionary<Material, int>();
            var owners = new Dictionary<Material, Renderer>();
            var starts = new Dictionary<MeshCollider, int[]>();
            var hasColors = false;
            var cache = new Dictionary<MeshCollider, KeyValuePair<Color[], int[]>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = frame.MultiplyPoint3x4(vertices[i]);
                foreach (var path in TerrainPaths)
                {
                    var go = GameObject.Find(path);
                    var collider = go != null ? go.GetComponent<MeshCollider>() : null;
                    if (collider == null || collider.sharedMesh == null) continue;
                    RaycastHit hit;
                    if (!collider.Raycast(new Ray(new Vector3(w.x, w.y + 5f, w.z), Vector3.down), out hit, 20f)) continue;
                    uv[i] = hit.textureCoord;
                    found[i] = true;
                    pairs.Add(new KeyValuePair<Vector3, Vector2>(w, hit.textureCoord));
                    var renderer = go.GetComponent<Renderer>();
                    var material = renderer != null ? MaterialAt(renderer, collider, hit.triangleIndex, starts) : null;
                    if (material != null)
                    {
                        votes[material] = (votes.TryGetValue(material, out var n) ? n : 0) + 1;
                        owners[material] = renderer;
                    }
                    KeyValuePair<Color[], int[]> data;
                    if (!cache.TryGetValue(collider, out data))
                        cache[collider] = data = new KeyValuePair<Color[], int[]>(collider.sharedMesh.colors, collider.sharedMesh.triangles);
                    var terrainColors = data.Key;
                    if (terrainColors != null && terrainColors.Length == collider.sharedMesh.vertexCount && hit.triangleIndex >= 0)
                    {
                        var tris = data.Value;
                        var b = hit.barycentricCoordinate;
                        colors[i] = terrainColors[tris[hit.triangleIndex * 3]] * b.x + terrainColors[tris[hit.triangleIndex * 3 + 1]] * b.y + terrainColors[tris[hit.triangleIndex * 3 + 2]] * b.z;
                        hasColors = true;
                    }
                    break;
                }
            }
            var map = Fit(pairs);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (found[i] || map == null) continue;
                var w = frame.MultiplyPoint3x4(vertices[i]);
                uv[i] = new Vector2(map[0].x * w.x + map[0].y * w.z + map[0].z, map[1].x * w.x + map[1].y * w.z + map[1].z);
                colors[i] = Nearest(vertices, found, colors, i);
            }
            mesh.uv = uv;
            if (hasColors) mesh.colors = colors;

            Material chosen = null;
            var most = 0;
            foreach (var pair in votes)
                if (pair.Value > most) { most = pair.Value; chosen = pair.Key; }
            var best = chosen != null ? owners[chosen] : null;

            fill = new GameObject("ApronFill_P2");
            fill.transform.parent = house.transform.parent;
            fill.transform.position = frame.MultiplyPoint3x4(Vector3.zero);
            fill.transform.rotation = Quaternion.LookRotation(frame.GetColumn(2), frame.GetColumn(1));
            fill.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = fill.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = chosen ?? Concrete(house);
            var fillCollider = fill.AddComponent<MeshCollider>();
            fillCollider.sharedMesh = mesh;
            if (best != null)
            {
                fill.layer = best.gameObject.layer;
                fill.tag = best.gameObject.tag;
                var terrainCollider = best.GetComponent<Collider>();
                if (terrainCollider != null) fillCollider.sharedMaterial = terrainCollider.sharedMaterial;
            }
            return $"aterro com {vertices.Length} pontos ({pairs.Count} alinhados à grama de {(best != null ? best.name : "nenhum terreno")}, material {(chosen != null ? chosen.name : "nenhum")} de {(best != null ? best.sharedMaterials.Length : 0)})";
        }

        static string BuildSkirt(GameObject house, Matrix4x4 frame)
        {
            var height = frame.MultiplyPoint3x4(new Vector3(0f, top, 0f)).y;
            var points = new List<Vector2>();
            foreach (var v in model.vertices)
            {
                var w = slab.transform.TransformPoint(v);
                if (w.y >= height - 0.02f) points.Add(new Vector2(w.x, w.z));
            }
            var hull = Hull(points);
            if (hull.Count < 3) return "rampa de grama: contorno não encontrado";
            var terrain = GameObject.Find("MAP/MESH/TERRAIN_OBJ");
            var n = hull.Count;
            var open = new bool[n];
            var normals = new Vector2[n];
            var sides = 0;
            for (int i = 0; i < n; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % n];
                var d = (b - a).normalized;
                normals[i] = new Vector2(d.y, -d.x);
                open[i] = true;
                for (int k = 1; k <= 3 && open[i]; k++)
                {
                    var probe = Vector2.Lerp(a, b, k / 4f) + normals[i] * 0.8f;
                    float y;
                    var what = Under(house, terrain, probe, height + 1f, out y);
                    if (what != 0 && y > height - 0.15f) open[i] = false;
                }
                if (open[i]) sides++;
            }
            if (sides == 0) return "rampa de grama: nenhuma borda solta";

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!open[i]) continue;
                var prev = (i + n - 1) % n;
                var next = (i + 1) % n;
                var startDir = open[prev] ? Miter(normals[prev], normals[i]) : normals[i];
                var endDir = open[next] ? Miter(normals[i], normals[next]) : normals[i];
                var a = hull[i];
                var b = hull[next];
                var start = vertices.Count;
                for (int r = 0; r <= SkirtRows; r++)
                {
                    var reach = r / (float)SkirtRows * SkirtWidth - SkirtInset;
                    var pa = a + startDir * reach;
                    var pb = b + endDir * reach;
                    vertices.Add(new Vector3(pa.x, 0f, pa.y));
                    vertices.Add(new Vector3(pb.x, 0f, pb.y));
                }
                for (int r = 0; r < SkirtRows; r++)
                {
                    var q = start + r * 2;
                    triangles.Add(q); triangles.Add(q + 1); triangles.Add(q + 2);
                    triangles.Add(q + 1); triangles.Add(q + 3); triangles.Add(q + 2);
                }
            }
            for (int c = 0; c < vertices.Count; c += (SkirtRows + 1) * 2)
                for (int side = 0; side < 2; side++)
                {
                    var outer = vertices[c + SkirtRows * 2 + side];
                    float end;
                    if (Under(house, terrain, new Vector2(outer.x, outer.z), height + 1f, out end) == 0) end = height - 0.3f;
                    end -= SkirtTuck;
                    for (int r = 0; r <= SkirtRows; r++)
                    {
                        var k = c + r * 2 + side;
                        var v = vertices[k];
                        var t = r / (float)SkirtRows;
                        var y = Mathf.Lerp(height - 0.005f, end, t * t * (3f - 2f * t));
                        float ground;
                        if (r > 0 && r < SkirtRows && Under(house, terrain, new Vector2(v.x, v.z), height + 1f, out ground) != 0) y = Mathf.Max(y, ground + SkirtLift);
                        v.y = Mathf.Min(y, height - 0.005f);
                        vertices[k] = v;
                    }
                }

            var mesh = new Mesh { name = "ApronSkirt_P2" };
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            skirt = new GameObject("ApronSkirt_P2");
            skirt.transform.parent = house.transform.parent;
            skirt.AddComponent<MeshFilter>().sharedMesh = mesh;
            skirt.AddComponent<MeshRenderer>();
            var collider = skirt.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            var grass = GameObject.Find(TerrainPaths[0]);
            var fallback = grass != null && grass.GetComponent<Renderer>() != null ? grass.GetComponent<Renderer>().sharedMaterial : Concrete(house);
            return $"rampa de grama em {sides} de {n} bordas ({PatioGround.Skin(skirt, collider, fallback)})";
        }

        static int Under(GameObject house, GameObject terrain, Vector2 p, float from, out float y)
        {
            y = float.MinValue;
            var kind = 0;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, from, p.y), Vector3.down, 30f))
            {
                var c = hit.collider;
                if (c == null || c.isTrigger || hit.point.y <= y) continue;
                if (slab != null && c.transform.IsChildOf(slab.transform)) continue;
                if (skirt != null && c.transform.IsChildOf(skirt.transform)) continue;
                int found;
                if (c.transform.IsChildOf(house.transform)) found = 2;
                else if (fill != null && c.transform.IsChildOf(fill.transform)) found = 1;
                else if (terrain != null && c.transform.IsChildOf(terrain.transform)) found = 1;
                else continue;
                y = hit.point.y;
                kind = found;
            }
            return kind;
        }

        static Vector2 Miter(Vector2 a, Vector2 b)
        {
            var m = (a + b).normalized;
            return m / Mathf.Max(Vector2.Dot(m, a), 0.5f);
        }

        static List<Vector2> Hull(List<Vector2> points)
        {
            points.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                var start = hull.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    var p = pass == 0 ? points[i] : points[points.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        internal static Material MaterialAt(Renderer renderer, MeshCollider collider, int triangle, Dictionary<MeshCollider, int[]> starts)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length <= 1 || triangle < 0) return renderer.sharedMaterial;
            int[] first;
            if (!starts.TryGetValue(collider, out first))
            {
                var mesh = collider.sharedMesh;
                first = new int[mesh.subMeshCount + 1];
                for (int s = 0; s < mesh.subMeshCount; s++) first[s + 1] = first[s] + mesh.GetTriangles(s).Length / 3;
                starts[collider] = first;
            }
            for (int s = 0; s + 1 < first.Length; s++)
                if (triangle < first[s + 1]) return materials[Mathf.Min(s, materials.Length - 1)];
            return renderer.sharedMaterial;
        }

        internal static Color Nearest(Vector3[] vertices, bool[] found, Color[] colors, int i)
        {
            var best = float.MaxValue;
            var color = Color.white;
            for (int k = 0; k < vertices.Length; k++)
            {
                if (!found[k]) continue;
                var d = (vertices[k] - vertices[i]).sqrMagnitude;
                if (d < best) { best = d; color = colors[k]; }
            }
            return color;
        }

        internal static Vector3[] Fit(List<KeyValuePair<Vector3, Vector2>> pairs)
        {
            if (pairs.Count < 3) return null;
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

        static bool LoadModel()
        {
            if (model != null) return true;
            var text = Resource(ModelResource);
            if (text == null) return false;
            var points = new List<Vector3>();
            var coords = new List<Vector2>();
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var footprint = new List<Vector2[]>();
            var keys = new Dictionary<string, int>();
            top = float.MinValue;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var parts = line.Split(' ');
                if (line.StartsWith("v "))
                {
                    var p = new Vector3(F(parts[1]), F(parts[3]), F(parts[2]));
                    points.Add(p);
                    top = Mathf.Max(top, p.y);
                }
                else if (line.StartsWith("vt ")) coords.Add(new Vector2(F(parts[1]), F(parts[2])));
                else if (line.StartsWith("f "))
                {
                    var face = new List<int>();
                    var corners = new List<Vector3>();
                    for (int k = 1; k < parts.Length; k++)
                    {
                        if (parts[k].Length == 0) continue;
                        int index;
                        if (!keys.TryGetValue(parts[k], out index))
                        {
                            var ids = parts[k].Split('/');
                            index = vertices.Count;
                            keys[parts[k]] = index;
                            vertices.Add(points[int.Parse(ids[0]) - 1]);
                            uv.Add(ids.Length > 1 && ids[1].Length > 0 ? coords[int.Parse(ids[1]) - 1] : Vector2.zero);
                        }
                        face.Add(index);
                        corners.Add(vertices[index]);
                    }
                    for (int k = 1; k + 1 < face.Count; k++)
                    {
                        triangles.Add(face[0]); triangles.Add(face[k + 1]); triangles.Add(face[k]);
                        var normal = Vector3.Cross(corners[k + 1] - corners[0], corners[k] - corners[0]);
                        if (normal.normalized.y > 0.5f)
                            footprint.Add(new[] { Flat(corners[0]), Flat(corners[k]), Flat(corners[k + 1]) });
                    }
                }
            }
            if (triangles.Count == 0) return false;
            model = new Mesh { name = "ApronExtension_P2" };
            model.vertices = vertices.ToArray();
            model.uv = uv.ToArray();
            model.triangles = triangles.ToArray();
            model.RecalculateNormals();
            model.RecalculateBounds();
            outline = footprint.ToArray();
            return true;
        }

        static Material Concrete(GameObject house)
        {
            ConcreteRenderer = null;
            foreach (var renderer in house.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == SlabName) { ConcreteRenderer = renderer; break; }
            if (concrete != null) return concrete;
            if (texture == null)
            {
                var bytes = ResourceBytes(TextureResource);
                if (bytes != null)
                {
                    texture = new Texture2D(2, 2, TextureFormat.ARGB32, true) { name = "MscDuoApron" };
                    texture.LoadImage(bytes);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.anisoLevel = 4;
                }
            }
            var source = ConcreteRenderer != null ? ConcreteRenderer.sharedMaterial : null;
            concrete = source != null ? new Material(source) : new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            concrete.name = "MscDuoApron";
            concrete.mainTexture = texture;
            concrete.mainTextureScale = Vector2.one;
            concrete.mainTextureOffset = Vector2.zero;
            foreach (var map in new[] { "_BumpMap", "_ParallaxMap", "_OcclusionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask", "_MetallicGlossMap", "_EmissionMap" })
                if (concrete.HasProperty(map)) concrete.SetTexture(map, null);
            if (texture == null) concrete.color = new Color(0.56f, 0.55f, 0.52f);
            return concrete;
        }

        static List<string> Clear(GameObject house, Matrix4x4 frame, out int cut, out int removed)
        {
            cut = 0;
            removed = 0;
            var unreadable = new List<string>();
            var surface = frame.MultiplyPoint3x4(new Vector3(0f, top, 0f)).y;
            var floor = surface - 3f;
            var ceiling = surface + ClearHeight;
            var area = WorldBounds(frame);

            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null || Kept(filter.transform, house)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null) continue;
                var bounds = renderer.bounds;
                if (bounds.max.x < area.min.x || bounds.min.x > area.max.x || bounds.max.z < area.min.z || bounds.min.z > area.max.z) continue;
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
                        if (center.y > floor && center.y < ceiling && Covers(house, center, 0.3f)) { removedHere++; continue; }
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
                if (!Covers(house, b.center, 0.5f)) continue;
                collider.enabled = false;
                disabled.Add(collider);
                removed++;
            }
            return unreadable;
        }

        static int LowerTerrain(GameObject house, Matrix4x4 frame)
        {
            var lowered = 0;
            var target = frame.MultiplyPoint3x4(new Vector3(0f, top, 0f)).y - TerrainGap;
            var area = WorldBounds(frame);
            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                var filter = go != null ? go.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var toWorld = go.transform.localToWorldMatrix;
                var toMesh = go.transform.worldToLocalMatrix;
                var changed = new List<int>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (w.y <= target || w.x < area.min.x || w.x > area.max.x || w.z < area.min.z || w.z > area.max.z) continue;
                    if (!Covers(house, w, 0.6f)) continue;
                    changed.Add(i);
                }
                if (changed.Count == 0) continue;
                if (!mesh.name.EndsWith("_flat") && !cutMeshes.ContainsKey(filter))
                {
                    var copy = Object.Instantiate(mesh);
                    copy.name = mesh.name + "_apron";
                    cutMeshes[filter] = mesh;
                    filter.sharedMesh = mesh = copy;
                }
                foreach (var i in changed)
                {
                    var w = toWorld.MultiplyPoint3x4(vertices[i]);
                    w.y = target;
                    vertices[i] = toMesh.MultiplyPoint3x4(w);
                }
                lowered += changed.Count;
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

        static Bounds WorldBounds(Matrix4x4 frame)
        {
            var first = true;
            var bounds = new Bounds();
            foreach (var t in outline)
                foreach (var p in t)
                {
                    var w = frame.MultiplyPoint3x4(new Vector3(p.x, top, p.y));
                    if (first) { bounds = new Bounds(w, Vector3.zero); first = false; }
                    else bounds.Encapsulate(w);
                }
            bounds.Expand(new Vector3(2f, 0f, 2f));
            return bounds;
        }

        internal static void Restore()
        {
            if (slab != null) Object.DestroyImmediate(slab);
            slab = null;
            if (fill != null) Object.DestroyImmediate(fill);
            fill = null;
            if (skirt != null) Object.DestroyImmediate(skirt);
            skirt = null;
            foreach (var pair in cutMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var pair in cutColliders)
            {
                if (pair.Key == null) continue;
                pair.Key.sharedMesh = null;
                pair.Key.sharedMesh = pair.Value;
            }
            foreach (var c in disabled) if (c != null) c.enabled = true;
            foreach (var path in TerrainPaths)
            {
                var go = GameObject.Find(path);
                var collider = go != null ? go.GetComponent<MeshCollider>() : null;
                var filter = go != null ? go.GetComponent<MeshFilter>() : null;
                if (collider == null || filter == null) continue;
                collider.sharedMesh = null;
                collider.sharedMesh = filter.sharedMesh;
            }
            cutMeshes.Clear();
            cutColliders.Clear();
            disabled.Clear();
        }

        static bool Kept(Transform t, GameObject house)
        {
            if (t.IsChildOf(house.transform) || (slab != null && t.IsChildOf(slab.transform)) || (fill != null && t.IsChildOf(fill.transform))) return true;
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

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Side(p, a, b);
            var d2 = Side(p, b, c);
            var d3 = Side(p, c, a);
            var negative = d1 < 0 || d2 < 0 || d3 < 0;
            var positive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(negative && positive);
        }

        static float Side(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        static float EdgeDistance(Vector2 p, Vector2[] t)
        {
            var best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                var a = t[i];
                var ab = t[(i + 1) % 3] - a;
                var k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (a + ab * k - p).magnitude);
            }
            return best;
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static string Resource(string name)
        {
            var bytes = ResourceBytes(name);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        static byte[] ResourceBytes(string name)
        {
            using (var stream = typeof(ApronExtension).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                return bytes;
            }
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
