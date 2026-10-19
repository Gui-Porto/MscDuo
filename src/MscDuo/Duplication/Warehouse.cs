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
        const string SwitchSource = "switch_kitchen";
        const string SwitchTag = "MscDuoGalpaoLuz";
        static readonly Vector3 Origin = new Vector3(-30f, -0.497f, 13f);

        static readonly Vector4[] Lamps =
        {
            new Vector4(6f, 3f, 6f, 14f), new Vector4(12f, 3f, 7f, 14f), new Vector4(18f, 3f, 6f, 14f),
            new Vector4(6f, 7.5f, 6f, 14f), new Vector4(12f, 7.5f, 7f, 14f), new Vector4(18f, 7.5f, 6f, 14f),
            new Vector4(3f, 12.9f, 2.8f, 7f), new Vector4(9f, 12.9f, 2.8f, 7f), new Vector4(15f, 12.9f, 2.8f, 7f),
            new Vector4(4f, 13f, 6f, 9f), new Vector4(12f, 13f, 7f, 9f), new Vector4(20f, 13f, 6f, 9f),
        };

        static readonly string[] Solid = { "Piso", "Paredes", "Mezanino", "Escada", "Salas", "GuardaCorpo", "Estrutura", "Telhado", "Janelas" };
        static readonly string[] Gates = { "Portao1", "Portao2" };

        static GameObject root;

        internal static Transform LightSwitch { get; private set; }

        internal static GameObject Root => root;

        internal static void Spawn()
        {
            if (root != null) Object.Destroy(root);
            WarehouseConfig.Load();
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

            var furniture = Furniture(root.transform, template, textures);

            var lights = BuildLights(root.transform);
            var lamp = root.transform.Find("Galpao_Luzes");
            if (lamp != null) lamp.gameObject.AddComponent<LampState>().Lights = lights;
            var switches = Switches(root.transform, lights);
            var light = switches.Count > 0;
            if (lamp != null) lamp.GetComponent<LampState>().Switches = switches;
            try { WarehouseGates.Build(root.transform, template); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] portões do galpão falharam: {e}"); }

            var cleared = TreeClearer.ClearArea(bounds.min - new Vector3(TreeMargin, 0f, TreeMargin), bounds.max + new Vector3(TreeMargin + WarehouseConfig.Get("patio_arvores"), 0f, TreeMargin));
            ModConsole.Print($"<color=orange>[MSC Duo]</color> galpão: {parts} peças, {colliders} com colisão, {textures.Count} materiais, {cleared} árvores removidas, {furniture} móveis, interruptor {(light ? "ok" : "não encontrado")}, modelo {(WarehouseConfig.DevFolder != null ? "da pasta " + WarehouseConfig.DevFolder : "da DLL")}; centro {HouseCloner.Format(bounds.center)}.");
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

        const string SyncEvent = "MSCDUO_SYNC";

        static List<PlayMakerFSM> Switches(Transform parent, GameObject lights)
        {
            var made = new List<PlayMakerFSM>();
            PlayMakerFSM source = null;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (fsm != null && fsm.gameObject.name == SwitchSource && fsm.FsmName == "Use" && fsm.gameObject.hideFlags == HideFlags.None && fsm.transform.root.name == "YARD")
                {
                    source = fsm;
                    break;
                }
            if (source == null) return made;

            var original = source.gameObject;
            var front = FrontOf(original.transform);
            var gap = GapToWall(original.transform, front);
            var height = WarehouseConfig.Get("interruptor_altura");
            var extra = WarehouseConfig.Get("interruptor_x");
            var main = Copy(original, front, Local(WarehouseConfig.Get("interruptor_frente_x"), WALL_INNER, height), Vector3.left, gap + extra, "Galpao_Interruptor", SwitchTag, parent, lights);
            var side = Copy(original, front, Local(WALL_INNER, WarehouseConfig.Get("interruptor_y"), height), Vector3.forward, gap + extra, "Galpao_Interruptor_Porta", SwitchTag + "2", parent, lights);
            LightSwitch = main.transform;
            made.Add(main);
            made.Add(side);
            ModConsole.Print(string.Format(CultureInfo.InvariantCulture, "<color=orange>[MSC Duo]</color> interruptores do galpão: frente {0}, porta {1}, {2:0.000} m da parede.", HouseCloner.Format(main.transform.position), HouseCloner.Format(side.transform.position), gap));
            return made;
        }

        static PlayMakerFSM Copy(GameObject original, Vector3 front, Vector3 wallPoint, Vector3 facing, float gap, string name, string tag, Transform parent, GameObject lights)
        {
            var copy = (GameObject)Object.Instantiate(original, wallPoint + facing * (gap + PlateDepth), original.transform.rotation);
            copy.name = name;
            copy.transform.parent = parent;
            copy.transform.rotation = Quaternion.AngleAxis(WarehouseConfig.Get("interruptor_giro"), Vector3.up) * Quaternion.FromToRotation(front, facing) * original.transform.rotation;
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
            Plate(copy, wallPoint, facing);
            var map = new Dictionary<Object, Object>();
            SatsumaCloner.MapHierarchy(original.transform, copy.transform, map);
            PlayMakerFSM use = null;
            foreach (var fsm in copy.GetComponents<PlayMakerFSM>())
            {
                FsmTools.EnsureLoaded(fsm);
                SatsumaCloner.Remap(fsm, map);
                var unique = fsm.FsmVariables.FindFsmString("UniqueTagSwitch");
                if (unique != null) unique.Value = tag;
                var target = fsm.FsmVariables.FindFsmGameObject("Lights");
                if (target != null) target.Value = lights;
                var on = fsm.FsmVariables.FindFsmBool("Switch");
                if (on != null) on.Value = false;
                if (fsm.FsmName == "Use")
                {
                    if (fsm.Fsm.GetState("Position") != null) fsm.AddGlobalTransition(SyncEvent, "Position");
                    use = fsm;
                }
            }
            copy.SetActive(true);
            return use;
        }

        const float WALL_INNER = 0.2f;
        const float PlateDepth = 0.012f;
        static Material plateMaterial;

        static void Plate(GameObject copy, Vector3 wallPoint, Vector3 facing)
        {
            var first = true;
            var bounds = new Bounds(copy.transform.position, Vector3.zero);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            var side = Vector3.Cross(Vector3.up, facing).normalized;
            var across = Mathf.Abs(Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), Mathf.Abs(side.z))));
            var width = Mathf.Max(0.085f, across + 0.035f);
            var height = Mathf.Max(0.12f, bounds.size.y + 0.045f);
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(plate.GetComponent<Collider>());
            plate.name = "Espelho";
            plate.transform.parent = copy.transform.parent;
            var center = bounds.center - facing * Vector3.Dot(bounds.center - wallPoint, facing);
            plate.transform.position = center + facing * (PlateDepth / 2f);
            plate.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            plate.transform.localScale = new Vector3(width, height, PlateDepth);
            if (plateMaterial == null)
            {
                plateMaterial = new Material(Template()) { name = "MscDuo_espelho_interruptor", color = new Color(0.93f, 0.92f, 0.88f), mainTexture = null };
            }
            plate.GetComponent<Renderer>().sharedMaterial = plateMaterial;
        }

        static float GapToWall(Transform t, Vector3 front)
        {
            var best = 0.05f;
            var found = false;
            foreach (var hit in Physics.RaycastAll(t.position + front * 0.3f, -front, 0.8f))
            {
                if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(t)) continue;
                var gap = 0.3f - hit.distance;
                if (gap < -0.2f || gap > 0.3f) continue;
                if (!found || gap < best) { best = gap; found = true; }
            }
            return Mathf.Clamp(best, 0f, 0.15f);
        }

        static Vector3 FrontOf(Transform t)
        {
            var plate = t.GetComponentInChildren<MeshFilter>();
            var axis = t.forward;
            if (plate != null && plate.sharedMesh != null)
            {
                var size = plate.sharedMesh.bounds.size;
                var local = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                axis = plate.transform.TransformDirection(local);
            }
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f) axis = t.forward;
            axis.Normalize();
            var house = HouseCenter(t);
            var inward = house - t.position;
            inward.y = 0f;
            return Vector3.Dot(axis, inward) >= 0f ? axis : -axis;
        }

        static Vector3 HouseCenter(Transform t)
        {
            var building = t;
            while (building != null && building.name != "Building") building = building.parent;
            if (building == null) return t.position;
            var first = true;
            var bounds = new Bounds(building.position, Vector3.zero);
            foreach (var renderer in building.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds.center;
        }

        class LampState : MonoBehaviour
        {
            internal GameObject Lights;
            internal List<PlayMakerFSM> Switches = new List<PlayMakerFSM>();
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
                foreach (var fsm in Switches)
                {
                    if (fsm == null) continue;
                    var state = fsm.FsmVariables.FindFsmBool("Switch");
                    if (state == null || state.Value == on) continue;
                    state.Value = on;
                    fsm.SendEvent(SyncEvent);
                }
            }
        }

        static Material Template()
        {
            var concrete = ApronExtension.ConcreteRenderer;
            if (concrete != null && concrete.sharedMaterial != null) return concrete.sharedMaterial;
            return new Material(Shader.Find("Diffuse"));
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
                var bounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
            return parts.Count;
        }

        static Material BuildingMaterial(string name, Material template, Dictionary<string, Material> cache, string folder, string prefix)
        {
            Material material;
            if (cache.TryGetValue(name, out material)) return material;
            Shader shader = null;
            if (name == "vidro") shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Transparent/Diffuse");
            else if (name == "luz") shader = Shader.Find("Unlit/Texture");
            material = shader != null ? new Material(shader) : new Material(template);
            material.name = "MscDuo_galpao_" + name;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.color = name == "vidro" ? new Color(0.75f, 0.85f, 0.9f, 0.35f) : Color.white;
            var bytes = Bytes(Path.Combine(folder, name + ".png"), prefix + name + ".png");
            if (bytes != null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "MscDuo_galpao_" + name, wrapMode = TextureWrapMode.Repeat };
                texture.LoadImage(bytes);
                material.mainTexture = texture;
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
