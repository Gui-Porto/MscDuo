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

        internal static int RedirectGlobal<T>(PlayMakerFSM fsm, string globalName, T replacement) where T : NamedVariable
        {
            EnsureLoaded(fsm);
            var count = 0;
            foreach (var state in fsm.FsmStates)
            {
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (field.FieldType == typeof(T))
                        {
                            var value = field.GetValue(action) as T;
                            if (value == null || !value.UseVariable || value.Name != globalName) continue;
                            field.SetValue(action, replacement);
                            count++;
                        }
                        else if (field.FieldType == typeof(T[]))
                        {
                            var array = field.GetValue(action) as T[];
                            if (array == null) continue;
                            for (int i = 0; i < array.Length; i++)
                            {
                                if (array[i] == null || !array[i].UseVariable || array[i].Name != globalName) continue;
                                array[i] = replacement;
                                count++;
                            }
                        }
                    }
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
