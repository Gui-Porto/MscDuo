using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class CurrentVehicle : MonoBehaviour
    {
        const string GlobalName = "PlayerCurrentVehicle";
        const string Satsuma = "Satsuma";
        const string OtherSatsuma = "SatsumaOutra";

        static FsmString global;
        static readonly FsmString view1 = new FsmString("MscDuoVehicle1");
        static readonly FsmString view2 = new FsmString("MscDuoVehicle2");
        static GameObject car1;
        static GameObject car2;
        static Transform player;

        internal static int Current { get; private set; }
        internal static string GlobalValue => global != null ? global.Value : "";

        internal static void Setup(GameObject satsuma1, GameObject parts1, GameObject satsuma2, GameObject parts2)
        {
            global = FsmVariables.GlobalVariables.FindFsmString(GlobalName);
            if (global == null)
            {
                ModConsole.Warning($"[MSC Duo] variável global '{GlobalName}' não encontrada; veículo atual não separado.");
                return;
            }
            car1 = satsuma1;
            car2 = satsuma2;
            var redirected = Redirect(view1, satsuma1, parts1) + Redirect(view2, satsuma2, parts2);
            if (FindObjectOfType<CurrentVehicle>() == null) new GameObject("MscDuo_CurrentVehicle").AddComponent<CurrentVehicle>();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> veículo atual separado entre as duas Satsumas ({redirected} verificações).");
        }

        static int Redirect(FsmString view, params GameObject[] roots)
        {
            var count = 0;
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try
                    {
                        FsmTools.EnsureLoaded(fsm);
                        foreach (var state in fsm.FsmStates)
                            foreach (var action in state.Actions)
                            {
                                var compare = action as StringCompare;
                                if (compare == null || compare.stringVariable == null || !compare.stringVariable.UseVariable || compare.stringVariable.Name != GlobalName) continue;
                                compare.stringVariable = view;
                                count++;
                            }
                    }
                    catch (System.Exception e)
                    {
                        ModConsole.Warning($"[MSC Duo] veículo atual: '{fsm.gameObject.name}/{fsm.FsmName}' não separado: {e.Message}");
                    }
                }
            }
            return count;
        }

        void Update()
        {
            var value = global.Value;
            Current = value == Satsuma ? Nearest() : 0;
            view1.Value = value == Satsuma && Current != 1 ? OtherSatsuma : value;
            view2.Value = value == Satsuma && Current != 2 ? OtherSatsuma : value;
        }

        static int Nearest()
        {
            if (player == null)
            {
                var found = GameObject.Find("PLAYER");
                if (found == null) return 1;
                player = found.transform;
            }
            if (car2 == null) return 1;
            if (car1 == null) return 2;
            var d1 = (car1.transform.position - player.position).sqrMagnitude;
            var d2 = (car2.transform.position - player.position).sqrMagnitude;
            return d2 < d1 ? 2 : 1;
        }
    }
}
