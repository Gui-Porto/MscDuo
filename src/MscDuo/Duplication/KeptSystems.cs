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

        internal static bool Keeps(Transform t) => IsDoorPart(t) || IsLightSwitch(t) || IsLamp(t);

        internal static void Prepare(GameObject clone)
        {
            var switches = 0;
            foreach (var fsm in clone.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (!IsLightSwitch(fsm.transform)) continue;
                var tag = fsm.FsmVariables.GetFsmString("UniqueTagSwitch");
                if (tag == null || tag.Value.StartsWith(SaveTagPrefix)) continue;
                tag.Value = SaveTagPrefix + tag.Value;
                switches++;
            }

            var lamps = 0;
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

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: {switches} interruptores e {lamps} lâmpadas ativos.");
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
