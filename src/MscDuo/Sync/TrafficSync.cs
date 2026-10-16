using System.Collections.Generic;
using System.IO;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class TrafficSync
    {
        const float SendInterval = 0.1f;
        const float MaxExtrapolation = 0.3f;
        const int MaxSnapshots = 12;
        const string KeptFsm = "LOD";

        static readonly string[] Roots = { "TRAFFIC", "NPC_CARS" };

        struct Snapshot
        {
            internal float Time;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Velocity;
            internal float Rpm;
        }

        class Car
        {
            internal Rigidbody Body;
            internal Drivetrain Drivetrain;
            internal readonly List<Snapshot> Snapshots = new List<Snapshot>();
            internal readonly List<Behaviour> Disabled = new List<Behaviour>();
            internal readonly List<GameObject> Hidden = new List<GameObject>();
            internal bool WasKinematic;
            internal bool RemoteActive = true;
        }

        static readonly List<Car> cars = new List<Car>();
        static bool takenOver;
        static float nextSend;
        static int remoteCount = -1;
        static bool warned;

        internal static int Count => cars.Count;
        internal static bool TakenOver => takenOver;

        internal static string[] RootNames => Roots;

        internal static void Build()
        {
            Restore();
            cars.Clear();
            var found = new List<KeyValuePair<string, Rigidbody>>();
            foreach (var name in Roots)
            {
                var root = SatsumaCloner.FindRoot(name);
                if (root == null) continue;
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (body.GetComponent<CarDynamics>() == null && body.GetComponent<MobileCarController>() == null && !body.name.StartsWith("AIboat")) continue;
                    found.Add(new KeyValuePair<string, Rigidbody>(Key(body.transform), body));
                }
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (var pair in found)
                cars.Add(new Car { Body = pair.Value, Drivetrain = pair.Value.GetComponent<Drivetrain>() });
            remoteCount = -1;
            warned = false;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> trânsito: {cars.Count} veículos de NPC registrados.");
        }

        internal static void Tick(float now, float remoteNow, float delay, bool isHost, bool connected, System.Action<System.Action<BinaryWriter>> send)
        {
            if (cars.Count == 0) return;
            if (isHost)
            {
                if (!connected || now < nextSend) return;
                nextSend = now + SendInterval;
                send(w => Write(w, now));
                return;
            }
            if (!connected)
            {
                if (takenOver) Restore();
                return;
            }
            if (remoteCount != cars.Count) return;
            if (!takenOver) TakeOver();
            var renderTime = remoteNow - delay;
            foreach (var car in cars) Apply(car, renderTime);
        }

        static void Write(BinaryWriter w, float now)
        {
            w.Write(now);
            w.Write((ushort)cars.Count);
            for (int i = 0; i < cars.Count; i++)
            {
                var body = cars[i].Body;
                var active = body != null && body.gameObject.activeInHierarchy;
                w.Write(active);
                if (!active) continue;
                Write(w, body.position);
                w.Write(body.rotation.x);
                w.Write(body.rotation.y);
                w.Write(body.rotation.z);
                w.Write(body.rotation.w);
                Write(w, body.velocity);
                var drivetrain = cars[i].Drivetrain;
                w.Write(drivetrain != null ? drivetrain.rpm : 0f);
            }
        }

        internal static void Read(BinaryReader r)
        {
            var time = r.ReadSingle();
            var count = r.ReadUInt16();
            remoteCount = count;
            if (count != cars.Count)
            {
                if (!warned) ModConsole.Warning($"[MSC Duo] trânsito diferente ({cars.Count} veículos aqui, {count} no anfitrião); trânsito não sincronizado.");
                warned = true;
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var car = cars[i];
                car.RemoteActive = r.ReadBoolean();
                if (!car.RemoteActive) continue;
                var snapshot = new Snapshot
                {
                    Time = time,
                    Position = ReadVector(r),
                    Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                    Velocity = ReadVector(r),
                    Rpm = r.ReadSingle(),
                };
                if (car.Snapshots.Count > 0 && car.Snapshots[car.Snapshots.Count - 1].Time >= time) continue;
                car.Snapshots.Add(snapshot);
                if (car.Snapshots.Count > MaxSnapshots) car.Snapshots.RemoveAt(0);
            }
        }

        static void TakeOver()
        {
            foreach (var car in cars)
            {
                if (car.Body == null) continue;
                var go = car.Body.gameObject;
                car.WasKinematic = car.Body.isKinematic;
                car.Body.isKinematic = true;
                foreach (var fsm in go.GetComponents<PlayMakerFSM>())
                    if (fsm.enabled && fsm.FsmName != KeptFsm) { fsm.enabled = false; car.Disabled.Add(fsm); }
                foreach (var behaviour in go.GetComponents<Behaviour>())
                    if (behaviour.enabled && (behaviour is MobileCarController || behaviour is Drivetrain)) { behaviour.enabled = false; car.Disabled.Add(behaviour); }
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    if (child.activeSelf && child.name.StartsWith("Navigation")) { child.SetActive(false); car.Hidden.Add(child); }
                }
            }
            takenOver = true;
            ModConsole.Print("<color=orange>[MSC Duo]</color> trânsito agora segue o do anfitrião.");
        }

        internal static void Restore()
        {
            if (!takenOver) return;
            foreach (var car in cars)
            {
                if (car.Body == null) continue;
                car.Body.isKinematic = car.WasKinematic;
                foreach (var behaviour in car.Disabled) if (behaviour != null) behaviour.enabled = true;
                foreach (var child in car.Hidden) if (child != null) child.SetActive(true);
                car.Disabled.Clear();
                car.Hidden.Clear();
                car.Snapshots.Clear();
            }
            takenOver = false;
            remoteCount = -1;
            ModConsole.Print("<color=orange>[MSC Duo]</color> trânsito voltou a ser deste jogo.");
        }

        static void Apply(Car car, float renderTime)
        {
            if (car.Body == null) return;
            var go = car.Body.gameObject;
            if (car.RemoteActive)
                for (var parent = go.transform.parent; parent != null; parent = parent.parent)
                    if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
            if (go.activeSelf != car.RemoteActive) go.SetActive(car.RemoteActive);
            if (!car.RemoteActive || car.Snapshots.Count == 0) return;
            var snaps = car.Snapshots;
            var last = snaps[snaps.Count - 1];
            Vector3 position;
            Quaternion rotation;
            if (renderTime >= last.Time)
            {
                position = last.Position + last.Velocity * Mathf.Min(renderTime - last.Time, MaxExtrapolation);
                rotation = last.Rotation;
            }
            else
            {
                var i = snaps.Count - 1;
                while (i > 0 && snaps[i - 1].Time > renderTime) i--;
                if (i == 0)
                {
                    position = snaps[0].Position;
                    rotation = snaps[0].Rotation;
                }
                else
                {
                    var t = Mathf.InverseLerp(snaps[i - 1].Time, snaps[i].Time, renderTime);
                    position = Vector3.Lerp(snaps[i - 1].Position, snaps[i].Position, t);
                    rotation = Quaternion.Slerp(snaps[i - 1].Rotation, snaps[i].Rotation, t);
                }
            }
            go.transform.position = position;
            go.transform.rotation = rotation;
            if (car.Drivetrain != null) car.Drivetrain.rpm = last.Rpm;
        }

        static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        static Vector3 ReadVector(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        static string Key(Transform t)
        {
            var chain = new List<string>();
            for (var c = t; c != null; c = c.parent)
                chain.Add(c.parent != null ? c.GetSiblingIndex() + "." + c.name : c.name);
            chain.Reverse();
            return string.Join("/", chain.ToArray());
        }
    }
}
