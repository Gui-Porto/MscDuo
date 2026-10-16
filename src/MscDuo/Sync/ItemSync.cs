using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using MscDuo.Duplication;
using MscDuo.Net;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class ItemSync
    {
        const float MoveThreshold = 0.05f;
        const float RestSeconds = 1f;
        const float PeerTimeout = 2f;
        const float ClaimScanInterval = 0.1f;
        const float HeldReach = 3f;
        const float MaxExtrapolation = 0.2f;
        const int MaxSnapshots = 16;
        const int MaxJointDepth = 6;

        enum Owner { None, Me, Peer }

        class Tracked
        {
            internal Owner Owner;
            internal float StillSince = -1f;
            internal float LastArrival;
            internal bool WasKinematic;
            internal bool Applying;
            internal uint Sequence;
            internal uint LastSequence;
            internal readonly List<ItemStateMessage> Snapshots = new List<ItemStateMessage>();
        }

        static readonly Dictionary<ushort, Tracked> tracked = new Dictionary<ushort, Tracked>();
        static Transform player;
        static FsmGameObject itemPivot;
        static FsmString pickedPart;
        static float nextScan;

        internal static bool Enabled { get; set; }
        internal static int OwnedByMe { get; private set; }
        internal static int OwnedByPeer { get; private set; }

        internal static void Tick(float now, float remoteNow, float delay, Vector3? remotePlayer, bool sendDue,
            Action<ItemStateMessage> sendState, Action<ItemStateMessage> sendRest)
        {
            if (!Enabled || ItemRegistry.Count == 0 || !FindPlayer()) return;

            if (now >= nextScan)
            {
                nextScan = now + ClaimScanInterval;
                Scan(now, remotePlayer);
            }

            var done = new List<ushort>();
            OwnedByMe = OwnedByPeer = 0;
            foreach (var pair in tracked)
            {
                var body = ItemRegistry.Get(pair.Key);
                var item = pair.Value;
                if (body == null) { done.Add(pair.Key); continue; }

                if (Installed(body, 0))
                {
                    Release(body, item);
                    done.Add(pair.Key);
                    continue;
                }

                if (item.Owner == Owner.Me)
                {
                    OwnedByMe++;
                    var held = Held(body);
                    var moving = body.velocity.sqrMagnitude > MoveThreshold * MoveThreshold || body.angularVelocity.sqrMagnitude > MoveThreshold;
                    if (held || moving) item.StillSince = -1f;
                    else if (item.StillSince < 0f) item.StillSince = now;

                    if (!held && item.StillSince >= 0f && now - item.StillSince > RestSeconds)
                    {
                        sendRest(Capture(pair.Key, item, body, now, false));
                        done.Add(pair.Key);
                    }
                    else if (sendDue) sendState(Capture(pair.Key, item, body, now, held));
                }
                else if (item.Owner == Owner.Peer)
                {
                    OwnedByPeer++;
                    if (now - item.LastArrival > PeerTimeout) { Release(body, item); done.Add(pair.Key); }
                    else if (item.Snapshots.Count > 0) Apply(body, item, remoteNow - delay);
                }
            }
            foreach (var id in done) tracked.Remove(id);
        }

        static void Scan(float now, Vector3? remotePlayer)
        {
            var mine = player.position;
            for (ushort id = 0; id < ItemRegistry.Count; id++)
            {
                var body = ItemRegistry.Get(id);
                if (body == null || tracked.ContainsKey(id) || !body.gameObject.activeInHierarchy) continue;
                var held = Held(body);
                if (!held)
                {
                    if (body.isKinematic || body.IsSleeping()) continue;
                    if (body.velocity.sqrMagnitude < MoveThreshold * MoveThreshold && body.angularVelocity.sqrMagnitude < MoveThreshold) continue;
                    if (Installed(body, 0)) continue;
                    var position = body.position;
                    if (remotePlayer.HasValue && (remotePlayer.Value - position).sqrMagnitude < (mine - position).sqrMagnitude) continue;
                }
                tracked[id] = new Tracked { Owner = Owner.Me };
            }
        }

        internal static void ReceiveState(ItemStateMessage state, float now, bool isHost)
        {
            if (!ItemRegistry.TryLocal(state.Id, out state.Id)) return;
            var body = ItemRegistry.Get(state.Id);
            if (!Enabled || body == null || Installed(body, 0)) return;
            Tracked item;
            if (tracked.TryGetValue(state.Id, out item) && item.Owner == Owner.Me)
            {
                var myHeld = Held(body);
                var yield = state.Held ? !myHeld : !myHeld && !isHost;
                if (!yield) return;
                item.Owner = Owner.Peer;
                item.Snapshots.Clear();
            }
            else if (item == null)
            {
                item = new Tracked { Owner = Owner.Peer };
                tracked[state.Id] = item;
            }
            if (state.Sequence <= item.LastSequence) return;
            item.LastSequence = state.Sequence;
            item.LastArrival = now;
            item.Snapshots.Add(state);
            if (item.Snapshots.Count > MaxSnapshots) item.Snapshots.RemoveAt(0);
        }

        internal static void ReceiveRest(ItemStateMessage state)
        {
            if (!ItemRegistry.TryLocal(state.Id, out state.Id)) return;
            var body = ItemRegistry.Get(state.Id);
            if (!Enabled || body == null || Installed(body, 0)) return;
            Tracked item;
            if (tracked.TryGetValue(state.Id, out item))
            {
                if (item.Owner == Owner.Me) return;
                Release(body, item);
                tracked.Remove(state.Id);
            }
            body.transform.position = state.Position;
            body.transform.rotation = state.Rotation;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }
        }

        internal static void Forget(ushort id)
        {
            Tracked item;
            if (!tracked.TryGetValue(id, out item)) return;
            var body = ItemRegistry.Get(id);
            if (body != null) Release(body, item);
            tracked.Remove(id);
        }

        internal static void Reset()
        {
            foreach (var pair in tracked)
            {
                var body = ItemRegistry.Get(pair.Key);
                if (body != null) Release(body, pair.Value);
            }
            tracked.Clear();
            OwnedByMe = OwnedByPeer = 0;
        }

        static ItemStateMessage Capture(ushort id, Tracked item, Rigidbody body, float now, bool held) => new ItemStateMessage
        {
            Id = id,
            Sequence = ++item.Sequence,
            Time = now,
            Position = body.position,
            Rotation = body.rotation,
            Velocity = body.velocity,
            Held = held,
        };

        static void Apply(Rigidbody body, Tracked item, float renderTime)
        {
            if (!item.Applying)
            {
                item.WasKinematic = body.isKinematic;
                body.isKinematic = true;
                item.Applying = true;
            }
            var snaps = item.Snapshots;
            var last = snaps[snaps.Count - 1];
            if (renderTime >= last.Time)
            {
                body.transform.position = last.Position + last.Velocity * Mathf.Min(renderTime - last.Time, MaxExtrapolation);
                body.transform.rotation = last.Rotation;
                return;
            }
            var i = snaps.Count - 1;
            while (i > 0 && snaps[i - 1].Time > renderTime) i--;
            if (i == 0)
            {
                body.transform.position = snaps[0].Position;
                body.transform.rotation = snaps[0].Rotation;
                return;
            }
            var a = snaps[i - 1];
            var b = snaps[i];
            var t = Mathf.InverseLerp(a.Time, b.Time, renderTime);
            body.transform.position = Vector3.Lerp(a.Position, b.Position, t);
            body.transform.rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
        }

        static void Release(Rigidbody body, Tracked item)
        {
            if (item.Applying) body.isKinematic = item.WasKinematic;
            item.Applying = false;
            item.Snapshots.Clear();
        }

        static bool Installed(Rigidbody body, int depth)
        {
            if (body == null || depth > MaxJointDepth) return false;
            var joints = body.GetComponents<Joint>();
            foreach (var joint in joints)
            {
                var other = joint.connectedBody;
                if (other == null || other == body) continue;
                if (OnCar(other.transform) || Installed(other, depth + 1)) return true;
            }
            return OnCar(body.transform) && (body.isKinematic || joints.Length > 0);
        }

        static bool OnCar(Transform t)
        {
            var car1 = SatsumaCloner.Original;
            var car2 = SatsumaCloner.Clone;
            return car1 != null && t.IsChildOf(car1.transform) || car2 != null && t.IsChildOf(car2.transform);
        }

        static bool Held(Rigidbody body)
        {
            if (itemPivot == null) itemPivot = FsmVariables.GlobalVariables.FindFsmGameObject("ItemPivot");
            if (pickedPart == null) pickedPart = FsmVariables.GlobalVariables.FindFsmString("PickedPart");
            var t = body.transform;
            if (itemPivot != null && itemPivot.Value != null && t.IsChildOf(itemPivot.Value.transform)) return true;
            return pickedPart != null && !string.IsNullOrEmpty(pickedPart.Value) && t.name == pickedPart.Value
                && (t.position - player.position).sqrMagnitude < HeldReach * HeldReach;
        }

        static bool FindPlayer()
        {
            if (player != null) return true;
            var go = GameObject.Find("PLAYER");
            if (go == null) return false;
            player = go.transform;
            return true;
        }
    }
}
