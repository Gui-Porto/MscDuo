using MSCLoader;
using Steamworks;

namespace MscDuo.Net
{
    internal static class SteamInvites
    {
        static Callback<GameLobbyJoinRequested_t> joinRequested;

        internal static void Listen()
        {
            if (joinRequested != null) return;
            try
            {
                joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            }
            catch (System.Exception e)
            {
                ModConsole.Warning($"[MSC Duo] Steam indisponível, convites desativados: {e.Message}");
            }
        }

        static void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            ModConsole.Print($"<color=orange>[MSC Duo]</color> entrando no lobby de {SteamTransport.PersonaName(request.m_steamIDFriend.m_SteamID)}...");
            NetSession.Start(SteamTransport.Join(request.m_steamIDLobby.m_SteamID), UnityEngine.Vector3.zero);
        }
    }
}
