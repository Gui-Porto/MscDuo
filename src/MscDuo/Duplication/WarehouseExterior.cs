using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using MscDuo.Sync;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal enum ExteriorMode : byte { Off = 0, Auto = 1, On = 2 }

    internal static class WarehouseExterior
    {
        const string SunPath = "MAP/SUN/Pivot/SUN";
        static readonly Vector3[] Knob = { new Vector3(-45f, 0f, 0f), Vector3.zero, new Vector3(45f, 0f, 0f) };

        internal static ExteriorMode Mode { get; private set; } = ExteriorMode.Auto;
        static Action sender;
        static ExteriorLights lights;

        internal static void Bind(Action send) => sender = send;

        internal static void Write(BinaryWriter w) => w.Write((byte)Mode);

        internal static void Read(BinaryReader r) => Set((ExteriorMode)r.ReadByte(), false);

        static void Set(ExteriorMode mode, bool notify)
        {
            Mode = mode;
            if (lights != null) lights.Refresh();
            if (notify && sender != null) sender();
        }

        static void Cycle() => Set(Mode == ExteriorMode.Auto ? ExteriorMode.On : Mode == ExteriorMode.On ? ExteriorMode.Off : ExteriorMode.Auto, true);

        static string Label() => Mode == ExteriorMode.Auto ? "Luz externa: AUTOMATICA" : Mode == ExteriorMode.On ? "Luz externa: LIGADA" : "Luz externa: DESLIGADA";

        internal static void Build(Transform root, Material template, Dictionary<string, float> saved)
        {
            float value;
            Mode = saved.TryGetValue("luz_externa", out value) ? (ExteriorMode)Mathf.Clamp((int)value, 0, 2) : ExteriorMode.Auto;
            var frame = new GameObject("Galpao_LuzExterna");
            frame.transform.parent = root;
            frame.transform.position = Warehouse.Local(0f, 0f, 0f);
            frame.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var t = frame.transform;

            var glowOn = Unlit("luz_externa_acesa", new Color(1f, 0.95f, 0.85f));
            var glowOff = Lit(template, "luz_externa_apagada", new Color(0.72f, 0.72f, 0.7f));
            var aluminum = Lit(template, "luz_externa_aluminio", new Color(0.62f, 0.63f, 0.65f));
            var dark = Lit(template, "luz_externa_escuro", new Color(0.12f, 0.12f, 0.13f));
            var plastic = Lit(template, "luz_externa_seletor", new Color(0.82f, 0.82f, 0.79f));
            var white = Lit(template, "luz_externa_marca", new Color(0.95f, 0.95f, 0.95f));


            var lamp = new GateShape();
            lamp.Use(0).Box(new Vector3(-0.09f, 2.37f, 12.86f), new Vector3(0f, 2.53f, 13.14f));
            lamp.Use(1).Box(new Vector3(-0.1f, 2.39f, 12.88f), new Vector3(-0.09f, 2.51f, 13.12f));
            lamp.Use(0);
            for (int i = 0; i < 3; i++)
            {
                var z = 12.92f + i * 0.08f;
                lamp.Box(new Vector3(-0.106f, 2.385f, z - 0.006f), new Vector3(-0.099f, 2.515f, z + 0.006f));
            }
            var lampGo = Part(t, "Arandela", lamp.ToMesh("MscDuo_arandela"), new[] { dark, glowOff });

            var group = new GameObject("Luzes");
            group.transform.parent = t;
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            var floods = new List<Renderer>();
            foreach (var side in new[] { new Vector2(0.7f, 7f), new Vector2(23.3f, 17f) })
                floods.Add(Flood(t, group.transform, new Vector3(side.x, 5.55f, -0.2f), new Vector3(side.y, 0f, -8f), dark, aluminum, glowOff));
            var door = new GameObject("Arandela_Luz");
            door.transform.parent = group.transform;
            door.transform.position = t.TransformPoint(new Vector3(-0.35f, 2.4f, 13f));
            var point = door.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = new Color(1f, 0.85f, 0.62f);
            point.range = 6f;
            point.intensity = 1.2f;
            point.shadows = LightShadows.None;

            var box = new GateShape();
            box.Use(0).Box(new Vector3(0.2f, 1.22f, 13.69f), new Vector3(0.25f, 1.38f, 13.81f));
            box.Use(1);
            foreach (var angle in new[] { -45f, 0f, 45f })
            {
                var dir = Quaternion.Euler(angle, 0f, 0f) * Vector3.up;
                var c = new Vector3(0.2505f, 1.3f, 13.75f) + new Vector3(0f, dir.y, dir.z) * 0.036f;
                box.Box(c - new Vector3(0f, 0.003f, 0.003f), c + new Vector3(0.001f, 0.003f, 0.003f));
            }
            Part(t, "Seletor", box.ToMesh("MscDuo_seletor"), new[] { plastic, white });
            var knob = new GateShape();
            knob.Use(0).Cylinder(Vector3.zero, new Vector3(0.02f, 0f, 0f), 0.019f, 24);
            knob.Use(0).Box(new Vector3(0.02f, -0.017f, -0.004f), new Vector3(0.03f, 0.017f, 0.004f));
            knob.Use(1).Box(new Vector3(0.03f, 0f, -0.0015f), new Vector3(0.0312f, 0.016f, 0.0015f));
            var knobGo = Part(t, "Botao_Seletor", knob.ToMesh("MscDuo_seletor_botao"), new[] { dark, white });
            knobGo.transform.localPosition = new Vector3(0.25f, 1.3f, 13.75f);
            var collider = knobGo.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.015f, 0f, 0f);
            collider.size = new Vector3(0.04f, 0.06f, 0.06f);

            lights = frame.AddComponent<ExteriorLights>();
            lights.Group = group;
            floods.Add(lampGo.GetComponent<Renderer>());
            lights.Renderers = floods.ToArray();
            lights.On = glowOn;
            lights.Off = glowOff;
            lights.KnobTransform = knobGo.transform;
            lights.Threshold = WarehouseConfig.Get("luz_externa_sol");
            lights.Refresh();

            var input = root.GetComponent<GateInput>() ?? root.gameObject.AddComponent<GateInput>();
            input.Buttons.Add(new GateButton { Collider = collider, Cap = knobGo.transform, Rest = knobGo.transform.localPosition, Click = Cycle, Text = Label });
        }

        static Renderer Flood(Transform frame, Transform group, Vector3 local, Vector3 aim, Material body, Material metal, Material lens)
        {
            var arm = new GateShape();
            arm.Box(new Vector3(local.x - 0.025f, local.y - 0.02f, local.z), new Vector3(local.x + 0.025f, local.y + 0.02f, 0f));
            arm.Box(new Vector3(local.x - 0.06f, local.y - 0.08f, -0.008f), new Vector3(local.x + 0.06f, local.y + 0.08f, 0f));
            Part(frame, "Refletor_Suporte", arm.ToMesh("MscDuo_refletor_suporte"), new[] { metal });

            var position = frame.TransformPoint(local);
            var rotation = Quaternion.LookRotation(frame.TransformPoint(aim) - position, Vector3.up);
            var shape = new GateShape();
            shape.Use(0).Box(new Vector3(-0.17f, -0.12f, -0.06f), new Vector3(0.17f, 0.12f, 0.02f));
            for (int i = 0; i < 7; i++)
            {
                var x = -0.15f + i * 0.05f;
                shape.Box(new Vector3(x - 0.006f, -0.11f, -0.1f), new Vector3(x + 0.006f, 0.11f, -0.06f));
            }
            shape.Box(new Vector3(-0.17f, 0.12f, -0.02f), new Vector3(0.17f, 0.135f, 0.05f));
            shape.Use(1).Box(new Vector3(-0.15f, -0.1f, 0.02f), new Vector3(0.15f, 0.1f, 0.026f));
            var go = new GameObject("Refletor");
            go.transform.parent = frame;
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_refletor");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { body, lens };

            var lightGo = new GameObject("Refletor_Luz");
            lightGo.transform.parent = group;
            lightGo.transform.position = position + rotation * new Vector3(0f, 0f, 0.05f);
            lightGo.transform.rotation = rotation;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.spotAngle = 95f;
            light.range = 20f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;
            return renderer;
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

        static Material Lit(Material template, string name, Color color)
        {
            var m = new Material(template) { name = "MscDuo_" + name, color = color, mainTexture = null };
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Unlit/Texture");
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "MscDuo_" + name };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return shader != null ? new Material(shader) { name = "MscDuo_" + name, mainTexture = texture } : new Material(Shader.Find("Diffuse")) { color = color };
        }

        class ExteriorLights : MonoBehaviour
        {
            internal GameObject Group;
            internal Renderer[] Renderers;
            internal Material On;
            internal Material Off;
            internal Transform KnobTransform;
            internal float Threshold = 0.1f;
            Light sun;
            bool dark;
            bool shown;
            float nextCheck;

            void Start() => Refresh();

            internal void Refresh()
            {
                if (KnobTransform != null) KnobTransform.localRotation = Quaternion.Euler(Knob[(int)Mode]);
                Apply(Mode == ExteriorMode.On || Mode == ExteriorMode.Auto && dark, true);
            }

            void Update()
            {
                if (Time.time < nextCheck) return;
                nextCheck = Time.time + 2f;
                dark = Dark();
                Apply(Mode == ExteriorMode.On || Mode == ExteriorMode.Auto && dark, false);
            }

            bool Dark()
            {
                if (sun == null)
                {
                    var go = GameObject.Find(SunPath);
                    if (go != null) sun = go.GetComponent<Light>();
                }
                if (sun != null)
                {
                    var height = Vector3.Dot(-sun.transform.forward, Vector3.up);
                    return dark ? height < Threshold + 0.04f : height < Threshold;
                }
                var hour = TimeSync.ClockTime;
                return hour.HasValue && (hour.Value >= 22 || hour.Value < 4);
            }

            void Apply(bool on, bool force)
            {
                if (on == shown && !force) return;
                shown = on;
                if (Group != null) Group.SetActive(on);
                foreach (var r in Renderers)
                {
                    if (r == null) continue;
                    var materials = r.sharedMaterials;
                    materials[1] = on ? On : Off;
                    r.sharedMaterials = materials;
                }
            }
        }
    }
}
