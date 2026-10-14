using MSCLoader;
using MscDuo.Player;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class PlayerCommand : ConsoleCommand
    {
        public override string Name => "duo_player";
        public override string Help => "duo_player [1 | 2 | info]  troca qual jogador você controla (progresso individual)";

        public override void Run(string[] args)
        {
            if (args.Length == 0 || args[0] == "info")
            {
                ModConsole.Print($"ativo: jogador {PlayerContext.Active}");
                ModConsole.Print(PlayerContext.Describe(1));
                ModConsole.Print(PlayerContext.Describe(2));
                return;
            }
            if (args[0] == "1") PlayerContext.Switch(1);
            else if (args[0] == "2") PlayerContext.Switch(2);
            else ModConsole.Error(Help);
        }
    }

    internal static class PlayerHud
    {
        static GUIStyle style;

        internal static void Draw()
        {
            if (PlayerContext.Active == 1) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(0.91f, 0.4f, 0.04f);
            }
            GUI.Label(new Rect(Screen.width / 2f - 150f, 8f, 300f, 28f), $"JOGADOR {PlayerContext.Active}", style);
        }
    }
}
