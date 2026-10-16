using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using MscDuo.Player;
using MscDuo.Sync;
using UnityEngine;

namespace MscDuo.Net
{
    internal class NetSession
    {
        internal const float SendRate = 20f;
        const float PingInterval = 1f;
        const float HandshakeInterval = 2f;

        internal static NetSession Current { get; private set; }

        readonly ITransport transport;
        readonly Dictionary<ulong, PeerState> peers = new Dictionary<ulong, PeerState>();
        readonly List<Packet> inbox = new List<Packet>();
        readonly HashSet<ulong> knownPeers = new HashSet<ulong>();
        readonly Vector3 ghostOffset;
        float nextSend;
        float nextPing;
        float nextHandshake;
        uint sequence;

        internal ITransport Transport => transport;
        internal IEnumerable<PeerState> Peers => peers.Values;
        internal bool Connected => knownPeers.Count > 0;

        internal Vector3? RemotePosition
        {
            get
            {
                foreach (var peer in peers.Values)
                    if (peer.Player.Buffered > 0) return peer.Player.Position;
                return null;
            }
        }
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
            var session = Current;
            InteractionSync.Bind((key, state, fsm) =>
            {
                if (!session.Connected || session.transport is LoopbackTransport) return;
                session.Broadcast(MessageType.Interaction, w => InteractionSync.WriteInteraction(w, key, state, fsm), Channel.Reliable);
            });
            ItemRegistry.Forget();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> sessão iniciada ({transport.Name}).");
        }

        internal static void Stop()
        {
            if (Current == null) return;
            InteractionSync.Bind(null);
            if (!Current.transport.IsHost)
            {
                PlayerContext.SaveActive();
                if (SaveTransfer.InMenu) SaveTransfer.Restore();
            }
            CarSync.Reset();
            ItemSync.Reset();
            TrafficSync.Restore();
            SleepSync.Reset();
            ItemRegistry.Forget();
            foreach (var peer in Current.peers.Values) peer.Player.Dispose();
            Current.transport.Shutdown();
            Current = null;
            ModConsole.Print("<color=orange>[MSC Duo]</color> sessão encerrada.");
        }

        internal void Tick()
        {
            var now = Time.realtimeSinceStartup;
            SyncPeers();
            Receive(now);

            var sendDue = now >= nextSend;
            if (sendDue)
            {
                nextSend = now + 1f / SendRate;
                SendPlayerState(now);
            }
            TickCars(now, sendDue);
            TimeSync.Tick(now, transport.IsHost, Connected, body => Broadcast(MessageType.TimeState, body, Channel.Reliable));
            SleepSync.Tick(now, Connected, waiting => Broadcast(MessageType.SleepState, w => w.Write(waiting), Channel.Reliable));
            if (!transport.IsHost) SaveTransfer.Tick(() => Broadcast(MessageType.SaveRequest, null, Channel.Reliable));
            if (now >= nextHandshake)
            {
                nextHandshake = now + HandshakeInterval;
                Handshake();
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

        void SyncPeers()
        {
            var current = new HashSet<ulong>(transport.Peers);
            foreach (var id in current)
            {
                if (knownPeers.Contains(id)) continue;
                knownPeers.Add(id);
                Peer(id);
                nextHandshake = 0f;
            }

            var gone = new List<ulong>();
            foreach (var id in knownPeers) if (!current.Contains(id)) gone.Add(id);
            foreach (var id in gone)
            {
                knownPeers.Remove(id);
                PeerState peer;
                if (peers.TryGetValue(id, out peer))
                {
                    peer.Player.Dispose();
                    peers.Remove(id);
                }
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

        void TickCars(float now, bool sendDue)
        {
            PeerState peer = null;
            foreach (var p in peers.Values) { peer = p; break; }
            var remoteNow = peer != null ? now + peer.ClockOffset : now;
            var delay = peer != null ? peer.Player.InterpolationDelay : 0.1f;
            Vector3? remotePlayer = null;
            if (peer != null && peer.Player.Buffered > 0) remotePlayer = peer.Player.Position;
            CarSync.Tick(now, remoteNow, delay, remotePlayer, transport.IsHost, sendDue && Connected, state => Broadcast(MessageType.CarState, state.Write, Channel.Unreliable));
            ItemSync.Tick(now, remoteNow, delay, remotePlayer, sendDue && Connected,
                state => Broadcast(MessageType.ItemState, state.Write, Channel.Unreliable),
                state => Broadcast(MessageType.ItemRest, state.Write, Channel.Reliable));
            TrafficSync.Tick(now, remoteNow, delay, transport.IsHost, Connected, body => Broadcast(MessageType.TrafficState, body, Channel.Unreliable));
        }

        void Broadcast(MessageType type, Action<BinaryWriter> body, Channel channel)
        {
            foreach (var id in transport.Peers) Send(id, type, body, channel);
        }

        void Handshake()
        {
            foreach (var id in transport.Peers)
            {
                var peer = Peer(id);
                if (!peer.PeerGotHello)
                    Send(id, MessageType.Hello, w => { w.Write(Protocol.Version); w.Write(transport.IsHost); w.Write(peer.GotHello); }, Channel.Reliable);
                if (ItemRegistry.Final && !peer.PeerMatchedItems && !(transport is LoopbackTransport))
                    Send(id, MessageType.ItemsInfo, ItemRegistry.WriteInfo, Channel.Reliable);
            }
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
                        {
                            ModConsole.Warning($"[MSC Duo] jogador {packet.From} usa protocolo {version}, este usa {Protocol.Version}.");
                            break;
                        }
                        var fromHost = reader.ReadBoolean();
                        if (reader.ReadBoolean()) peer.PeerGotHello = true;
                        if (!peer.GotHello)
                        {
                            peer.GotHello = true;
                            Send(packet.From, MessageType.Hello, w => { w.Write(Protocol.Version); w.Write(transport.IsHost); w.Write(true); }, Channel.Reliable);
                        }
                        if (fromHost && !transport.IsHost)
                        {
                            var host = packet.From;
                            if (SaveTransfer.InMenu) SaveTransfer.OnHostHello(() => Send(host, MessageType.SaveRequest, null, Channel.Reliable));
                            else PlayerContext.JoinAsGuest();
                        }
                        break;
                    case MessageType.TrafficState:
                        if (!transport.IsHost) TrafficSync.Read(reader);
                        break;
                    case MessageType.TimeState:
                        if (!transport.IsHost) TimeSync.Read(reader);
                        break;
                    case MessageType.Interaction:
                        InteractionSync.ReadInteraction(reader);
                        break;
                    case MessageType.SaveRequest:
                        if (transport.IsHost) Send(packet.From, MessageType.SaveData, SaveTransfer.WriteSave, Channel.Reliable);
                        break;
                    case MessageType.SaveData:
                        if (!transport.IsHost) SaveTransfer.ReadSave(reader);
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
                    case MessageType.ItemsInfo:
                        if (transport is LoopbackTransport) break;
                        if (ItemRegistry.ReadInfo(reader)) Send(packet.From, MessageType.ItemsMatched, null, Channel.Reliable);
                        break;
                    case MessageType.ItemsMatched:
                        peer.PeerMatchedItems = true;
                        break;
                    case MessageType.ItemState:
                        ItemSync.ReceiveState(ItemStateMessage.Read(reader), now, transport.IsHost);
                        break;
                    case MessageType.ItemRest:
                        ItemSync.ReceiveRest(ItemStateMessage.Read(reader));
                        break;
                    case MessageType.SleepState:
                        SleepSync.Receive(reader.ReadBoolean());
                        break;
                    case MessageType.CarState:
                        CarSync.Receive(CarStateMessage.Read(reader), now, transport.IsHost);
                        break;
                }
            }
        }

        PeerState Peer(ulong id)
        {
            PeerState peer;
            if (!peers.TryGetValue(id, out peer))
            {
                var loopback = transport is LoopbackTransport;
                var name = loopback ? $"Jogador {id}" : SteamTransport.PersonaName(id);
                peer = new PeerState(id, new RemotePlayer(name, loopback ? ghostOffset : Vector3.zero));
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
        internal bool GotHello;
        internal bool PeerGotHello;
        internal bool PeerMatchedItems;
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
