using System.Collections.Generic;
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

        class Pair
        {
            internal string Name;
            internal GameObject Car1;
            internal GameObject Car2;
            internal FsmString View1;
            internal FsmString View2;
        }

        static FsmString global;
        static readonly List<Pair> pairs = new List<Pair>();
        static Transform player;

        internal static int Current { get; private set; }

        internal static string Describe()
        {
            var text = $"PlayerCurrentVehicle='{GlobalValue}'";
            foreach (var pair in pairs) text += $"  {pair.Name}: 1='{pair.View1.Value}' 2='{pair.View2.Value}'";
            return text;
        }
        internal static string GlobalValue => global != null ? global.Value : "";

        internal static void Setup(GameObject satsuma1, GameObject parts1, GameObject satsuma2, GameObject parts2) =>
            Separate(Satsuma, satsuma1, satsuma2, new[] { satsuma1, parts1 }, new[] { satsuma2, parts2 });

        internal static void Separate(string name, GameObject car1, GameObject car2, GameObject[] roots1, GameObject[] roots2)
        {
            global = FsmVariables.GlobalVariables.FindFsmString(GlobalName);
            if (global == null)
            {
                ModConsole.Warning($"[MSC Duo] variável global '{GlobalName}' não encontrada; veículo atual não separado.");
                return;
            }
            pairs.RemoveAll(p => p.Name == name);
            var pair = new Pair
            {
                Name = name,
                Car1 = car1,
                Car2 = car2,
                View1 = new FsmString("MscDuo" + name + "1"),
                View2 = new FsmString("MscDuo" + name + "2"),
            };
            pairs.Add(pair);
            var redirected = Redirect(pair.View1, roots1) + Redirect(pair.View2, roots2);
            if (FindObjectOfType<CurrentVehicle>() == null) new GameObject("MscDuo_CurrentVehicle").AddComponent<CurrentVehicle>();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> veículo atual separado entre os dois {name} ({redirected} verificações).");
        }

        static int Redirect(FsmString view, GameObject[] roots)
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
            if (global == null) return;
            var value = global.Value;
            Current = 0;
            foreach (var pair in pairs)
            {
                var which = value == pair.Name ? Nearest(pair) : 0;
                if (pair.Name == Satsuma) Current = which;
                pair.View1.Value = value == pair.Name && which != 1 ? "MscDuoOther" : value;
                pair.View2.Value = value == pair.Name && which != 2 ? "MscDuoOther" : value;
            }
        }

        static int Nearest(Pair pair)
        {
            if (player == null)
            {
                var found = GameObject.Find("PLAYER");
                if (found == null) return 1;
                player = found.transform;
            }
            if (pair.Car2 == null) return 1;
            if (pair.Car1 == null) return 2;
            if (player.IsChildOf(pair.Car2.transform)) return 2;
            if (player.IsChildOf(pair.Car1.transform)) return 1;
            var d1 = (pair.Car1.transform.position - player.position).sqrMagnitude;
            var d2 = (pair.Car2.transform.position - player.position).sqrMagnitude;
            return d2 < d1 ? 2 : 1;
        }
    }
}
