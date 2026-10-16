using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class UncleVan
    {
        internal static void Lock(GameObject van)
        {
            var disabled = 0;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.gameObject.name != "UNCLE" || fsm.FsmName != "States") continue;
                FsmTools.EnsureLoaded(fsm);
                foreach (var state in fsm.FsmStates)
                    foreach (var action in state.Actions)
                    {
                        var move = action as SetPosition;
                        if (move == null || !action.Enabled || !Targets(move.gameObject, fsm, van)) continue;
                        action.Enabled = false;
                        disabled++;
                    }
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> tio não leva mais a van embora ({disabled} ações desligadas).");
        }

        static bool Targets(FsmOwnerDefault target, PlayMakerFSM fsm, GameObject van)
        {
            if (target == null || target.OwnerOption != OwnerDefaultOption.SpecifyGameObject || target.GameObject == null) return false;
            if (target.GameObject.UseVariable)
            {
                var variable = fsm.FsmVariables.FindFsmGameObject(target.GameObject.Name);
                return variable != null && (variable.Value == van || target.GameObject.Name == "Van");
            }
            return target.GameObject.Value == van;
        }
    }
}
