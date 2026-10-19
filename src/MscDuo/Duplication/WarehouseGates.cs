using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class WarehouseGates
    {
        internal const string SaveFile = "MscDuo_galpao.txt";
        const string SoundPrefix = "MscDuo.portao.";
        const float RemoteRange = 40f;
        const float RequestSeconds = 2f;
        static readonly float[][] Openings = { new[] { 2f, 22f } };

        static readonly List<GateController> gates = new List<GateController>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static Action<GateController> sender;
        static bool received;
        static float nextRequest;

        internal static IList<GateController> All => gates;

        static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

        internal static void Bind(Action<GateController> send) => sender = send;

        internal static void Notify(GateController gate)
        {
            if (sender != null) sender(gate);
        }

        internal static void Build(Transform root, Material template)
        {
            gates.Clear();
            received = false;
            nextRequest = 0f;
            LoadClips();
            var saved = Load();
            var materials = new GateMaterials(template);
            var input = root.gameObject.AddComponent<GateInput>();
            for (int i = 0; i < Openings.Length; i++)
            {
                var gate = Create(i, Openings[i][0], Openings[i][1], i == 0 ? -1f : 1f, root, materials, input);
                float position;
                gate.SetPosition(saved.TryGetValue("portao" + (i + 1), out position) ? position : 0f);
                gates.Add(gate);
            }
            Acoustics(root);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> portões do galpão: {gates.Count}, {clips.Count} sons.");
        }

        internal static void Save()
        {
            if (gates.Count == 0) return;
            var sb = new StringBuilder();
            for (int i = 0; i < gates.Count; i++)
                if (gates[i] != null) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "portao{0} = {1:0.0000}", i + 1, gates[i].Position));
            sb.AppendLine("luz_externa = " + (int)WarehouseExterior.Mode);
            File.WriteAllText(SavePath, sb.ToString());
        }

        internal static Dictionary<string, float> Load()
        {
            var result = new Dictionary<string, float>();
            if (!File.Exists(SavePath)) return result;
            foreach (var raw in File.ReadAllLines(SavePath))
            {
                var eq = raw.IndexOf('=');
                float value;
                if (eq > 0 && float.TryParse(raw.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) result[raw.Substring(0, eq).Trim()] = value;
            }
            return result;
        }

        internal static void Read(BinaryReader r)
        {
            var index = r.ReadByte();
            var motion = r.ReadSByte();
            var position = r.ReadSingle();
            var last = r.ReadSByte();
            if (index >= gates.Count || gates[index] == null) return;
            received = true;
            gates[index].Apply(motion, position, last);
        }

        internal static void Tick(float now, Action request)
        {
            if (received || gates.Count == 0 || now < nextRequest) return;
            nextRequest = now + RequestSeconds;
            request();
        }

        internal static void Remote()
        {
            var cam = Camera.main;
            if (cam == null) return;
            GateController best = null;
            var bestDistance = RemoteRange;
            foreach (var gate in gates)
            {
                if (gate == null) continue;
                var distance = Vector3.Distance(cam.transform.position, gate.Center);
                if (distance < bestDistance) { bestDistance = distance; best = gate; }
            }
            if (best != null) best.Remote();
        }

        static GateController Create(int index, float x0, float x1, float side, Transform root, GateMaterials m, GateInput input)
        {
            var width = x1 - x0;
            var go = new GameObject("Galpao_Portao" + (index + 1));
            go.transform.parent = root;
            go.transform.position = Warehouse.Local(x0, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var t = go.transform;
            var gate = go.AddComponent<GateController>();
            gate.Index = index;
            gate.Width = width;
            gate.Speed = Mathf.Clamp(WarehouseConfig.Get("portao_velocidade"), 0.05f, 3f);
            gate.Volume = Mathf.Max(0f, WarehouseConfig.Get("portao_volume"));
            Func<float, float> X = outward => side < 0f ? -outward : width + outward;
            float cz = GateController.CurtainZ, dy = GateController.DrumY, dz = GateController.DrumZ;

            var frame = new GateShape();
            frame.Use(0);
            foreach (var edge in new[] { 0f, width })
            {
                var o = edge == 0f ? -1f : 1f;
                frame.Box(new Vector3(edge + o * 0.075f, 0f, cz - 0.045f), new Vector3(edge + o * 0.068f, GateController.GuideTop, cz + 0.045f));
                frame.Box(new Vector3(edge + o * 0.075f, 0f, cz - 0.045f), new Vector3(edge - o * 0.03f, GateController.GuideTop, cz - 0.038f));
                frame.Box(new Vector3(edge + o * 0.075f, 0f, cz + 0.038f), new Vector3(edge - o * 0.03f, GateController.GuideTop, cz + 0.045f));
                frame.Box(new Vector3(edge + o * 0.075f, GateController.GuideTop - 0.02f, cz - 0.06f), new Vector3(edge - o * 0.03f, GateController.GuideTop, cz - 0.045f));
                frame.Box(new Vector3(edge + o * 0.075f, GateController.GuideTop - 0.02f, cz + 0.045f), new Vector3(edge - o * 0.03f, GateController.GuideTop, cz + 0.06f));
                frame.Box(new Vector3(edge + o * 0.127f, 4.5f, 0.2f), new Vector3(edge + o * 0.135f, 4.95f, 0.72f));
                frame.Box(new Vector3(edge + o * 0.068f, 4.5f, 0.2f), new Vector3(edge + o * 0.135f, 4.53f, 0.26f));
                frame.Cylinder(new Vector3(edge + o * 0.1f, dy, dz), new Vector3(edge + o * 0.127f, dy, dz), 0.045f, 16);
            }
            frame.Use(1);
            frame.Box(new Vector3(X(0.135f), dy - 0.1f, dz - 0.1f), new Vector3(X(0.305f), dy + 0.1f, dz + 0.1f));
            frame.Box(new Vector3(X(0.36f), dy + 0.065f, dz - 0.045f), new Vector3(X(0.46f), dy + 0.12f, dz + 0.045f));
            frame.Use(2);
            frame.Cylinder(new Vector3(X(0.305f), dy, dz), new Vector3(X(0.515f), dy, dz), 0.075f, 20);
            for (int k = 0; k < 6; k++)
            {
                var ring = X(0.33f + k * 0.03f);
                frame.Cylinder(new Vector3(ring, dy, dz), new Vector3(ring + (side < 0f ? -0.008f : 0.008f), dy, dz), 0.079f, 20, false);
            }
            frame.Use(3);
            frame.Cylinder(new Vector3(X(0.515f), dy, dz), new Vector3(X(0.55f), dy, dz), 0.07f, 20);
            var px = X(0.75f);
            var top = dy + 0.19f;
            frame.Use(4);
            frame.Cylinder(new Vector3(px, 1.49f, 0.215f), new Vector3(px, top + 0.011f, 0.215f), 0.011f, 10);
            frame.Cylinder(new Vector3(px, top, 0.215f), new Vector3(X(0.41f), top, 0.215f), 0.011f, 10);
            frame.Cylinder(new Vector3(X(0.41f), top, 0.204f), new Vector3(X(0.41f), top, dz + 0.011f), 0.011f, 10);
            frame.Cylinder(new Vector3(X(0.41f), top + 0.011f, dz), new Vector3(X(0.41f), dy + 0.12f, dz), 0.011f, 10);
            Part(t, "Galpao_Portao_Estrutura", frame.ToMesh("MscDuo_portao_estrutura"), new[] { m.Steel, m.Gear, m.Motor, m.Dark, m.Conduit });

            var slatMesh = SlatMesh(-0.045f, width + 0.045f);
            gate.Slats = new Transform[GateController.SlatCount];
            for (int k = 0; k < GateController.SlatCount; k++)
            {
                var slat = Part(t, "Lamina", slatMesh, new[] { m.Galvanized[(k * 7 + index * 3) % m.Galvanized.Length] });
                gate.Slats[k] = slat.transform;
            }

            var bar = new GateShape();
            bar.Use(0).Box(new Vector3(-0.04f, 0.015f, -0.024f), new Vector3(width + 0.04f, GateController.BarHeight, 0.024f));
            bar.Use(1).Box(new Vector3(-0.035f, 0f, -0.012f), new Vector3(width + 0.035f, 0.015f, 0.012f));
            bar.Use(2);
            foreach (var face in new[] { -1f, 1f })
            {
                var z0 = face * 0.024f;
                var z1 = face * 0.05f;
                bar.Box(new Vector3(width / 2f - 0.1f, 0.03f, z0), new Vector3(width / 2f - 0.085f, 0.05f, z1));
                bar.Box(new Vector3(width / 2f + 0.085f, 0.03f, z0), new Vector3(width / 2f + 0.1f, 0.05f, z1));
                bar.Box(new Vector3(width / 2f - 0.1f, 0.033f, z1 - face * 0.012f), new Vector3(width / 2f + 0.1f, 0.047f, z1));
            }
            bar.Cylinder(new Vector3(width / 2f + 0.3f, 0.045f, -0.024f), new Vector3(width / 2f + 0.3f, 0.045f, -0.032f), 0.012f, 14);
            var barGo = Part(t, "Galpao_Portao_Soleira", bar.ToMesh("MscDuo_portao_soleira"), new[] { m.Aluminum, m.Rubber, m.Dark });
            gate.Bar = barGo.transform;

            var shaft = new GateShape();
            shaft.Use(0).Cylinder(new Vector3(-0.127f, 0f, 0f), new Vector3(width + 0.127f, 0f, 0f), 0.05f, 20);
            foreach (var edge in new[] { -0.055f, width + 0.055f })
            {
                shaft.Use(0).Cylinder(new Vector3(edge - 0.005f, 0f, 0f), new Vector3(edge + 0.005f, 0f, 0f), 0.12f, 28);
                var face = edge < 0f ? edge - 0.005f : edge + 0.005f;
                var outward = edge < 0f ? -1f : 1f;
                shaft.Use(1);
                for (int k = 0; k < 4; k++)
                {
                    var a = k * Mathf.PI / 2f;
                    var p = new Vector3(face, Mathf.Cos(a) * 0.085f, Mathf.Sin(a) * 0.085f);
                    shaft.Box(p + new Vector3(0f, -0.008f, -0.008f), p + new Vector3(outward * 0.01f, 0.008f, 0.008f));
                }
                shaft.Box(new Vector3(face, -0.004f, 0.02f), new Vector3(face + outward * 0.004f, 0.004f, 0.11f));
            }
            var shaftGo = Part(t, "Galpao_Portao_Eixo", shaft.ToMesh("MscDuo_portao_eixo"), new[] { m.Steel, m.Gear });
            shaftGo.transform.localPosition = new Vector3(0f, dy, dz);
            gate.Shaft = shaftGo.transform;

            var body = new GameObject("Galpao_Portao_Folha");
            body.transform.parent = t;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            gate.Curtain = body.AddComponent<BoxCollider>();
            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            gate.MotorLoop = Source(t, new Vector3(X(0.4f), dy, dz), Clip("motor"), true, 1.5f, 45f);

            Panel(t, gate, input, m, new Vector3(px, 1.4f, 0.2f), Quaternion.identity);
            Panel(t, gate, input, m, new Vector3(px, 1.4f, 0f), Quaternion.Euler(0f, 180f, 0f));
            return gate;
        }

        static Mesh SlatMesh(float x0, float x1)
        {
            const int count = 12;
            var front = new Vector2[count];
            var back = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var y = GateController.SlatHeight * i / (count - 1);
                var u = y / GateController.SlatHeight;
                var z = -0.009f * Mathf.Sin(Mathf.PI * u);
                if (u > 0.82f) z += 0.004f * Mathf.Sin(Mathf.PI * (u - 0.82f) / 0.18f);
                front[i] = new Vector2(z, y);
                back[i] = new Vector2(z + 0.0012f, y);
            }
            var shape = new GateShape();
            shape.Profile(front, back, x0, x1);
            return shape.ToMesh("MscDuo_portao_lamina");
        }

        static void Panel(Transform gateTransform, GateController gate, GateInput input, GateMaterials m, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Galpao_Portao_Botoeira");
            go.transform.parent = gateTransform;
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            var box = new GateShape();
            box.Use(0).Box(new Vector3(-0.05f, -0.095f, 0f), new Vector3(0.05f, 0.095f, 0.055f));
            box.Use(1);
            box.Cylinder(new Vector3(0f, -0.095f, 0.03f), new Vector3(0f, -0.11f, 0.03f), 0.012f, 12);
            foreach (var y in new[] { 0.05f, 0f, -0.05f }) box.Cylinder(new Vector3(0f, y, 0.055f), new Vector3(0f, y, 0.06f), 0.019f, 20);
            Part(go.transform, "Caixa", box.ToMesh("MscDuo_botoeira"), new[] { m.Panel, m.Dark });
            Button(go.transform, gate, input, 0.05f, 0.0135f, m.Green, m.White, 1, "Abrir portao");
            Button(go.transform, gate, input, 0f, 0.016f, m.Red, null, 0, "Parar portao");
            Button(go.transform, gate, input, -0.05f, 0.0135f, m.Black, m.White, -1, "Fechar portao");
        }

        static void Button(Transform panel, GateController gate, GateInput input, float y, float radius, Material color, Material mark, int command, string label)
        {
            var shape = new GateShape();
            shape.Use(0).Cylinder(Vector3.zero, new Vector3(0f, 0f, 0.012f), radius, 24);
            if (mark != null)
            {
                shape.Use(1);
                var s = 0.0065f * command;
                shape.Triangle(new Vector3(0f, s, 0.0122f), new Vector3(-0.0065f, -s * 0.8f, 0.0122f), new Vector3(0.0065f, -s * 0.8f, 0.0122f), Vector3.forward);
            }
            var cap = Part(panel, "Botao", shape.ToMesh("MscDuo_botao"), mark != null ? new[] { color, mark } : new[] { color });
            cap.transform.localPosition = new Vector3(0f, y, 0.06f);
            var collider = cap.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0.006f);
            collider.size = new Vector3(0.04f, 0.044f, 0.03f);
            input.Buttons.Add(new GateButton { Collider = collider, Cap = cap.transform, Rest = cap.transform.localPosition, Gate = gate, Command = command, Label = label });
        }

        static void Acoustics(Transform root)
        {
            var go = new GameObject("Galpao_Acustica");
            go.transform.parent = root;
            go.transform.position = Warehouse.Local(12f, 8f, 3f);
            var zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.User;
            zone.minDistance = 8f;
            zone.maxDistance = 12f;
            zone.room = -1000;
            zone.roomHF = -500;
            zone.decayTime = 1.5f;
            zone.decayHFRatio = 0.6f;
            zone.reflections = -600;
            zone.reflectionsDelay = 0.012f;
            zone.reverb = -400;
            zone.reverbDelay = 0.025f;
            zone.diffusion = 100f;
            zone.density = 100f;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static AudioSource Source(Transform parent, Vector3 position, AudioClip clip, bool loop, float min, float max)
        {
            var go = new GameObject("Som");
            go.transform.parent = parent;
            go.transform.localPosition = position;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
            return s;
        }

        static AudioClip Clip(string name)
        {
            AudioClip clip;
            return clips.TryGetValue(name, out clip) ? clip : null;
        }

        static void LoadClips()
        {
            foreach (var clip in clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
            clips.Clear();
            foreach (var name in new[] { "motor" })
            {
                var bytes = Warehouse.Bytes(Path.Combine("sons_portao", name + ".wav"), SoundPrefix + name + ".wav");
                var clip = bytes != null ? Wav(name, bytes) : null;
                if (clip != null) clips[name] = clip;
                else ModConsole.Warning($"[MSC Duo] som do portão '{name}' não encontrado.");
            }
        }

        internal static AudioClip Wav(string name, byte[] b)
        {
            int channels = 1, rate = 32000, bits = 16;
            var pos = 12;
            while (pos + 8 <= b.Length)
            {
                var id = Encoding.ASCII.GetString(b, pos, 4);
                var size = BitConverter.ToInt32(b, pos + 4);
                pos += 8;
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, pos + 2);
                    rate = BitConverter.ToInt32(b, pos + 4);
                    bits = BitConverter.ToInt16(b, pos + 14);
                }
                else if (id == "data")
                {
                    if (bits != 16) return null;
                    var count = Mathf.Min(size, b.Length - pos) / 2;
                    var data = new float[count];
                    for (int i = 0; i < count; i++) data[i] = BitConverter.ToInt16(b, pos + 2 * i) / 32768f;
                    var clip = AudioClip.Create("MscDuo_portao_" + name, count / channels, channels, rate, false);
                    clip.SetData(data, 0);
                    return clip;
                }
                pos += size + (size & 1);
            }
            return null;
        }
    }

    internal class GateMaterials
    {
        internal readonly Material[] Galvanized;
        internal readonly Material Steel, Gear, Motor, Dark, Conduit, Aluminum, Rubber, Panel, Green, Red, Black, White;

        internal GateMaterials(Material template)
        {
            Galvanized = new[]
            {
                Lit(template, "lamina1", new Color(0.66f, 0.68f, 0.70f), 0.55f),
                Lit(template, "lamina2", new Color(0.63f, 0.65f, 0.67f), 0.5f),
                Lit(template, "lamina3", new Color(0.69f, 0.70f, 0.71f), 0.6f),
            };
            Steel = Lit(template, "aco", new Color(0.30f, 0.31f, 0.33f), 0.35f);
            Gear = Lit(template, "redutor", new Color(0.52f, 0.54f, 0.56f), 0.6f);
            Motor = Lit(template, "motor", new Color(0.16f, 0.22f, 0.32f), 0.45f);
            Dark = Lit(template, "preto", new Color(0.09f, 0.09f, 0.1f), 0.3f);
            Conduit = Lit(template, "eletroduto", new Color(0.62f, 0.62f, 0.6f), 0.2f);
            Aluminum = Lit(template, "aluminio", new Color(0.78f, 0.79f, 0.8f), 0.7f);
            Rubber = Lit(template, "borracha", new Color(0.05f, 0.05f, 0.05f), 0.05f);
            Panel = Lit(template, "botoeira", new Color(0.82f, 0.82f, 0.79f), 0.3f);
            Green = Lit(template, "verde", new Color(0.08f, 0.58f, 0.16f), 0.5f);
            Red = Lit(template, "vermelho", new Color(0.82f, 0.07f, 0.05f), 0.5f);
            Black = Lit(template, "botao_preto", new Color(0.07f, 0.07f, 0.07f), 0.5f);
            White = Lit(template, "branco", new Color(0.95f, 0.95f, 0.95f), 0.2f);
        }

        static Material Lit(Material template, string name, Color color, float gloss)
        {
            var specular = Shader.Find("Specular") ?? Shader.Find("Legacy Shaders/Specular");
            var m = specular != null ? new Material(specular) : new Material(template);
            m.name = "MscDuo_portao_" + name;
            m.color = color;
            m.mainTexture = null;
            if (m.HasProperty("_Shininess")) m.SetFloat("_Shininess", Mathf.Lerp(0.6f, 0.08f, gloss));
            if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", new Color(gloss * 0.5f, gloss * 0.5f, gloss * 0.5f));
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }
    }

    internal class GateButton
    {
        internal Collider Collider;
        internal Transform Cap;
        internal Vector3 Rest;
        internal GateController Gate;
        internal int Command;
        internal string Label;
        internal Action Click;
        internal Func<string> Text;
        internal float PressedAt = -10f;

        internal string Current => Text != null ? Text() : Label;

        internal void Press()
        {
            PressedAt = Time.time;
            if (Click != null) Click();
            else if (Gate != null) Gate.Command(Command);
        }

        internal void Animate(float now)
        {
            if (Cap == null || Click != null) return;
            var t = now - PressedAt;
            if (t > 0.4f) return;
            var depth = t < 0.05f ? t / 0.05f : t < 0.14f ? 1f : t < 0.24f ? 1f - (t - 0.14f) / 0.1f : 0f;
            Cap.localPosition = Rest - Vector3.forward * (0.005f * depth);
        }
    }

    internal class GateInput : MonoBehaviour
    {
        const float Reach = 1.3f;
        internal readonly List<GateButton> Buttons = new List<GateButton>();
        GateButton hovered;
        FsmBool use;
        FsmString text;

        void Update()
        {
            GateButton found = null;
            var cam = Camera.main;
            if (cam != null)
            {
                Collider nearest = null;
                var best = float.MaxValue;
                foreach (var hit in Physics.RaycastAll(cam.transform.position, cam.transform.forward, Reach))
                {
                    if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.root.name == "PLAYER") continue;
                    if (hit.distance < best) { best = hit.distance; nearest = hit.collider; }
                }
                if (nearest != null)
                    foreach (var b in Buttons)
                        if (b.Collider == nearest) { found = b; break; }
            }
            Hover(found);
            if (found != null && Input.GetMouseButtonDown(0)) found.Press();
            var now = Time.time;
            foreach (var b in Buttons) b.Animate(now);
        }

        void Hover(GateButton found)
        {
            if (use == null && PlayMakerGlobals.Instance != null)
            {
                use = PlayMakerGlobals.Instance.Variables.FindFsmBool("GUIuse");
                text = PlayMakerGlobals.Instance.Variables.FindFsmString("GUIinteraction");
            }
            if (found != null)
            {
                if (use != null) use.Value = true;
                if (text != null) text.Value = found.Current;
                hovered = found;
            }
            else if (hovered != null)
            {
                if (use != null) use.Value = false;
                if (text != null) text.Value = "";
                hovered = null;
            }
        }

        void OnDestroy()
        {
            if (hovered != null) Hover(null);
        }
    }
}
