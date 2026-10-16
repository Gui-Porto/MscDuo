using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using MscDuo.Player;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class VanCommand : ConsoleCommand
    {
        const string Van = "HAYOSIKO(1500kg, 250)";
        const float Side = 4.5f;

        public override string Name => "duo_van";
        public override string Help => "duo_van  coloca a van 1 ao lado da Satsuma 1, a van 2 ao lado da Satsuma 2 e dá a chave da van ao jogador atual";

        public override void Run(string[] args)
        {
            var placed = Place(SatsumaCloner.FindRoot(Van), SatsumaCloner.Original, "van 1") + Place(VehicleCloner.CloneOf(Van), SatsumaCloner.Clone, "van 2");
            var key = FsmVariables.GlobalVariables.FindFsmInt("PlayerKeyHayosiko");
            if (key != null) key.Value = 1;
            PlayerContext.SaveActive();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {placed} vans posicionadas; o jogador {PlayerContext.Active} agora tem a chave da van. Salve o jogo para guardar.");
        }

        static int Place(GameObject van, GameObject satsuma, string label)
        {
            if (van == null || satsuma == null)
            {
                ModConsole.Warning($"[MSC Duo] {label}: van ou Satsuma não encontrada.");
                return 0;
            }
            var player = GameObject.Find("PLAYER");
            if (player != null && player.transform.IsChildOf(van.transform))
            {
                ModConsole.Warning($"[MSC Duo] {label}: saia da van antes.");
                return 0;
            }
            var left = -satsuma.transform.right;
            left.y = 0f;
            left.Normalize();
            van.SetActive(true);
            van.transform.position = satsuma.transform.position + left * Side + Vector3.up;
            van.transform.rotation = Quaternion.Euler(0f, satsuma.transform.eulerAngles.y, 0f);
            VehicleCloner.Snap(van);
            var body = van.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            return 1;
        }
    }
}
