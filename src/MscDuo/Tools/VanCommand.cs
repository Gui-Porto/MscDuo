using MSCLoader;
using MscDuo.Duplication;
using MscDuo.Player;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class VanCommand : ConsoleCommand
    {
        const string Van = "HAYOSIKO(1500kg, 250)";

        public override string Name => "duo_van";
        public override string Help => "duo_van  leva a van 1 para o lado da Satsuma 1 e a van 2 para o lado da Satsuma 2";

        public override void Run(string[] args)
        {
            var player = GameObject.Find("PLAYER");
            var placed = 0;
            foreach (var pair in new[] { new[] { SatsumaCloner.FindRoot(Van), SatsumaCloner.Original }, new[] { VehicleCloner.CloneOf(Van), SatsumaCloner.Clone } })
            {
                var van = pair[0];
                if (van == null || pair[1] == null) continue;
                if (player != null && player.transform.IsChildOf(van.transform)) { ModConsole.Warning("[MSC Duo] saia da van antes."); continue; }
                van.SetActive(true);
                if (Placement.Beside(van, pair[1])) placed++;
                else ModConsole.Warning($"[MSC Duo] sem espaço livre ao lado de {pair[1].name}.");
            }
            PlayerContext.SaveActive();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {placed} vans posicionadas. Salve o jogo para guardar.");
        }
    }
}
