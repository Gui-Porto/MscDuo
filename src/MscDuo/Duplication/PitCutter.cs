using System.Collections.Generic;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class PitCutter
    {
        const int MaxDepth = 5;
        const int MaxVertices = 64000;

        readonly List<Vector2> pit;
        readonly float near;
        readonly System.Func<Vector3, bool> covered;
        readonly Matrix4x4 toWorld;
        readonly Dictionary<int, bool> coveredCache = new Dictionary<int, bool>();
        readonly Dictionary<long, int> midpoints = new Dictionary<long, int>();

        readonly List<Vector3> vertices;
        readonly List<Vector2> uv;
        readonly List<Vector2> uv2;
        readonly List<Color> colors;
        readonly List<Vector4> tangents;

        internal int Cut { get; private set; }
        internal int Kept { get; private set; }
        internal bool Changed { get; private set; }

        internal PitCutter(Mesh source, Vector3[] flattened, Matrix4x4 toWorld, List<Vector2> pit, float near, System.Func<Vector3, bool> covered)
        {
            this.toWorld = toWorld;
            this.pit = pit;
            this.near = near;
            this.covered = covered;
            vertices = new List<Vector3>(flattened);
            uv = Read(source.uv);
            uv2 = Read(source.uv2);
            colors = Read(source.colors);
            tangents = Read(source.tangents);
        }

        static List<T> Read<T>(T[] values) => values != null && values.Length > 0 ? new List<T>(values) : null;

        internal int[] Process(int[] triangles)
        {
            if (pit.Count == 0) return triangles;
            var output = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
                Split(triangles[i], triangles[i + 1], triangles[i + 2], 0, output);
            if (output.Count != triangles.Length) Changed = true;
            return output.ToArray();
        }

        void Split(int a, int b, int c, int depth, List<int> output)
        {
            if (!OverPit(World(a), World(b), World(c)))
            {
                Add(output, a, b, c);
                return;
            }
            if (Covered(a) && Covered(b) && Covered(c))
            {
                Cut++;
                Changed = true;
                return;
            }
            if (depth >= MaxDepth || vertices.Count + 3 > MaxVertices)
            {
                Kept++;
                Add(output, a, b, c);
                return;
            }
            var ab = Midpoint(a, b);
            var bc = Midpoint(b, c);
            var ca = Midpoint(c, a);
            Changed = true;
            Split(a, ab, ca, depth + 1, output);
            Split(ab, b, bc, depth + 1, output);
            Split(ca, bc, c, depth + 1, output);
            Split(ab, bc, ca, depth + 1, output);
        }

        static void Add(List<int> output, int a, int b, int c)
        {
            output.Add(a);
            output.Add(b);
            output.Add(c);
        }

        Vector3 World(int i) => toWorld.MultiplyPoint3x4(vertices[i]);

        bool Covered(int i)
        {
            bool value;
            if (!coveredCache.TryGetValue(i, out value))
            {
                value = covered(World(i));
                coveredCache[i] = value;
            }
            return value;
        }

        int Midpoint(int a, int b)
        {
            var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int index;
            if (midpoints.TryGetValue(key, out index)) return index;
            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]) * 0.5f);
            if (uv != null) uv.Add((uv[a] + uv[b]) * 0.5f);
            if (uv2 != null) uv2.Add((uv2[a] + uv2[b]) * 0.5f);
            if (colors != null) colors.Add(Color.Lerp(colors[a], colors[b], 0.5f));
            if (tangents != null) tangents.Add((tangents[a] + tangents[b]) * 0.5f);
            midpoints[key] = index;
            return index;
        }

        bool OverPit(Vector3 a, Vector3 b, Vector3 c)
        {
            var limit = near * near;
            foreach (var p in pit)
            {
                var d1 = Side(p, a, b);
                var d2 = Side(p, b, c);
                var d3 = Side(p, c, a);
                var negative = d1 < 0f || d2 < 0f || d3 < 0f;
                var positive = d1 > 0f || d2 > 0f || d3 > 0f;
                if (!(negative && positive)) return true;
                if (Near(p, a, limit) || Near(p, b, limit) || Near(p, c, limit)) return true;
            }
            return false;
        }

        static bool Near(Vector2 p, Vector3 v, float limit)
        {
            var dx = p.x - v.x;
            var dz = p.y - v.z;
            return dx * dx + dz * dz < limit;
        }

        static float Side(Vector2 p, Vector3 a, Vector3 b) => (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.y - b.z);

        internal Mesh Build(string name, int[][] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices.ToArray();
            if (uv != null) mesh.uv = uv.ToArray();
            if (uv2 != null) mesh.uv2 = uv2.ToArray();
            if (colors != null) mesh.colors = colors.ToArray();
            if (tangents != null) mesh.tangents = tangents.ToArray();
            mesh.subMeshCount = triangles.Length;
            for (int s = 0; s < triangles.Length; s++) mesh.SetTriangles(triangles[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
