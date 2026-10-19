using System.Collections.Generic;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class GateShape
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> submeshes = new List<List<int>> { new List<int>() };
        int current;

        internal GateShape Use(int submesh)
        {
            while (submeshes.Count <= submesh) submeshes.Add(new List<int>());
            current = submesh;
            return this;
        }

        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal) => Quad(a, b, c, d, normal, normal, normal, normal);

        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
        {
            var i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(na); normals.Add(nb); normals.Add(nc); normals.Add(nd);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            var t = submeshes[current];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), na + nb + nc + nd) >= 0f)
            {
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            else
            {
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
        }

        internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
            var t = submeshes[current];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        internal void Box(Vector3 a, Vector3 b)
        {
            var min = Vector3.Min(a, b);
            var max = Vector3.Max(a, b);
            Vector3 p000 = min, p111 = max;
            var p100 = new Vector3(max.x, min.y, min.z);
            var p010 = new Vector3(min.x, max.y, min.z);
            var p001 = new Vector3(min.x, min.y, max.z);
            var p110 = new Vector3(max.x, max.y, min.z);
            var p101 = new Vector3(max.x, min.y, max.z);
            var p011 = new Vector3(min.x, max.y, max.z);
            Quad(p000, p010, p110, p100, Vector3.back);
            Quad(p001, p101, p111, p011, Vector3.forward);
            Quad(p000, p001, p011, p010, Vector3.left);
            Quad(p100, p110, p111, p101, Vector3.right);
            Quad(p000, p100, p101, p001, Vector3.down);
            Quad(p010, p011, p111, p110, Vector3.up);
        }

        internal void Cylinder(Vector3 a, Vector3 b, float radius, int segments, bool caps = true)
        {
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(axis, u);
            for (int k = 0; k < segments; k++)
            {
                var a0 = 2f * Mathf.PI * k / segments;
                var a1 = 2f * Mathf.PI * (k + 1) / segments;
                var d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0);
                var d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                Quad(a + d0 * radius, a + d1 * radius, b + d1 * radius, b + d0 * radius, d0, d1, d1, d0);
                if (!caps) continue;
                Triangle(a, a + d0 * radius, a + d1 * radius, -axis);
                Triangle(b, b + d1 * radius, b + d0 * radius, axis);
            }
        }

        internal void Profile(Vector2[] front, Vector2[] back, float x0, float x1)
        {
            var count = front.Length;
            var frontNormals = ProfileNormals(front, -1f);
            var backNormals = ProfileNormals(back, 1f);
            for (int i = 0; i + 1 < count; i++)
            {
                Quad(P(x0, front[i]), P(x1, front[i]), P(x1, front[i + 1]), P(x0, front[i + 1]), frontNormals[i], frontNormals[i], frontNormals[i + 1], frontNormals[i + 1]);
                Quad(P(x0, back[i]), P(x0, back[i + 1]), P(x1, back[i + 1]), P(x1, back[i]), backNormals[i], backNormals[i + 1], backNormals[i + 1], backNormals[i]);
                Quad(P(x0, front[i]), P(x0, front[i + 1]), P(x0, back[i + 1]), P(x0, back[i]), Vector3.left);
                Quad(P(x1, front[i]), P(x1, back[i]), P(x1, back[i + 1]), P(x1, front[i + 1]), Vector3.right);
            }
            Quad(P(x0, front[0]), P(x0, back[0]), P(x1, back[0]), P(x1, front[0]), Vector3.down);
            Quad(P(x0, front[count - 1]), P(x1, front[count - 1]), P(x1, back[count - 1]), P(x0, back[count - 1]), Vector3.up);
        }

        static Vector3 P(float x, Vector2 zy) => new Vector3(x, zy.y, zy.x);

        static Vector3[] ProfileNormals(Vector2[] points, float side)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var tangent = points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)];
                var n = new Vector2(tangent.y, -tangent.x).normalized * side;
                result[i] = new Vector3(0f, n.y, n.x);
            }
            return result;
        }

        internal Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i].ToArray(), i);
            mesh.RecalculateBounds();
            return mesh;
        }

        internal int Submeshes => submeshes.Count;
    }
}
