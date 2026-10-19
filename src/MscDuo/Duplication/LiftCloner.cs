using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class LiftCloner
    {
        internal const string Prefix = "Galpao_Elevador";
        const string VisualPath = "REPAIRSHOP/LOD/Lifter";
        const string PhysicsPath = "REPAIRSHOP/Lifter";
        const float SendInterval = 0.1f;

        static LiftLink link;
        static Action<float, bool> sender;

        internal static void Bind(Action<float, bool> send) => sender = send;

        internal static void Write(BinaryWriter w)
        {
            w.Write(link != null ? link.Position : 0f);
            w.Write(false);
        }

        internal static void Read(BinaryReader r)
        {
            var position = r.ReadSingle();
            var moving = r.ReadBoolean();
            if (link != null) link.Receive(position, moving);
        }

        internal static void Build(Transform root)
        {
            link = null;
            var visual = DynoCloner.Find(VisualPath);
            var physics = DynoCloner.Find(PhysicsPath);
            var platform = physics != null ? physics.transform.Find("Platform") : null;
            if (visual == null || platform == null)
            {
                ModConsole.Warning("[MSC Duo] elevador da oficina não encontrado; galpão sem elevador.");
                return;
            }
            var floor = DynoCloner.Lowest(visual.transform);
            var length = LongAxis(platform);
            var depth = Warehouse.Local(0f, 1f, 0f) - Warehouse.Local(0f, 0f, 0f);
            var turnA = Quaternion.FromToRotation(length, depth);
            var turnB = Quaternion.FromToRotation(-length, depth);
            var turn = Quaternion.Angle(Quaternion.identity, turnA) <= Quaternion.Angle(Quaternion.identity, turnB) ? turnA : turnB;
            var target = Warehouse.Local(12f, WarehouseConfig.Get("elevador_y"), 0f);

            var holder = new GameObject(Prefix);
            holder.transform.parent = root;
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var copies = new List<GameObject>();
            foreach (var source in new[] { visual, physics })
            {
                var offset = source.transform.position - platform.position;
                var position = target + turn * new Vector3(offset.x, 0f, offset.z);
                position.y = target.y + (source.transform.position.y - floor);
                var copy = (GameObject)UnityEngine.Object.Instantiate(source, position, turn * source.transform.rotation);
                copy.name = source.name;
                copy.transform.parent = holder.transform;
                SatsumaCloner.MapHierarchy(source.transform, copy.transform, map);
                copies.Add(copy);
            }

            PlayMakerFSM functions = null;
            int fsms = 0, remapped = 0;
            foreach (var copy in copies)
            {
                copy.SetActive(true);
                foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    FsmTools.EnsureLoaded(fsm);
                    DynoCloner.StripHooks(fsm);
                    remapped += SatsumaCloner.Remap(fsm, map);
                    if (fsm.gameObject.name == "Functions" && fsm.FsmName == "Use") functions = fsm;
                    fsms++;
                }
            }
            if (functions != null)
            {
                link = holder.AddComponent<LiftLink>();
                link.Setup(functions);
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> elevador no galpão: {fsms} FSMs, {remapped} referências, {(link != null ? "sincronizado" : "sem sincronia")}.");
        }

        static Vector3 LongAxis(Transform platform)
        {
            var best = Vector3.zero;
            var size = 0f;
            foreach (var box in platform.GetComponentsInChildren<BoxCollider>(true))
            {
                var s = Vector3.Scale(box.size, box.transform.lossyScale);
                var axes = new[] { box.transform.right * Mathf.Abs(s.x), box.transform.up * Mathf.Abs(s.y), box.transform.forward * Mathf.Abs(s.z) };
                foreach (var axis in axes)
                {
                    var flat = new Vector3(axis.x, 0f, axis.z);
                    if (flat.magnitude > size) { size = flat.magnitude; best = flat; }
                }
            }
            return best.sqrMagnitude > 0f ? best.normalized : Vector3.forward;
        }

        class LiftLink : MonoBehaviour
        {
            PlayMakerFSM fsm;
            FsmFloat position;
            Transform platform;
            GameObject sound;
            bool operating;
            float nextSend;
            float remoteUntil;

            internal float Position => position != null ? position.Value : 0f;

            internal void Setup(PlayMakerFSM functions)
            {
                fsm = functions;
                position = fsm.FsmVariables.FindFsmFloat("PlatformPos");
                var p = fsm.FsmVariables.FindFsmGameObject("Platform");
                platform = p != null && p.Value != null ? p.Value.transform : null;
                var s = fsm.FsmVariables.FindFsmGameObject("LifterSound");
                sound = s != null ? s.Value : null;
            }

            void Update()
            {
                if (fsm == null || position == null) return;
                var now = Time.time;
                var active = fsm.ActiveStateName == "Lift up" || fsm.ActiveStateName == "Lift down";
                if (active)
                {
                    remoteUntil = 0f;
                    if (now >= nextSend)
                    {
                        nextSend = now + SendInterval;
                        if (sender != null) sender(position.Value, true);
                    }
                }
                else
                {
                    if (operating && sender != null) sender(position.Value, false);
                    if (sound != null && remoteUntil > 0f && now > remoteUntil)
                    {
                        sound.SetActive(false);
                        remoteUntil = 0f;
                    }
                }
                operating = active;
            }

            internal void Receive(float value, bool moving)
            {
                if (operating || position == null) return;
                position.Value = Mathf.Clamp(value, 0f, 1.67f);
                if (platform != null)
                {
                    var local = platform.localPosition;
                    platform.localPosition = new Vector3(local.x, local.y, position.Value);
                }
                if (sound != null && moving)
                {
                    sound.SetActive(true);
                    remoteUntil = Time.time + 0.3f;
                }
            }
        }
    }
}
