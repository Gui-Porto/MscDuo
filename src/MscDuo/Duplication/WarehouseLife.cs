using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class WarehouseLife
    {
        const string SoundPrefix = "MscDuo.galpao_som.";
        const float Mezzanine = 3.205f;
        static readonly float[][] Doors = { new[] { 18.9f, 19.8f }, new[] { 21.9f, 22.8f } };
        const float DoorWall = 12.06f;
        const float DoorHeight = 2.12f;

        static Material template;
        static Transform frame;
        static Transform root;
        static GameObject lights;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        internal static void Build(Transform warehouse, Material baseMaterial, GameObject lightGroup, Dictionary<string, Vector3[]> screens)
        {
            template = baseMaterial;
            lights = lightGroup;
            root = warehouse;
            var go = new GameObject("Galpao_Vida");
            go.transform.parent = warehouse;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;
            LoadClips();
            var input = warehouse.GetComponent<GateInput>() ?? warehouse.gameObject.AddComponent<GateInput>();

            Fans();
            Vector3[] quad;
            if (screens.TryGetValue("tv", out quad)) Tv(quad, input);
            if (screens.TryGetValue("fliperama", out quad)) Arcade(quad, ArcadeKind.Elben, "fliperama", new Color(1f, 0.6f, 0.3f));
            if (screens.TryGetValue("fliperama2", out quad)) Arcade(quad, ArcadeKind.Defender, "fliperama2", new Color(0.6f, 0.4f, 1f));
            if (screens.TryGetValue("fliperama3", out quad)) Arcade(quad, ArcadeKind.Invaders, "fliperama3", new Color(0.4f, 1f, 0.5f));
            for (int i = 0; i < Doors.Length; i++) Door(i, input);
            Chair(input);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> vida no galpão: ventiladores, {screens.Count} telas, {Doors.Length} portas, {clips.Count} sons.");
        }

        static Vector3 L(float x, float y, float z) => new Vector3(x, z, y);

        static Material Paint(string name, Color color) => new Material(template) { name = "MscDuo_vida_" + name, color = color, mainTexture = null };

        static Material Screen(string name, Texture texture)
        {
            var shader = Shader.Find("Unlit/Texture");
            return shader != null ? new Material(shader) { name = "MscDuo_tela_" + name, mainTexture = texture } : new Material(template) { mainTexture = texture };
        }

        static void LoadClips()
        {
            clips.Clear();
            foreach (var name in new[] { "fliperama", "fliperama2", "fliperama3", "tv_estatica", "tv_tom", "tv_programa" })
            {
                var bytes = Warehouse.Bytes(Path.Combine("sons_galpao", name + ".wav"), SoundPrefix + name + ".wav");
                var clip = bytes != null ? WarehouseGates.Wav(name, bytes) : null;
                if (clip != null) clips[name] = clip;
            }
        }

        static AudioClip Clip(string name)
        {
            AudioClip clip;
            return clips.TryGetValue(name, out clip) ? clip : null;
        }

        static AudioSource Source(GameObject host, float volume, float min, float max)
        {
            var s = host.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
            s.volume = volume;
            return s;
        }

        static GameObject Part(Transform parent, string name, GateShape shape, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_" + name);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static float Roof(float x) => 6f + 2f * (1f - Mathf.Abs(x - 12f) / 12f) - 0.22f;

        static void Fans()
        {
            var metal = Paint("ventilador", new Color(0.2f, 0.21f, 0.23f));
            var blade = Paint("pa", new Color(0.62f, 0.63f, 0.65f));
            foreach (var x in new[] { 3f, 9f, 15f, 21f })
            {
                var y = 4.5f;
                var hub = Roof(x) - 0.9f;
                var mount = new GateShape();
                mount.Cylinder(L(x, y, hub + 0.12f), L(x, y, Roof(x)), 0.022f, 10);
                mount.Cylinder(L(x, y, hub), L(x, y, hub + 0.16f), 0.13f, 20);
                Part(frame, "Ventilador", mount, metal);
                var rotor = new GameObject("Ventilador_Pas");
                rotor.transform.parent = frame;
                rotor.transform.localPosition = L(x, y, hub + 0.02f);
                rotor.transform.localRotation = Quaternion.identity;
                var blades = new GateShape();
                for (int k = 0; k < 3; k++)
                {
                    var rot = Quaternion.Euler(0f, k * 120f, 0f);
                    var tilt = Quaternion.AngleAxis(12f, rot * Vector3.right);
                    var a = rot * new Vector3(0.1f, 0f, -0.07f);
                    var b = rot * new Vector3(0.95f, 0f, -0.09f);
                    var c = rot * new Vector3(0.95f, 0f, 0.09f);
                    var d = rot * new Vector3(0.1f, 0f, 0.07f);
                    a = tilt * a; b = tilt * b; c = tilt * c; d = tilt * d;
                    blades.Quad(a, b, c, d, Vector3.down);
                    blades.Quad(a, d, c, b, Vector3.up);
                }
                var mesh = new GameObject("Pas");
                mesh.transform.parent = rotor.transform;
                mesh.transform.localPosition = Vector3.zero;
                mesh.transform.localRotation = Quaternion.identity;
                mesh.AddComponent<MeshFilter>().sharedMesh = blades.ToMesh("MscDuo_pas");
                mesh.AddComponent<MeshRenderer>().sharedMaterial = blade;
                rotor.AddComponent<FanSpin>().Phase = x * 37f;
            }
        }

        class FanSpin : MonoBehaviour
        {
            internal float Phase;
            float speed;
            float angle;

            void Update()
            {
                var target = lights != null && lights.activeSelf ? 300f : 0f;
                speed = Mathf.MoveTowards(speed, target, Time.deltaTime * (target > speed ? 70f : 45f));
                angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
                transform.localRotation = Quaternion.Euler(0f, angle + Phase, 0f);
            }
        }

        static GameObject Quad(string name, Vector3[] q, Material material, out Vector3 normal)
        {
            var go = new GameObject(name);
            go.transform.parent = root;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var mesh = new Mesh { name = "MscDuo_" + name };
            mesh.vertices = q;
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            normal = Vector3.Cross(q[3] - q[0], q[2] - q[0]).normalized;
            return go;
        }

        static void Tv(Vector3[] q, GateInput input)
        {
            var canvas = new PixelCanvas(160, 90);
            var on = Screen("tv", canvas.Texture);
            var off = Paint("tv_desligada", new Color(0.03f, 0.03f, 0.035f));
            Vector3 normal;
            var go = Quad("Tela_TV", q, off, out normal);
            var center = (q[0] + q[2]) / 2f;
            var box = go.AddComponent<BoxCollider>();
            box.center = center + normal * 0.006f;
            var right = q[1] - q[0];
            var up = q[3] - q[0];
            box.size = new Vector3(Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(normal.x) * 0.02f, Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(normal.y) * 0.02f, Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(normal.z) * 0.02f);
            var tv = go.AddComponent<TvScreen>();
            tv.Canvas = canvas;
            tv.Renderer = go.GetComponent<Renderer>();
            tv.On = on;
            tv.Off = off;
            tv.Audio = Source(go, 0.35f, 1f, 9f);
            var lightGo = new GameObject("TV_Luz");
            lightGo.transform.parent = go.transform;
            lightGo.transform.position = center + normal * 0.5f;
            tv.Glow = lightGo.AddComponent<Light>();
            tv.Glow.type = LightType.Point;
            tv.Glow.range = 3.5f;
            tv.Glow.intensity = 0.7f;
            tv.Glow.shadows = LightShadows.None;
            tv.Glow.enabled = false;
            WarehouseToggles.Register("tv", 0, tv.SetChannel);
            input.Buttons.Add(new GateButton { Collider = box, Cap = go.transform, Click = () => WarehouseToggles.Set("tv", (WarehouseToggles.Get("tv") + 1) % 4), Text = () => WarehouseToggles.Get("tv") == 0 ? "Ligar TV" : WarehouseToggles.Get("tv") == 3 ? "Desligar TV" : "Trocar canal" });
        }

        class TvScreen : MonoBehaviour
        {
            internal PixelCanvas Canvas;
            internal Renderer Renderer;
            internal Material On;
            internal Material Off;
            internal AudioSource Audio;
            internal Light Glow;
            int channel;
            float next;
            float started;
            readonly System.Random rng = new System.Random(7);

            internal void SetChannel(int value)
            {
                channel = value;
                started = Time.time;
                Renderer.sharedMaterial = channel == 0 ? Off : On;
                Glow.enabled = channel != 0;
                var clip = channel == 1 ? Clip("tv_programa") : channel == 2 ? Clip("tv_tom") : channel == 3 ? Clip("tv_estatica") : null;
                if (clip == null) Audio.Stop();
                else
                {
                    Audio.clip = clip;
                    Audio.Play();
                }
                next = 0f;
            }

            void Update()
            {
                if (channel == 0 || Time.time < next) return;
                next = Time.time + 1f / 15f;
                var t = Time.time - started;
                if (channel == 1) Program(t);
                else if (channel == 2) Bars();
                else Canvas.Noise(rng);
                Canvas.Apply();
                Glow.color = Color.Lerp(Canvas.Average(), Color.white, 0.25f);
            }

            void Program(float t)
            {
                var c = Canvas;
                c.Clear(new Color32(12, 16, 48, 255));
                for (int i = 0; i < 40; i++) c.Set((i * 37 + (int)(t * 6)) % c.Width, (i * 23) % 70, new Color32(70, 80, 140, 255));
                var w = c.TextWidth("MSC DUO", 3);
                var x = (int)Mathf.PingPong(t * 34f, c.Width - w);
                var y = (int)Mathf.PingPong(t * 21f, 62 - 15);
                var h = Mathf.Repeat(t * 0.12f, 1f) * 6f;
                var hue = new Color(Mathf.Clamp01(Mathf.Abs(h - 3f) - 1f), Mathf.Clamp01(2f - Mathf.Abs(h - 2f)), Mathf.Clamp01(2f - Mathf.Abs(h - 4f)));
                hue = Color.Lerp(hue, Color.white, 0.3f);
                c.Text(x, y + 4, "MSC DUO", hue, 3);
                c.Rect(0, 72, c.Width, 18, new Color32(160, 20, 20, 255));
                c.Text(3, 74, "AO VIVO", new Color32(255, 255, 255, 255));
                c.Rect(0, 81, c.Width, 9, new Color32(10, 10, 10, 255));
                const string news = "  GALPAO DUO - CERVEJA GELADA NA GELADEIRA - DINAMOMETRO LIVRE - FLIPERAMA DE GRACA - ";
                var offset = (int)(t * 30f) % (news.Length * 4);
                c.Text(-offset, 83, news + news, new Color32(255, 220, 90, 255));
            }

            void Bars()
            {
                var c = Canvas;
                var colors = new[] { new Color32(200, 200, 200, 255), new Color32(200, 200, 20, 255), new Color32(20, 200, 200, 255), new Color32(20, 200, 20, 255), new Color32(200, 20, 200, 255), new Color32(200, 20, 20, 255), new Color32(20, 20, 200, 255) };
                var bw = c.Width / colors.Length + 1;
                for (int i = 0; i < colors.Length; i++) c.Rect(i * bw, 0, bw, 62, colors[i]);
                c.Rect(0, 62, c.Width, 28, new Color32(16, 16, 16, 255));
                c.Rect(c.Width / 2 - 34, 66, 68, 18, new Color32(0, 0, 0, 255));
                c.Centered(70, "MSC DUO", new Color32(255, 255, 255, 255), 2);
            }
        }

        enum ArcadeKind { Elben, Defender, Invaders }

        static void Arcade(Vector3[] q, ArcadeKind kind, string sound, Color tint)
        {
            var width = (q[1] - q[0]).magnitude;
            var height = (q[3] - q[0]).magnitude;
            var canvas = new PixelCanvas(Mathf.Clamp(Mathf.RoundToInt(96f * width / Mathf.Max(height, 0.01f)), 64, 200), 96);
            Vector3 normal;
            var go = Quad("Tela_" + sound, q, Screen(sound, canvas.Texture), out normal);
            var screen = go.AddComponent<ArcadeScreen>();
            screen.Canvas = canvas;
            screen.Kind = kind;
            var audio = Source(go, 0.22f, 0.7f, 6f);
            audio.clip = Clip(sound);
            if (audio.clip != null) audio.Play();
            var lightGo = new GameObject("Fliperama_Luz");
            lightGo.transform.parent = go.transform;
            lightGo.transform.position = (q[0] + q[2]) / 2f + normal * 0.35f;
            screen.Glow = lightGo.AddComponent<Light>();
            screen.Glow.type = LightType.Point;
            screen.Glow.range = 1.4f;
            screen.Glow.intensity = 0.6f;
            screen.Glow.color = tint;
            screen.Glow.shadows = LightShadows.None;
        }

        class ArcadeScreen : MonoBehaviour
        {
            internal PixelCanvas Canvas;
            internal ArcadeKind Kind;
            internal Light Glow;
            float next;
            int[] stars;

            static readonly string[] InvaderA = { "..#..#..", "...##...", ".######.", "##.##.##", "########", "#.#..#.#" };
            static readonly string[] InvaderB = { "..#..#..", "#..##..#", "########", "##.##.##", ".######.", ".#....#." };
            static readonly string[] Cannon = { "...#...", ".#####.", "#######" };
            static readonly string[] Ship = { "##......", "#####...", "########", "#####...", "##......" };
            static readonly string[] HeroA = { ".##.", ".##.", "####", ".##.", ".#.#", "#..#" };
            static readonly string[] HeroB = { ".##.", ".##.", "####", ".##.", ".##.", ".##." };
            static readonly string[] Castle = { "#.#.#......#.#.#", "#####......#####", "#####.#.#..#####", "###############.", "################", "######....######", "######....######", "######....######" };

            void Update()
            {
                if (Time.time < next) return;
                next = Time.time + 1f / 12f;
                var cam = Camera.main;
                if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 900f) return;
                var t = Time.time;
                if (Kind == ArcadeKind.Invaders) Invaders(t);
                else if (Kind == ArcadeKind.Defender) Defender(t);
                else Elben(t);
                Canvas.Apply();
            }

            void Invaders(float t)
            {
                var c = Canvas;
                c.Clear(new Color32(0, 0, 0, 255));
                c.Text(2, 2, "SCORE 0420", new Color32(255, 255, 255, 255));
                c.Text(c.Width - c.TextWidth("HI 9990") - 2, 2, "HI 9990", new Color32(255, 255, 255, 255));
                var step = (int)(t / 0.35f);
                var span = c.Width - 88 - 4;
                var cycle = span * 2;
                var pos = step * 2 % Mathf.Max(cycle, 1);
                var ox = 2 + (pos < span ? pos : cycle - pos);
                var oy = 14 + (step * 2 / Mathf.Max(cycle, 1)) % 6 * 4;
                var frame = step % 2 == 0 ? InvaderA : InvaderB;
                var rows = new[] { new Color32(255, 80, 255, 255), new Color32(80, 220, 255, 255), new Color32(80, 220, 255, 255), new Color32(120, 255, 120, 255), new Color32(120, 255, 120, 255) };
                for (int r = 0; r < 5; r++)
                    for (int k = 0; k < 8; k++) c.Sprite(ox + k * 11, oy + r * 9, frame, rows[r]);
                var cx = (int)Mathf.PingPong(t * 22f, c.Width - 8);
                c.Sprite(cx, c.Height - 12, Cannon, new Color32(80, 255, 80, 255));
                var shot = (int)(t * 60f) % 60;
                if (shot < 40) c.Rect(cx + 3, c.Height - 14 - shot * 2, 1, 3, new Color32(255, 255, 255, 255));
                c.Rect(0, c.Height - 4, c.Width, 1, new Color32(80, 255, 80, 255));
                if ((int)(t * 2f) % 2 == 0) c.Centered(c.Height - 30, "INSERT COIN", new Color32(255, 255, 255, 255));
            }

            void Defender(float t)
            {
                var c = Canvas;
                c.Clear(new Color32(0, 0, 8, 255));
                if (stars == null)
                {
                    var rng = new System.Random(3);
                    stars = new int[60];
                    for (int i = 0; i < stars.Length; i++) stars[i] = rng.Next(c.Width * 60);
                }
                for (int i = 0; i < stars.Length; i++)
                {
                    var sx = (stars[i] % c.Width - (int)(t * (8 + i % 3 * 6))) % c.Width;
                    if (sx < 0) sx += c.Width;
                    c.Set(sx, 12 + stars[i] / c.Width, new Color32(200, 200, 255, 255));
                }
                var scroll = t * 40f;
                for (int x = 0; x < c.Width; x++)
                {
                    var h = 10 + (int)(6 * Mathf.Sin((x + scroll) * 0.08f) + 4 * Mathf.Sin((x + scroll) * 0.21f));
                    c.Rect(x, c.Height - h, 1, 1, new Color32(200, 80, 255, 255));
                    c.Rect(x, c.Height - h + 1, 1, h - 1, new Color32(40, 10, 60, 255));
                }
                var sy = 40 + (int)(14 * Mathf.Sin(t * 1.3f));
                c.Sprite(10, sy, Ship, new Color32(230, 230, 255, 255));
                if ((int)(t * 3f) % 3 == 0) c.Rect(19, sy + 2, c.Width - 19, 1, new Color32(255, 60, 60, 255));
                for (int i = 0; i < 4; i++)
                {
                    var ex = (int)(c.Width - Mathf.Repeat(t * 30f + i * 37f, c.Width + 10));
                    var ey = 22 + (i * 17 + (int)(t * 5)) % 45;
                    c.Rect(ex, ey, 3, 3, new Color32(80, 255, 80, 255));
                }
                c.Centered(2, "DEFENDER", new Color32(255, 200, 40, 255));
                if ((int)(t * 2f) % 2 == 0) c.Centered(c.Height - 26, "INSERT COIN", new Color32(255, 255, 255, 255));
            }

            void Elben(float t)
            {
                var c = Canvas;
                for (int y = 0; y < c.Height; y++)
                {
                    var k = y / (float)c.Height;
                    c.Rect(0, y, c.Width, 1, new Color(0.15f + 0.5f * k, 0.15f + 0.25f * k, 0.45f - 0.15f * k));
                }
                c.Sprite(c.Width - 40, c.Height - 30, Castle, new Color32(40, 24, 40, 255), 2);
                var ground = c.Height - 14;
                c.Rect(0, ground, c.Width, 14, new Color32(40, 110, 40, 255));
                var scroll = (int)(t * 20f);
                for (int x = -scroll % 8; x < c.Width; x += 8) c.Rect(x, ground + 4, 4, 2, new Color32(60, 150, 50, 255));
                var hx = (int)Mathf.PingPong(t * 18f, c.Width - 60) + 6;
                c.Sprite(hx, ground - 12, (int)(t * 6f) % 2 == 0 ? HeroA : HeroB, new Color32(255, 210, 120, 255), 2);
                c.Centered(8, "ELBEN", new Color32(255, 220, 60, 255), 3);
                c.Centered(28, "QUEST", new Color32(255, 140, 40, 255), 3);
                if ((int)(t * 2f) % 2 == 0) c.Centered(52, "PRESS START", new Color32(255, 255, 255, 255));
            }
        }

        static void Door(int index, GateInput input)
        {
            var d0 = Doors[index][0];
            var d1 = Doors[index][1];
            const float wall0 = 12f, wall1 = 12.12f, top = 2.15f, trim = 0.07f, lip = 0.015f;
            var white = Paint("batente", new Color(0.92f, 0.91f, 0.88f));
            var frameShape = new GateShape();
            frameShape.Box(L(d0 - trim, wall0 - lip, 0f), L(d0, wall1 + lip, top + trim));
            frameShape.Box(L(d1, wall0 - lip, 0f), L(d1 + trim, wall1 + lip, top + trim));
            frameShape.Box(L(d0 - trim, wall0 - lip, top), L(d1 + trim, wall1 + lip, top + trim));
            frameShape.Box(L(d0, wall0, top - 0.02f), L(d1, wall0 + 0.035f, top));
            frameShape.Box(L(d0, wall0, 0f), L(d0 + 0.02f, wall0 + 0.035f, top));
            frameShape.Box(L(d1 - 0.02f, wall0, 0f), L(d1, wall0 + 0.035f, top));
            Part(frame, "Batente_Sala" + (index + 1), frameShape, white);

            var width = d1 - d0 - 0.006f;
            const float thick = 0.04f;
            var hinge = new GameObject("Porta_Sala" + (index + 1));
            hinge.transform.parent = frame;
            hinge.transform.localPosition = L(d0 + 0.003f, wall0 + 0.035f + thick, 0f);
            hinge.transform.localRotation = Quaternion.identity;
            var shape = new GateShape();
            shape.Use(0).Box(new Vector3(0f, 0.005f, -thick), new Vector3(width, top - 0.022f, 0f));
            shape.Use(1);
            var hx = width - 0.08f;
            foreach (var side in new[] { -1f, 1f })
            {
                var face = side < 0f ? -thick : 0f;
                var outward = side;
                shape.Box(new Vector3(hx - 0.022f, 0.94f, face), new Vector3(hx + 0.022f, 1.1f, face + outward * 0.008f));
                shape.Cylinder(new Vector3(hx, 1.04f, face + outward * 0.008f), new Vector3(hx, 1.04f, face + outward * 0.055f), 0.009f, 12);
                shape.Box(new Vector3(hx - 0.13f, 1.03f, face + outward * 0.045f), new Vector3(hx + 0.008f, 1.05f, face + outward * 0.062f));
            }
            var leaf = Part(hinge.transform, "Folha", shape, Paint("porta", new Color(0.8f, 0.79f, 0.76f)), Paint("macaneta", new Color(0.72f, 0.72f, 0.74f)));
            var box = leaf.AddComponent<BoxCollider>();
            box.center = new Vector3(width / 2f, top / 2f, -thick / 2f);
            box.size = new Vector3(width, top - 0.03f, thick);
            var swing = hinge.AddComponent<Swing>();
            var key = "porta" + (index + 1);
            WarehouseToggles.Register(key, 0, v => swing.Target = v != 0 ? 95f : 0f);
            input.Buttons.Add(new GateButton { Collider = box, Cap = leaf.transform, Click = () => WarehouseToggles.Set(key, WarehouseToggles.Get(key) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(key) == 0 ? "Abrir porta" : "Fechar porta" });
        }

        class Swing : MonoBehaviour
        {
            internal float Target;
            float angle;

            void Update()
            {
                if (Mathf.Approximately(angle, Target)) return;
                angle = Mathf.MoveTowards(angle, Target, Time.deltaTime * 140f);
                transform.localRotation = Quaternion.Euler(0f, -angle, 0f);
            }
        }

        static void Chair(GateInput input)
        {
            var chair = root.Find("Galpao_Mezanino/Escritorio/chair_wooden");
            if (chair == null) return;
            var first = true;
            var bounds = new Bounds();
            foreach (var r in chair.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            if (first) return;
            var holder = new GameObject("Cadeira_Clique");
            holder.transform.parent = chair;
            holder.transform.position = bounds.center;
            holder.transform.rotation = Quaternion.identity;
            var box = holder.AddComponent<BoxCollider>();
            box.size = bounds.size;
            var away = Warehouse.Local(0f, 1f, 0f) - Warehouse.Local(0f, 0f, 0f);
            var slide = chair.gameObject.AddComponent<Slide>();
            slide.Rest = chair.position;
            slide.RestRotation = chair.rotation;
            slide.Away = away.normalized;
            WarehouseToggles.Register("cadeira", 0, v => slide.Target = v);
            input.Buttons.Add(new GateButton { Collider = box, Cap = holder.transform, Click = () => WarehouseToggles.Set("cadeira", WarehouseToggles.Get("cadeira") == 0 ? 1 : 0), Text = () => WarehouseToggles.Get("cadeira") == 0 ? "Puxar cadeira" : "Empurrar cadeira" });
        }

        class Slide : MonoBehaviour
        {
            internal Vector3 Rest;
            internal Quaternion RestRotation;
            internal Vector3 Away;
            internal int Target;
            float amount;

            void Update()
            {
                var goal = (float)Target;
                if (Mathf.Approximately(amount, goal)) return;
                amount = Mathf.MoveTowards(amount, goal, Time.deltaTime * 2.2f);
                var k = Mathf.SmoothStep(0f, 1f, amount);
                transform.position = Rest + Away * (0.4f * k);
                transform.rotation = Quaternion.AngleAxis(25f * k, Vector3.up) * RestRotation;
            }
        }
    }
}
