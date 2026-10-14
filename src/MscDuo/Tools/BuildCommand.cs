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
        const string TriggerEnter = "TRIGGER ENTER";
        const string Assemble = "ASSEMBLE";
        const int MaxCandidates = 5;
        const int TightenSteps = 10;
        const int MaxPasses = 12;
        const float AssembleTimeout = 6f;

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
            var hand = FsmVariables.GlobalVariables.FindFsmGameObject("ItemPivot");
            if (car == null || parts == null || hand == null || hand.Value == null) { ModConsole.Error($"Satsuma {target} ou a mão do jogador não encontrada"); return; }

            running = true;
            DevCommands.StartRoutine(Build(target, car, parts, hand.Value.transform));
        }

        static IEnumerator Build(string target, GameObject car, GameObject parts, Transform hand)
        {
            ModConsole.Print($"<color=orange>[MSC Duo]</color> montando a Satsuma {target}, uma peça por vez...");
            int installed = 0, tightened = 0;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                List<KeyValuePair<PlayMakerFSM, GameObject>> jobs;
                try { jobs = Jobs(car, parts); }
                catch (System.Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] montagem: {e.Message}");
                    break;
                }
                var before = installed;
                foreach (var job in jobs)
                {
                    if (job.Key == null || job.Value == null || !Waiting(job.Key)) continue;
                    var done = false;
                    var routine = Fit(job.Key, job.Value, hand, ok => done = ok);
                    while (true)
                    {
                        bool more;
                        try { more = routine.MoveNext(); }
                        catch (System.Exception e)
                        {
                            ModConsole.Warning($"[MSC Duo] montagem de '{job.Value.name}': {e.Message}");
                            break;
                        }
                        if (!more) break;
                        yield return routine.Current;
                    }
                    if (!done) continue;
                    installed++;
                    if (installed % 10 == 0) ModConsole.Print($"<color=orange>[MSC Duo]</color> {installed} peças montadas...");
                }

                for (int step = 0; step < TightenSteps; step++)
                {
                    var bolts = 0;
                    try { bolts = Tighten(car, parts); }
                    catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] parafusos: {e.Message}"); }
                    if (step == 0) tightened = Mathf.Max(tightened, bolts);
                    yield return new WaitForSeconds(0.1f);
                }
                yield return new WaitForSeconds(0.5f);
                if (installed == before) break;
            }

            running = false;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma {target} pronta: {installed} peças montadas, {tightened} parafusos apertados.{Missing(car, parts)}");
        }

        static List<KeyValuePair<PlayMakerFSM, GameObject>> Jobs(GameObject car, GameObject parts)
        {
            var loose = new Dictionary<string, List<GameObject>>();
            foreach (var t in parts.GetComponentsInChildren<Transform>())
            {
                if (!t.CompareTag("PART")) continue;
                if (!loose.ContainsKey(t.name)) loose[t.name] = new List<GameObject>();
                loose[t.name].Add(t.gameObject);
            }

            var jobs = new List<KeyValuePair<PlayMakerFSM, GameObject>>();
            var used = new HashSet<GameObject>();
            foreach (var fsm in Assemblies(car, parts))
            {
                if (!Waiting(fsm)) continue;
                foreach (var name in Wanted(fsm))
                {
                    List<GameObject> options;
                    if (!loose.TryGetValue(name, out options)) continue;
                    var part = options.Find(p => !used.Contains(p));
                    if (part == null) continue;
                    used.Add(part);
                    jobs.Add(new KeyValuePair<PlayMakerFSM, GameObject>(fsm, part));
                    break;
                }
            }
            return jobs;
        }

        static IEnumerator Fit(PlayMakerFSM fsm, GameObject part, Transform hand, System.Action<bool> result)
        {
            var parent = part.transform.parent;
            var position = part.transform.position;
            var rotation = part.transform.rotation;
            part.transform.parent = hand;
            part.transform.position = fsm.transform.position;

            fsm.SendEvent(TriggerEnter);
            yield return null;
            yield return null;
            if (HasTransition(fsm, Assemble)) fsm.SendEvent(Assemble);

            var timeout = Time.time + AssembleTimeout;
            while (part != null && part.transform.parent == hand && Time.time < timeout) yield return null;

            var ok = part == null || part.transform.parent != hand;
            if (!ok)
            {
                part.transform.parent = parent;
                part.transform.position = position;
                part.transform.rotation = rotation;
            }
            result(ok);
        }

        static IEnumerable<PlayMakerFSM> Assemblies(GameObject car, GameObject parts)
        {
            foreach (var root in new[] { car, parts })
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>())
                    if (fsm.enabled && fsm.FsmName == "Assembly") yield return fsm;
        }

        static bool Waiting(PlayMakerFSM fsm) =>
            fsm.ActiveStateName != null && fsm.ActiveStateName.StartsWith("Check") && HasTransition(fsm, TriggerEnter);

        static bool HasTransition(PlayMakerFSM fsm, string eventName)
        {
            foreach (var state in fsm.FsmStates)
            {
                if (state.Name != fsm.ActiveStateName) continue;
                foreach (var transition in state.Transitions)
                    if (transition.EventName == eventName) return true;
            }
            return false;
        }

        static List<string> Wanted(PlayMakerFSM fsm)
        {
            var names = new List<string>();
            var hand = fsm.FsmVariables.FindFsmString("HandChild");
            if (hand != null && !string.IsNullOrEmpty(hand.Value)) names.Add(hand.Value);
            for (int i = 1; i <= MaxCandidates; i++)
            {
                var option = fsm.FsmVariables.FindFsmGameObject("Part" + i);
                if (option != null && option.Value != null && !names.Contains(option.Value.name)) names.Add(option.Value.name);
            }
            return names;
        }

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
                if (!Waiting(fsm)) continue;
                var names = Wanted(fsm);
                if (++count <= 60) text.Append($"\n  vazio: {fsm.gameObject.name} ({string.Join(", ", names.ToArray())})");
            }
            return count == 0 ? "" : $" {count} encaixes ainda vazios:{text}";
        }
    }
}
