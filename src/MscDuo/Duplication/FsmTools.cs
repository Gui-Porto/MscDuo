using System.Reflection;
using HutongGames.PlayMaker;

namespace MscDuo.Duplication
{
    internal static class FsmTools
    {
        internal static void EnsureLoaded(PlayMakerFSM fsm)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.InitData();
            foreach (var state in fsm.FsmStates)
                if (!state.ActionsLoaded) state.LoadActions();
        }

        internal static int RedirectGlobal<T>(PlayMakerFSM fsm, string globalName, T replacement) where T : NamedVariable =>
            RedirectGlobal(fsm, globalName, (NamedVariable)replacement);

        internal static int RedirectGlobal(PlayMakerFSM fsm, string globalName, NamedVariable replacement)
        {
            EnsureLoaded(fsm);
            if (fsm.FsmVariables.FindVariable(globalName) != null) return 0;
            var count = 0;
            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                    if (action != null) count += RedirectFields(action, globalName, replacement);
            return count;
        }

        static int RedirectFields(object owner, string globalName, NamedVariable replacement)
        {
            var count = 0;
            var type = replacement.GetType();
            foreach (var field in owner.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == type)
                {
                    var value = field.GetValue(owner) as NamedVariable;
                    if (value == null || !value.UseVariable || value.Name != globalName) continue;
                    field.SetValue(owner, replacement);
                    count++;
                }
                else if (field.FieldType.IsArray && field.FieldType.GetElementType() == type)
                {
                    var array = field.GetValue(owner) as NamedVariable[];
                    if (array == null) continue;
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (array[i] == null || !array[i].UseVariable || array[i].Name != globalName) continue;
                        array.SetValue(replacement, i);
                        count++;
                    }
                }
                else if (field.FieldType == typeof(FsmProperty))
                {
                    var property = field.GetValue(owner);
                    if (property != null) count += RedirectFields(property, globalName, replacement);
                }
            }
            return count;
        }

        internal static int RetargetObject(PlayMakerFSM fsm, UnityEngine.GameObject from, UnityEngine.GameObject to)
        {
            EnsureLoaded(fsm);
            var count = 0;
            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var value = field.GetValue(action);
                        FsmGameObject target = null;
                        if (value is FsmGameObject go) target = go;
                        else if (value is FsmOwnerDefault owner && owner.OwnerOption == OwnerDefaultOption.SpecifyGameObject) target = owner.GameObject;
                        else if (value is FsmEventTarget eventTarget && eventTarget.gameObject != null && eventTarget.gameObject.OwnerOption == OwnerDefaultOption.SpecifyGameObject) target = eventTarget.gameObject.GameObject;
                        if (target == null || target.UseVariable || target.Value != from) continue;
                        target.Value = to;
                        count++;
                    }
                }
            return count;
        }

        internal static int ConfineBroadcasts(PlayMakerFSM fsm, UnityEngine.GameObject root)
        {
            EnsureLoaded(fsm);
            var count = 0;
            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var target = field.GetValue(action) as FsmEventTarget;
                        if (target == null || target.target != FsmEventTarget.EventTarget.BroadcastAll) continue;
                        target.target = FsmEventTarget.EventTarget.GameObject;
                        target.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = root } };
                        target.sendToChildren = new FsmBool { Value = true };
                        count++;
                    }
                }
            return count;
        }

        internal static int PrefixSaveTags(PlayMakerFSM fsm, string prefix)
        {
            var count = 0;
            foreach (var s in fsm.FsmVariables.StringVariables)
            {
                if (!s.Name.StartsWith("UniqueTag") || string.IsNullOrEmpty(s.Value) || s.Value.StartsWith(prefix)) continue;
                s.Value = prefix + s.Value;
                count++;
            }
            return count;
        }
    }
}
