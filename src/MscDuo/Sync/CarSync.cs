using System;
using System.Collections.Generic;
using MscDuo.Duplication;
using MscDuo.Net;
using MscDuo.Player;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class CarSync
    {
        const float DriverTimeout = 0.5f;
        const float StateTimeout = 1f;
        const float MaxExtrapolation = 0.25f;
        const float MoveThreshold = 0.1f;
        const float SettleSeconds = 2f;
        const float RootScanInterval = 10f;
        const float YieldSeconds = 1f;
        const int MaxSnapshots = 32;

        internal static readonly string[] Vehicles = BuildVehicles();

        static string[] BuildVehicles()
        {
            var names = new List<string>
            {
                "JONNEZ ES(Clone)", "HAYOSIKO(1500kg, 250)", "GIFU(750/450psi)", "KEKMET(350-400psi)", "FERNDALE(1630kg)",
                "RCO_RUSCKO12(270)", "FLATBED", "BOAT", "COMBINE(350-400psi)",
            };
            foreach (var spec in VehicleCloner.Specs) names.Add(spec.Root + VehicleCloner.Suffix);
            return names.ToArray();
        }

        static readonly List<CarLink> links = new List<CarLink>();
        static readonly Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
        static float nextRootScan;
        static Transform player;

        internal static IEnumerable<CarLink> Links => links;

        static CarSync()
        {
            links.Add(new CarLink(1, "Satsuma 1", null));
            links.Add(new CarLink(2, "Satsuma 2", null));
            for (int i = 0; i < Vehicles.Length; i++) links.Add(new CarLink(3 + i, Short(Vehicles[i]), Vehicles[i]));
        }

        internal static void Tick(float now, float remoteNow, float delay, Vector3? remotePlayer, bool isHost, bool sendDue, Action<CarStateMessage> broadcast)
        {
            if (player == null)
            {
                var go = GameObject.Find("PLAYER");
                if (go != null) player = go.transform;
            }
            foreach (var link in links)
            {
                if (!link.Resolve(now)) continue;
                var inside = link.Owned ? CurrentVehicle.Current == link.Index : player != null && player.IsChildOf(link.Root);
                var peerDriving = now < link.PeerDrivingUntil;
                if (inside) link.LastLocalDriver = true;
                if (peerDriving) link.LastLocalDriver = false;

                if (link.Owned)
                    link.Sending = inside || (link.Index == PlayerContext.Active && !peerDriving);
                else
                {
                    var moving = link.Moving;
                    if (moving) link.LastMoving = now;
                    var settling = now - link.LastMoving < SettleSeconds;
                    var nearer = player != null && (!remotePlayer.HasValue ||
                        (player.position - link.Root.position).sqrMagnitude < (remotePlayer.Value - link.Root.position).sqrMagnitude);
                    var receiving = link.Snapshots.Count > 0 && now - link.LastArrival < StateTimeout;
                    link.Sending = inside || (!peerDriving && now >= link.YieldUntil &&
                        (link.LastLocalDriver && settling || moving && nearer && !receiving));
                    if (!link.Sending && !settling) link.LastLocalDriver = false;
                }

                if (link.Sending)
                {
                    link.Release();
                    if (sendDue) broadcast(link.Capture(now, inside));
                }
                else if (link.Snapshots.Count > 0 && now - link.LastArrival < StateTimeout)
                    link.Apply(remoteNow - delay);
                else
                    link.Release();
            }
        }

        internal static void Receive(CarStateMessage state, float now, bool isHost)
        {
            var link = Find(state.Car);
            if (link == null) return;
            if (state.Driving) link.PeerDrivingUntil = now + DriverTimeout;
            if (link.Sending)
            {
                var inside = link.Owned ? CurrentVehicle.Current == link.Index : player != null && link.Root != null && player.IsChildOf(link.Root);
                if (link.Owned || inside || (!state.Driving && isHost)) return;
                link.YieldUntil = now + YieldSeconds;
                link.LastLocalDriver = false;
                link.Sending = false;
            }
            if (state.Sequence <= link.LastSequence) return;
            link.LastSequence = state.Sequence;
            link.LastArrival = now;
            link.Snapshots.Add(state);
            if (link.Snapshots.Count > MaxSnapshots) link.Snapshots.RemoveAt(0);
        }

        internal static void Reset()
        {
            foreach (var link in links)
            {
                link.Release();
                link.LastSequence = 0;
                link.PeerDrivingUntil = 0f;
                link.YieldUntil = 0f;
                link.LastLocalDriver = false;
                link.Sending = false;
            }
        }

        internal static Dictionary<string, GameObject> FindRoots()
        {
            var found = new Dictionary<string, GameObject>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null && t.hideFlags == HideFlags.None && Array.IndexOf(Vehicles, t.name) >= 0 && !found.ContainsKey(t.name))
                    found[t.name] = t.gameObject;
            return found;
        }

        static GameObject RootOf(string name, float now)
        {
            GameObject go;
            if (roots.TryGetValue(name, out go) && go != null) return go;
            if (now < nextRootScan) return null;
            nextRootScan = now + RootScanInterval;
            roots.Clear();
            foreach (var pair in FindRoots()) roots[pair.Key] = pair.Value;
            return roots.TryGetValue(name, out go) ? go : null;
        }

        static CarLink Find(byte index)
        {
            foreach (var link in links)
                if (link.Index == index) return link;
            return null;
        }

        static string Short(string name)
        {
            var copy = name.EndsWith(VehicleCloner.Suffix);
            var cut = name.IndexOf('(');
            var text = cut > 0 ? name.Substring(0, cut) : copy ? name.Substring(0, name.Length - VehicleCloner.Suffix.Length) : name;
            return text.Trim().ToLowerInvariant() + (copy ? " 2" : "");
        }

        internal class CarLink
        {
            internal readonly int Index;
            internal readonly string Label;
            internal readonly string RootName;
            internal readonly List<CarStateMessage> Snapshots = new List<CarStateMessage>();
            internal bool Sending;
            internal float PeerDrivingUntil;
            internal float YieldUntil;
            internal float LastArrival;
            internal float LastMoving = -100f;
            internal bool LastLocalDriver;
            internal uint LastSequence;
            internal bool Applying { get; private set; }
            internal bool Owned => RootName == null;
            internal Transform Root => car != null ? car.transform : null;
            internal bool Moving => body != null && (body.velocity.sqrMagnitude > MoveThreshold * MoveThreshold || body.angularVelocity.sqrMagnitude > MoveThreshold);
            GameObject car;
            Rigidbody body;
            Drivetrain drivetrain;
            bool wasKinematic;
            bool drivetrainWasEnabled;
            uint sequence;

            internal CarLink(int index, string label, string rootName)
            {
                Index = index;
                Label = label;
                RootName = rootName;
            }

            internal bool Resolve(float now)
            {
                GameObject current;
                if (Owned) current = Index == 1 ? SatsumaCloner.Original : SatsumaCloner.Clone;
                else if (car != null) current = car;
                else current = RootOf(RootName, now);
                if (current != car)
                {
                    Release();
                    car = current;
                    body = car != null ? car.GetComponent<Rigidbody>() : null;
                    drivetrain = car != null ? car.GetComponentInChildren<Drivetrain>() : null;
                }
                return body != null && car.activeInHierarchy;
            }

            internal CarStateMessage Capture(float now, bool driving) => new CarStateMessage
            {
                Car = (byte)Index,
                Sequence = ++sequence,
                Time = now,
                Position = body.position,
                Rotation = body.rotation,
                Velocity = body.velocity,
                AngularVelocity = body.angularVelocity,
                Driving = driving,
                Rpm = drivetrain != null && drivetrain.enabled ? drivetrain.rpm : 0f,
                Throttle = drivetrain != null && drivetrain.enabled ? drivetrain.throttle : 0f,
            };

            internal void Apply(float renderTime)
            {
                if (!Applying)
                {
                    wasKinematic = body.isKinematic;
                    body.isKinematic = true;
                    if (drivetrain != null)
                    {
                        drivetrainWasEnabled = drivetrain.enabled;
                        drivetrain.enabled = false;
                    }
                    Applying = true;
                }

                CarStateMessage a, b;
                var i = Snapshots.Count - 1;
                while (i > 0 && Snapshots[i - 1].Time > renderTime) i--;
                Vector3 position;
                Quaternion rotation;
                var latest = Snapshots[Snapshots.Count - 1];
                if (renderTime >= latest.Time)
                {
                    b = latest;
                    var ahead = Mathf.Min(renderTime - b.Time, MaxExtrapolation);
                    position = b.Position + b.Velocity * ahead;
                    var spin = b.AngularVelocity.magnitude;
                    rotation = spin > 0.0001f ? Quaternion.AngleAxis(spin * Mathf.Rad2Deg * ahead, b.AngularVelocity / spin) * b.Rotation : b.Rotation;
                }
                else if (i == 0)
                {
                    position = Snapshots[0].Position;
                    rotation = Snapshots[0].Rotation;
                }
                else
                {
                    a = Snapshots[i - 1];
                    b = Snapshots[i];
                    var t = Mathf.InverseLerp(a.Time, b.Time, renderTime);
                    position = Vector3.Lerp(a.Position, b.Position, t);
                    rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
                }

                car.transform.position = position;
                car.transform.rotation = rotation;
                if (drivetrain != null)
                {
                    drivetrain.rpm = latest.Rpm;
                    drivetrain.throttle = latest.Throttle;
                }
            }

            internal void Release()
            {
                if (Applying && body != null)
                {
                    body.isKinematic = wasKinematic;
                    if (!wasKinematic && Snapshots.Count > 0)
                    {
                        var last = Snapshots[Snapshots.Count - 1];
                        body.velocity = last.Velocity;
                        body.angularVelocity = last.AngularVelocity;
                    }
                    if (drivetrain != null) drivetrain.enabled = drivetrainWasEnabled;
                }
                Applying = false;
                Snapshots.Clear();
            }
        }
    }
}
