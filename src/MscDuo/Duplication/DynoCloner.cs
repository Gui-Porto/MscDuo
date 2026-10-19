using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Net;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class DynoCloner
    {
        internal const string Prefix = "Galpao_Dinamometro";
        const string VisualPath = "REPAIRSHOP/LOD/Dyno";
        const string CollidersPath = "REPAIRSHOP/Dyno";
        const string MeasurePath = "Sheets/DynoSheet/Dyno/Measure";
        const float FarEnd = 11.7f;
        const float WaitSeconds = 180f;
        static readonly float[] Bays = { 4.5f, 19.5f };
        static readonly HashSet<int> injected = new HashSet<int>();

        internal static bool InDyno(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith(Prefix)) return true;
            return false;
        }

        internal static void Build(Transform root)
        {
            var visual = Find(VisualPath);
            var colliders = Find(CollidersPath);
            if (visual == null || colliders == null)
            {
                ModConsole.Warning("[MSC Duo] dinamômetro da oficina não encontrado; galpão sem dinamômetros.");
                return;
            }
            InjectCar(visual.transform, SatsumaCloner.Original != null ? SatsumaCloner.Original : Find("SATSUMA(557kg, 248)"));
            Clone(root, visual, colliders, 0, null);
            root.gameObject.AddComponent<DynoWaiter>().Begin(root, visual, colliders);
        }

        class DynoWaiter : MonoBehaviour
        {
            internal void Begin(Transform root, GameObject visual, GameObject colliders) => StartCoroutine(Wait(root, visual, colliders));

            IEnumerator Wait(Transform root, GameObject visual, GameObject colliders)
            {
                var started = Time.realtimeSinceStartup;
                while (SatsumaCloner.Clone == null || SatsumaCloner.Map == null)
                {
                    if (Time.realtimeSinceStartup - started > WaitSeconds)
                    {
                        ModConsole.Warning("[MSC Duo] Satsuma 2 não apareceu; galpão sem o dinamômetro 2.");
                        yield break;
                    }
                    yield return new WaitForSeconds(1f);
                }
                try { Clone(root, visual, colliders, 1, SatsumaCloner.Map); }
                catch (System.Exception e) { ModConsole.Error($"[MSC Duo] dinamômetro 2 falhou: {e}"); }
            }
        }

        static void Clone(Transform root, GameObject visual, GameObject colliders, int index, Dictionary<Object, Object> carMap)
        {
            var attach = visual.transform.Find("AttachPoint");
            var computer = visual.transform.Find("dyno_computer");
            if (attach == null || computer == null) return;
            var floor = Lowest(computer);
            var forward = -visual.transform.right;
            forward.y = 0f;
            var turn = Quaternion.FromToRotation(forward.normalized, Warehouse.Local(0f, 1f, 0f) - Warehouse.Local(0f, 0f, 0f));
            var target = Warehouse.Local(Bays[index], 7.5f, 0f);

            var holder = new GameObject(Prefix + (index + 1));
            holder.transform.parent = root;
            var map = new Dictionary<Object, Object>();
            var copies = new List<GameObject>();
            foreach (var source in new[] { visual, colliders })
            {
                var offset = source.transform.position - attach.position;
                var position = target + turn * new Vector3(offset.x, 0f, offset.z);
                position.y = target.y + (source.transform.position.y - floor);
                var copy = (GameObject)Object.Instantiate(source, position, turn * source.transform.rotation);
                copy.name = source.name;
                copy.transform.parent = holder.transform;
                SatsumaCloner.MapHierarchy(source.transform, copy.transform, map);
                copies.Add(copy);
            }
            if (carMap != null) foreach (var pair in carMap) if (!map.ContainsKey(pair.Key)) map[pair.Key] = pair.Value;

            var far = float.MinValue;
            foreach (var copy in copies)
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.min.z) })
                        far = Mathf.Max(far, Warehouse.ToLocal(corner).y);
                }
            var shift = Warehouse.Local(0f, FarEnd - far, 0f) - Warehouse.Local(0f, 0f, 0f);
            foreach (var copy in copies) copy.transform.position += shift;

            int fsms = 0, remapped = 0;
            PlayMakerFSM clonedComputer = null;
            foreach (var copy in copies)
            {
                copy.SetActive(true);
                foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    FsmTools.EnsureLoaded(fsm);
                    StripHooks(fsm);
                    remapped += SatsumaCloner.Remap(fsm, map);
                    if (fsm.gameObject.name == "Computer" && fsm.FsmName == "Use") clonedComputer = fsm;
                    if (fsm.gameObject.name == "AttachPoint") copy.AddComponent<DynoOwner>().Setup(fsm, index);
                    fsms++;
                }
            }
            if (clonedComputer != null) MakeFree(clonedComputer);
            var car = carMap != null ? SatsumaCloner.Clone : (SatsumaCloner.Original != null ? SatsumaCloner.Original : Find("SATSUMA(557kg, 248)"));
            if (clonedComputer != null) InjectCar(clonedComputer, car);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> dinamômetro {index + 1} no galpão: {fsms} FSMs, {remapped} referências, ligado a {(car != null ? car.name : "nada")}.");
        }

        static void StripHooks(PlayMakerFSM fsm)
        {
            foreach (var state in fsm.FsmStates)
            {
                var kept = new List<FsmStateAction>();
                foreach (var action in state.Actions)
                    if (action != null && action.GetType().Name != "MSCL_PMHook") kept.Add(action);
                if (kept.Count != state.Actions.Length) state.Actions = kept.ToArray();
            }
        }

        static void MakeFree(PlayMakerFSM computer)
        {
            var time = computer.FsmVariables.FindFsmFloat("TimeBought");
            if (time != null) time.Value = 999999f;
            var on = computer.Fsm.GetState("On");
            if (on == null) return;
            foreach (var action in on.Actions)
                if (action != null && action.GetType().Name == "FloatSubtract") action.Enabled = false;
        }

        static void InjectCar(Transform dyno, GameObject car)
        {
            var computer = dyno.Find("dyno_computer/Computer");
            if (computer == null) return;
            foreach (var fsm in computer.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "Use") InjectCar(fsm, car);
        }

        static void InjectCar(PlayMakerFSM computer, GameObject car)
        {
            if (car == null || computer.Fsm.GetState("State 1") == null || !injected.Add(computer.GetInstanceID())) return;
            var drivetrain = car.GetComponent<Drivetrain>();
            computer.FsmInject("State 1", () => PointMeasureAt(drivetrain), false, 0, false);
        }

        static void PointMeasureAt(Drivetrain drivetrain)
        {
            var measure = Find(MeasurePath);
            if (measure == null || drivetrain == null) return;
            foreach (var fsm in measure.GetComponents<PlayMakerFSM>())
            {
                var variable = fsm.FsmVariables.FindFsmObject("CarDrivetrain");
                if (variable != null) variable.Value = drivetrain;
            }
        }

        static float Lowest(Transform t)
        {
            var low = float.MaxValue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) low = Mathf.Min(low, r.bounds.min.y);
            return low == float.MaxValue ? t.position.y : low;
        }

        static GameObject Find(string path)
        {
            var go = GameObject.Find(path);
            if (go != null) return go;
            var name = path.Substring(path.LastIndexOf('/') + 1);
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.hideFlags == HideFlags.None && t.name == name && PathOf(t) == path) return t.gameObject;
            return null;
        }

        static string PathOf(Transform t)
        {
            var p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        class DynoOwner : MonoBehaviour
        {
            PlayMakerFSM attach;
            int index;

            internal void Setup(PlayMakerFSM fsm, int dyno)
            {
                attach = fsm;
                index = dyno;
            }

            void Update()
            {
                if (attach == null) return;
                var session = NetSession.Current;
                var mine = session == null || !session.Connected || (index == 0) == session.Transport.IsHost;
                if (attach.enabled != mine) attach.enabled = mine;
            }
        }
    }
}
