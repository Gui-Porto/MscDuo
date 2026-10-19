using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class MezzanineDecor
    {
        const float Floor = 3.205f;
        const float Back = 15.8f;
        const float LeftWall = 0.2f;
        const float RightWall = 23.8f;
        const float Front = 10.0f;
        const float WainscotHeight = 1.1f;
        const string DeskPath = "YARD/Building/BEDROOM1/ATK Table";
        const string ChairPath = "YARD/Building/BEDROOM1/chair_wooden";
        const string ComputerPath = "YARD/Building/BEDROOM1/COMPUTER/Computer/Meshes";
        static readonly string[] ComputerParts = { "386_case(xxxxx)", "386_monitor(xxxxx)", "386_peripherals(xxxxx)", "386_speakers(xxxxx)" };

        static Transform frame;
        static Material template;

        internal static void Build(Transform root, Material baseMaterial, GameObject lights)
        {
            template = baseMaterial;
            var go = new GameObject("Galpao_Mezanino");
            go.transform.parent = root;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;

            Wainscot();
            Rug("tapete_sala", 1.4f, 11.3f, 6.4f, 14.6f);
            Rug("tapete_jogos", 9.6f, 11.3f, 13.6f, 14.3f);
            Rug("tapete_escritorio", 16.4f, 10.9f, 21.2f, 13.4f);
            Pendant(11.6f, 12.8f, lights);
            Shelf(1.5f, 2.6f, 1.8f, 5, 1);
            Shelf(5.2f, 6.3f, 1.8f, 5, 2);
            Shelf(15.6f, 19.2f, 2.0f, 5, 3);
            HighTable(14.4f, 11.0f);
            Neon("ARCADE", 9.6f, 2.35f, lights);
            Office(18.4f, 11.8f);
        }

        static Vector3 L(float x, float y, float z) => new Vector3(x, z, y);

        static GameObject Part(string name, GateShape shape, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.parent = frame;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_" + name);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static Material Color(string name, Color color) =>
            new Material(template) { name = "MscDuo_mezanino_" + name, color = color, mainTexture = null };

        static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) return Color(name, color);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return new Material(shader) { name = "MscDuo_mezanino_" + name, mainTexture = texture };
        }

        static void Wainscot()
        {
            var wood = Warehouse.Textured("lambri", template);
            var trim = Color("lambri_rodameio", new Color(0.24f, 0.15f, 0.09f));
            var shape = new GateShape();
            var top = Floor + WainscotHeight;
            shape.Use(0);
            for (var x = LeftWall; x < RightWall - 0.01f; x += 1f)
            {
                var x1 = Mathf.Min(x + 1f, RightWall);
                var z = Back - 0.012f;
                shape.Quad(L(x1, z, Floor), L(x, z, Floor), L(x, z, top), L(x1, z, top), Vector3.back);
            }
            foreach (var wall in new[] { LeftWall + 0.012f, RightWall - 0.012f })
            {
                var inward = wall < 12f ? Vector3.right : Vector3.left;
                for (var y = Front; y < Back - 0.01f; y += 1f)
                {
                    var y1 = Mathf.Min(y + 1f, Back);
                    if (wall < 12f) shape.Quad(L(wall, y, Floor), L(wall, y1, Floor), L(wall, y1, top), L(wall, y, top), inward);
                    else shape.Quad(L(wall, y1, Floor), L(wall, y, Floor), L(wall, y, top), L(wall, y1, top), inward);
                }
            }
            shape.Use(1);
            shape.Box(L(LeftWall, Back - 0.04f, top), L(RightWall, Back, top + 0.05f));
            shape.Box(L(LeftWall, Front, top), L(LeftWall + 0.04f, Back, top + 0.05f));
            shape.Box(L(RightWall - 0.04f, Front, top), L(RightWall, Back, top + 0.05f));
            Part("Lambri", shape, wood, trim);
        }

        static void Rug(string texture, float x0, float y0, float x1, float y1)
        {
            var shape = new GateShape();
            shape.Use(0).Quad(L(x0, y0, Floor + 0.008f), L(x1, y0, Floor + 0.008f), L(x1, y1, Floor + 0.008f), L(x0, y1, Floor + 0.008f), Vector3.up);
            shape.Use(1);
            shape.Quad(L(x0, y0, Floor), L(x1, y0, Floor), L(x1, y0, Floor + 0.008f), L(x0, y0, Floor + 0.008f), Vector3.forward);
            shape.Quad(L(x1, y1, Floor), L(x0, y1, Floor), L(x0, y1, Floor + 0.008f), L(x1, y1, Floor + 0.008f), Vector3.back);
            shape.Quad(L(x0, y1, Floor), L(x0, y0, Floor), L(x0, y0, Floor + 0.008f), L(x0, y1, Floor + 0.008f), Vector3.left);
            shape.Quad(L(x1, y0, Floor), L(x1, y1, Floor), L(x1, y1, Floor + 0.008f), L(x1, y0, Floor + 0.008f), Vector3.right);
            Part(texture, shape, Warehouse.Textured(texture, template), Color(texture + "_borda", new Color(0.12f, 0.1f, 0.09f)));
        }

        static float Roof(float x) => 6f + 2f * (1f - Mathf.Abs(x - 12f) / 12f) - 0.22f;

        static void Pendant(float x, float y, GameObject lights)
        {
            var bottom = Floor + 1.75f;
            var shape = new GateShape();
            shape.Use(0);
            shape.Box(L(x - 0.65f, y - 0.16f, bottom + 0.03f), L(x + 0.65f, y + 0.16f, bottom + 0.14f));
            shape.Box(L(x - 0.67f, y - 0.18f, bottom), L(x - 0.63f, y + 0.18f, bottom + 0.14f));
            shape.Box(L(x + 0.63f, y - 0.18f, bottom), L(x + 0.67f, y + 0.18f, bottom + 0.14f));
            shape.Use(1);
            foreach (var dx in new[] { -0.45f, 0.45f })
                shape.Cylinder(L(x + dx, y, bottom + 0.14f), L(x + dx, y, Roof(x + dx)), 0.006f, 6);
            shape.Use(2).Box(L(x - 0.6f, y - 0.12f, bottom + 0.02f), L(x + 0.6f, y + 0.12f, bottom + 0.03f));
            Part("Pendente_Sinuca", shape, Color("pendente", new Color(0.06f, 0.28f, 0.14f)), Color("corrente", new Color(0.2f, 0.2f, 0.2f)), Unlit("pendente_luz", new Color(1f, 0.92f, 0.75f)));
            if (lights == null) return;
            var go = new GameObject("Pendente_Luz");
            go.transform.parent = lights.transform;
            go.transform.position = frame.TransformPoint(L(x, y, bottom));
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 110f;
            light.range = 3.5f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.9f, 0.72f);
            light.shadows = LightShadows.None;
        }

        static void Shelf(float x0, float x1, float height, int levels, int seed)
        {
            var depth = 0.45f;
            var y1 = Back - 0.03f;
            var y0 = y1 - depth;
            var shape = new GateShape();
            shape.Use(0);
            foreach (var x in new[] { x0, x1 - 0.035f })
                foreach (var y in new[] { y0, y1 - 0.035f })
                    shape.Box(L(x, y, Floor), L(x + 0.035f, y + 0.035f, Floor + height));
            var bays = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 1.2f));
            for (int b = 1; b < bays; b++)
            {
                var x = x0 + (x1 - x0) * b / bays;
                foreach (var y in new[] { y0, y1 - 0.035f })
                    shape.Box(L(x - 0.017f, y, Floor), L(x + 0.017f, y + 0.035f, Floor + height));
            }
            shape.Use(1);
            for (int i = 0; i < levels; i++)
            {
                var z = Floor + 0.12f + (height - 0.2f) * i / (levels - 1);
                shape.Box(L(x0, y0, z), L(x1, y1, z + 0.02f));
            }
            shape.Use(2);
            var rng = new System.Random(seed * 97);
            for (int i = 0; i < levels - 1; i++)
            {
                var z = Floor + 0.14f + (height - 0.2f) * i / (levels - 1);
                var gap = (height - 0.2f) / (levels - 1) - 0.06f;
                var x = x0 + 0.06f;
                while (x < x1 - 0.25f)
                {
                    var w = 0.22f + (float)rng.NextDouble() * 0.28f;
                    if (x + w > x1 - 0.05f) break;
                    if (rng.NextDouble() < 0.7)
                    {
                        var h = Mathf.Min(gap, 0.12f + (float)rng.NextDouble() * 0.2f);
                        var d = 0.25f + (float)rng.NextDouble() * 0.15f;
                        shape.Box(L(x, y1 - 0.04f - d, z), L(x + w, y1 - 0.04f, z + h));
                    }
                    x += w + 0.04f + (float)rng.NextDouble() * 0.12f;
                }
            }
            var part = Part("Estante", shape, Color("estante_coluna", new Color(0.18f, 0.32f, 0.55f)), Color("estante_prateleira", new Color(0.62f, 0.5f, 0.32f)), Color("caixa", new Color(0.58f, 0.43f, 0.26f)));
            var box = part.AddComponent<BoxCollider>();
            box.center = L((x0 + x1) / 2f, (y0 + y1) / 2f, Floor + height / 2f);
            box.size = L(x1 - x0, depth, height);
        }

        static void HighTable(float x, float y)
        {
            var shape = new GateShape();
            var metal = 0;
            shape.Use(metal);
            shape.Cylinder(L(x, y, Floor), L(x, y, Floor + 0.02f), 0.25f, 24);
            shape.Cylinder(L(x, y, Floor + 0.02f), L(x, y, Floor + 1.05f), 0.035f, 12);
            var stools = new[] { new Vector2(x - 0.55f, y + 0.35f), new Vector2(x + 0.55f, y + 0.35f) };
            foreach (var s in stools)
            {
                shape.Cylinder(L(s.x, s.y, Floor), L(s.x, s.y, Floor + 0.015f), 0.2f, 20);
                shape.Cylinder(L(s.x, s.y, Floor + 0.015f), L(s.x, s.y, Floor + 0.72f), 0.022f, 10);
                shape.Cylinder(L(s.x, s.y, Floor + 0.3f), L(s.x, s.y, Floor + 0.32f), 0.15f, 16, false);
            }
            shape.Use(1).Cylinder(L(x, y, Floor + 1.05f), L(x, y, Floor + 1.09f), 0.36f, 28);
            shape.Use(2);
            foreach (var s in stools) shape.Cylinder(L(s.x, s.y, Floor + 0.72f), L(s.x, s.y, Floor + 0.79f), 0.19f, 20);
            var part = Part("Mesa_Alta", shape, Color("metal_preto", new Color(0.08f, 0.08f, 0.09f)), Color("tampo", new Color(0.38f, 0.24f, 0.13f)), Color("assento", new Color(0.5f, 0.08f, 0.07f)));
            var c = part.AddComponent<BoxCollider>();
            c.center = L(x, y, Floor + 0.55f);
            c.size = L(0.7f, 0.7f, 1.1f);
        }

        static readonly Dictionary<char, float[]> Strokes = new Dictionary<char, float[]>
        {
            { 'A', new[] { 0f, 0f, 0.25f, 1f, 0.25f, 1f, 0.5f, 0f, 0.1f, 0.4f, 0.4f, 0.4f } },
            { 'R', new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.35f, 1f, 0.35f, 1f, 0.45f, 0.85f, 0.45f, 0.85f, 0.45f, 0.65f, 0.45f, 0.65f, 0.35f, 0.5f, 0.35f, 0.5f, 0f, 0.5f, 0.2f, 0.5f, 0.5f, 0f } },
            { 'C', new[] { 0.5f, 1f, 0.12f, 1f, 0.12f, 1f, 0f, 0.85f, 0f, 0.85f, 0f, 0.15f, 0f, 0.15f, 0.12f, 0f, 0.12f, 0f, 0.5f, 0f } },
            { 'D', new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.3f, 1f, 0.3f, 1f, 0.5f, 0.75f, 0.5f, 0.75f, 0.5f, 0.25f, 0.5f, 0.25f, 0.3f, 0f, 0.3f, 0f, 0f, 0f } },
            { 'E', new[] { 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 1f, 0.5f, 1f, 0f, 0.5f, 0.38f, 0.5f } },
        };

        static void Neon(string text, float centerX, float height, GameObject lights)
        {
            const float letter = 0.42f;
            const float advance = 0.62f;
            var width = advance * (text.Length - 1) + letter * 0.5f;
            var start = centerX - width / 2f;
            var z0 = Floor + height;
            var face = Back - 0.06f;
            var glow = new GateShape();
            for (int i = 0; i < text.Length; i++)
            {
                float[] s;
                if (!Strokes.TryGetValue(text[i], out s)) continue;
                var ox = start + i * advance;
                for (int k = 0; k + 3 < s.Length; k += 4)
                {
                    var a = L(ox + s[k] * letter, face, z0 + s[k + 1] * letter);
                    var b = L(ox + s[k + 2] * letter, face, z0 + s[k + 3] * letter);
                    glow.Cylinder(a, b, 0.012f, 8);
                    glow.Cylinder(a - (b - a).normalized * 0.01f, a + (b - a).normalized * 0.01f, 0.013f, 8);
                }
            }
            Part("Neon", glow, Unlit("neon", new Color(1f, 0.25f, 0.75f)));
            var board = new GateShape();
            board.Box(L(start - 0.15f, Back - 0.035f, z0 - 0.15f), L(start + width + 0.15f, Back - 0.015f, z0 + letter + 0.15f));
            Part("Neon_Fundo", board, Color("neon_fundo", new Color(0.05f, 0.05f, 0.07f)));
            var go = new GameObject("Neon_Luz");
            go.transform.parent = lights != null ? lights.transform : frame;
            go.transform.position = frame.TransformPoint(L(centerX, Back - 0.6f, z0 + letter / 2f));
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.2f;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.3f, 0.75f);
            light.shadows = LightShadows.None;
        }

        static void Office(float x, float y)
        {
            var desk = DynoCloner.Find(DeskPath);
            if (desk == null)
            {
                ModConsole.Warning("[MSC Duo] mesa do computador do quarto não encontrada; escritório sem mesa.");
                return;
            }
            var chair = DynoCloner.Find(ChairPath);
            var computer = DynoCloner.Find(ComputerPath);
            var floor = float.MaxValue;
            foreach (var c in desk.GetComponentsInChildren<Collider>(true)) floor = Mathf.Min(floor, c.bounds.min.y);
            if (floor == float.MaxValue) floor = desk.transform.position.y;
            var user = chair != null ? chair.transform.position - desk.transform.position : -desk.transform.right;
            user.y = 0f;
            var toward = Warehouse.Local(0f, 1f, 0f) - Warehouse.Local(0f, 0f, 0f);
            var turn = Quaternion.FromToRotation(user.normalized, toward);
            var target = frame.TransformPoint(L(x, y, Floor));
            var pivot = desk.transform.position;
            var holder = new GameObject("Escritorio");
            holder.transform.parent = frame;
            int copied = 0;
            foreach (var source in new[] { desk, chair, computer })
            {
                if (source == null) continue;
                var offset = source.transform.position - pivot;
                var position = target + turn * new Vector3(offset.x, 0f, offset.z);
                position.y = target.y + (source.transform.position.y - floor);
                var copy = (GameObject)Object.Instantiate(source, position, turn * source.transform.rotation);
                copy.name = source.name;
                copy.transform.parent = holder.transform;
                HouseCloner.FixStaticBatching(source, copy);
                Decorative(copy, source == computer);
                copy.SetActive(true);
                copied++;
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> escritório do mezanino: {copied} peças do quarto copiadas.");
        }

        static void Decorative(GameObject copy, bool computer)
        {
            foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true)) Object.DestroyImmediate(fsm);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Object.DestroyImmediate(behaviour);
            foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
            foreach (var light in copy.GetComponentsInChildren<Light>(true)) light.enabled = false;
            if (!computer) return;
            foreach (var name in ComputerParts)
            {
                var part = copy.transform.Find(name);
                if (part != null) part.gameObject.SetActive(true);
            }
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name == "Lights" || t.name == "ElectricShockPoint") t.gameObject.SetActive(false);
        }
    }
}
