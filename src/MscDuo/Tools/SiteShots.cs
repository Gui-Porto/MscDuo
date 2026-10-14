using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class SiteShots
    {
        internal const string CandidatesFile = "candidates.txt";
        const int Width = 1600;
        const int Height = 900;
        const float SettleSeconds = 2f;

        internal static List<KeyValuePair<string, Vector2>> LoadCandidates()
        {
            var list = new List<KeyValuePair<string, Vector2>>();
            var path = Path.Combine(MscDuo.Instance.SettingsFolder, CandidatesFile);
            if (!File.Exists(path)) return list;
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Trim().Split(' ');
                if (parts.Length < 3 || parts[0].StartsWith("#")) continue;
                float x, z;
                if (DevCommands.TryFloat(parts[1], out x) && DevCommands.TryFloat(parts[2], out z))
                    list.Add(new KeyValuePair<string, Vector2>(parts[0], new Vector2(x, z)));
            }
            return list;
        }

        internal static IEnumerator Run(List<KeyValuePair<string, Vector2>> sites)
        {
            var player = DevCommands.Player;
            if (player == null)
            {
                ModConsole.Error("PLAYER não encontrado");
                yield break;
            }

            var folder = Path.Combine(MscDuo.Instance.SettingsFolder, Path.Combine("shots", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(folder);
            var home = player.position;

            foreach (var site in sites)
            {
                var ground = GroundY(site.Value.x, site.Value.y, home.y);
                var target = new Vector3(site.Value.x, ground, site.Value.y);
                player.position = target + Vector3.up * 1.5f;
                ModConsole.Print($"<color=orange>[MSC Duo]</color> fotografando {site.Key}...");
                yield return new WaitForSeconds(SettleSeconds);
                yield return new WaitForEndOfFrame();

                Save(folder, site.Key + "_cima.png", target + Vector3.up * 45f, Quaternion.Euler(90f, 0f, 0f));
                Save(folder, site.Key + "_sul.png", target + new Vector3(0f, 14f, -38f), LookAt(target + new Vector3(0f, 14f, -38f), target));
                Save(folder, site.Key + "_leste.png", target + new Vector3(38f, 14f, 0f), LookAt(target + new Vector3(38f, 14f, 0f), target));
                Save(folder, site.Key + "_chao.png", target + new Vector3(-20f, 1.7f, -20f), LookAt(target + new Vector3(-20f, 1.7f, -20f), target + Vector3.up * 1.7f));
            }

            player.position = home;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> fotos salvas em {folder}");
        }

        static Quaternion LookAt(Vector3 from, Vector3 to) => Quaternion.LookRotation(to - from);

        static float GroundY(float x, float z, float fallback)
        {
            RaycastHit best = default(RaycastHit);
            var found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f))
            {
                if (hit.collider.isTrigger) continue;
                if (!hit.collider.transform.root.name.Equals("MAP")) continue;
                if (!found || hit.point.y > best.point.y)
                {
                    best = hit;
                    found = true;
                }
            }
            return found ? best.point.y : fallback;
        }

        static void Save(string folder, string file, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("MscDuo_ShotCamera");
            var rt = new RenderTexture(Width, Height, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 1500f;
                cam.transform.position = position;
                cam.transform.rotation = rotation;
                cam.targetTexture = rt;
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(folder, file), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }
    }

    internal class ShotsCommand : ConsoleCommand
    {
        public override string Name => "duo_shots";
        public override string Help => "duo_shots [x z]  fotografa de perto as opções de candidates.txt (ou um ponto)";

        public override void Run(string[] args)
        {
            List<KeyValuePair<string, Vector2>> sites;
            float x, z;
            if (args.Length >= 2 && DevCommands.TryFloat(args[0], out x) && DevCommands.TryFloat(args[1], out z))
                sites = new List<KeyValuePair<string, Vector2>> { new KeyValuePair<string, Vector2>(string.Format(CultureInfo.InvariantCulture, "p_{0:0}_{1:0}", x, z), new Vector2(x, z)) };
            else
                sites = SiteShots.LoadCandidates();

            if (sites.Count == 0)
            {
                ModConsole.Error($"nenhuma opção em {SiteShots.CandidatesFile}");
                return;
            }
            DevCommands.StartRoutine(SiteShots.Run(sites));
        }
    }
}
