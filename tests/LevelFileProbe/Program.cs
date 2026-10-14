using System;
using System.IO;
using MscDuo.Duplication;
using UnityEngine;

namespace LevelFileProbe
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\My Summer Car\mysummercar_Data\level2";
            var failures = 0;
            using (var level = new LevelFile(path))
            {
                for (int n = 1; n <= 14; n++)
                {
                    var name = n == 1 ? "Combined Mesh (root: scene)" : $"Combined Mesh (root: scene) {n}";
                    var mesh = level.Read(name);
                    if (mesh == null) { Console.WriteLine($"{name}: ausente"); continue; }
                    var built = 0;
                    for (int s = 0; s < mesh.SubMeshes.Length; s++)
                    {
                        try
                        {
                            var data = StaticBatchExtractor.BuildData(mesh, s, 1, Matrix4x4.identity);
                            var b = mesh.SubMeshes[s].Bounds;
                            var min = b.center - b.extents - Vector3.one * 0.01f;
                            var max = b.center + b.extents + Vector3.one * 0.01f;
                            foreach (var v in data.Vertices)
                                if (v.x < min.x || v.y < min.y || v.z < min.z || v.x > max.x || v.y > max.y || v.z > max.z)
                                    throw new Exception($"vértice fora dos bounds: {v}");
                            if (data.Normals != null)
                                foreach (var nv in data.Normals)
                                    if (Math.Abs(nv.magnitude - 1f) > 0.02f) throw new Exception($"normal inválida: {nv}");
                            built++;
                        }
                        catch (Exception e)
                        {
                            failures++;
                            if (failures < 10) Console.WriteLine($"{name} sub {s}: {e.GetType().Name} {e.Message}\n{e.StackTrace}");
                        }
                    }
                    Console.WriteLine($"{name}: {mesh.VertexCount} vértices, {built}/{mesh.SubMeshes.Length} submeshes ok");
                }
            }
            Console.WriteLine(failures == 0 ? "OK" : $"{failures} falhas");
            return failures == 0 ? 0 : 1;
        }
    }
}
