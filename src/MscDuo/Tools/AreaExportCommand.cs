using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class AreaExportCommand : ConsoleCommand
    {
        const float Vertical = 40f;
        const int MaxTexture = 1024;
        const float DefaultX = -22f;
        const float DefaultZ = 26f;
        const float DefaultHalf = 70f;

        static readonly string[] Skipped = { "PLAYER", "GUI", "Database", "Systems", "Sheets", "MscDuo_" };

        public override string Name => "duo_export_area";
        public override string Help => "duo_export_area [meia largura = 70] [x z]  exporta tudo da área (casas, árvores, estradas, terreno) com texturas em coordenadas do mundo para o Blender";

        public override void Run(string[] args)
        {
            var half = DefaultHalf;
            var x = DefaultX;
            var z = DefaultZ;
            if (args.Length > 0 && !DevCommands.TryFloat(args[0], out half)) { ModConsole.Error(Help); return; }
            if (args.Length > 2 && (!DevCommands.TryFloat(args[1], out x) || !DevCommands.TryFloat(args[2], out z))) { ModConsole.Error(Help); return; }

            var folder = Path.Combine(Path.Combine(MscDuo.Instance.SettingsFolder, "dumps"), "area");
            var textures = Path.Combine(folder, "texturas");
            Directory.CreateDirectory(textures);
            var center = new Vector3(x, 0f, z);
            float ground;
            center.y = TryGround(center, out ground) ? ground : 0f;

            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            obj.AppendLine("# MSC Duo área: coordenadas do mundo em eixos do Blender (x, z, y da Unity)");
            obj.AppendLine($"# centro {F(x)} {F(z)} meia {F(half)}");
            obj.AppendLine("mtllib area.mtl");

            var materials = new Dictionary<Material, string>();
            var images = new Dictionary<Texture, string>();
            var doneBatches = new HashSet<Mesh>();
            int offset = 1, objects = 0, triangles = 0, unreadable = 0;

            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || Skip(renderer.transform)) continue;
                var b = renderer.bounds;
                if (b.max.x < x - half || b.min.x > x + half || b.max.z < z - half || b.min.z > z + half) continue;
                if (b.max.y < center.y - Vertical || b.min.y > center.y + Vertical) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var batched = renderer.isPartOfStaticBatch;
                if (batched && !doneBatches.Add(mesh)) continue;

                Vector3[] vertices;
                try { vertices = mesh.vertices; } catch { vertices = null; }
                if (vertices == null || vertices.Length == 0) { unreadable++; continue; }
                var uv = mesh.uv;
                var hasUv = uv != null && uv.Length == vertices.Length;
                var toWorld = batched ? Matrix4x4.identity : renderer.transform.localToWorldMatrix;
                var world = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) world[i] = toWorld.MultiplyPoint3x4(vertices[i]);

                var shared = renderer.sharedMaterials;
                var wrote = false;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = s < shared.Length ? shared[s] : null;
                    int[] tris;
                    try { tris = mesh.GetTriangles(s); } catch { continue; }
                    var used = new Dictionary<int, int>();
                    var faces = new List<int>();
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        if (!Inside(world[tris[t]], center, half) && !Inside(world[tris[t + 1]], center, half) && !Inside(world[tris[t + 2]], center, half)) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int mapped;
                            if (!used.TryGetValue(tris[t + k], out mapped)) used[tris[t + k]] = mapped = used.Count;
                            faces.Add(mapped);
                        }
                    }
                    if (faces.Count == 0) continue;
                    if (!wrote) { obj.AppendLine($"o {Clean(PathOf(renderer.transform))}"); wrote = true; objects++; }
                    var name = MaterialName(material, materials, images, mtl, textures);
                    obj.AppendLine($"usemtl {name}");
                    var order = new int[used.Count];
                    foreach (var pair in used) order[pair.Value] = pair.Key;
                    var scale = material != null && material.HasProperty("_MainTex") ? material.mainTextureScale : Vector2.one;
                    var shift = material != null && material.HasProperty("_MainTex") ? material.mainTextureOffset : Vector2.zero;
                    foreach (var index in order) obj.AppendLine("v " + Point(world[index]));
                    if (hasUv)
                        foreach (var index in order) obj.AppendLine($"vt {F(uv[index].x * scale.x + shift.x)} {F(uv[index].y * scale.y + shift.y)}");
                    for (int f = 0; f < faces.Count; f += 3)
                    {
                        int a = faces[f] + offset, c = faces[f + 1] + offset, d = faces[f + 2] + offset;
                        obj.AppendLine(hasUv ? $"f {a}/{a} {d}/{d} {c}/{c}" : $"f {a} {d} {c}");
                    }
                    offset += used.Count;
                    triangles += faces.Count / 3;
                }
            }

            File.WriteAllText(Path.Combine(folder, "area.obj"), obj.ToString());
            File.WriteAllText(Path.Combine(folder, "area.mtl"), mtl.ToString());
            ModConsole.Print($"<color=orange>[MSC Duo]</color> área exportada: {objects} objetos, {triangles} triângulos, {materials.Count} materiais, {images.Count} texturas{(unreadable > 0 ? $", {unreadable} sem leitura" : "")}. Pasta: {folder}");
        }

        static string MaterialName(Material material, Dictionary<Material, string> materials, Dictionary<Texture, string> images, StringBuilder mtl, string folder)
        {
            if (material == null) return "sem_material";
            string name;
            if (materials.TryGetValue(material, out name)) return name;
            name = Clean(material.name) + "_" + materials.Count;
            materials[material] = name;
            mtl.AppendLine($"newmtl {name}");
            var color = material.HasProperty("_Color") ? material.color : Color.white;
            mtl.AppendLine($"Kd {F(color.r)} {F(color.g)} {F(color.b)}");
            if (color.a < 0.99f) mtl.AppendLine($"d {F(color.a)}");
            var texture = material.HasProperty("_MainTex") ? material.mainTexture : null;
            if (texture != null)
            {
                string file;
                if (!images.TryGetValue(texture, out file))
                {
                    file = "texturas/" + Clean(texture.name) + "_" + images.Count + ".png";
                    if (SaveTexture(texture, Path.Combine(folder, Path.GetFileName(file)))) images[texture] = file;
                    else file = null;
                }
                if (file != null) mtl.AppendLine($"map_Kd {file}");
            }
            mtl.AppendLine();
            return name;
        }

        static bool SaveTexture(Texture texture, string path)
        {
            try
            {
                var scale = Mathf.Min(1f, (float)MaxTexture / Mathf.Max(texture.width, texture.height));
                int w = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
                var target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                var copy = new Texture2D(w, h, TextureFormat.ARGB32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes(path, copy.EncodeToPNG());
                Object.Destroy(copy);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool TryGround(Vector3 p, out float y)
        {
            y = 0f;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out hit, 400f)) return false;
            y = hit.point.y;
            return true;
        }

        static bool Inside(Vector3 p, Vector3 center, float half) =>
            Mathf.Abs(p.x - center.x) <= half && Mathf.Abs(p.z - center.z) <= half && Mathf.Abs(p.y - center.y) <= Vertical;

        static bool Skip(Transform t)
        {
            var root = t.root.name;
            foreach (var name in Skipped)
                if (root.StartsWith(name)) return true;
            return false;
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '/' || c == '-' ? c : '_');
            return sb.ToString();
        }

        static string Point(Vector3 p) => $"{F(p.x)} {F(p.z)} {F(p.y)}";

        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
