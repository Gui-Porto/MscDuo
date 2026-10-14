using MSCLoader;

namespace MscDuo.Tools
{
    internal class DumpCommand : ConsoleCommand
    {
        public override string Name => "duo_dump";
        public override string Help => "duo_dump [roots | globals | <nome do objeto raiz>]";

        public override void Run(string[] args)
        {
            if (args.Length == 0)
            {
                SceneDumper.DumpDefault();
                return;
            }

            var arg = string.Join(" ", args);
            switch (arg.ToLowerInvariant())
            {
                case "roots":
                    SceneDumper.DumpRootsOnly();
                    break;
                case "globals":
                    SceneDumper.DumpGlobalsOnly();
                    break;
                default:
                    SceneDumper.DumpTargets(arg);
                    break;
            }
        }
    }
}
