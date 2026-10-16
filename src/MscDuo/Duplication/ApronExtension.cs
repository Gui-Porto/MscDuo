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
        const string SlabName = "house_base_concrete_mesh";
        const float ClearHeight = 2.5f;
        const float TerrainGap = 0.08f;

        static readonly string[] RoadNames = { "Road", "DirtRoad", "Gravel", "Asphalt", "Roadside", "Pavement", "4tie" };
        static readonly string[] KeptRoots = { "MSCDUO", "PLAYER", "SATSUMA", "CARPARTS", "Database", "ITEMS", "STORE", "Systems", "Sheets", "GUI" };
        static readonly string[] TerrainPaths = { "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2" };

        static GameObject slab;
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
            var extra = unreadable.Count > 0 ? $" Sem leitura (ficaram): {string.Join(", ", unreadable.ToArray())}." : "";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: extensão do Blender criada (topo {frame.MultiplyPoint3x4(new Vector3(0f, top, 0f)).y:0.00}, {model.vertexCount} vértices, material {(concrete != null ? concrete.name : "cinza")}), {cut} triângulos e {removed} objetos tirados do caminho, {lowered} pontos de terreno rebaixados.{extra}");
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
