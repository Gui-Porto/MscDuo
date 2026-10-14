using System;
using System.Collections.Generic;
using MSCLoader;
using Steamworks;

namespace MscDuo.Net
{
    internal class SteamTransport : ITransport
    {
        const int MaxMembers = 2;
        const int UnreliableChannel = 0;
        const int ReliableChannel = 1;
        const string LobbyKey = "msc_duo";

        readonly List<ulong> peers = new List<ulong>();
        readonly byte[] buffer = new byte[64 * 1024];
        readonly CSteamID self;
        Callback<LobbyCreated_t> lobbyCreated;
        Callback<LobbyEnter_t> lobbyEntered;
        Callback<LobbyChatUpdate_t> lobbyChanged;
        Callback<P2PSessionRequest_t> sessionRequested;
        Callback<P2PSessionConnectFail_t> sessionFailed;
        CSteamID lobby = CSteamID.Nil;
        string status = "iniciando";

        public ulong LocalId => self.m_SteamID;
        public IEnumerable<ulong> Peers => peers;
        public string Name => $"steam {status}";

        internal ulong Lobby => lobby.m_SteamID;

        SteamTransport()
        {
            if (!SteamAPI.Init()) throw new InvalidOperationException("a Steam não inicializou; abra o jogo pela Steam");
            self = SteamUser.GetSteamID();
            SteamNetworking.AllowP2PPacketRelay(true);
            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            lobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);
            sessionRequested = Callback<P2PSessionRequest_t>.Create(OnSessionRequested);
            sessionFailed = Callback<P2PSessionConnectFail_t>.Create(OnSessionFailed);
        }

        internal static SteamTransport Host()
        {
            var transport = new SteamTransport();
            transport.status = "criando lobby";
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxMembers);
            return transport;
        }

        internal static SteamTransport Join(ulong lobbyId)
        {
            var transport = new SteamTransport();
            transport.status = "entrando no lobby";
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            return transport;
        }

        internal void OpenInviteOverlay()
        {
            if (lobby == CSteamID.Nil)
            {
                ModConsole.Warning("[MSC Duo] o lobby ainda não foi criado.");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        }

        public void Send(ulong to, byte[] data, Channel channel)
        {
            var reliable = channel == Channel.Reliable;
            SteamNetworking.SendP2PPacket(new CSteamID(to), data, (uint)data.Length,
                reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay,
                reliable ? ReliableChannel : UnreliableChannel);
        }

        public void Poll(List<Packet> received)
        {
            SteamAPI.RunCallbacks();
            Read(UnreliableChannel, Channel.Unreliable, received);
            Read(ReliableChannel, Channel.Reliable, received);
        }

        void Read(int steamChannel, Channel channel, List<Packet> received)
        {
            uint size;
            while (SteamNetworking.IsP2PPacketAvailable(out size, steamChannel))
            {
                CSteamID from;
                uint read;
                var data = size <= buffer.Length ? buffer : new byte[size];
                if (!SteamNetworking.ReadP2PPacket(data, size, out read, out from, steamChannel)) break;
                if (!peers.Contains(from.m_SteamID)) continue;
                var copy = new byte[read];
                Array.Copy(data, copy, read);
                received.Add(new Packet(from.m_SteamID, copy, channel));
            }
        }

        public void Shutdown()
        {
            foreach (var peer in peers) SteamNetworking.CloseP2PSessionWithUser(new CSteamID(peer));
            peers.Clear();
            if (lobby != CSteamID.Nil) SteamMatchmaking.LeaveLobby(lobby);
            lobby = CSteamID.Nil;
            lobbyCreated?.Dispose();
            lobbyEntered?.Dispose();
            lobbyChanged?.Dispose();
            sessionRequested?.Dispose();
            sessionFailed?.Dispose();
        }

        void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                status = $"falha ao criar lobby ({result.m_eResult})";
                ModConsole.Error($"[MSC Duo] {status}");
                return;
            }
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, LobbyKey, Protocol.Version.ToString());
            status = $"hospedando, lobby {lobby.m_SteamID}";
            ModConsole.Print($"<color=orange>[MSC Duo]</color> lobby criado. Convide com 'duo_net invite' ou passe o código: <b>{lobby.m_SteamID}</b>");
        }

        void OnLobbyEntered(LobbyEnter_t result)
        {
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            var version = SteamMatchmaking.GetLobbyData(lobby, LobbyKey);
            if (version != Protocol.Version.ToString())
                ModConsole.Warning($"[MSC Duo] lobby usa protocolo '{version}', este mod usa {Protocol.Version}.");
            RefreshMembers();
            if (status == "entrando no lobby") status = $"conectado ao lobby {lobby.m_SteamID}";
        }

        void OnLobbyChanged(LobbyChatUpdate_t update)
        {
            if (update.m_ulSteamIDLobby != lobby.m_SteamID) return;
            RefreshMembers();
        }

        void OnSessionRequested(P2PSessionRequest_t request)
        {
            if (peers.Contains(request.m_steamIDRemote.m_SteamID))
                SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
        }

        void OnSessionFailed(P2PSessionConnectFail_t failure)
        {
            ModConsole.Warning($"[MSC Duo] conexão P2P com {failure.m_steamIDRemote.m_SteamID} falhou (erro {failure.m_eP2PSessionError}).");
        }

        void RefreshMembers()
        {
            var current = new List<ulong>();
            var count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int i = 0; i < count; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                if (member != self) current.Add(member.m_SteamID);
            }

            foreach (var id in current)
                if (!peers.Contains(id))
                    ModConsole.Print($"<color=orange>[MSC Duo]</color> {SteamFriends.GetFriendPersonaName(new CSteamID(id))} entrou.");
            foreach (var id in peers)
                if (!current.Contains(id))
                {
                    ModConsole.Print($"<color=orange>[MSC Duo]</color> {SteamFriends.GetFriendPersonaName(new CSteamID(id))} saiu.");
                    SteamNetworking.CloseP2PSessionWithUser(new CSteamID(id));
                }

            peers.Clear();
            peers.AddRange(current);
        }

        internal static string PersonaName(ulong id)
        {
            try { return SteamFriends.GetFriendPersonaName(new CSteamID(id)); }
            catch { return id.ToString(); }
        }
    }
}
