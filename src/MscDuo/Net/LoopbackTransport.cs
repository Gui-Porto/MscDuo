using System;
using System.Collections.Generic;
using UnityEngine;

namespace MscDuo.Net
{
    internal class LoopbackTransport : ITransport
    {
        internal const ulong GhostId = 2;

        readonly List<InFlight> inFlight = new List<InFlight>();
        readonly System.Random random = new System.Random(1234);
        float lastReliableDelivery;

        internal float PingMs { get; set; }
        internal float JitterMs { get; set; }
        internal float LossPercent { get; set; }

        public ulong LocalId => 1;
        public IEnumerable<ulong> Peers { get { yield return GhostId; } }
        public string Name => $"loopback {PingMs:0}ms ±{JitterMs:0} perda {LossPercent:0.#}%";

        internal LoopbackTransport(float pingMs, float jitterMs, float lossPercent)
        {
            PingMs = pingMs;
            JitterMs = jitterMs;
            LossPercent = lossPercent;
        }

        public void Send(ulong to, byte[] data, Channel channel)
        {
            var now = Time.realtimeSinceStartup;
            if (channel == Channel.Unreliable && random.NextDouble() * 100.0 < LossPercent) return;

            var delay = OneWayDelay();
            var deliverAt = now + delay;
            if (channel == Channel.Reliable)
            {
                if (random.NextDouble() * 100.0 < LossPercent) deliverAt += PingMs / 1000f;
                deliverAt = Mathf.Max(deliverAt, lastReliableDelivery);
                lastReliableDelivery = deliverAt;
            }
            inFlight.Add(new InFlight(deliverAt, new Packet(GhostId, data, channel)));
        }

        public void Poll(List<Packet> received)
        {
            var now = Time.realtimeSinceStartup;
            inFlight.Sort((a, b) => a.DeliverAt.CompareTo(b.DeliverAt));
            var delivered = 0;
            while (delivered < inFlight.Count && inFlight[delivered].DeliverAt <= now)
            {
                received.Add(inFlight[delivered].Packet);
                delivered++;
            }
            if (delivered > 0) inFlight.RemoveRange(0, delivered);
        }

        public void Shutdown()
        {
            inFlight.Clear();
        }

        float OneWayDelay()
        {
            var jitter = (float)(random.NextDouble() * 2.0 - 1.0) * JitterMs;
            return Math.Max(0f, PingMs / 2f + jitter) / 1000f;
        }

        struct InFlight
        {
            internal readonly float DeliverAt;
            internal readonly Packet Packet;

            internal InFlight(float deliverAt, Packet packet)
            {
                DeliverAt = deliverAt;
                Packet = packet;
            }
        }
    }
}
