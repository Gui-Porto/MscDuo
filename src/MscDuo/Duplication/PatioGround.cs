using System.Collections.Generic;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class PatioGround
    {
        static readonly string[] TerrainPaths = { "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2" };
        internal static readonly string[] DirtPaths = { "MAP/MESH/TERRAIN_OBJ/Gravel" };

        const float Left = -1f;
        const float Right = 25f;
        const float Front = -9.5f;
        const float WallClear = 0.3f;
        const float EdgeClear = 0.2f;
        const float Top = -0.02f;
        const float Sink = 0.05f;

        internal static string Skin(GameObject go, MeshCollider collider, Material fallback, string[] paths = null, float spread = 0f)
        {
            var filter = go.GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var found = new bool[vertices.Length];
            var pairs = new List<KeyValuePair<Vector3, Vector2>>();
            var votes = new Dictionary<Material, int>();
            var owners = new Dictionary<Material, Renderer>();
            var starts = new Dictionary<MeshCollider, int[]>();
            var cache = new Dictionary<MeshCollider, KeyValuePair<Color[], int[]>>();
            var hasColors = false;
            var terrains = new List<MeshCollider>();
            foreach (var path in paths ?? TerrainPaths)
            {
                var terrain = GameObject.Find(path);
                var c = terrain != null ? terrain.GetComponent<MeshCollider>() : null;
                if (c != null && c.sharedMesh != null) terrains.Add(c);
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = go.transform.TransformPoint(vertices[i]);
                foreach (var terrain in terrains)
                {
                    RaycastHit hit;
                    if (!terrain.Raycast(new Ray(new Vector3(w.x, w.y + 5f, w.z), Vector3.down), out hit, 20f)) continue;
                    uv[i] = hit.textureCoord;
                    found[i] = true;
                    pairs.Add(new KeyValuePair<Vector3, Vector2>(w, hit.textureCoord));
                    var renderer = terrain.GetComponent<Renderer>();
                    var material = renderer != null ? ApronExtension.MaterialAt(renderer, terrain, hit.triangleIndex, starts) : null;
                    if (material != null)
                    {
                        votes[material] = (votes.TryGetValue(material, out var n) ? n : 0) + 1;
                        owners[material] = renderer;
                    }
                    KeyValuePair<Color[], int[]> data;
                    if (!cache.TryGetValue(terrain, out data))
                        cache[terrain] = data = new KeyValuePair<Color[], int[]>(terrain.sharedMesh.colors, terrain.sharedMesh.triangles);
                    if (data.Key != null && data.Key.Length == terrain.sharedMesh.vertexCount && hit.triangleIndex >= 0)
                    {
                        var tris = data.Value;
                        var b = hit.barycentricCoordinate;
                        colors[i] = data.Key[tris[hit.triangleIndex * 3]] * b.x + data.Key[tris[hit.triangleIndex * 3 + 1]] * b.y + data.Key[tris[hit.triangleIndex * 3 + 2]] * b.z;
                        hasColors = true;
                    }
                    break;
                }
            }
            if (spread > 0f)
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = go.transform.TransformPoint(vertices[i]);
                    foreach (var offset in new[] { new Vector3(spread, 0f, 0f), new Vector3(-spread, 0f, 0f), new Vector3(0f, 0f, spread), new Vector3(0f, 0f, -spread) })
                        foreach (var terrain in terrains)
                        {
                            RaycastHit hit;
                            if (!terrain.Raycast(new Ray(w + offset + Vector3.up * 5f, Vector3.down), out hit, 20f)) continue;
                            pairs.Add(new KeyValuePair<Vector3, Vector2>(hit.point, hit.textureCoord));
                            break;
                        }
                }
            var map = ApronExtension.Fit(pairs);
            for (int i = 0; i < vertices.Length; i++)
            {
                if (found[i] || map == null) continue;
                var w = go.transform.TransformPoint(vertices[i]);
                uv[i] = new Vector2(map[0].x * w.x + map[0].y * w.z + map[0].z, map[1].x * w.x + map[1].y * w.z + map[1].z);
                colors[i] = ApronExtension.Nearest(vertices, found, colors, i);
            }
            mesh.uv = uv;
            if (hasColors) mesh.colors = colors;

            Material chosen = null;
            var most = 0;
            foreach (var pair in votes)
                if (pair.Value > most) { most = pair.Value; chosen = pair.Key; }
            var best = chosen != null ? owners[chosen] : null;
            go.GetComponent<MeshRenderer>().sharedMaterial = chosen ?? fallback;
            if (best != null)
            {
                go.layer = best.gameObject.layer;
                go.tag = best.gameObject.tag;
                var surface = best.GetComponent<Collider>();
                if (surface != null) collider.sharedMaterial = surface.sharedMaterial;
            }
            return $"grama do pátio com {vertices.Length} pontos ({pairs.Count} alinhados ao terreno{(hasColors ? ", com cores" : "")}, material {(chosen != null ? chosen.name : "nenhum")})";
        }

        internal static string Hedges(Transform root)
        {
            var house = HouseCloner.Clone;
            var fence = house != null ? Find(house.transform, "HedgeFence") : null;
            if (fence == null) return "cerca viva: casa 2 sem cerca";
            Transform template = null;
            var hidden = 0;
            foreach (Transform hedge in fence)
            {
                var renderer = Visible(hedge);
                if (renderer == null) continue;
                if (template == null) template = hedge;
                var local = Warehouse.ToLocal(renderer.bounds.center);
                if (local.x < Left || local.x > Right || local.y < Front || local.y > 17f) continue;
                hedge.gameObject.SetActive(false);
                template = hedge;
                hidden++;
            }
            if (template == null) return "cerca viva: nenhum pedaço na casa 2";

            var shape = Visible(template, true);
            var mesh = shape.GetComponent<MeshFilter>().sharedMesh;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            Vector3 along = Vector3.zero, across = Vector3.zero;
            foreach (var axis in axes)
            {
                var v = shape.transform.TransformVector(Vector3.Scale(axis, mesh.bounds.size));
                v.y = 0f;
                if (v.magnitude > along.magnitude) { across = along; along = v; }
                else if (v.magnitude > across.magnitude) across = v;
            }
            var length = along.magnitude;
            var width = across.magnitude;
            var inset = width / 2f + EdgeClear;
            var pieces = 0;
            pieces += Row(root, template, along.normalized, length, Left + inset);
            pieces += Row(root, template, along.normalized, length, Right - inset);
            return $"cerca viva: {pieces} pedaços nas laterais do pátio, {hidden} da casa 2 escondidos";
        }

        static int Row(Transform root, Transform template, Vector3 along, float length, float side)
        {
            var a = Warehouse.Local(side, -WallClear, 0f);
            var b = Warehouse.Local(side, Front + EdgeClear, 0f);
            var line = b - a;
            line.y = 0f;
            var total = line.magnitude;
            var dir = line / total;
            var count = Mathf.Max(1, Mathf.CeilToInt(total / (length * 0.95f)));
            var step = count > 1 ? (total - length) / (count - 1) : 0f;
            var turn = Quaternion.FromToRotation(along, dir);
            var floor = Warehouse.Local(0f, 0f, Top).y - Sink;
            for (int k = 0; k < count; k++)
            {
                var center = count > 1 ? a + dir * (length / 2f + k * step) : (a + b) / 2f;
                var copy = (GameObject)Object.Instantiate(template.gameObject);
                copy.name = "Galpao_CercaViva";
                copy.SetActive(true);
                copy.transform.parent = root;
                copy.transform.rotation = turn * template.rotation;
                var bounds = Visible(copy.transform).bounds;
                copy.transform.position += new Vector3(center.x - bounds.center.x, floor - bounds.min.y, center.z - bounds.center.z);
            }
            return count;
        }

        static Renderer Visible(Transform t, bool inactive = false)
        {
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(inactive))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (r.enabled && filter != null && filter.sharedMesh != null) return r;
            }
            return null;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
