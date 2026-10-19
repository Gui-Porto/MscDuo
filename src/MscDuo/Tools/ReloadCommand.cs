using MSCLoader;
using MscDuo.Duplication;

namespace MscDuo.Tools
{
    internal class ReloadCommand : ConsoleCommand
    {
        public override string Name => "duo_reload";
        public override string Help => "duo_reload  monta o galpão de novo lendo galpao.txt e, se houver pasta_dev.txt, o modelo e as texturas da pasta do projeto";

        public override void Run(string[] args)
        {
            if (ModLoader.CurrentScene != CurrentScene.Game) { ModConsole.Error("[MSC Duo] carregue o jogo antes."); return; }
            Warehouse.Spawn();
        }
    }
}
