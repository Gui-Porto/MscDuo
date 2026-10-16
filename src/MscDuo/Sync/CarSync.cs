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
        const int MaxSnapshots = 32;

        static readonly CarLink[] links = { new CarLink(1), new CarLink(2) };

        internal static IEnumerable<CarLink> Links => links;

        internal static void Tick(float now, float remoteNow, float delay, bool sendDue, Action<CarStateMessage> broadcast)
        {
            foreach (var link in links)
            {
                if (!link.Resolve()) continue;
                var driving = CurrentVehicle.Current == link.Index;
                var peerDriving = now < link.PeerDrivingUntil;
                link.Sending = driving || (link.Index == PlayerContext.Active && !peerDriving);

                if (link.Sending)
                {
                    link.Release();
                    if (sendDue) broadcast(link.Capture(now, driving));
                }
                else if (link.Snapshots.Count > 0 && now - link.LastArrival < StateTimeout)
                    link.Apply(remoteNow - delay);
                else
                    link.Release();
            }
        }

        internal static void Receive(CarStateMessage state, float now)
        {
            if (state.Car < 1 || state.Car > links.Length) return;
            var link = links[state.Car - 1];
            if (state.Driving) link.PeerDrivingUntil = now + DriverTimeout;
            if (link.Sending || state.Sequence <= link.LastSequence) return;
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
                link.Sending = false;
            }
        }

        internal class CarLink
        {
            internal readonly int Index;
            internal readonly List<CarStateMessage> Snapshots = new List<CarStateMessage>();
            internal bool Sending;
            internal float PeerDrivingUntil;
            internal float LastArrival;
            internal uint LastSequence;
            internal bool Applying { get; private set; }
            GameObject car;
            Rigidbody body;
            bool wasKinematic;
            uint sequence;

            internal CarLink(int index)
            {
                Index = index;
            }

            internal bool Resolve()
            {
                var current = Index == 1 ? SatsumaCloner.Original : SatsumaCloner.Clone;
                if (current != car)
                {
                    Release();
                    car = current;
                    body = car != null ? car.GetComponent<Rigidbody>() : null;
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
            };

            internal void Apply(float renderTime)
            {
                if (!Applying)
                {
                    wasKinematic = body.isKinematic;
                    body.isKinematic = true;
                    Applying = true;
                }

                CarStateMessage a, b;
                var i = Snapshots.Count - 1;
                while (i > 0 && Snapshots[i - 1].Time > renderTime) i--;
                Vector3 position;
                Quaternion rotation;
                if (renderTime >= Snapshots[Snapshots.Count - 1].Time)
                {
                    b = Snapshots[Snapshots.Count - 1];
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
                }
                Applying = false;
                Snapshots.Clear();
            }
        }
    }
}
