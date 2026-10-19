using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class Warehouse
    {
        const string BuildingResource = "MscDuo.galpao_jogo.obj";
        const string PlatformResource = "MscDuo.plataforma_jogo.obj";
        const string TexturePrefix = "MscDuo.galpao.";
        const string FurnitureResource = "MscDuo.moveis_jogo.obj";
        const string FurniturePrefix = "MscDuo.moveis.";
        const string GravelPath = "MAP/MESH/TERRAIN_OBJ/Gravel";
        const string GrassPath = "MAP/MESH/TERRAIN_OBJ/Grass1";
        const float TreeMargin = 2f;
        static readonly Vector3 Origin = new Vector3(-30f, -0.497f, 13f);

        static readonly Vector4[] Lamps =
        {
            new Vector4(6f, 3f, 6f, 14f), new Vector4(12f, 3f, 7f, 14f), new Vector4(18f, 3f, 6f, 14f),
            new Vector4(6f, 7.5f, 6f, 14f), new Vector4(12f, 7.5f, 7f, 14f), new Vector4(18f, 7.5f, 6f, 14f),
            new Vector4(3f, 12.9f, 2.8f, 7f), new Vector4(9f, 12.9f, 2.8f, 7f), new Vector4(15f, 12.9f, 2.8f, 7f),
            new Vector4(4f, 13f, 6f, 9f), new Vector4(12f, 13f, 7f, 9f), new Vector4(20f, 13f, 6f, 9f),
        };

        static readonly string[] Solid = { "Piso", "Paredes", "Mezanino", "Escada", "Salas", "GuardaCorpo", "Estrutura", "Telhado", "Janelas" };
        static readonly string[] Gates = { "Portao1", "Portao2", "PortaLateral" };

        static GameObject root;

        internal static Transform LightSwitch { get; private set; }

        internal static GameObject Root => root;

        internal static void Spawn()
        {
            if (root != null) Object.Destroy(root);
            WarehouseConfig.Load();
            WarehouseToggles.Clear();
            Screens.Clear();
            var building = Parse(Text("galpao_jogo.obj", BuildingResource));
            var platform = Parse(Text("plataforma_jogo.obj", PlatformResource));
            if (building == null || platform == null)
            {
                ModConsole.Warning("[MSC Duo] galpão não encontrado na DLL.");
                return;
            }
            root = new GameObject("MscDuo_Galpao");
            var template = Template();
            var textures = new Dictionary<string, Material>();
            int parts = 0, colliders = 0;
            var bounds = new Bounds();
            var first = true;

            foreach (var part in building)
            {
                var materials = new List<Material>();
                foreach (var name in part.Materials) materials.Add(BuildingMaterial(name, template, textures, "galpao_tex_jogo", TexturePrefix));
                var go = Create(part, materials.ToArray(), root.transform);
                parts++;
                if (System.Array.IndexOf(Gates, part.Name) >= 0) go.SetActive(false);
                else if (System.Array.IndexOf(Solid, part.Name) >= 0)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    colliders++;
                }
                var b = go.GetComponent<Renderer>().bounds;
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }

            var shelter = Shelter(root.transform);

            var gravel = GameObject.Find(GravelPath);
            var grass = GameObject.Find(GrassPath);
            foreach (var part in platform)
            {
                var materials = new Material[part.Materials.Count];
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = part.Materials[i] == "cascalho" ? gravel : grass;
                    var renderer = source != null ? source.GetComponent<Renderer>() : null;
                    materials[i] = renderer != null ? renderer.sharedMaterial : template;
                }
                var go = Create(part, materials, root.transform);
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                if (gravel != null)
                {
                    go.layer = gravel.layer;
                    go.tag = gravel.tag;
                    var surface = gravel.GetComponent<Collider>();
                    if (surface != null) collider.sharedMaterial = surface.sharedMaterial;
                }
                parts++;
            }

            var furniture = 0;
            try { furniture = Furniture(root.transform, template, textures); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] móveis do galpão falharam: {e}"); }

            var lights = BuildLights(root.transform);
            var lamp = root.transform.Find("Galpao_Luzes");
            if (lamp != null) lamp.gameObject.AddComponent<LampState>().Lights = lights;
            try { MezzanineDecor.Build(root.transform, template, lights); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] decoração do mezanino falhou: {e}"); }
            var light = true;
            try { SlotCloner.Build(root.transform); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] caça-níquel do galpão falhou: {e}"); }
            try { LiftCloner.Build(root.transform, template); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] elevador do galpão falhou: {e}"); }
            try { DynoCloner.Build(root.transform); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] dinamômetros do galpão falharam: {e}"); }
            try { WarehouseGates.Build(root.transform, template); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] portões do galpão falharam: {e}"); }
            try { WarehouseExterior.Build(root.transform, template, WarehouseGates.Load()); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] luz externa do galpão falhou: {e}"); }
            try { WarehouseLife.Build(root.transform, template, lights, Screens); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] animações do galpão falharam: {e}"); }
            try { WarehouseRooms.Build(root.transform, template, root.GetComponent<GateInput>() ?? root.AddComponent<GateInput>(), lights, WarehouseGates.Load()); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] banheiro e depósito falharam: {e}"); }

            var cleared = TreeClearer.ClearArea(bounds.min - new Vector3(TreeMargin, 0f, TreeMargin), bounds.max + new Vector3(TreeMargin + WarehouseConfig.Get("patio_arvores"), 0f, TreeMargin));
            ModConsole.Print($"<color=orange>[MSC Duo]</color> galpão: {parts} peças, {colliders} com colisão, {textures.Count} materiais, {cleared} árvores removidas, telhado {(shelter ? "protege da chuva" : "SEM proteção de chuva")}, {furniture} móveis, interruptor {(light ? "ok" : "não encontrado")}, modelo {(WarehouseConfig.DevFolder != null ? "da pasta " + WarehouseConfig.DevFolder : "da DLL")}; centro {HouseCloner.Format(bounds.center)}.");
        }

        static bool Shelter(Transform parent)
        {
            Collider sample = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<Collider>())
                if (c != null && c.hideFlags == HideFlags.None && c.gameObject.name == "NoRain" && c.transform.root.name == "YARD") { sample = c; break; }
            var roof = parent.Find("Galpao_Telhado");
            if (sample == null || roof == null) return false;
            var go = new GameObject("NoRain");
            go.transform.parent = parent;
            go.transform.position = roof.position;
            go.transform.rotation = roof.rotation;
            go.layer = sample.gameObject.layer;
            go.tag = sample.gameObject.tag;
            var mesh = roof.GetComponent<MeshFilter>().sharedMesh;
            if (sample.isTrigger)
            {
                var box = go.AddComponent<BoxCollider>();
                var b = mesh.bounds;
                box.center = new Vector3(b.center.x, (b.max.y + Origin.y) / 2f, b.center.z);
                box.size = new Vector3(b.size.x, b.max.y - Origin.y, b.size.z);
                box.isTrigger = true;
            }
            else go.AddComponent<MeshCollider>().sharedMesh = mesh;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> telhado contra chuva na camada {LayerMask.LayerToName(go.layer)} ({go.layer}), tag {go.tag}, trigger {sample.isTrigger}.");
            return true;
        }

        internal static Vector3 ToLocal(Vector3 world) => new Vector3(world.z - Origin.z, Origin.x - world.x, world.y - Origin.y);

        internal static Vector3 Local(float x, float y, float z) => new Vector3(Origin.x - y, Origin.y + z, Origin.z + x);

        static GameObject BuildLights(Transform parent)
        {
            var group = new GameObject("Galpao_Iluminacao");
            group.transform.parent = parent;
            for (int index = 0; index < Lamps.Length; index++)
            {
                var lamp = Lamps[index];
                var kind = index < 6 ? "alta" : index < 9 ? "baixa" : "mezanino";
                var go = new GameObject("Lampada");
                go.transform.parent = group.transform;
                go.transform.position = Local(lamp.x, lamp.y, lamp.z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = WarehouseConfig.Get("luz_" + kind + "_alcance");
                light.intensity = WarehouseConfig.Get("luz_" + kind + "_intensidade");
                light.color = new Color(1f, 0.97f, 0.9f);
                light.shadows = LightShadows.None;
            }
            group.SetActive(false);
            return group;
        }

        class LampState : MonoBehaviour
        {
            internal GameObject Lights;
            Renderer lampRenderer;
            Material[] lit;
            Material[] dark;
            bool shown = true;

            void Start()
            {
                lampRenderer = GetComponent<Renderer>();
                lit = lampRenderer.sharedMaterials;
                dark = new Material[lit.Length];
                for (int i = 0; i < lit.Length; i++)
                {
                    if (lit[i] != null && lit[i].name.EndsWith("_luz"))
                    {
                        var shader = Shader.Find("Diffuse") ?? Shader.Find("Legacy Shaders/Diffuse") ?? lit[i].shader;
                        dark[i] = new Material(shader) { name = lit[i].name + "_apagada", color = new Color(0.55f, 0.55f, 0.57f), mainTexture = lit[i].mainTexture };
                    }
                    else dark[i] = lit[i];
                }
            }

            void Update()
            {
                var on = Lights != null && Lights.activeSelf;
                if (on == shown || lampRenderer == null) return;
                shown = on;
                lampRenderer.sharedMaterials = on ? lit : dark;
            }
        }

        static Material Template()
        {
            var concrete = ApronExtension.ConcreteRenderer;
            if (concrete != null && concrete.sharedMaterial != null) return concrete.sharedMaterial;
            return new Material(Shader.Find("Diffuse"));
        }

        internal static readonly Dictionary<string, Vector3[]> Screens = new Dictionary<string, Vector3[]>();

        static void Hinged(GameObject door, string name)
        {
            var b = door.GetComponent<MeshFilter>().sharedMesh.bounds;
            var pivot = new GameObject(door.name + "_Dobradica");
            pivot.transform.parent = door.transform.parent;
            pivot.transform.position = new Vector3(b.max.x, b.center.y, b.min.z);
            pivot.transform.rotation = Quaternion.identity;
            door.transform.parent = pivot.transform;
            var box = door.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            var swing = pivot.AddComponent<DoorSwing>();
            var key = name;
            WarehouseToggles.Register(key, 0, v => swing.Target = v != 0 ? 100f : 0f);
            var input = root.GetComponent<GateInput>() ?? root.AddComponent<GateInput>();
            input.Buttons.Add(new GateButton { Collider = box, Cap = door.transform, Click = () => WarehouseToggles.Set(key, WarehouseToggles.Get(key) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(key) == 0 ? "Abrir porta" : "Fechar porta" });
        }

        class DoorSwing : MonoBehaviour
        {
            internal float Target;
            float angle;

            void Update()
            {
                if (Mathf.Approximately(angle, Target)) return;
                angle = Mathf.MoveTowards(angle, Target, Time.deltaTime * 160f);
                transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        static void Cooler(GameObject fridge)
        {
            var b = fridge.GetComponent<MeshFilter>().sharedMesh.bounds;
            var inside = new GameObject("Geladeira_Frio");
            inside.transform.parent = fridge.transform;
            inside.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.55f, b.center.z);
            House2Fridge.Extra = inside.transform;
            var lamp = new GameObject("Geladeira_Luz");
            lamp.transform.parent = fridge.transform;
            lamp.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.8f, b.center.z);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.0f;
            light.intensity = 1.2f;
            light.color = new Color(0.85f, 0.95f, 1f);
            light.shadows = LightShadows.None;
        }

        static int Furniture(Transform parent, Material template, Dictionary<string, Material> cache)
        {
            var parts = Parse(Text("moveis_jogo.obj", FurnitureResource));
            if (parts == null) return 0;
            var group = new GameObject("Galpao_Moveis");
            group.transform.parent = parent;
            foreach (var part in parts)
            {
                var materials = new List<Material>();
                foreach (var name in part.Materials) materials.Add(BuildingMaterial(name, template, cache, "moveis_tex_jogo", FurniturePrefix));
                var go = Create(part, materials.ToArray(), group.transform);
                go.name = "Movel_" + part.Name;
                if (part.Screen != null) Screens[part.Name] = part.Screen;
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                if (part.Name.EndsWith("_porta"))
                {
                    Hinged(go, part.Name);
                    continue;
                }
                if (part.Name == "geladeira") Cooler(go);
                if (part.Name.StartsWith("planta"))
                {
                    var b = mesh.bounds;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(b.center.x, b.min.y + b.size.y * 0.2f, b.center.z);
                    box.size = new Vector3(b.size.x * 0.6f, b.size.y * 0.4f, b.size.z * 0.6f);
                }
                else go.AddComponent<MeshCollider>().sharedMesh = mesh;
                foreach (var offset in part.Copies)
                {
                    var twin = (GameObject)Object.Instantiate(go, go.transform.position + offset, go.transform.rotation);
                    twin.name = go.name;
                    twin.transform.parent = group.transform;
                }
            }
            return parts.Count;
        }

        internal static Material Textured(string name, Material template) => BuildingMaterial(name, template, new Dictionary<string, Material>(), "galpao_tex_jogo", TexturePrefix);

        static Material BuildingMaterial(string name, Material template, Dictionary<string, Material> cache, string folder, string prefix)
        {
            Material material;
            if (cache.TryGetValue(name, out material)) return material;
            Shader shader = null;
            if (name == "vidro" || name.EndsWith("_vidro")) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Transparent/Diffuse");
            else if (name == "luz") shader = Shader.Find("Unlit/Texture");
            var cutout = name.EndsWith("_alfa");
            if (cutout) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Shader.Find("Transparent/Cutout/Diffuse");
            material = shader != null ? new Material(shader) : new Material(template);
            material.name = "MscDuo_galpao_" + name;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.color = name == "vidro" ? new Color(0.75f, 0.85f, 0.9f, 0.35f) : name.EndsWith("_vidro") ? new Color(0.85f, 0.92f, 0.95f, 0.22f) : Color.white;
            var bytes = Bytes(Path.Combine(folder, name + ".png"), prefix + name + ".png") ?? Bytes(Path.Combine(folder, name + ".jpg"), prefix + name + ".jpg");
            if (bytes != null)
            {
                var texture = new Texture2D(2, 2, cutout ? TextureFormat.ARGB32 : TextureFormat.RGB24, true) { name = "MscDuo_galpao_" + name, wrapMode = cutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat };
                texture.LoadImage(bytes);
                material.mainTexture = texture;
                if (cutout && material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.45f);
            }
            cache[name] = material;
            return material;
        }

        static GameObject Create(Part part, Material[] materials, Transform parent)
        {
            var mesh = new Mesh { name = "MscDuo_" + part.Name };
            mesh.vertices = part.Vertices.ToArray();
            mesh.uv = part.Uvs.ToArray();
            mesh.subMeshCount = part.Triangles.Count;
            for (int i = 0; i < part.Triangles.Count; i++) mesh.SetTriangles(part.Triangles[i].ToArray(), i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Galpao_" + part.Name);
            go.transform.parent = parent;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        class Part
        {
            internal string Name;
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector2> Uvs = new List<Vector2>();
            internal readonly List<string> Materials = new List<string>();
            internal readonly List<List<int>> Triangles = new List<List<int>>();
            internal readonly Dictionary<long, int> Index = new Dictionary<long, int>();
            internal readonly List<Vector3> Copies = new List<Vector3>();
            internal Vector3[] Screen;
        }

        static List<Part> Parse(string text)
        {
            if (text == null) return null;
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var parts = new List<Part>();
            Part current = null;
            List<int> triangles = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(' ');
                switch (p[0])
                {
                    case "v":
                        positions.Add(new Vector3(F(p[1]), F(p[3]), F(p[2])));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(F(p[1]), F(p[2])));
                        break;
                    case "screen":
                        foreach (var part in parts)
                            if (part.Name == p[1])
                            {
                                part.Screen = new Vector3[4];
                                for (int k = 0; k < 4; k++) part.Screen[k] = new Vector3(F(p[2 + k * 3]), F(p[4 + k * 3]), F(p[3 + k * 3]));
                            }
                        break;
                    case "copy":
                        foreach (var part in parts)
                            if (part.Name == p[1]) part.Copies.Add(new Vector3(F(p[2]), F(p[4]), F(p[3])));
                        break;
                    case "o":
                        current = new Part { Name = line.Substring(2).Trim() };
                        parts.Add(current);
                        triangles = null;
                        break;
                    case "usemtl":
                        if (current == null) break;
                        current.Materials.Add(p[1]);
                        triangles = new List<int>();
                        current.Triangles.Add(triangles);
                        break;
                    case "f":
                        if (current == null || triangles == null) break;
                        var corner = new int[3];
                        for (int k = 0; k < 3; k++)
                        {
                            var ids = p[k + 1].Split('/');
                            int v = int.Parse(ids[0]) - 1, t = ids.Length > 1 && ids[1].Length > 0 ? int.Parse(ids[1]) - 1 : -1;
                            long key = ((long)v << 32) | (uint)t;
                            int index;
                            if (!current.Index.TryGetValue(key, out index))
                            {
                                index = current.Vertices.Count;
                                current.Index[key] = index;
                                current.Vertices.Add(positions[v]);
                                current.Uvs.Add(t >= 0 ? uvs[t] : Vector2.zero);
                            }
                            corner[k] = index;
                        }
                        triangles.Add(corner[0]);
                        triangles.Add(corner[2]);
                        triangles.Add(corner[1]);
                        break;
                }
            }
            return parts;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static string Text(string file, string resource)
        {
            var bytes = Bytes(file, resource);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        internal static byte[] Bytes(string file, string resource)
        {
            var folder = WarehouseConfig.DevFolder;
            if (folder != null)
            {
                var path = Path.Combine(folder, file);
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }
            return ResourceBytes(resource);
        }

        static string Resource(string name)
        {
            var bytes = ResourceBytes(name);
            return bytes == null ? null : System.Text.Encoding.UTF8.GetString(bytes);
        }

        static byte[] ResourceBytes(string name)
        {
            using (var stream = typeof(Warehouse).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                return bytes;
            }
        }
    }
}
