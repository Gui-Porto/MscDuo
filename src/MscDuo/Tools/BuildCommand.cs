using System.Collections;
using System.Collections.Generic;
using System.Text;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class BuildCommand : ConsoleCommand
    {
        const int MaxCandidates = 5;
        const int TightenSteps = 10;
        const int IdlePassesToStop = 3;

        static bool running;

        public override string Name => "duo_build";
        public override string Help => "duo_build [1 | 2]  monta todas as peças disponíveis da Satsuma e aperta os parafusos (padrão: Satsuma 2)";

        public override void Run(string[] args)
        {
            if (running) { ModConsole.Error("a montagem ainda está em andamento"); return; }
            var target = args.Length > 0 ? args[0] : "2";
            GameObject car, parts;
            if (target == "1")
            {
                car = GameObject.Find("SATSUMA(557kg, 248)");
                parts = GameObject.Find("CARPARTS");
            }
            else if (target == "2")
            {
                car = SatsumaCloner.Clone;
                parts = SatsumaCloner.Parts;
            }
            else { ModConsole.Error(Help); return; }
            if (car == null || parts == null) { ModConsole.Error($"Satsuma {target} não encontrada"); return; }

            running = true;
            DevCommands.StartRoutine(Build(target, car, parts));
        }

        static IEnumerator Build(string target, GameObject car, GameObject parts)
        {
            ModConsole.Print($"<color=orange>[MSC Duo]</color> montando a Satsuma {target}...");
            int installed = 0, tightened = 0;
            for (int round = 0; round < 2; round++)
            {
                var idle = 0;
                while (idle < IdlePassesToStop)
                {
                    var started = 0;
                    try { started = AssembleStep(car, parts); }
                    catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] montagem: {e.Message}"); }
                    installed += started;
                    idle = started == 0 ? idle + 1 : 0;
                    yield return new WaitForSeconds(1.5f);
                }

                for (int step = 0; step < TightenSteps; step++)
                {
                    var bolts = 0;
                    try { bolts = Tighten(car, parts); }
                    catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] parafusos: {e.Message}"); }
                    if (step == 0) tightened += bolts;
                    yield return new WaitForSeconds(0.1f);
                }
                yield return new WaitForSeconds(1f);
            }

            running = false;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma {target}: {installed} peças montadas, {tightened} parafusos apertados.{Missing(car, parts)}");
        }

        static IEnumerable<PlayMakerFSM> Assemblies(GameObject car, GameObject parts)
        {
            foreach (var root in new[] { car, parts })
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>())
                    if (fsm.enabled && fsm.FsmName == "Assembly") yield return fsm;
        }

        static int AssembleStep(GameObject car, GameObject parts)
        {
            var used = new HashSet<GameObject>();
            var started = 0;
            foreach (var fsm in Assemblies(car, parts))
            {
                var setup = fsm.FsmVariables.FindFsmBool("Setup");
                var part = fsm.FsmVariables.FindFsmGameObject("Part");
                if (setup == null || part == null || setup.Value) continue;
                if (fsm.ActiveStateName == null || !fsm.ActiveStateName.StartsWith("Check")) continue;

                var candidate = Candidate(fsm, part, used);
                if (candidate == null) continue;
                part.Value = candidate;
                setup.Value = true;
                used.Add(candidate);
                started++;
            }
            return started;
        }

        static GameObject Candidate(PlayMakerFSM fsm, FsmGameObject part, HashSet<GameObject> used)
        {
            for (int i = 1; i <= MaxCandidates; i++)
            {
                var option = fsm.FsmVariables.FindFsmGameObject("Part" + i);
                if (Free(option != null ? option.Value : null, used)) return option.Value;
            }
            return Free(part.Value, used) ? part.Value : null;
        }

        static bool Free(GameObject part, HashSet<GameObject> used) =>
            part != null && part.activeSelf && part.CompareTag("PART") && !used.Contains(part);

        static int Tighten(GameObject car, GameObject parts)
        {
            var count = 0;
            foreach (var root in new[] { car, parts })
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (!fsm.enabled || fsm.FsmName != "Screw") continue;
                    fsm.SendEvent("TIGHTEN");
                    count++;
                }
            return count;
        }

        static string Missing(GameObject car, GameObject parts)
        {
            var text = new StringBuilder();
            var count = 0;
            foreach (var fsm in Assemblies(car, parts))
            {
                if (fsm.ActiveStateName == null || !fsm.ActiveStateName.StartsWith("Check")) continue;
                if (++count <= 40) text.Append($"\n  vazio: {fsm.gameObject.name}{Wanted(fsm)}");
            }
            return count == 0 ? "" : $" {count} encaixes ainda vazios:{text}";
        }

        static string Wanted(PlayMakerFSM fsm)
        {
            var names = new List<string>();
            for (int i = 1; i <= MaxCandidates; i++)
            {
                var option = fsm.FsmVariables.FindFsmGameObject("Part" + i);
                if (option != null && option.Value != null) names.Add(option.Value.name);
            }
            var hand = fsm.FsmVariables.FindFsmString("HandChild");
            if (names.Count == 0 && hand != null && !string.IsNullOrEmpty(hand.Value)) names.Add(hand.Value);
            return names.Count == 0 ? "" : " (" + string.Join(", ", names.ToArray()) + ")";
        }
    }
}
