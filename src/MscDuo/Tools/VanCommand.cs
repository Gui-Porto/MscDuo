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
        const float MaxStep = 0.8f;
        const float Headroom = 2.6f;
        const float HalfWidth = 1.1f;
        const float HalfLength = 2.4f;

        static readonly Vector2[] Candidates =
        {
            new Vector2(-4.5f, 0f), new Vector2(4.5f, 0f), new Vector2(0f, 7f), new Vector2(0f, -7f),
            new Vector2(-8f, 0f), new Vector2(8f, 0f), new Vector2(-4.5f, 7f), new Vector2(4.5f, 7f),
            new Vector2(-4.5f, -7f), new Vector2(4.5f, -7f), new Vector2(0f, 12f), new Vector2(0f, -12f),
        };

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
            float baseGround;
            if (!Ground.TryHeight(satsuma.transform.position, 0.3f, out baseGround, satsuma, van))
            {
                ModConsole.Warning($"[MSC Duo] {label}: chão embaixo da Satsuma não encontrado.");
                return 0;
            }
            var right = satsuma.transform.right;
            right.y = 0f;
            right.Normalize();
            var forward = Vector3.Cross(right, Vector3.up);
            var yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            foreach (var offset in Candidates)
            {
                var spot = satsuma.transform.position + right * offset.x + forward * offset.y;
                spot.y = baseGround;
                float ground;
                if (!Free(spot, right, forward, baseGround, out ground, satsuma, van)) continue;
                van.SetActive(true);
                van.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                van.transform.position = new Vector3(spot.x, ground, spot.z);
                van.transform.position += Vector3.up * (0.05f - VehicleCloner.BottomOffset(van));
                var body = van.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                return 1;
            }
            ModConsole.Warning($"[MSC Duo] {label}: nenhum espaço livre ao redor da Satsuma.");
            return 0;
        }

        static bool Free(Vector3 spot, Vector3 right, Vector3 forward, float baseGround, out float ground, params GameObject[] ignore)
        {
            ground = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    var point = spot + right * (HalfWidth * i) + forward * (HalfLength * j);
                    point.y = baseGround;
                    float y;
                    if (!Ground.TryHeight(point, MaxStep, out y, ignore) || Mathf.Abs(y - baseGround) > MaxStep) return false;
                    if (Blocked(new Vector3(point.x, y + 0.3f, point.z), ignore)) return false;
                    ground = Mathf.Max(ground, y);
                }
            return true;
        }

        static bool Blocked(Vector3 from, GameObject[] ignore)
        {
            foreach (var hit in Physics.RaycastAll(from, Vector3.up, Headroom))
            {
                if (hit.collider.isTrigger) continue;
                var mine = false;
                foreach (var root in ignore)
                    if (root != null && hit.collider.transform.IsChildOf(root.transform)) mine = true;
                if (!mine) return true;
            }
            return false;
        }
    }
}
