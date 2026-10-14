using System.Globalization;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class DevCommands
    {
        static bool registered;

        internal static void Register()
        {
            if (registered) return;
            registered = true;
            ConsoleCommand.Add(new DumpCommand());
            ConsoleCommand.Add(new MapCommand());
            ConsoleCommand.Add(new HouseCommand());
            ConsoleCommand.Add(new TeleportCommand());
        }

        internal static bool TryFloat(string s, out float value) =>
            float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        internal static Transform Player
        {
            get
            {
                var player = GameObject.Find("PLAYER");
                return player != null ? player.transform : null;
            }
        }

        static Coroutines runner;

        class Coroutines : MonoBehaviour { }

        internal static void StartRoutine(System.Collections.IEnumerator routine)
        {
            if (runner == null)
                runner = new GameObject("MscDuo_Commands").AddComponent<Coroutines>();
            runner.StartCoroutine(routine);
        }
    }

    internal class MapCommand : ConsoleCommand
    {
        public override string Name => "duo_map";
        public override string Help => "duo_map [meia largura em metros = 120] [resolução = 2048]  foto aérea + alturas ao redor da casa";

        public override void Run(string[] args)
        {
            var half = 120f;
            var resolution = 2048;
            if (args.Length > 0 && !DevCommands.TryFloat(args[0], out half)) { ModConsole.Error("meia largura inválida"); return; }
            if (args.Length > 1 && !int.TryParse(args[1], out resolution)) { ModConsole.Error("resolução inválida"); return; }

            var house = HouseCloner.Original;
            if (house == null) { ModConsole.Error("[MSC Duo] casa original não encontrada"); return; }
            DevCommands.StartRoutine(MapCapture.Capture(house.transform.position, half, resolution));
        }
    }

    internal class HouseCommand : ConsoleCommand
    {
        public override string Name => "duo_house";
        public override string Help => "duo_house [x z [yaw] | here | y <altura> | yaw <graus> | info]  posiciona a casa 2";

        public override void Run(string[] args)
        {
            if (HouseCloner.Original == null) { ModConsole.Error("[MSC Duo] casa original não encontrada"); return; }
            var pos = HouseCloner.Position;
            var yaw = HouseCloner.Yaw;

            if (args.Length == 0 || args[0] == "info")
            {
                ModConsole.Print($"casa 1: {HouseCloner.Format(HouseCloner.Original.transform.position)} yaw {HouseCloner.OriginalYaw:0.#}");
                ModConsole.Print($"casa 2: {HouseCloner.Format(pos)} yaw relativo {yaw:0.#}");
                return;
            }

            float a, b, c;
            switch (args[0])
            {
                case "here":
                    var player = DevCommands.Player;
                    if (player == null) { ModConsole.Error("PLAYER não encontrado"); return; }
                    pos = player.position;
                    break;
                case "y":
                    if (args.Length < 2 || !DevCommands.TryFloat(args[1], out a)) { ModConsole.Error("uso: duo_house y <altura>"); return; }
                    pos.y = a;
                    break;
                case "yaw":
                    if (args.Length < 2 || !DevCommands.TryFloat(args[1], out a)) { ModConsole.Error("uso: duo_house yaw <graus>"); return; }
                    yaw = a;
                    break;
                default:
                    if (args.Length < 2 || !DevCommands.TryFloat(args[0], out a) || !DevCommands.TryFloat(args[1], out b))
                    {
                        ModConsole.Error(Help);
                        return;
                    }
                    pos = new Vector3(a, pos.y, b);
                    if (args.Length > 2 && DevCommands.TryFloat(args[2], out c)) yaw = c;
                    break;
            }

            HouseCloner.Move(pos, yaw);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2 em {HouseCloner.Format(pos)} yaw {yaw:0.#} (salvo)");
        }
    }

    internal class TeleportCommand : ConsoleCommand
    {
        public override string Name => "duo_tp";
        public override string Help => "duo_tp [1 | 2 | x y z]  teleporta para a casa 1, casa 2 ou coordenada";

        public override void Run(string[] args)
        {
            var player = DevCommands.Player;
            if (player == null) { ModConsole.Error("PLAYER não encontrado"); return; }

            Vector3 target;
            if (args.Length == 1 && args[0] == "1" && HouseCloner.Original != null)
                target = HouseCloner.Original.transform.position;
            else if (args.Length == 1 && args[0] == "2" && HouseCloner.Clone != null)
                target = HouseCloner.Clone.transform.position;
            else if (args.Length == 3
                     && DevCommands.TryFloat(args[0], out var x)
                     && DevCommands.TryFloat(args[1], out var y)
                     && DevCommands.TryFloat(args[2], out var z))
                target = new Vector3(x, y, z);
            else
            {
                ModConsole.Error(Help);
                return;
            }

            player.position = target + Vector3.up * 2f;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> teleportado para {HouseCloner.Format(target)}");
        }
    }
}
