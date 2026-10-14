using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using MscDuo.Sync;
using UnityEngine;

namespace MscDuo.Net
{
    internal class NetSession
    {
        internal const float SendRate = 20f;
        const float PingInterval = 1f;

        internal static NetSession Current { get; private set; }

        readonly ITransport transport;
        readonly Dictionary<ulong, PeerState> peers = new Dictionary<ulong, PeerState>();
        readonly List<Packet> inbox = new List<Packet>();
        readonly Vector3 ghostOffset;
        float nextSend;
        float nextPing;
        uint sequence;

        internal ITransport Transport => transport;
        internal IEnumerable<PeerState> Peers => peers.Values;
        internal int SentPerSecond { get; private set; }
        internal int ReceivedPerSecond { get; private set; }
        internal int BytesOutPerSecond { get; private set; }
        int sentCounter, receivedCounter, bytesCounter;
        float counterWindow;

        NetSession(ITransport transport, Vector3 ghostOffset)
        {
            this.transport = transport;
            this.ghostOffset = ghostOffset;
        }

        internal static void Start(ITransport transport, Vector3 ghostOffset)
        {
            Stop();
            Current = new NetSession(transport, ghostOffset);
            foreach (var id in transport.Peers) Current.Send(id, MessageType.Hello, w => w.Write(Protocol.Version), Channel.Reliable);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> sessão iniciada ({transport.Name}).");
        }

        internal static void Stop()
        {
            if (Current == null) return;
            foreach (var peer in Current.peers.Values) peer.Player.Dispose();
            Current.transport.Shutdown();
            Current = null;
            ModConsole.Print("<color=orange>[MSC Duo]</color> sessão encerrada.");
        }

        internal void Tick()
        {
            var now = Time.realtimeSinceStartup;
            Receive(now);

            if (now >= nextSend)
            {
                nextSend = now + 1f / SendRate;
                SendPlayerState(now);
            }
            if (now >= nextPing)
            {
                nextPing = now + PingInterval;
                foreach (var id in transport.Peers) Send(id, MessageType.Ping, w => w.Write(now), Channel.Unreliable);
            }

            foreach (var peer in peers.Values) peer.Player.Update(now + peer.ClockOffset);

            counterWindow += Time.unscaledDeltaTime;
            if (counterWindow >= 1f)
            {
                SentPerSecond = sentCounter;
                ReceivedPerSecond = receivedCounter;
                BytesOutPerSecond = bytesCounter;
                sentCounter = receivedCounter = bytesCounter = 0;
                counterWindow = 0f;
            }
        }

        void SendPlayerState(float now)
        {
            var player = Local.Capture();
            if (!player.HasValue) return;
            var state = player.Value;
            state.Sequence = ++sequence;
            state.Time = now;
            foreach (var id in transport.Peers) Send(id, MessageType.PlayerState, state.Write, Channel.Unreliable);
        }

        void Receive(float now)
        {
            inbox.Clear();
            transport.Poll(inbox);
            foreach (var packet in inbox)
            {
                receivedCounter++;
                try
                {
                    Handle(packet, now);
                }
                catch (Exception e)
                {
                    ModConsole.Error($"[MSC Duo] pacote inválido de {packet.From}: {e.Message}");
                }
            }
        }

        void Handle(Packet packet, float now)
        {
            var peer = Peer(packet.From);
            using (var reader = new BinaryReader(new MemoryStream(packet.Data)))
            {
                var type = (MessageType)reader.ReadByte();
                switch (type)
                {
                    case MessageType.Hello:
                        var version = reader.ReadUInt16();
                        if (version != Protocol.Version)
                            ModConsole.Warning($"[MSC Duo] jogador {packet.From} usa protocolo {version}, este usa {Protocol.Version}.");
                        break;
                    case MessageType.Ping:
                        var sentAt = reader.ReadSingle();
                        Send(packet.From, MessageType.Pong, w => { w.Write(sentAt); w.Write(now); }, Channel.Unreliable);
                        break;
                    case MessageType.Pong:
                        peer.OnPong(reader.ReadSingle(), reader.ReadSingle(), now);
                        break;
                    case MessageType.PlayerState:
                        var state = PlayerStateMessage.Read(reader);
                        peer.Player.Push(state, now);
                        break;
                }
            }
        }

        PeerState Peer(ulong id)
        {
            PeerState peer;
            if (!peers.TryGetValue(id, out peer))
            {
                var offset = transport is LoopbackTransport ? ghostOffset : Vector3.zero;
                peer = new PeerState(id, new RemotePlayer($"Jogador {id}", offset));
                peers.Add(id, peer);
            }
            return peer;
        }

        void Send(ulong to, MessageType type, Action<BinaryWriter> body, Channel channel)
        {
            var data = MessageWriter.Build(type, body);
            transport.Send(to, data, channel);
            sentCounter++;
            bytesCounter += data.Length;
        }
    }

    internal class PeerState
    {
        const float Smoothing = 0.1f;

        internal readonly ulong Id;
        internal readonly RemotePlayer Player;
        internal float RttMs { get; private set; }
        internal float ClockOffset { get; private set; }
        bool hasRtt;

        internal PeerState(ulong id, RemotePlayer player)
        {
            Id = id;
            Player = player;
        }

        internal void OnPong(float sentAt, float remoteTime, float now)
        {
            var rtt = now - sentAt;
            var offset = remoteTime - (sentAt + rtt / 2f);
            if (!hasRtt)
            {
                RttMs = rtt * 1000f;
                ClockOffset = offset;
                hasRtt = true;
                return;
            }
            RttMs = Mathf.Lerp(RttMs, rtt * 1000f, Smoothing);
            ClockOffset = Mathf.Lerp(ClockOffset, offset, Smoothing);
        }
    }
}
