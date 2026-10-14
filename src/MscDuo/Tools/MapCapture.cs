using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class MapCapture
    {
        const float GridStep = 2f;
        const float CameraHeight = 400f;
        const double FrameBudgetMs = 8;

        internal static IEnumerator Capture(Vector3 center, float half, int resolution)
        {
            var folder = Path.Combine(MscDuo.Instance.SettingsFolder, Path.Combine("maps", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(folder);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> capturando mapa {half * 2:0}m x {half * 2:0}m...");

            yield return new WaitForEndOfFrame();
            File.WriteAllBytes(Path.Combine(folder, "map.png"), Render(center, half, resolution));

            var csv = new StringBuilder();
            csv.AppendLine("x,z,y,slope,object");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (float z = center.z - half; z <= center.z + half; z += GridStep)
            {
                for (float x = center.x - half; x <= center.x + half; x += GridStep)
                {
                    Sample(x, z, center.y, csv);
                    if (clock.Elapsed.TotalMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        clock.Reset();
                        clock.Start();
                    }
                }
            }
            File.WriteAllText(Path.Combine(folder, "heights.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(folder, "objects.csv"), BuildObjects(center, half));
            File.WriteAllText(Path.Combine(folder, "terrain_trees.csv"), BuildTerrainTrees(center, half));

            var inv = CultureInfo.InvariantCulture;
            File.WriteAllText(Path.Combine(folder, "meta.txt"), string.Format(inv,
                "center_x={0}\ncenter_y={1}\ncenter_z={2}\nhalf={3}\nresolution={4}\nup=+Z\nright=+X\n",
                center.x, center.y, center.z, half, resolution));

            ModConsole.Print($"<color=orange>[MSC Duo]</color> mapa salvo em {folder}");
        }

        static byte[] Render(Vector3 center, float half, int resolution)
        {
            var go = new GameObject("MscDuo_MapCamera");
            var rt = new RenderTexture(resolution, resolution, 24);
            var fog = RenderSettings.fog;
            var lodBias = QualitySettings.lodBias;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = half;
                cam.aspect = 1f;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = CameraHeight * 2f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.transform.position = center + Vector3.up * CameraHeight;
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.targetTexture = rt;

                RenderSettings.fog = false;
                QualitySettings.lodBias = 20f;
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                var png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                return png;
            }
            finally
            {
                RenderSettings.fog = fog;
                QualitySettings.lodBias = lodBias;
                UnityEngine.Object.Destroy(go);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        static string BuildObjects(Vector3 center, float half)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("x,z,y,size_x,size_y,size_z,active,root,path\n");
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r.hideFlags != HideFlags.None) continue;
                var p = r.transform.position;
                if (Mathf.Abs(p.x - center.x) > half || Mathf.Abs(p.z - center.z) > half) continue;
                var size = r.bounds.size;
                var path = r.name;
                var t = r.transform.parent;
                for (int depth = 0; t != null && depth < 4; depth++, t = t.parent) path = t.name + "/" + path;
                sb.AppendLine(string.Format(inv, "{0:0.##},{1:0.##},{2:0.##},{3:0.##},{4:0.##},{5:0.##},{6},\"{7}\",\"{8}\"",
                    p.x, p.z, p.y, size.x, size.y, size.z, r.gameObject.activeInHierarchy ? 1 : 0,
                    r.transform.root.name.Replace("\"", "'"), path.Replace("\"", "'")));
            }
            return sb.ToString();
        }

        static string BuildTerrainTrees(Vector3 center, float half)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("terrain,x,z,y,prototype\n");
            foreach (var terrain in Resources.FindObjectsOfTypeAll<Terrain>())
            {
                if (terrain.hideFlags != HideFlags.None || terrain.terrainData == null) continue;
                var data = terrain.terrainData;
                var origin = terrain.transform.position;
                foreach (var tree in data.treeInstances)
                {
                    var p = origin + Vector3.Scale(tree.position, data.size);
                    if (Mathf.Abs(p.x - center.x) > half || Mathf.Abs(p.z - center.z) > half) continue;
                    sb.AppendLine(string.Format(inv, "\"{0}\",{1:0.##},{2:0.##},{3:0.##},{4}", terrain.name, p.x, p.z, p.y, tree.prototypeIndex));
                }
            }
            return sb.ToString();
        }

        static void Sample(float x, float z, float baseY, StringBuilder csv)
        {
            var origin = new Vector3(x, baseY + CameraHeight, z);
            RaycastHit best = default(RaycastHit);
            var found = false;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, CameraHeight * 2f))
            {
                if (hit.collider.isTrigger) continue;
                if (!found || hit.point.y > best.point.y)
                {
                    best = hit;
                    found = true;
                }
            }

            var inv = CultureInfo.InvariantCulture;
            if (!found)
            {
                csv.AppendLine(string.Format(inv, "{0:0.##},{1:0.##},,,", x, z));
                return;
            }
            var slope = Vector3.Angle(best.normal, Vector3.up);
            var name = best.collider.transform.root.name + "/" + best.collider.name;
            csv.AppendLine(string.Format(inv, "{0:0.##},{1:0.##},{2:0.##},{3:0.#},\"{4}\"", x, z, best.point.y, slope, name.Replace("\"", "'")));
        }
    }
}
