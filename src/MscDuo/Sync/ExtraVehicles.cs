using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;

namespace MscDuo.Sync
{
    internal static class ExtraVehicles
    {
        const string FileName = "veiculos_extras.txt";
        const string ModPrefix = "mod:";
        static readonly string[] Defaults =
        {
            "# objeto raiz | pasta do mod em Mod Settings (opcional)",
            "# os dois jogadores precisam ter este arquivo igual",
            "Ram1500 | ram1500",
            "CamperShell | ram1500",
        };
        static readonly string[] SkippedFiles = { "settings.json" };

        static List<string> roots;
        static List<string> folders;

        internal static List<string> Roots { get { Load(); return roots; } }
        internal static List<string> Folders { get { Load(); return folders; } }

        static string ModSettings => MscDuo.Instance != null ? Path.GetDirectoryName(MscDuo.Instance.SettingsFolder) : null;

        static void Load()
        {
            if (roots != null) return;
            roots = new List<string>();
            folders = new List<string>();
            if (MscDuo.Instance == null) return;
            var path = Path.Combine(MscDuo.Instance.SettingsFolder, FileName);
            string[] lines;
            try
            {
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(MscDuo.Instance.SettingsFolder);
                    File.WriteAllLines(path, Defaults);
                }
                lines = File.ReadAllLines(path);
            }
            catch (Exception e)
            {
                ModConsole.Warning($"[MSC Duo] não foi possível ler {FileName}: {e.Message}");
                lines = Defaults;
            }
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split('|');
                var root = parts[0].Trim();
                if (root.Length > 0 && !roots.Contains(root)) roots.Add(root);
                var folder = parts.Length > 1 ? parts[1].Trim() : "";
                if (ValidFolder(folder) && !folders.Contains(folder)) folders.Add(folder);
            }
        }

        static bool ValidFolder(string folder) =>
            folder.Length > 0 && folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && folder != "." && folder != ".." && folder != "MscDuo";

        static bool ValidFile(string file) =>
            file.Length > 0 && file.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && file != "." && file != ".." &&
            Array.IndexOf(SkippedFiles, file.ToLowerInvariant()) < 0;

        internal static string Key(string folder, string file) => ModPrefix + folder + "/" + file;

        internal static bool Parse(string key, out string folder, out string file)
        {
            folder = file = null;
            if (!key.StartsWith(ModPrefix)) return false;
            var rest = key.Substring(ModPrefix.Length);
            var cut = rest.IndexOf('/');
            if (cut <= 0) return false;
            folder = rest.Substring(0, cut);
            file = rest.Substring(cut + 1);
            return Folders.Contains(folder) && ValidFile(file);
        }

        internal static string PathOf(string folder, string file) => Path.Combine(Path.Combine(ModSettings, folder), file);

        internal static IEnumerable<string> Present()
        {
            if (ModSettings == null) yield break;
            foreach (var folder in Folders)
            {
                var dir = Path.Combine(ModSettings, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var path in Directory.GetFiles(dir))
                {
                    var file = Path.GetFileName(path);
                    if (ValidFile(file)) yield return Key(folder, file);
                }
            }
        }

        internal static void Clear()
        {
            foreach (var key in new List<string>(Present()))
            {
                string folder, file;
                if (Parse(key, out folder, out file)) File.Delete(PathOf(folder, file));
            }
        }
    }
}
