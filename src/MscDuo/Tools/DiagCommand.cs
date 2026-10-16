using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using MscDuo.Net;
using MscDuo.Player;
using MscDuo.Sync;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class DiagCommand : ConsoleCommand
    {
        public override string Name => "duo_diag";
        public override string Help => "duo_diag  mostra o estado dos veículos próximos e do veículo atual (para diagnóstico)";

        public override void Run(string[] args)
        {
            var now = Time.realtimeSinceStartup;
            var session = NetSession.Current;
            ModConsole.Print($"[diag] jogador {PlayerContext.Active}, {(session == null ? "sem sessão" : session.Transport.IsHost ? "anfitrião" : "convidado")}, conectado={session != null && session.Connected}");
            var key = FsmVariables.GlobalVariables.FindFsmInt("PlayerKeyHayosiko");
            ModConsole.Print($"[diag] {CurrentVehicle.Describe()}  chaveVan={(key != null ? key.Value.ToString() : "-")}");
            var player = GameObject.Find("PLAYER");
            foreach (var link in CarSync.Links)
            {
                if (!link.Ready || link.Root == null) continue;
                if (player != null && (link.Root.position - player.transform.position).magnitude > 30f) continue;
                ModConsole.Print("[diag] " + CarSync.Describe(link, now));
            }
        }
    }
}
