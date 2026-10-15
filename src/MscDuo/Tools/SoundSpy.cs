using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class SoundSpy
    {
        static bool injected;
        static bool enabled;
        static readonly Dictionary<string, float> lastLog = new Dictionary<string, float>();
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        internal static string Toggle()
        {
            if (SatsumaCloner.Clone == null) return "Satsuma 2 não está ativa";
            if (!injected) Inject();
            enabled = !enabled;
            if (enabled)
            {
                counts.Clear();
                return "detector de sons ligado: cada som da Satsuma 2 vai para o console. Rode de novo para desligar e ver o resumo.";
            }
            var summary = new List<string>();
            foreach (var pair in counts) summary.Add($"  {pair.Value}x {pair.Key}");
            summary.Sort((a, b) => string.CompareOrdinal(b, a));
            return "detector de sons desligado. Resumo:\n" + string.Join("\n", summary.ToArray());
        }

        static void Inject()
        {
            injected = true;
            var hooked = 0;
            foreach (var root in new[] { SatsumaCloner.Clone, SatsumaCloner.Parts })
            {
                if (root == null) continue;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try
                    {
                        FsmTools.EnsureLoaded(fsm);
                        foreach (var state in fsm.FsmStates)
                        {
                            var sound = SoundOf(state);
                            if (sound == null) continue;
                            var label = $"{Path(fsm.transform)} [{fsm.FsmName} / {state.Name}] {sound}";
                            if (fsm.FsmInject(state.Name, () => Heard(label), false, 0)) hooked++;
                        }
                    }
                    catch { }
                }
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> detector de sons em {hooked} estados da Satsuma 2.");
        }

        static string SoundOf(FsmState state)
        {
            foreach (var action in state.Actions)
            {
                if (action == null || !action.GetType().Name.StartsWith("MasterAudioPlaySound")) continue;
                var group = Read(action, "soundGroupName");
                var variation = Read(action, "variationName");
                return $"{group}/{variation}";
            }
            return null;
        }

        static string Read(object action, string field)
        {
            var info = action.GetType().GetField(field);
            var value = info != null ? info.GetValue(action) : null;
            var text = value as FsmString;
            return text != null ? text.Value : value != null ? value.ToString() : "";
        }

        static void Heard(string label)
        {
            if (!enabled) return;
            counts[label] = counts.TryGetValue(label, out var n) ? n + 1 : 1;
            if (lastLog.TryGetValue(label, out var last) && Time.time - last < 1f) return;
            lastLog[label] = Time.time;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> som: {label}");
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
