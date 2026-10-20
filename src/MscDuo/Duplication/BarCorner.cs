using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MscDuo.Duplication
{
    internal static class BarCorner
    {
        const float Back = 15.8f;
        const float Ceiling = 2.94f;
        const string FridgePath = "YARD/Building/KITCHEN/Fridge";
        const string FridgeEvent = "MSCDUO_GELADEIRA";
        const string FridgeKey = "geladeira_bar";

        static Transform frame;
        static Material template;
        static GateInput input;
        static AudioClip openClip;
        static AudioClip closeClip;

        internal static void Build(Transform warehouse, Material baseMaterial, GateInput gateInput, GameObject lights)
        {
            template = baseMaterial;
            input = gateInput;
            openClip = WarehouseRooms.GameClip("sauna_door_open", false) ?? WarehouseRooms.GameClip("door_open", false);
            closeClip = WarehouseRooms.GameClip("sauna_door_close", false) ?? WarehouseRooms.GameClip("door_close", false);
            var go = new GameObject("Galpao_Bar");
            go.transform.parent = warehouse;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;

            Planks(13.9f, 12.3f, 17.95f, Back);
            Cabinets(14.1f, 16.9f, 15.25f);
            BackShelves(14.1f, 16.9f);
            Neon("BAR", 15.5f, 2.2f, lights);
            Counter(14.1f, 16.6f, 13.3f, 13.85f);
            Tap(15.0f, 13.6f);
            foreach (var x in new[] { 14.5f, 15.35f, 16.2f }) Stool(x, 12.75f);
            foreach (var x in new[] { 14.6f, 15.35f, 16.1f }) Pendant(x, 13.55f, lights);
            var fridge = Fridge(17.45f);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> bar: montado, geladeira da cozinha {(fridge ? "ok" : "não encontrada")}, som do armário {(openClip != null ? "ok" : "não encontrado")}.");
        }

        static Vector3 L(float x, float y, float z) => new Vector3(x, z, y);

        static Material Paint(string name, Color color) => new Material(template) { name = "MscDuo_bar_" + name, color = color, mainTexture = null };

        static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) return Paint(name, color);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return new Material(shader) { name = "MscDuo_bar_" + name, mainTexture = texture };
        }

        static GameObject Part(string name, GateShape shape, Transform parent, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_" + name);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static GameObject Part(string name, GateShape shape, params Material[] materials) => Part(name, shape, frame, materials);

        static void Box(GameObject go, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = L((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f);
            b.size = L(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        }

        static void Planks(float x0, float y0, float x1, float y1)
        {
            const float width = 0.14f;
            const float length = 1.2f;
            var shape = new GateShape();
            var row = 0;
            for (var y = y0; y < y1 - 0.01f; y += width, row++)
            {
                var top = Mathf.Min(y + width - 0.003f, y1);
                var start = x0 - (row * 0.37f % length);
                var k = 0;
                for (var x = start; x < x1; x += length, k++)
                {
                    var a = Mathf.Max(x, x0);
                    var b = Mathf.Min(x + length - 0.003f, x1);
                    if (b - a < 0.02f) continue;
                    shape.Use((row + k) % 3).Box(L(a, y, 0f), L(b, top, 0.012f));
                }
            }
            var part = Part("Bar_Piso", shape, Paint("tabua1", new Color(0.55f, 0.38f, 0.22f)), Paint("tabua2", new Color(0.48f, 0.32f, 0.19f)), Paint("tabua3", new Color(0.6f, 0.43f, 0.26f)));
            Box(part, x0, y0, 0f, x1, y1, 0.012f);
        }

        static void Cabinets(float x0, float x1, float front)
        {
            var wall = Back - 0.02f;
            var mid = (x0 + x1) / 2f;
            var shape = new GateShape();
            shape.Use(0);
            foreach (var x in new[] { x0, mid - 0.01f, x1 - 0.02f }) shape.Box(L(x, front, 0.08f), L(x + 0.02f, wall, 0.86f));
            shape.Box(L(x0, front, 0.08f), L(x1, wall, 0.1f));
            shape.Box(L(x0, front + 0.02f, 0.46f), L(x1, wall, 0.48f));
            shape.Box(L(x0, wall - 0.015f, 0.1f), L(x1, wall, 0.86f));
            shape.Use(1).Box(L(x0 + 0.02f, front + 0.05f, 0f), L(x1 - 0.02f, wall, 0.08f));
            shape.Use(2).Box(L(x0 - 0.05f, front - 0.04f, 0.86f), L(x1 + 0.05f, Back, 0.9f));
            var wood = Paint("armario", new Color(0.34f, 0.2f, 0.11f));
            var part = Part("Bar_Armarios", shape, wood, Paint("rodape", new Color(0.08f, 0.07f, 0.07f)), Paint("tampo_fundo", new Color(0.16f, 0.15f, 0.15f)));
            foreach (var x in new[] { x0, mid - 0.01f, x1 - 0.02f }) Box(part, x, front, 0.08f, x + 0.02f, wall, 0.86f);
            Box(part, x0, front, 0f, x1, wall, 0.1f);
            Box(part, x0, front + 0.02f, 0.46f, x1, wall, 0.48f);
            Box(part, x0, wall - 0.015f, 0.1f, x1, wall, 0.86f);
            Box(part, x0 - 0.05f, front - 0.04f, 0.86f, x1 + 0.05f, Back, 0.9f);

            var door = Paint("porta_armario", new Color(0.4f, 0.25f, 0.14f));
            var metal = Paint("puxador", new Color(0.7f, 0.68f, 0.62f));
            var half = (mid - x0) / 2f;
            Door(1, x0, x0 + half, true, front, door, metal);
            Door(2, x0 + half, mid, false, front, door, metal);
            Door(3, mid, mid + half, true, front, door, metal);
            Door(4, mid + half, x1, false, front, door, metal);
        }

        static void Door(int index, float a, float b, bool left, float front, Material wood, Material metal)
        {
            var hinge = new GameObject("Bar_Porta" + index);
            hinge.transform.parent = frame;
            hinge.transform.localPosition = L(left ? a : b, front, 0f);
            hinge.transform.localRotation = Quaternion.identity;
            var w = b - a;
            var x0 = left ? 0.002f : -w + 0.002f;
            var x1 = left ? w - 0.002f : -0.002f;
            var free = left ? w - 0.05f : -w + 0.05f;
            var shape = new GateShape();
            shape.Use(0).Box(L(x0, -0.02f, 0.1f), L(x1, 0f, 0.855f));
            shape.Use(1).Box(L(free - 0.008f, -0.045f, 0.6f), L(free + 0.008f, -0.02f, 0.78f));
            shape.Box(L(free - 0.008f, -0.045f, 0.6f), L(free + 0.008f, -0.02f, 0.615f));
            var panel = Part("Folha", shape, hinge.transform, wood, metal);
            var box = panel.AddComponent<BoxCollider>();
            box.center = L((x0 + x1) / 2f, -0.015f, 0.4775f);
            box.size = L(x1 - x0, 0.03f, 0.755f);
            var swing = hinge.AddComponent<BarSwing>();
            var sound = hinge.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.minDistance = 0.6f;
            sound.maxDistance = 8f;
            sound.dopplerLevel = 0f;
            var key = "bar_armario" + index;
            var open = left ? 105f : -105f;
            var first = true;
            WarehouseToggles.Register(key, 0, v =>
            {
                swing.Target = v != 0 ? open : 0f;
                var clip = v != 0 ? openClip : closeClip;
                if (!first && clip != null) sound.PlayOneShot(clip, 0.45f);
                first = false;
            });
            input.Buttons.Add(new GateButton { Collider = box, Cap = hinge.transform, Click = () => WarehouseToggles.Set(key, WarehouseToggles.Get(key) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(key) == 0 ? "Abrir armário" : "Fechar armário" });
        }

        static void BackShelves(float x0, float x1)
        {
            var levels = new[] { 1.3f, 1.65f };
            var a = x0 + 0.1f;
            var b = x1 - 0.1f;
            var shape = new GateShape();
            shape.Use(0).Box(L(x0 + 0.05f, Back - 0.025f, 1.0f), L(x1 - 0.05f, Back - 0.01f, 2.05f));
            shape.Use(1).Box(L(x0 + 0.1f, Back - 0.03f, 1.05f), L(x1 - 0.1f, Back - 0.025f, 2.0f));
            shape.Use(2);
            foreach (var z in levels)
            {
                shape.Box(L(a, Back - 0.24f, z - 0.025f), L(b, Back - 0.03f, z));
                foreach (var x in new[] { a + 0.1f, (a + b) / 2f, b - 0.1f }) shape.Box(L(x - 0.01f, Back - 0.2f, z - 0.09f), L(x + 0.01f, Back - 0.03f, z - 0.025f));
            }
            var bottles = new[] { new Color(0.08f, 0.3f, 0.12f), new Color(0.42f, 0.22f, 0.06f), new Color(0.72f, 0.76f, 0.72f), new Color(0.35f, 0.05f, 0.06f) };
            var i = 0;
            foreach (var z in levels)
                for (var x = a + 0.08f; x < b - 0.05f; x += 0.17f, i++)
                {
                    var h = 0.17f + (i % 3) * 0.03f;
                    var y = Back - 0.13f;
                    shape.Use(3 + i % bottles.Length);
                    shape.Cylinder(L(x, y, z), L(x, y, z + h), 0.034f, 10);
                    shape.Cylinder(L(x, y, z + h), L(x, y, z + h + 0.08f), 0.012f, 8);
                    shape.Use(7).Cylinder(L(x, y, z + h + 0.08f), L(x, y, z + h + 0.095f), 0.014f, 8);
                }
            var materials = new List<Material> { Paint("moldura_espelho", new Color(0.12f, 0.1f, 0.09f)), Paint("espelho_bar", new Color(0.62f, 0.7f, 0.74f)), Paint("prateleira_bar", new Color(0.3f, 0.18f, 0.1f)) };
            for (int k = 0; k < bottles.Length; k++) materials.Add(Paint("garrafa" + (k + 1), bottles[k]));
            materials.Add(Paint("tampa_garrafa", new Color(0.75f, 0.6f, 0.25f)));
            var part = Part("Bar_Prateleiras", shape, materials.ToArray());
            foreach (var z in levels) Box(part, a, Back - 0.24f, z - 0.025f, b, Back - 0.03f, z);
        }

        static readonly Dictionary<char, float[]> Strokes = new Dictionary<char, float[]>
        {
            { 'A', new[] { 0f, 0f, 0.25f, 1f, 0.25f, 1f, 0.5f, 0f, 0.1f, 0.4f, 0.4f, 0.4f } },
            { 'B', new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.35f, 1f, 0.35f, 1f, 0.45f, 0.85f, 0.45f, 0.85f, 0.45f, 0.65f, 0.45f, 0.65f, 0.35f, 0.5f, 0f, 0.5f, 0.38f, 0.5f, 0.38f, 0.5f, 0.5f, 0.35f, 0.5f, 0.35f, 0.5f, 0.15f, 0.5f, 0.15f, 0.38f, 0f, 0.38f, 0f, 0f, 0f } },
            { 'R', new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.35f, 1f, 0.35f, 1f, 0.45f, 0.85f, 0.45f, 0.85f, 0.45f, 0.65f, 0.45f, 0.65f, 0.35f, 0.5f, 0.35f, 0.5f, 0f, 0.5f, 0.2f, 0.5f, 0.5f, 0f } },
        };

        static void Neon(string text, float centerX, float height, GameObject lights)
        {
            const float letter = 0.42f;
            const float advance = 0.62f;
            var width = advance * (text.Length - 1) + letter * 0.5f;
            var start = centerX - width / 2f;
            var face = Back - 0.06f;
            var glow = new GateShape();
            for (int i = 0; i < text.Length; i++)
            {
                float[] s;
                if (!Strokes.TryGetValue(text[i], out s)) continue;
                var ox = start + i * advance;
                for (int k = 0; k + 3 < s.Length; k += 4)
                {
                    var a = L(ox + s[k] * letter, face, height + s[k + 1] * letter);
                    var b = L(ox + s[k + 2] * letter, face, height + s[k + 3] * letter);
                    glow.Cylinder(a, b, 0.012f, 8);
                    glow.Cylinder(a - (b - a).normalized * 0.01f, a + (b - a).normalized * 0.01f, 0.013f, 8);
                }
            }
            Part("Bar_Neon", glow, Unlit("neon", new Color(1f, 0.45f, 0.1f)));
            var board = new GateShape();
            board.Box(L(start - 0.15f, Back - 0.035f, height - 0.15f), L(start + width + 0.15f, Back - 0.015f, height + letter + 0.15f));
            Part("Bar_Neon_Fundo", board, Paint("neon_fundo", new Color(0.05f, 0.05f, 0.07f)));
            var go = new GameObject("Bar_Neon_Luz");
            go.transform.parent = lights != null ? lights.transform : frame;
            go.transform.position = frame.TransformPoint(L(centerX, Back - 0.6f, height + letter / 2f));
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.2f;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.5f, 0.15f);
            light.shadows = LightShadows.None;
        }

        static void Counter(float x0, float x1, float y0, float y1)
        {
            const float top = 1.06f;
            var shape = new GateShape();
            shape.Use(0);
            shape.Box(L(x0, y0, 0.08f), L(x1, y0 + 0.03f, top));
            shape.Box(L(x0, y0, 0.08f), L(x0 + 0.03f, y1, top));
            shape.Box(L(x1 - 0.03f, y0, 0.08f), L(x1, y1, top));
            shape.Box(L(x0 + 0.03f, y0 + 0.03f, 0.08f), L(x1 - 0.03f, y1, 0.1f));
            shape.Box(L(x0 + 0.03f, y0 + 0.03f, 0.5f), L(x1 - 0.03f, y1 - 0.05f, 0.52f));
            shape.Use(1);
            for (var x = x0 + 0.08f; x < x1 - 0.1f; x += 0.16f) shape.Box(L(x, y0 - 0.012f, 0.1f), L(x + 0.07f, y0, top - 0.02f));
            shape.Use(2).Box(L(x0 + 0.02f, y0 + 0.04f, 0f), L(x1 - 0.02f, y1, 0.08f));
            shape.Use(3).Box(L(x0 - 0.1f, y0 - 0.2f, top), L(x1 + 0.1f, y1 + 0.05f, top + 0.04f));
            shape.Use(4).Cylinder(L(x0 + 0.05f, y0 - 0.12f, 0.22f), L(x1 - 0.05f, y0 - 0.12f, 0.22f), 0.02f, 10);
            foreach (var x in new[] { x0 + 0.15f, (x0 + x1) / 2f, x1 - 0.15f }) shape.Cylinder(L(x, y0 - 0.12f, 0.22f), L(x, y0 - 0.01f, 0.22f), 0.012f, 8);
            var part = Part("Bar_Balcao", shape, Paint("balcao", new Color(0.3f, 0.17f, 0.09f)), Paint("ripas", new Color(0.45f, 0.28f, 0.15f)), Paint("rodape_balcao", new Color(0.08f, 0.07f, 0.07f)), Paint("tampo_balcao", new Color(0.22f, 0.12f, 0.06f)), Paint("latao", new Color(0.78f, 0.6f, 0.28f)));
            Box(part, x0, y0, 0f, x1, y0 + 0.03f, top);
            Box(part, x0, y0, 0f, x0 + 0.03f, y1, top);
            Box(part, x1 - 0.03f, y0, 0f, x1, y1, top);
            Box(part, x0, y0, 0f, x1, y1, 0.1f);
            Box(part, x0 + 0.03f, y0 + 0.03f, 0.5f, x1 - 0.03f, y1 - 0.05f, 0.52f);
            Box(part, x0 - 0.1f, y0 - 0.2f, top, x1 + 0.1f, y1 + 0.05f, top + 0.04f);
        }

        static void Tap(float x, float y)
        {
            const float z = 1.1f;
            var shape = new GateShape();
            shape.Use(0).Box(L(x - 0.16f, y - 0.07f, z), L(x + 0.16f, y + 0.07f, z + 0.015f));
            shape.Use(1);
            shape.Cylinder(L(x, y + 0.03f, z), L(x, y + 0.03f, z + 0.32f), 0.03f, 14);
            shape.Cylinder(L(x - 0.13f, y + 0.03f, z + 0.32f), L(x + 0.13f, y + 0.03f, z + 0.32f), 0.032f, 14);
            foreach (var dx in new[] { -0.09f, 0.09f })
            {
                shape.Use(1);
                shape.Cylinder(L(x + dx, y + 0.03f, z + 0.32f), L(x + dx, y - 0.04f, z + 0.32f), 0.012f, 8);
                shape.Cylinder(L(x + dx, y - 0.04f, z + 0.33f), L(x + dx, y - 0.04f, z + 0.27f), 0.01f, 8);
                shape.Use(2).Cylinder(L(x + dx, y - 0.03f, z + 0.34f), L(x + dx, y - 0.03f, z + 0.48f), 0.013f, 8);
            }
            var part = Part("Bar_Chopeira", shape, Paint("bandeja", new Color(0.15f, 0.15f, 0.16f)), Paint("cromado", new Color(0.78f, 0.79f, 0.82f)), Paint("alavanca", new Color(0.05f, 0.05f, 0.05f)));
            Box(part, x - 0.16f, y - 0.07f, z, x + 0.16f, y + 0.07f, z + 0.5f);
        }

        static void Stool(float x, float y)
        {
            var shape = new GateShape();
            shape.Use(0);
            shape.Cylinder(L(x, y, 0f), L(x, y, 0.015f), 0.2f, 20);
            shape.Cylinder(L(x, y, 0.015f), L(x, y, 0.74f), 0.025f, 10);
            shape.Cylinder(L(x, y, 0.3f), L(x, y, 0.32f), 0.16f, 16, false);
            shape.Use(1).Cylinder(L(x, y, 0.74f), L(x, y, 0.8f), 0.19f, 20);
            var part = Part("Bar_Banqueta", shape, Paint("banqueta_metal", new Color(0.08f, 0.08f, 0.09f)), Paint("banqueta_assento", new Color(0.5f, 0.08f, 0.07f)));
            Box(part, x - 0.2f, y - 0.2f, 0f, x + 0.2f, y + 0.2f, 0.8f);
        }

        static void Pendant(float x, float y, GameObject lights)
        {
            const float bottom = 2.05f;
            var shape = new GateShape();
            shape.Use(0).Cylinder(L(x, y, bottom + 0.15f), L(x, y, Ceiling), 0.005f, 6);
            shape.Use(1).Cylinder(L(x, y, bottom), L(x, y, bottom + 0.15f), 0.11f, 18);
            shape.Use(2).Cylinder(L(x, y, bottom - 0.005f), L(x, y, bottom), 0.09f, 18);
            Part("Bar_Pendente", shape, Paint("fio", new Color(0.1f, 0.1f, 0.1f)), Paint("cupula", new Color(0.12f, 0.12f, 0.13f)), Unlit("lampada_bar", new Color(1f, 0.88f, 0.65f)));
            if (lights == null) return;
            var go = new GameObject("Bar_Pendente_Luz");
            go.transform.parent = lights.transform;
            go.transform.position = frame.TransformPoint(L(x, y, bottom - 0.01f));
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 100f;
            light.range = 3.2f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.85f, 0.65f);
            light.shadows = LightShadows.None;
        }

        static bool Fridge(float centerX)
        {
            var source = DynoCloner.Find(FridgePath);
            if (source == null) return false;
            var body = source.transform.Find("mesh");
            var handle = source.transform.Find("Pivot/Handle");
            var bodyCollider = body != null ? body.GetComponent<MeshCollider>() : null;
            if (bodyCollider == null || bodyCollider.sharedMesh == null || handle == null) return false;
            var outward = handle.position - bodyCollider.bounds.center;
            var front = Vector3.zero;
            var best = float.MinValue;
            var t = source.transform;
            foreach (var axis in new[] { t.right, -t.right, t.forward, -t.forward, t.up, -t.up })
            {
                var flat = new Vector3(axis.x, 0f, axis.z);
                if (flat.sqrMagnitude < 0.5f) continue;
                flat.Normalize();
                var d = Vector3.Dot(flat, outward);
                if (d > best) { best = d; front = flat; }
            }
            var wanted = frame.TransformDirection(L(0f, -1f, 0f));
            wanted.y = 0f;
            var turn = Quaternion.FromToRotation(front, wanted.normalized);
            var sources = new List<GameObject> { source };
            var floor = WarehouseRooms.Floor(source.transform, sources);
            var copies = WarehouseRooms.Place(sources, source.transform, floor, turn, frame.TransformPoint(L(centerX, Back - 0.4f, 0f)), "Geladeira_Cozinha");
            if (copies == null || copies.Count == 0) return false;
            var copy = copies[0];
            var copyBody = copy.transform.Find("mesh");
            var mesh = copyBody.GetComponent<MeshCollider>().sharedMesh;
            var lb = mesh.bounds;
            float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z);
                var local = frame.InverseTransformPoint(copyBody.TransformPoint(corner));
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                maxY = Mathf.Max(maxY, local.z);
                minH = Mathf.Min(minH, local.y);
                maxH = Mathf.Max(maxH, local.y);
            }
            var shift = L(centerX - (minX + maxX) / 2f, Back - 0.03f - maxY, 0f);
            copy.transform.position += frame.TransformVector(shift);

            var paper = copy.transform.Find("Pivot/fridge_paper");
            if (paper != null) paper.gameObject.SetActive(false);
            var point = copy.transform.Find("FridgePoint");
            var chill = point != null ? point.Find("ChillArea") : null;
            if (point != null)
                foreach (var fsm in point.GetComponents<PlayMakerFSM>()) Object.DestroyImmediate(fsm);
            var inside = frame.TransformPoint(L(centerX, Back - 0.03f - 0.3f, (minH + maxH) / 2f));
            if (chill == null)
            {
                chill = new GameObject("ChillArea").transform;
                chill.parent = copy.transform;
                chill.position = inside;
            }
            House2Fridge.Extras.Add(chill);

            var lamp = new GameObject("Geladeira_Luz");
            lamp.transform.parent = copy.transform;
            lamp.transform.position = frame.TransformPoint(L(centerX, Back - 0.03f - 0.3f, minH + (maxH - minH) * 0.8f));
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 0.9f;
            light.intensity = 1.2f;
            light.color = new Color(0.9f, 0.95f, 1f);
            light.shadows = LightShadows.None;
            light.enabled = false;

            var copyHandle = copy.transform.Find("Pivot/Handle");
            PlayMakerFSM use = null;
            if (copyHandle != null)
                foreach (var fsm in copyHandle.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Use") use = fsm;
            if (use == null) return true;
            foreach (var state in use.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    var name = action.GetType().Name;
                    if (name == "SetFsmFloat" || name == "SendEventByName" || name == "ActivateGameObject") action.Enabled = false;
                }
            var open = use.FsmVariables.FindFsmBool("DoorOpen");
            if (use.Fsm.GetState("Check position") != null)
            {
                use.AddEvent(FridgeEvent);
                use.AddGlobalTransition(FridgeEvent, "Check position");
                var fsm = use;
                WarehouseToggles.Register(FridgeKey, 0, v =>
                {
                    if (open != null && open.Value != (v != 0)) fsm.SendEvent(FridgeEvent);
                });
            }
            var watch = copy.AddComponent<FridgeWatch>();
            watch.Open = open;
            watch.Chill = chill.gameObject;
            watch.Lamp = light;
            return true;
        }

        class BarSwing : MonoBehaviour
        {
            internal float Target;
            float angle;

            void Update()
            {
                if (Mathf.Approximately(angle, Target)) return;
                angle = Mathf.MoveTowards(angle, Target, Time.deltaTime * 220f);
                transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        class FridgeWatch : MonoBehaviour
        {
            internal FsmBool Open;
            internal GameObject Chill;
            internal Light Lamp;
            bool last;

            void Update()
            {
                if (Open == null || Open.Value == last) return;
                last = Open.Value;
                if (Chill != null) Chill.SetActive(!last);
                if (Lamp != null) Lamp.enabled = last;
                var value = last ? 1 : 0;
                if (WarehouseToggles.Get(FridgeKey) != value) WarehouseToggles.Set(FridgeKey, value);
            }
        }
    }
}
