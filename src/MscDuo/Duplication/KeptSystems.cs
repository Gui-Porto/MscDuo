using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class KeptSystems
    {
        internal const string SaveTagPrefix = "P2_";
        const string Appliances = "ElectricAppliances";
        const string Switches = "LightSwitches";
        const string ConsumptionGlobal = "HouseElectricityKWH";
        const string ConsumptionLocal = "MscDuoHouse2Kwh";

        static readonly HashSet<string> KeptSubtrees = new HashSet<string>
        {
            "Shower", "KitchenWaterTap", "Sauna", "GarageDoors", "StoveBuzz", "FireSauna",
        };

        internal static bool Keeps(Transform t) =>
            IsDoorPart(t) || IsLightSwitch(t) || IsLamp(t) || t.name == Appliances || InKeptSubtree(t);

        internal static void Prepare(GameObject clone)
        {
            int tags = 0, redirected = 0, lamps = 0;
            var meter = clone.AddComponent<House2Meter>();

            foreach (var fsm in clone.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (!Keeps(fsm.transform)) continue;
                tags += PrefixSaveTags(fsm);
                redirected += RedirectConsumption(fsm, meter);
            }

            foreach (var light in clone.GetComponentsInChildren<Light>(true))
            {
                if (!IsLamp(light.transform)) continue;
                light.gameObject.SetActive(true);
                lamps++;
            }

            foreach (var t in clone.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == Appliances) t.gameObject.SetActive(true);
                if (t.parent != null && t.parent.name == Appliances && LightConsumption.RateFor(t.name) > 0f)
                    t.gameObject.AddComponent<LightConsumption>();
            }

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: {lamps} lâmpadas, {tags} etiquetas de save separadas, {redirected} consumos desviados para a conta da casa 2.");
        }

        static int PrefixSaveTags(PlayMakerFSM fsm)
        {
            var count = 0;
            foreach (var s in fsm.FsmVariables.StringVariables)
            {
                if (!s.Name.StartsWith("UniqueTag") || string.IsNullOrEmpty(s.Value) || s.Value.StartsWith(SaveTagPrefix)) continue;
                s.Value = SaveTagPrefix + s.Value;
                count++;
            }
            return count;
        }

        static int RedirectConsumption(PlayMakerFSM fsm, House2Meter meter)
        {
            FsmFloat local = null;
            var count = 0;
            foreach (var state in fsm.FsmStates)
            {
                if (!state.ActionsLoaded) state.LoadActions();
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (field.FieldType != typeof(FsmFloat)) continue;
                        var value = field.GetValue(action) as FsmFloat;
                        if (value == null || !value.UseVariable || value.Name != ConsumptionGlobal) continue;
                        if (local == null)
                        {
                            local = new FsmFloat(ConsumptionLocal) { Value = 0f };
                            fsm.AddVariable(local);
                            meter.Track(local);
                        }
                        field.SetValue(action, local);
                        count++;
                    }
                }
            }
            return count;
        }

        static bool InKeptSubtree(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (KeptSubtrees.Contains(t.name)) return true;
                if (t.name == "Building_P2") return false;
            }
            return false;
        }

        static bool IsDoorPart(Transform t)
        {
            for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
            {
                if (t.name != "Pivot" || t.parent == null) continue;
                return t.parent.name.StartsWith("Door");
            }
            return false;
        }

        static bool IsLightSwitch(Transform t) =>
            t.parent != null && t.parent.name == Switches;

        static bool IsLamp(Transform t)
        {
            if (!t.name.StartsWith("Light") || t.name == Switches) return false;
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == Appliances) return true;
            return false;
        }
    }

    internal class House2Meter : MonoBehaviour
    {
        readonly List<FsmFloat> tracked = new List<FsmFloat>();

        internal void Track(FsmFloat value) => tracked.Add(value);

        void Update()
        {
            foreach (var value in tracked)
            {
                if (value.Value == 0f) continue;
                House2Power.Kwh += value.Value;
                value.Value = 0f;
            }
        }
    }

    internal class LightConsumption : MonoBehaviour
    {
        internal static float RateFor(string room)
        {
            switch (room)
            {
                case "Garage": return 0.005f;
                case "Bathroom":
                case "Livingroom": return 0.0028f;
                case "Kitchen":
                case "BedroomBoy":
                case "BedroomParents":
                case "Entry":
                case "WC": return 0.0014f;
                default:
                    return 0f;
            }
        }

        float rate;

        void Awake()
        {
            rate = RateFor(name);
        }

        void Update()
        {
            House2Power.Kwh += rate * Time.deltaTime;
        }
    }

    internal static class House2Power
    {
        const string SaveKey = "House2Kwh";

        internal static float Kwh;

        internal static void Load()
        {
            Kwh = SaveLoad.ValueExists(MscDuo.Instance, SaveKey) ? SaveLoad.ReadValue<float>(MscDuo.Instance, SaveKey) : 0f;
        }

        internal static void Save()
        {
            SaveLoad.WriteValue(MscDuo.Instance, SaveKey, Kwh);
        }
    }
}
