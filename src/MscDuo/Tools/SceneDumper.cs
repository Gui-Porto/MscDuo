using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class SceneDumper
    {
        static readonly string[] DefaultTargets = { "YARD", "SATSUMA(557kg, 248)" };
        const int MaxArrayItems = 12;
        const double FrameBudgetMs = 8;

        static DumpRunner runner;
        static bool busy;

        class DumpRunner : MonoBehaviour { }

        internal static void DumpDefault() => Run(DumpJob(true, true, DefaultTargets));

        internal static void DumpRootsOnly() => Run(DumpJob(true, false));

        internal static void DumpGlobalsOnly() => Run(DumpJob(false, true));

        internal static void DumpTargets(string name) => Run(DumpJob(false, false, name));

        static void Run(IEnumerator job)
        {
            if (busy)
            {
                ModConsole.Warning("[MSC Duo] já existe um dump em andamento.");
                return;
            }
            if (runner == null)
                runner = new GameObject("MscDuo_DumpRunner").AddComponent<DumpRunner>();
            runner.StartCoroutine(Guard(job));
        }

        static IEnumerator Guard(IEnumerator job)
        {
            busy = true;
            while (true)
            {
                bool more;
                try { more = job.MoveNext(); }
                catch (Exception e)
                {
                    ModConsole.Error($"[MSC Duo] dump falhou: {e}");
                    break;
                }
                if (!more) break;
                yield return job.Current;
            }
            busy = false;
        }

        static IEnumerator DumpJob(bool roots, bool globals, params string[] targets)
        {
            var folder = NewFolder();
            ModConsole.Print("<color=orange>[MSC Duo]</color> dump iniciado...");
            if (roots) Write(folder, "roots.txt", BuildRoots());
            if (globals) Write(folder, "globals.txt", BuildGlobals());

            foreach (var name in targets)
            {
                var root = FindRoot(name);
                if (root == null)
                {
                    ModConsole.Warning($"[MSC Duo] objeto raiz '{name}' não encontrado. Use 'duo_dump roots' para ver os nomes.");
                    continue;
                }
                var writer = new HierarchyWriter(GlobalNames());
                var steps = writer.Build(root);
                while (steps.MoveNext()) yield return null;
                Write(folder, SafeFileName(name) + ".txt", writer.Result);
                ModConsole.Print($"<color=orange>[MSC Duo]</color> {name}: {writer.Summary}");
            }
            Done(folder);
        }

        static string NewFolder()
        {
            var folder = Path.Combine(MscDuo.Instance.SettingsFolder, Path.Combine("dumps", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(folder);
            return folder;
        }

        static void Write(string folder, string file, string content)
        {
            File.WriteAllText(Path.Combine(folder, file), content, Encoding.UTF8);
        }

        static void Done(string folder)
        {
            ModConsole.Print($"<color=orange>[MSC Duo]</color> dump salvo em {folder}");
        }

        static IEnumerable<Transform> SceneRoots()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null && t.hideFlags == HideFlags.None)
                    yield return t;
        }

        static GameObject FindRoot(string name)
        {
            GameObject inactive = null;
            foreach (var t in SceneRoots())
            {
                if (t.name != name) continue;
                if (t.gameObject.activeInHierarchy) return t.gameObject;
                inactive = inactive ?? t.gameObject;
            }
            return inactive;
        }

        static string BuildRoots()
        {
            var roots = new List<Transform>(SceneRoots());
            roots.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder();
            sb.AppendLine($"# Objetos raiz da cena ({roots.Count})");
            sb.AppendLine("# nome | ativo | filhos (total) | FSMs (total) | posição");
            foreach (var t in roots)
            {
                var fsms = t.GetComponentsInChildren<PlayMakerFSM>(true).Length;
                var children = t.GetComponentsInChildren<Transform>(true).Length - 1;
                sb.AppendLine($"{t.name} | {(t.gameObject.activeSelf ? "on" : "off")} | {children} | {fsms} | {Vec(t.position)}");
            }
            return sb.ToString();
        }

        static string BuildGlobals()
        {
            var vars = FsmVariables.GlobalVariables.GetAllNamedVariables();
            var list = new List<NamedVariable>(vars);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder();
            sb.AppendLine($"# Variáveis globais do PlayMaker ({list.Count})");
            sb.AppendLine("# tipo | nome | valor atual");
            foreach (var v in list)
                sb.AppendLine($"{v.GetType().Name} | {v.Name} | {Values.Of(v)}");
            return sb.ToString();
        }

        static HashSet<string> GlobalNames()
        {
            var set = new HashSet<string>();
            foreach (var v in FsmVariables.GlobalVariables.GetAllNamedVariables())
                set.Add(v.Name);
            return set;
        }

        static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        internal static string Vec(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

        class HierarchyWriter
        {
            readonly HashSet<string> globals;
            readonly StringBuilder body = new StringBuilder();
            readonly List<string> globalRefs = new List<string>();
            readonly List<string> findRefs = new List<string>();
            readonly List<string> crossFsmRefs = new List<string>();
            int objectCount;
            int fsmCount;
            int initializedHere;

            internal string Result { get; private set; }
            internal string Summary =>
                $"{objectCount} objetos, {fsmCount} FSMs ({initializedHere} inicializadas só para leitura)";

            internal HierarchyWriter(HashSet<string> globals)
            {
                this.globals = globals;
            }

            internal IEnumerator Build(GameObject root)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var stack = new Stack<KeyValuePair<Transform, int>>();
                var paths = new Dictionary<Transform, string> { { root.transform, root.name } };
                stack.Push(new KeyValuePair<Transform, int>(root.transform, 0));

                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    var t = item.Key;
                    var path = paths[t];
                    Visit(t, item.Value, path);

                    for (int i = t.childCount - 1; i >= 0; i--)
                    {
                        var child = t.GetChild(i);
                        paths[child] = path + "/" + child.name;
                        stack.Push(new KeyValuePair<Transform, int>(child, item.Value + 1));
                    }

                    if (clock.Elapsed.TotalMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        clock.Reset();
                        clock.Start();
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"# Dump de '{root.name}'  posição {Vec(root.transform.position)}  rotação {Vec(root.transform.eulerAngles)}");
                sb.AppendLine($"# {objectCount} objetos, {fsmCount} FSMs");
                sb.AppendLine();
                Section(sb, "Usos de variáveis GLOBAIS", globalRefs);
                Section(sb, "Ações que buscam objetos (FIND)", findRefs);
                Section(sb, "Ações que falam com OUTRA FSM", crossFsmRefs);
                sb.AppendLine("## Hierarquia");
                sb.Append(body);
                Result = sb.ToString();
            }

            static void Section(StringBuilder sb, string title, List<string> items)
            {
                sb.AppendLine($"## {title} ({items.Count})");
                foreach (var i in items) sb.AppendLine("  " + i);
                sb.AppendLine();
            }

            void Visit(Transform t, int depth, string path)
            {
                objectCount++;
                var pad = new string(' ', depth * 2);
                var comps = new List<string>();
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) { comps.Add("<missing>"); continue; }
                    if (c is Transform) continue;
                    comps.Add(c.GetType().Name);
                }
                body.AppendLine($"{pad}- {t.name}{(t.gameObject.activeSelf ? "" : " [off]")}  {Vec(t.localPosition)}  [{string.Join(", ", comps.ToArray())}]");

                foreach (var fsm in t.GetComponents<PlayMakerFSM>())
                    WriteFsm(fsm, pad + "    ", path);
            }

            void WriteFsm(PlayMakerFSM pm, string pad, string path)
            {
                fsmCount++;
                var fsm = pm.Fsm;
                var where = $"{path} :: {pm.FsmName}";
                var wasInitialized = fsm.Initialized;
                if (!wasInitialized)
                {
                    try
                    {
                        fsm.InitData();
                        initializedHere++;
                    }
                    catch (Exception e)
                    {
                        body.AppendLine($"{pad}FSM \"{pm.FsmName}\"  <erro ao inicializar: {e.Message}>");
                        return;
                    }
                }
                body.AppendLine($"{pad}FSM \"{pm.FsmName}\"  start={fsm.StartState}  ativo={fsm.ActiveStateName}{(wasInitialized ? "" : "  (não inicializada no jogo)")}");

                var locals = fsm.Variables.GetAllNamedVariables();
                if (locals.Length > 0)
                {
                    var parts = new List<string>();
                    foreach (var v in locals) parts.Add($"{v.GetType().Name.Replace("Fsm", "")} {v.Name}={Values.Of(v)}");
                    body.AppendLine($"{pad}  vars: {string.Join("; ", parts.ToArray())}");
                }

                foreach (var tr in fsm.GlobalTransitions)
                    body.AppendLine($"{pad}  global: {tr.EventName} => {tr.ToState}");

                foreach (var state in fsm.States)
                {
                    body.AppendLine($"{pad}  [{state.Name}]");
                    try
                    {
                        if (!state.ActionsLoaded) state.LoadActions();
                    }
                    catch (Exception e)
                    {
                        body.AppendLine($"{pad}    <erro ao carregar ações: {e.Message}>");
                        continue;
                    }

                    var actions = state.Actions ?? new FsmStateAction[0];
                    for (int i = 0; i < actions.Length; i++)
                        WriteAction(actions[i], i, pad + "    ", $"{where} [{state.Name}]");

                    foreach (var tr in state.Transitions)
                        body.AppendLine($"{pad}    -> {tr.EventName} => {tr.ToState}");
                }
            }

            void WriteAction(FsmStateAction action, int index, string pad, string where)
            {
                if (action == null) { body.AppendLine($"{pad}{index}. <null>"); return; }

                var type = action.GetType();
                var fields = new List<string>();
                var usedGlobals = new List<string>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object value;
                    try { value = f.GetValue(action); }
                    catch { continue; }
                    fields.Add($"{f.Name}={Values.Describe(value, globals, usedGlobals)}");
                }

                var flags = new List<string>();
                if (usedGlobals.Count > 0)
                {
                    flags.Add("GLOBAL");
                    globalRefs.Add($"{where} {type.Name}: {string.Join(", ", usedGlobals.ToArray())}");
                }
                if (type.Name.IndexOf("Find", StringComparison.Ordinal) >= 0)
                {
                    flags.Add("FIND");
                    findRefs.Add($"{where} {type.Name}: {string.Join("; ", fields.ToArray())}");
                }
                if (IsCrossFsm(type.Name))
                {
                    flags.Add("XFSM");
                    crossFsmRefs.Add($"{where} {type.Name}: {string.Join("; ", fields.ToArray())}");
                }

                var flagText = flags.Count > 0 ? $" <{string.Join(",", flags.ToArray())}>" : "";
                var enabled = action.Enabled ? "" : " (desativada)";
                body.AppendLine($"{pad}{index}. {type.Name}{enabled}{flagText}  {string.Join("; ", fields.ToArray())}");
            }

            static bool IsCrossFsm(string name) =>
                name.StartsWith("GetFsm", StringComparison.Ordinal)
                || name.StartsWith("SetFsm", StringComparison.Ordinal)
                || name.StartsWith("SendEvent", StringComparison.Ordinal)
                || name == "SendMessage"
                || name == "BroadcastEvent";
        }

        static class Values
        {
            internal static string Of(NamedVariable v)
            {
                var prop = v.GetType().GetProperty("Value");
                if (prop == null) return "?";
                object raw;
                try { raw = prop.GetValue(v, null); }
                catch { return "?"; }
                return Plain(raw);
            }

            internal static string Describe(object value, HashSet<string> globals, List<string> usedGlobals)
            {
                switch (value)
                {
                    case null:
                        return "null";
                    case NamedVariable nv:
                        if (nv.UseVariable && !string.IsNullOrEmpty(nv.Name))
                        {
                            if (globals.Contains(nv.Name))
                            {
                                usedGlobals.Add(nv.Name);
                                return "@@" + nv.Name;
                            }
                            return "@" + nv.Name;
                        }
                        return nv.IsNone ? "none" : Of(nv);
                    case FsmOwnerDefault owner:
                        return owner.OwnerOption == OwnerDefaultOption.UseOwner
                            ? "owner"
                            : Describe(owner.GameObject, globals, usedGlobals);
                    case FsmEventTarget target:
                        return $"{target.target}:{Describe(target.gameObject, globals, usedGlobals)}/{Describe(target.fsmName, globals, usedGlobals)}";
                    case FsmEvent ev:
                        return "!" + ev.Name;
                    case string s:
                        return Quote(s);
                    case UnityEngine.Object uo:
                        return Plain(uo);
                    case IList list:
                        var items = new List<string>();
                        for (int i = 0; i < list.Count && i < MaxArrayItems; i++)
                            items.Add(Describe(list[i], globals, usedGlobals));
                        if (list.Count > MaxArrayItems) items.Add($"+{list.Count - MaxArrayItems}");
                        return "[" + string.Join(", ", items.ToArray()) + "]";
                    default:
                        return Plain(value);
                }
            }

            static string Plain(object raw)
            {
                switch (raw)
                {
                    case null:
                        return "null";
                    case string s:
                        return Quote(s);
                    case Vector3 v:
                        return Vec(v);
                    case UnityEngine.Object uo:
                        return uo == null ? "null" : $"'{uo.name}'";
                    default:
                        return raw.ToString();
                }
            }

            static string Quote(string s)
            {
                if (s.Length > 80) s = s.Substring(0, 80) + "…";
                return "\"" + s.Replace("\n", "\\n") + "\"";
            }
        }
    }
}
