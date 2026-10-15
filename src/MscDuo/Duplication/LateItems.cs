using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class LateItems : MonoBehaviour
    {
        static readonly float[] Checks = { 4f, 10f, 20f };

        IEnumerator Start()
        {
            var elapsed = 0f;
            foreach (var at in Checks)
            {
                yield return new WaitForSeconds(at - elapsed);
                elapsed = at;
                try { Sync(); }
                catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] Satsuma 2: itens tardios: {e.Message}"); }
            }
        }

        static void Sync()
        {
            var map = SatsumaCloner.Map;
            var car1 = SatsumaCloner.Original;
            if (map == null || car1 == null || SatsumaCloner.Clone == null) return;

            var copies = new List<GameObject>();
            foreach (var t in car1.GetComponentsInChildren<Transform>(true))
            {
                if (map.ContainsKey(t.gameObject) || t.parent == null || !IsItem(t)) continue;
                Object parent;
                if (!map.TryGetValue(t.parent.gameObject, out parent)) continue;
                var copy = (GameObject)Instantiate(t.gameObject);
                copy.name = t.name;
                copy.transform.parent = ((GameObject)parent).transform;
                copy.transform.localPosition = t.localPosition;
                copy.transform.localRotation = t.localRotation;
                copy.transform.localScale = t.localScale;
                SatsumaCloner.MapHierarchy(t, copy.transform, map);
                copies.Add(copy);
            }

            var remapped = 0;
            foreach (var copy in copies)
                foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    FsmTools.EnsureLoaded(fsm);
                    remapped += SatsumaCloner.Remap(fsm, map);
                    if (SatsumaCloner.Variables != null)
                        foreach (var pair in SatsumaCloner.Variables) FsmTools.RedirectGlobal(fsm, pair.Key, pair.Value);
                }

            var records = SyncDatabase(map);
            var physics = SyncPhysics(car1, map);
            if (copies.Count > 0 || records > 0)
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {copies.Count} itens tardios copiados ({Names(copies)}), {records} fichas atualizadas, {remapped} referências.");
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {physics}");
        }

        static readonly string[] WheelSettings =
        {
            "pressure", "optimalPressure", "tirePuncture", "mass", "originalMass", "radius", "rimRadius", "width",
            "suspensionTravel", "suspensionRate", "bumpRate", "reboundRate", "fastBumpFactor", "fastReboundFactor",
            "rotationalInertia", "rollingFrictionCoefficient", "gripMaterial", "sidewaysGripFactor", "forwardGripFactor",
            "gripPressure", "maxSteeringAngle", "isPowered",
        };

        static string SyncPhysics(GameObject car1, Dictionary<Object, Object> map)
        {
            var wheels = 0;
            foreach (var wheel1 in car1.GetComponentsInChildren<Wheel>(true))
            {
                Object mapped;
                if (!map.TryGetValue(wheel1, out mapped)) continue;
                var wheel2 = (Wheel)mapped;
                foreach (var name in WheelSettings)
                {
                    var field = typeof(Wheel).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null) field.SetValue(wheel2, field.GetValue(wheel1));
                }
                wheel2.SetTireStiffness();
                wheels++;
            }

            var dynamics1 = car1.GetComponent<CarDynamics>();
            Object dynamics;
            var center = "-";
            if (dynamics1 != null && dynamics1.centerOfMass != null && map.TryGetValue(dynamics1, out dynamics))
            {
                var dynamics2 = (CarDynamics)dynamics;
                var original = typeof(CarDynamics).GetField("originalCenterOfMass", BindingFlags.NonPublic | BindingFlags.Instance);
                if (original != null) original.SetValue(dynamics2, original.GetValue(dynamics1));
                dynamics2.SetCenterOfMass(dynamics1.centerOfMass.localPosition);
                center = dynamics1.centerOfMass.localPosition.ToString();
            }
            return $"{wheels} rodas com pneus recalculados como na Satsuma 1, centro de massa em {center}.";
        }

        static bool IsItem(Transform t) =>
            t.name.EndsWith("(Clone)") && t.GetComponent<PlayMakerFSM>() != null && t.GetComponentInParent<Rigidbody>() != null;

        static int SyncDatabase(Dictionary<Object, Object> map)
        {
            var db1 = SatsumaCloner.OriginalDatabase;
            if (db1 == null) return 0;
            var count = 0;
            foreach (var fsm in db1.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != "Data") continue;
                var installed1 = fsm.FsmVariables.FindFsmBool("Installed");
                Object mapped;
                if (installed1 == null || !installed1.Value || !map.TryGetValue(fsm, out mapped)) continue;
                var fsm2 = (PlayMakerFSM)mapped;
                var installed2 = fsm2.FsmVariables.FindFsmBool("Installed");
                if (installed2 == null || installed2.Value) continue;

                foreach (var v in fsm.FsmVariables.FloatVariables) { var o = fsm2.FsmVariables.FindFsmFloat(v.Name); if (o != null) o.Value = v.Value; }
                foreach (var v in fsm.FsmVariables.BoolVariables) { var o = fsm2.FsmVariables.FindFsmBool(v.Name); if (o != null) o.Value = v.Value; }
                foreach (var v in fsm.FsmVariables.IntVariables) { var o = fsm2.FsmVariables.FindFsmInt(v.Name); if (o != null) o.Value = v.Value; }
                foreach (var v in fsm.FsmVariables.GameObjectVariables)
                {
                    var o = fsm2.FsmVariables.FindFsmGameObject(v.Name);
                    if (o == null || v.Value == null) continue;
                    o.Value = map.TryGetValue(v.Value, out mapped) ? (GameObject)mapped : v.Value;
                }
                count++;
            }
            return count;
        }

        static string Names(List<GameObject> copies)
        {
            var names = new List<string>();
            foreach (var c in copies) if (!names.Contains(c.name)) names.Add(c.name);
            return string.Join(", ", names.ToArray());
        }
    }
}
