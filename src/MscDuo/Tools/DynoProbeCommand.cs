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
    internal class DynoProbeCommand : ConsoleCommand
    {
        static readonly string[] Names = { "DynoSheet", "Results", "Dyno", "DynoDistance", "AttachPoint", "roll_coll", "TireCatcher" };

        public override string Name => "duo_dino";
        public override string Help => "duo_dino  levanta tudo sobre o dinamômetro da oficina e salva em dumps/dino.txt";

        public override void Run(string[] args)
        {
            var sb = new StringBuilder();
            var roots = new List<Transform>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.hideFlags != HideFlags.None || Array.IndexOf(Names, t.name) < 0) continue;
                if (t.root.name.EndsWith("_P2")) continue;
                roots.Add(t);
            }
            foreach (var t in roots)
            {
                sb.AppendLine("==== " + Path(t) + (t.gameObject.activeInHierarchy ? "" : " [inativo]"));
                Tree(sb, t, 1, t.name == "Dyno" && t.parent != null && t.parent.name == "LOD" ? 6 : 4);
            }
            var file = System.IO.Path.Combine(System.IO.Path.Combine(MscDuo.Instance.SettingsFolder, "dumps"), "dino.txt");
            File.WriteAllText(file, sb.ToString());
            ModConsole.Print($"<color=orange>[MSC Duo]</color> dinamômetro: {roots.Count} objetos levantados em {file}");
        }

        static void Tree(StringBuilder sb, Transform t, int depth, int max)
        {
            var pad = new string(' ', depth * 2);
            sb.AppendLine($"{pad}- {t.name} {(t.gameObject.activeSelf ? "" : "[off] ")}local {t.localPosition} giro {t.localEulerAngles} escala {t.localScale} camada {LayerMask.LayerToName(t.gameObject.layer)}");
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                if (c is PlayMakerFSM fsm) Fsm(sb, fsm, pad + "    ");
                else Fields(sb, c, pad + "    ");
            }
            if (depth >= max) return;
            for (int i = 0; i < t.childCount; i++) Tree(sb, t.GetChild(i), depth + 1, max);
        }

        static void Fsm(StringBuilder sb, PlayMakerFSM fsm, string pad)
        {
            try { Duplication.FsmTools.EnsureLoaded(fsm); } catch { }
            sb.AppendLine($"{pad}FSM \"{fsm.FsmName}\" ativo={fsm.ActiveStateName}");
            foreach (var v in fsm.FsmVariables.GetAllNamedVariables()) sb.AppendLine($"{pad}  var {v.GetType().Name} {v.Name} = {Describe(Value(v))}");
            foreach (var state in fsm.FsmStates)
            {
                sb.AppendLine($"{pad}  [{state.Name}]");
                foreach (var tr in state.Transitions) sb.AppendLine($"{pad}    -> {tr.EventName} => {tr.ToState}");
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    var parts = new List<string>();
                    foreach (var f in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var value = f.GetValue(action);
                        parts.Add(f.Name + "=" + (value is FsmProperty p ? Property(p) : Describe(value)));
                    }
                    sb.AppendLine($"{pad}    {action.GetType().Name}{(action.Enabled ? "" : " (off)")}: {string.Join("; ", parts.ToArray())}");
                }
            }
        }

        static string Property(FsmProperty p)
        {
            var target = p.TargetObject != null ? (p.TargetObject.UseVariable ? "@" + p.TargetObject.Name : Describe(p.TargetObject.Value)) : "null";
            return $"{{alvo={target}; tipo={p.TargetTypeName}; propriedade={p.PropertyName}; set={p.setProperty}; float={Describe(p.FloatParameter)}; bool={Describe(p.BoolParameter)}; int={Describe(p.IntParameter)}; vec={Describe(p.Vector3Parameter)}}}";
        }

        static object Value(NamedVariable v)
        {
            var prop = v.GetType().GetProperty("Value");
            return prop != null ? prop.GetValue(v, null) : null;
        }

        static void Fields(StringBuilder sb, Component c, string pad)
        {
            var type = c.GetType();
            var parts = new List<string>();
            if (c is Behaviour b) parts.Add("enabled=" + b.enabled);
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.DeclaringType == typeof(MonoBehaviour) || f.DeclaringType == typeof(Behaviour) || f.DeclaringType == typeof(Component)) continue;
                try { parts.Add(f.Name + "=" + Describe(f.GetValue(c))); } catch { }
            }
            if (c is Rigidbody rb) parts.Add($"kinematic={rb.isKinematic}; massa={rb.mass}; gravidade={rb.useGravity}");
            if (c is ConfigurableJoint j) parts.Add($"conectado={Describe(j.connectedBody)}; anchor={j.anchor}; connectedAnchor={j.connectedAnchor}; autoConfig={j.autoConfigureConnectedAnchor}; x={j.xMotion}; y={j.yMotion}; z={j.zMotion}; ax={j.angularXMotion}; ay={j.angularYMotion}; az={j.angularZMotion}");
            if (c is Collider col) parts.Add($"trigger={col.isTrigger}; bounds={col.bounds.size}");
            if (c is Renderer r) parts.Add($"materiais={Describe(r.sharedMaterials)}");
            sb.AppendLine($"{pad}{type.FullName}: {string.Join("; ", parts.ToArray())}");
        }

        static string Describe(object value)
        {
            if (value == null) return "null";
            if (value is NamedVariable nv)
            {
                if (nv.UseVariable) return "@" + nv.Name;
                return Describe(Value(nv));
            }
            if (value is FsmOwnerDefault owner) return owner.OwnerOption == OwnerDefaultOption.UseOwner ? "owner" : Describe(owner.GameObject);
            if (value is FsmEventTarget et) return $"{et.target}:{(et.gameObject != null ? Describe(et.gameObject) : "")}/{Describe(et.fsmName)}";
            if (value is FsmEvent ev) return "!" + ev.Name;
            if (value is GameObject go) return "'" + Path(go.transform) + "'";
            if (value is Component comp) return comp.GetType().Name + "@'" + Path(comp.transform) + "'";
            if (value is UnityEngine.Object o) return o.GetType().Name + ":" + o.name;
            if (value is string s) return "\"" + s + "\"";
            if (value is IEnumerable list && !(value is string))
            {
                var items = new List<string>();
                foreach (var item in list) { items.Add(Describe(item)); if (items.Count > 12) break; }
                return "[" + string.Join(", ", items.ToArray()) + "]";
            }
            return value.ToString();
        }

        static string Path(Transform t)
        {
            var p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
