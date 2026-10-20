using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class ModParts
    {
        static HashSet<string> game;

        internal static int Removed { get; private set; }
        internal static readonly HashSet<string> Mods = new HashSet<string>();

        internal static void Reset()
        {
            Removed = 0;
            Mods.Clear();
        }

        static string ModOf(MonoBehaviour behaviour)
        {
            if (behaviour == null) return null;
            var assembly = behaviour.GetType().Assembly.GetName().Name;
            return Game().Contains(assembly) ? null : assembly;
        }

        static HashSet<string> Game()
        {
            if (game != null) return game;
            game = new HashSet<string> { typeof(ModParts).Assembly.GetName().Name };
            try
            {
                foreach (var dll in Directory.GetFiles(Path.Combine(Application.dataPath, "Managed"), "*.dll"))
                    game.Add(Path.GetFileNameWithoutExtension(dll));
            }
            catch (IOException) { }
            if (game.Count == 1) game.UnionWith(new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass", "Assembly-UnityScript", "Assembly-UnityScript-firstpass", "PlayMaker", "UnityEngine", "UnityEngine.UI", "MSCLoader", "ES2", "cInput", "HOTween" });
            return game;
        }

        internal static void Strip(GameObject root)
        {
            var doomed = new List<GameObject>();
            var foreign = new List<MonoBehaviour>();
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var mod = ModOf(behaviour);
                if (mod == null) continue;
                Mods.Add(mod);
                var go = behaviour.gameObject;
                if (go != root && go.GetComponent<PlayMakerFSM>() == null) doomed.Add(go);
                else foreign.Add(behaviour);
            }
            foreach (var behaviour in foreign)
                if (behaviour != null) Object.DestroyImmediate(behaviour);
            foreach (var go in doomed)
            {
                if (go == null) continue;
                go.SetActive(false);
                Object.DestroyImmediate(go);
                Removed++;
            }
        }
    }
}
