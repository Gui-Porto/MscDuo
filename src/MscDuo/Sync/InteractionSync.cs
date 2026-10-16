using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class InteractionSync
    {
        const string EventPrefix = "MSCDUO_";
        const int MaxChain = 40;

        static readonly string[] SkippedFsms = { "Assembly", "Removal", "Activate", "Remove", "Assemble" };
        static readonly string[] SkippedRoots = { "PLAYER", "SATSUMA(557kg, 248)", "SATSUMA_P2", "CARPARTS", "CARPARTS_P2", "Database", "Database_P2", "Systems", "GUI" };
        static readonly Dictionary<string, PlayMakerFSM> byKey = new Dictionary<string, PlayMakerFSM>();
        static readonly Dictionary<PlayMakerFSM, string> keys = new Dictionary<PlayMakerFSM, string>();
        static readonly Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>> sources = new Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>>();
        static Action<string, string, PlayMakerFSM> send;
        static bool applying;

        internal static int Count => byKey.Count;
        internal static int Sent { get; private set; }
        internal static int Applied { get; private set; }

        internal static void Patch()
        {
            byKey.Clear();
            keys.Clear();
            sources.Clear();
            Sent = Applied = 0;
            int hooked = 0, unsafeCount = 0, ambiguousCount = 0;
            var found = new Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>>();
            var byPath = new Dictionary<string, List<PlayMakerFSM>>();
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || Array.IndexOf(SkippedFsms, fsm.FsmName) >= 0) continue;
                if (Array.IndexOf(SkippedRoots, fsm.transform.root.name) >= 0) continue;
                try
                {
                    FsmTools.EnsureLoaded(fsm);
                    var targets = Targets(fsm);
                    if (targets.Count == 0) continue;
                    if (!Safe(fsm, new List<string>(targets.Keys))) { unsafeCount++; continue; }
                    found[fsm] = targets;
                    var path = PathOf(fsm);
                    List<PlayMakerFSM> list;
                    if (!byPath.TryGetValue(path, out list)) byPath[path] = list = new List<PlayMakerFSM>();
                    list.Add(fsm);
                }
                catch (Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] interação '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                }
            }

            var candidates = new Dictionary<string, List<PlayMakerFSM>>();
            foreach (var pair in byPath)
                foreach (var fsm in pair.Value)
                {
                    var key = pair.Value.Count == 1 ? pair.Key : pair.Key + "|" + Rounded(fsm.transform.position);
                    List<PlayMakerFSM> list;
                    if (!candidates.TryGetValue(key, out list)) candidates[key] = list = new List<PlayMakerFSM>();
                    list.Add(fsm);
                }

            foreach (var pair in candidates)
            {
                if (pair.Value.Count > 1) { ambiguousCount += pair.Value.Count; continue; }
                var fsm = pair.Value[0];
                try
                {
                    foreach (var target in found[fsm].Keys)
                    {
                        var stateName = target;
                        var owner = fsm;
                        fsm.AddGlobalTransition(EventPrefix + stateName, stateName);
                        fsm.FsmInject(stateName, () => OnEnter(owner, stateName), false, 0, false);
                        hooked++;
                    }
                    byKey[pair.Key] = fsm;
                    keys[fsm] = pair.Key;
                    sources[fsm] = found[fsm];
                }
                catch (Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] interação '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                }
            }
            if (ambiguousCount > 0) ModConsole.Warning($"[MSC Duo] {ambiguousCount} objetos interativos com nome e posição repetidos ficaram sem sincronizar.");
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {byKey.Count} objetos interativos sincronizados ({hooked} ações; {unsafeCount} ignorados por mexerem no jogador).");
        }

        internal static void Bind(Action<string, string, PlayMakerFSM> sender) => send = sender;

        static void OnEnter(PlayMakerFSM fsm, string state)
        {
            if (applying || send == null) return;
            string key;
            if (!keys.TryGetValue(fsm, out key)) return;
            Dictionary<string, HashSet<string>> map;
            HashSet<string> from;
            var previous = fsm.Fsm.PreviousActiveState;
            if (previous == null || !sources.TryGetValue(fsm, out map) || !map.TryGetValue(state, out from) || !from.Contains(previous.Name)) return;
            Sent++;
            send(key, state, fsm);
        }

        internal static void WriteInteraction(BinaryWriter w, string key, string state, PlayMakerFSM fsm)
        {
            w.Write(key);
            w.Write(state);
            var bools = fsm.FsmVariables.BoolVariables;
            var ints = fsm.FsmVariables.IntVariables;
            w.Write(bools.Length);
            foreach (var b in bools) { w.Write(b.Name); w.Write(b.Value); }
            w.Write(ints.Length);
            foreach (var i in ints) { w.Write(i.Name); w.Write(i.Value); }
        }

        internal static void ReadInteraction(BinaryReader r)
        {
            var key = r.ReadString();
            var state = r.ReadString();
            var bools = new Dictionary<string, bool>();
            var ints = new Dictionary<string, int>();
            var count = r.ReadInt32();
            for (int n = 0; n < count; n++) bools[r.ReadString()] = r.ReadBoolean();
            count = r.ReadInt32();
            for (int n = 0; n < count; n++) ints[r.ReadString()] = r.ReadInt32();

            PlayMakerFSM fsm;
            if (!byKey.TryGetValue(key, out fsm) || fsm == null) return;
            foreach (var b in fsm.FsmVariables.BoolVariables)
            {
                bool value;
                if (bools.TryGetValue(b.Name, out value)) b.Value = value;
            }
            foreach (var i in fsm.FsmVariables.IntVariables)
            {
                int value;
                if (ints.TryGetValue(i.Name, out value)) i.Value = value;
            }
            applying = true;
            try
            {
                fsm.SendEvent(EventPrefix + state);
                Applied++;
            }
            finally
            {
                applying = false;
            }
        }

        static Dictionary<string, HashSet<string>> Targets(PlayMakerFSM fsm)
        {
            var targets = new Dictionary<string, HashSet<string>>();
            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null || !action.Enabled) continue;
                    FsmEvent trigger = null;
                    if (action is GetMouseButtonDown mouse) trigger = mouse.sendEvent;
                    else if (action is GetButtonDown button && button.buttonName != null && button.buttonName.Value == "Use") trigger = button.sendEvent;
                    else if (action is MousePickEvent pick) trigger = pick.mouseDown;
                    if (trigger == null || string.IsNullOrEmpty(trigger.Name)) continue;
                    foreach (var transition in state.Transitions)
                    {
                        if (transition.EventName != trigger.Name || string.IsNullOrEmpty(transition.ToState)) continue;
                        HashSet<string> from;
                        if (!targets.TryGetValue(transition.ToState, out from)) targets[transition.ToState] = from = new HashSet<string>();
                        from.Add(state.Name);
                    }
                }
            return targets;
        }

        static bool Safe(PlayMakerFSM fsm, List<string> targets)
        {
            var seen = new HashSet<string>();
            var queue = new Queue<string>(targets);
            while (queue.Count > 0 && seen.Count < MaxChain)
            {
                var name = queue.Dequeue();
                if (!seen.Add(name)) continue;
                var state = fsm.Fsm.GetState(name);
                if (state == null) continue;
                foreach (var action in state.Actions)
                    if (action != null && action.Enabled && TouchesPlayer(action, 0)) return false;
                foreach (var transition in state.Transitions)
                    if (!string.IsNullOrEmpty(transition.ToState)) queue.Enqueue(transition.ToState);
            }
            return true;
        }

        static bool TouchesPlayer(object owner, int depth)
        {
            if (owner == null || depth > 2) return false;
            foreach (var field in owner.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = field.GetValue(owner);
                if (value is NamedVariable variable)
                {
                    if (variable.UseVariable && IsPlayerName(variable.Name)) return true;
                    if (variable is FsmGameObject go && !go.UseVariable && go.Value != null && go.Value.transform.root.name == "PLAYER") return true;
                }
                else if (value is FsmOwnerDefault ownerDefault && ownerDefault.OwnerOption == OwnerDefaultOption.SpecifyGameObject && ownerDefault.GameObject != null)
                {
                    var target = ownerDefault.GameObject;
                    if (target.UseVariable && IsPlayerName(target.Name)) return true;
                    if (!target.UseVariable && target.Value != null && target.Value.transform.root.name == "PLAYER") return true;
                }
                else if (value is NamedVariable[] array)
                {
                    foreach (var item in array)
                        if (item != null && item.UseVariable && IsPlayerName(item.Name)) return true;
                }
                else if (value is FsmProperty || value is FsmEventTarget)
                {
                    if (TouchesPlayer(value, depth + 1)) return true;
                }
            }
            return false;
        }

        static bool IsPlayerName(string name) =>
            !string.IsNullOrEmpty(name) && (name.StartsWith("Player") || name == "Player" || name.StartsWith("SavePlayer") || name == "PLAYER");

        static string PathOf(PlayMakerFSM fsm)
        {
            var chain = new List<string>();
            for (var c = fsm.transform; c != null; c = c.parent) chain.Add(c.name);
            chain.Reverse();
            return string.Join("/", chain.ToArray()) + ":" + fsm.FsmName;
        }

        static string Rounded(Vector3 p) =>
            Mathf.RoundToInt(p.x * 2f) + "," + Mathf.RoundToInt(p.y * 2f) + "," + Mathf.RoundToInt(p.z * 2f);
    }
}
