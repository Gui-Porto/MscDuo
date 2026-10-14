using MSCLoader;

namespace MscDuo.Tools
{
    internal class DumpCommand : ConsoleCommand
    {
        static bool registered;

        public override string Name => "duo_dump";
        public override string Help => "duo_dump [roots | globals | <nome do objeto raiz>]";

        internal static void Register()
        {
            if (registered) return;
            registered = true;
            ConsoleCommand.Add(new DumpCommand());
        }

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
