using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MscDuo.Sync
{
    internal class Avatar
    {
        const float FeetBelowPivot = 0.5f;
        const float NameHeight = 2.1f;
        const float StrideLength = 1.1f;
        const float MaxSwing = 32f;
        const float FullSwingSpeed = 2f;

        class Part
        {
            internal string Name;
            internal Vector3 Pivot;
            internal Mesh Mesh;
            internal Material[] Materials;
        }

        static readonly Dictionary<int, List<Part>> skins = new Dictionary<int, List<Part>>();

        readonly GameObject root;
        readonly TextMesh label;
        readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        GameObject body;
        int skin;
        Vector3 lastFeet;
        bool hasLast;
        float phase;
        float speed;

        internal Avatar(string name)
        {
            root = new GameObject("MscDuo_Avatar_" + name);

            var tag = new GameObject("Name");
            tag.transform.parent = root.transform;
            tag.transform.localPosition = new Vector3(0f, NameHeight, 0f);
            label = tag.AddComponent<TextMesh>();
            label.text = name;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.018f;
            label.fontSize = 48;
            label.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.font = font;
            tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        internal void SetSkin(int value)
        {
            if (value == skin) return;
            var model = Load(value);
            if (model == null) return;
            skin = value;
            if (body != null) Object.Destroy(body);
            parts.Clear();
            body = new GameObject("Jogador" + value);
            body.transform.parent = root.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            foreach (var part in model)
            {
                var go = new GameObject(part.Name);
                go.transform.parent = body.transform;
                go.transform.localPosition = part.Pivot;
                go.transform.localRotation = Quaternion.identity;
                go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = part.Materials;
                parts[part.Name] = go.transform;
            }
        }

        internal void Apply(Vector3 pivot, float yaw, float pitch)
        {
            var feet = pivot - Vector3.up * FeetBelowPivot;
            root.transform.position = feet;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Animate(feet, pitch);

            var camera = Camera.main;
            if (camera != null)
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position);
        }

        void Animate(Vector3 feet, float pitch)
        {
            var dt = Time.deltaTime;
            if (hasLast && dt > 0f)
            {
                var moved = feet - lastFeet;
                moved.y = 0f;
                speed = Mathf.Lerp(speed, moved.magnitude / dt, Mathf.Clamp01(dt * 10f));
            }
            lastFeet = feet;
            hasLast = true;

            // ponytail: balanço senoidal de braços e pernas, troca por animação do Blender se precisar de agachar/sentar
            phase = Mathf.Repeat(phase + speed * dt / StrideLength * Mathf.PI, Mathf.PI * 2f);
            var swing = speed < 0.1f ? 0f : Mathf.Sin(phase) * MaxSwing * Mathf.Clamp01(speed / FullSwingSpeed);

            Rotate("Cabeca", Mathf.Clamp(pitch, -60f, 60f));
            Rotate("Perna_E", swing);
            Rotate("Perna_D", -swing);
            Rotate("Braco_E", -swing * 0.8f);
            Rotate("Braco_D", swing * 0.8f);
        }

        void Rotate(string part, float angle)
        {
            Transform t;
            if (parts.TryGetValue(part, out t)) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        internal void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        static List<Part> Load(int skin)
        {
            List<Part> model;
            if (skins.TryGetValue(skin, out model)) return model;
            var text = Resource("MscDuo.jogador" + skin + ".obj");
            if (text == null) return null;

            model = new List<Part>();
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var colors = new Dictionary<string, Color>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var faces = new List<KeyValuePair<string, List<int[]>>>();
            List<int[]> current = null;
            Part part = null;

            System.Action flush = () =>
            {
                if (part == null) return;
                var v = new List<Vector3>();
                var n = new List<Vector3>();
                var triangles = new List<int[]>();
                foreach (var group in faces)
                {
                    var tris = new int[group.Value.Count];
                    for (int i = 0; i < tris.Length; i++)
                    {
                        tris[i] = v.Count;
                        v.Add(vertices[group.Value[i][0]] - part.Pivot);
                        n.Add(normals[group.Value[i][1]]);
                    }
                    triangles.Add(tris);
                }
                part.Mesh = new Mesh { name = "Jogador" + skin + "_" + part.Name, vertices = v.ToArray(), normals = n.ToArray() };
                part.Mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) part.Mesh.SetTriangles(triangles[i], i);
                part.Mesh.RecalculateBounds();
                part.Materials = new Material[faces.Count];
                for (int i = 0; i < faces.Count; i++)
                {
                    Color color;
                    if (!colors.TryGetValue(faces[i].Key, out color)) color = Color.magenta;
                    var material = new Material(shader) { name = "Jogador" + skin + "_" + faces[i].Key, color = color };
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
                    part.Materials[i] = material;
                }
                model.Add(part);
                faces.Clear();
                current = null;
            };

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var p = line.Split(' ');
                if (line.StartsWith("# cor ")) colors[p[2]] = new Color(F(p[3]), F(p[4]), F(p[5]));
                else if (line.StartsWith("# pivo ")) part.Pivot = new Vector3(F(p[2]), F(p[4]), F(p[3]));
                else if (line.StartsWith("o ")) { flush(); part = new Part { Name = p[1] }; }
                else if (line.StartsWith("v ")) vertices.Add(new Vector3(F(p[1]), F(p[3]), F(p[2])));
                else if (line.StartsWith("vn ")) normals.Add(new Vector3(F(p[1]), F(p[3]), F(p[2])));
                else if (line.StartsWith("usemtl "))
                {
                    current = null;
                    foreach (var group in faces)
                        if (group.Key == p[1]) current = group.Value;
                    if (current == null)
                    {
                        current = new List<int[]>();
                        faces.Add(new KeyValuePair<string, List<int[]>>(p[1], current));
                    }
                }
                else if (line.StartsWith("f ") && current != null)
                {
                    // eixo Y/Z trocado inverte a mão: grava os cantos em ordem a, c, b
                    foreach (var k in new[] { 1, 3, 2 })
                    {
                        var idx = p[k].Split('/');
                        current.Add(new[] { int.Parse(idx[0]) - 1, int.Parse(idx[2]) - 1 });
                    }
                }
            }
            flush();
            if (model.Count == 0) return null;
            skins[skin] = model;
            return model;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static string Resource(string name)
        {
            using (var stream = typeof(Avatar).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                var read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
