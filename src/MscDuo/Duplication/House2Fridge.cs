using System.Reflection;
using Harmony;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class House2Fridge
    {
        const string FridgeGlobal = "FridgePoint1";

        internal static Transform Point;
        static bool patched;

        internal static void Patch()
        {
            if (patched) return;
            try
            {
                HarmonyInstance.Create("MscDuo.Fridge").PatchAll(Assembly.GetExecutingAssembly());
                patched = true;
            }
            catch (System.Exception e)
            {
                ModConsole.Warning($"[MSC Duo] geladeira da casa 2 não vai gelar: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(GetDistance), "DoGetDistance")]
        static class GetDistancePatch
        {
            static void Postfix(GetDistance __instance)
            {
                if (Point == null || __instance.storeResult == null) return;
                var target = __instance.target;
                if (target == null || !target.UseVariable || target.Name != FridgeGlobal) return;
                var owner = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
                if (owner == null) return;
                var distance = Vector3.Distance(owner.transform.position, Point.position);
                if (distance < __instance.storeResult.Value) __instance.storeResult.Value = distance;
            }
        }
    }
}
