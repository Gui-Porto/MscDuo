using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class StaticBatchExtractor : IDisposable
    {
        const float Tolerance = 0.05f;
        const string LevelFileName = "level2";

        readonly Dictionary<string, CombinedMesh> cache = new Dictionary<string, CombinedMesh>();
        LevelFile level;
        bool levelFailed;

        internal int Unmatched { get; private set; }

        public void Dispose()
        {
            if (level != null) level.Dispose();
        }

        internal Mesh Extract(Renderer original, Mesh combined)
        {
            var data = Get(combined.name);
            if (data == null) return null;

            var count = original.sharedMaterials.Length;
            var start = FindStart(data, original.bounds, count);
            if (start < 0)
            {
                Unmatched++;
                return null;
            }
            return Build(data, start, count, original.transform.worldToLocalMatrix, original.name);
        }

        CombinedMesh Get(string name)
        {
            CombinedMesh data;
            if (cache.TryGetValue(name, out data)) return data;

            if (level == null && !levelFailed)
            {
                try
                {
                    level = new LevelFile(Path.Combine(Application.dataPath, LevelFileName));
                }
                catch (Exception e)
                {
                    levelFailed = true;
                    ModConsole.Error($"[MSC Duo] não foi possível ler {LevelFileName}: {e.Message}");
                }
            }
            if (level == null) return null;

            try
            {
                data = level.Read(name);
            }
            catch (Exception e)
            {
                ModConsole.Error($"[MSC Duo] erro ao ler '{name}': {e.Message}");
                data = null;
            }
            cache[name] = data;
            return data;
        }

        static int FindStart(CombinedMesh data, Bounds target, int count)
        {
            var best = -1;
            var bestError = float.MaxValue;
            var limit = Mathf.Max(target.size.magnitude * Tolerance, 0.01f);
            var subs = data.SubMeshes;
            for (int s = 0; s + count <= subs.Length; s++)
            {
                var b = subs[s].Bounds;
                for (int j = 1; j < count; j++) b.Encapsulate(subs[s + j].Bounds);
                var error = (b.min - target.min).magnitude + (b.max - target.max).magnitude;
                if (error < bestError)
                {
                    bestError = error;
                    best = s;
                }
            }
            return bestError <= limit ? best : -1;
        }

        static Mesh Build(CombinedMesh data, int start, int count, Matrix4x4 toLocal, string name) =>
            BuildData(data, start, count, toLocal).ToMesh(name + "_p2");

        internal static MeshData BuildData(CombinedMesh data, int start, int count, Matrix4x4 toLocal)
        {
            var map = new Dictionary<int, int>();
            var order = new List<int>();
            var result = new MeshData { Triangles = new int[count][] };
            for (int j = 0; j < count; j++)
            {
                var sub = data.SubMeshes[start + j];
                var first = (int)(sub.FirstByte / 2);
                var dst = new int[sub.IndexCount];
                for (int k = 0; k < dst.Length; k++)
                {
                    int src = data.Indices[first + k];
                    int idx;
                    if (!map.TryGetValue(src, out idx))
                    {
                        idx = order.Count;
                        map.Add(src, idx);
                        order.Add(src);
                    }
                    dst[k] = idx;
                }
                result.Triangles[j] = dst;
            }

            var n = order.Count;
            var pos = data.Channels[CombinedMesh.Position];
            result.Vertices = new Vector3[n];
            for (int i = 0; i < n; i++) result.Vertices[i] = toLocal.MultiplyPoint3x4(V3(pos, order[i]));

            var nor = data.Channels[CombinedMesh.Normal];
            if (nor != null)
            {
                result.Normals = new Vector3[n];
                for (int i = 0; i < n; i++) result.Normals[i] = toLocal.MultiplyVector(V3(nor, order[i])).normalized;
            }

            var tan = data.Channels[CombinedMesh.Tangent];
            if (tan != null && data.Dimensions[CombinedMesh.Tangent] == 4)
            {
                result.Tangents = new Vector4[n];
                for (int i = 0; i < n; i++)
                {
                    var o = order[i] * 4;
                    var v = toLocal.MultiplyVector(new Vector3(tan[o], tan[o + 1], tan[o + 2])).normalized;
                    result.Tangents[i] = new Vector4(v.x, v.y, v.z, tan[o + 3] < 0 ? -1f : 1f);
                }
            }

            var uv0 = data.Channels[CombinedMesh.Uv0];
            if (uv0 != null) result.Uv = Uv(uv0, data.Dimensions[CombinedMesh.Uv0], order);
            var uv1 = data.Channels[CombinedMesh.Uv1];
            if (uv1 != null) result.Uv2 = Uv(uv1, data.Dimensions[CombinedMesh.Uv1], order);

            var col = data.Channels[CombinedMesh.Color];
            if (col != null && data.Dimensions[CombinedMesh.Color] == 4)
            {
                result.Colors = new Color[n];
                for (int i = 0; i < n; i++)
                {
                    var o = order[i] * 4;
                    result.Colors[i] = new Color(col[o], col[o + 1], col[o + 2], col[o + 3]);
                }
            }
            return result;
        }

        static Vector3 V3(float[] values, int index)
        {
            var o = index * 3;
            return new Vector3(values[o], values[o + 1], values[o + 2]);
        }

        static Vector2[] Uv(float[] values, int dimension, List<int> order)
        {
            var result = new Vector2[order.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var o = order[i] * dimension;
                result[i] = new Vector2(values[o], values[o + 1]);
            }
            return result;
        }
    }

    internal class MeshData
    {
        internal Vector3[] Vertices;
        internal Vector3[] Normals;
        internal Vector4[] Tangents;
        internal Vector2[] Uv;
        internal Vector2[] Uv2;
        internal Color[] Colors;
        internal int[][] Triangles;

        internal Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, vertices = Vertices };
            if (Normals != null) mesh.normals = Normals;
            if (Tangents != null) mesh.tangents = Tangents;
            if (Uv != null) mesh.uv = Uv;
            if (Uv2 != null) mesh.uv2 = Uv2;
            if (Colors != null) mesh.colors = Colors;
            mesh.subMeshCount = Triangles.Length;
            for (int j = 0; j < Triangles.Length; j++) mesh.SetTriangles(Triangles[j], j);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
