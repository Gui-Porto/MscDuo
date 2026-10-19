using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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

        static readonly string[] ToolEvents = { "TIGHTEN", "UNTIGHTEN" };
        static readonly string[] SkippedFsms = { "Activate" };
        static readonly string[] SkippedRoots = { "PLAYER", "Database", "Database_P2", "Systems", "GUI" };
        static readonly Dictionary<string, PlayMakerFSM> byKey = new Dictionary<string, PlayMakerFSM>();
        static readonly Dictionary<PlayMakerFSM, string> worldKeys = new Dictionary<PlayMakerFSM, string>();
        static readonly HashSet<PlayMakerFSM> anchored = new HashSet<PlayMakerFSM>();
        static readonly Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>> sources = new Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>>();
        static Action<string, string, PlayMakerFSM> send;
        static bool applying;

        internal static int Count => worldKeys.Count + anchored.Count;
        internal static int Sent { get; private set; }
        internal static int Applied { get; private set; }

        internal static void Patch()
        {
            byKey.Clear();
            worldKeys.Clear();
            anchored.Clear();
            sources.Clear();
            Sent = Applied = 0;
            int hooked = 0, unsafeCount = 0, ambiguousCount = 0, parts = 0;
            var found = new Dictionary<PlayMakerFSM, Dictionary<string, HashSet<string>>>();
            var byPath = new Dictionary<string, List<PlayMakerFSM>>();
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || Array.IndexOf(SkippedFsms, fsm.FsmName) >= 0) continue;
                if (Array.IndexOf(SkippedRoots, fsm.transform.root.name) >= 0) continue;
                if (Duplication.DynoCloner.InDyno(fsm.transform)) continue;
                try
                {
                    FsmTools.EnsureLoaded(fsm);
                    var targets = Targets(fsm);
                    if (targets.Count == 0) continue;
                    if (!Safe(fsm, new List<string>(targets.Keys))) { unsafeCount++; continue; }
                    found[fsm] = targets;
                    if (HasAnchor(fsm.transform))
                    {
                        anchored.Add(fsm);
                        parts++;
                        continue;
                    }
                    var path = PathOf(fsm.transform) + ":" + fsm.FsmName;
                    List<PlayMakerFSM> list;
                    if (!byPath.TryGetValue(path, out list)) byPath[path] = list = new List<PlayMakerFSM>();
                    list.Add(fsm);
                }
                catch (Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] interação '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                }
            }

            foreach (var pair in byPath)
                foreach (var fsm in pair.Value)
                {
                    var key = "w|" + (pair.Value.Count == 1 ? pair.Key : pair.Key + "|" + Rounded(fsm.transform.position));
                    if (byKey.ContainsKey(key))
                    {
                        var other = byKey[key];
                        if (other != null) { worldKeys.Remove(other); ambiguousCount++; }
                        byKey[key] = null;
                        ambiguousCount++;
                        continue;
                    }
                    byKey[key] = fsm;
                    worldKeys[fsm] = key;
                }

            foreach (var pair in found)
            {
                var fsm = pair.Key;
                if (!anchored.Contains(fsm) && !worldKeys.ContainsKey(fsm)) continue;
                try
                {
                    foreach (var target in pair.Value.Keys)
                    {
                        var stateName = target;
                        var owner = fsm;
                        fsm.AddGlobalTransition(EventPrefix + stateName, stateName);
                        fsm.FsmInject(stateName, () => OnEnter(owner, stateName), false, 0, false);
                        hooked++;
                    }
                    sources[fsm] = pair.Value;
                }
                catch (Exception e)
                {
                    anchored.Remove(fsm);
                    worldKeys.Remove(fsm);
                    ModConsole.Warning($"[MSC Duo] interação '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                }
            }
            if (ambiguousCount > 0) ModConsole.Warning($"[MSC Duo] {ambiguousCount} objetos interativos com nome e posição repetidos ficaram sem sincronizar.");
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {Count} objetos interativos sincronizados, {parts} deles em peças e carros ({hooked} ações; {unsafeCount} ignorados por mexerem no jogador).");
        }

        internal static void Bind(Action<string, string, PlayMakerFSM> sender) => send = sender;

        static void OnEnter(PlayMakerFSM fsm, string state)
        {
            if (applying || send == null) return;
            Dictionary<string, HashSet<string>> map;
            HashSet<string> from;
            if (!sources.TryGetValue(fsm, out map) || !map.TryGetValue(state, out from)) return;
            if (from != null)
            {
                var previous = fsm.Fsm.PreviousActiveState;
                if (previous == null || !from.Contains(previous.Name)) return;
            }
            var key = KeyOf(fsm);
            if (key == null) return;
            Sent++;
            send(key, state, fsm);
        }

        static string KeyOf(PlayMakerFSM fsm)
        {
            string key;
            if (worldKeys.TryGetValue(fsm, out key)) return key;
            if (!anchored.Contains(fsm)) return null;
            var steps = new List<string>();
            for (var c = fsm.transform; c != null; c = c.parent)
            {
                var anchor = AnchorOf(c);
                if (anchor != null)
                {
                    steps.Reverse();
                    return anchor + "|" + string.Join("/", steps.ToArray()) + "|" + fsm.FsmName;
                }
                steps.Add(c.GetSiblingIndex() + "." + c.name);
            }
            return null;
        }

        static string AnchorOf(Transform t)
        {
            var body = t.GetComponent<Rigidbody>();
            ushort id;
            if (body != null && ItemRegistry.TryId(body, out id)) return "i|" + id;
            if (SatsumaCloner.Original != null && t == SatsumaCloner.Original.transform) return "c|1";
            if (SatsumaCloner.Clone != null && t == SatsumaCloner.Clone.transform) return "c|2";
            return null;
        }

        static bool HasAnchor(Transform t)
        {
            for (var c = t; c != null; c = c.parent)
                if (AnchorOf(c) != null) return true;
            return false;
        }

        static PlayMakerFSM Resolve(string key)
        {
            if (key.StartsWith("w|"))
            {
                PlayMakerFSM fsm;
                return byKey.TryGetValue(key, out fsm) ? fsm : null;
            }
            var parts = key.Split('|');
            if (parts.Length != 4) return null;
            Transform root = null;
            if (parts[0] == "i")
            {
                ushort remote, local;
                if (!ushort.TryParse(parts[1], out remote) || !ItemRegistry.TryLocal(remote, out local)) return null;
                var body = ItemRegistry.Get(local);
                if (body != null) root = body.transform;
            }
            else if (parts[0] == "c")
            {
                var car = parts[1] == "1" ? SatsumaCloner.Original : parts[1] == "2" ? SatsumaCloner.Clone : null;
                if (car != null) root = car.transform;
            }
            if (root == null) return null;
            var current = root;
            if (parts[2].Length > 0)
                foreach (var step in parts[2].Split('/'))
                {
                    var dot = step.IndexOf('.');
                    int index;
                    if (dot <= 0 || !int.TryParse(step.Substring(0, dot), out index) || index >= current.childCount) return null;
                    var child = current.GetChild(index);
                    if (child.name != step.Substring(dot + 1)) return null;
                    current = child;
                }
            foreach (var fsm in current.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == parts[3]) return fsm;
            return null;
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
            var objects = new List<KeyValuePair<string, ushort>>();
            foreach (var g in fsm.FsmVariables.GameObjectVariables)
            {
                if (g.Value == null) continue;
                var body = g.Value.GetComponent<Rigidbody>();
                ushort id;
                if (body != null && ItemRegistry.TryId(body, out id)) objects.Add(new KeyValuePair<string, ushort>(g.Name, id));
            }
            w.Write(objects.Count);
            foreach (var pair in objects) { w.Write(pair.Key); w.Write(pair.Value); }
        }

        internal static void ReadInteraction(BinaryReader r)
        {
            var key = r.ReadString();
            var state = r.ReadString();
            var bools = new Dictionary<string, bool>();
            var ints = new Dictionary<string, int>();
            var objects = new Dictionary<string, ushort>();
            var count = r.ReadInt32();
            for (int n = 0; n < count; n++) bools[r.ReadString()] = r.ReadBoolean();
            count = r.ReadInt32();
            for (int n = 0; n < count; n++) ints[r.ReadString()] = r.ReadInt32();
            count = r.ReadInt32();
            for (int n = 0; n < count; n++) objects[r.ReadString()] = r.ReadUInt16();

            var fsm = Resolve(key);
            if (fsm == null) return;
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
            foreach (var g in fsm.FsmVariables.GameObjectVariables)
            {
                ushort remote, local;
                if (!objects.TryGetValue(g.Name, out remote) || !ItemRegistry.TryLocal(remote, out local)) continue;
                var body = ItemRegistry.Get(local);
                if (body == null) continue;
                ItemSync.Forget(local);
                g.Value = body.gameObject;
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
                        if (from != null) from.Add(state.Name);
                    }
                }
            foreach (var transition in fsm.FsmGlobalTransitions)
                if (Array.IndexOf(ToolEvents, transition.EventName) >= 0 && !string.IsNullOrEmpty(transition.ToState))
                    targets[transition.ToState] = null;
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
            !string.IsNullOrEmpty(name) && (name.StartsWith("Player") || name.StartsWith("SavePlayer") || name == "PLAYER");

        static string PathOf(Transform t)
        {
            var chain = new List<string>();
            for (var c = t; c != null; c = c.parent) chain.Add(c.name);
            chain.Reverse();
            return string.Join("/", chain.ToArray());
        }

        static string Rounded(Vector3 p) =>
            Mathf.RoundToInt(p.x * 2f) + "," + Mathf.RoundToInt(p.y * 2f) + "," + Mathf.RoundToInt(p.z * 2f);
    }
}
