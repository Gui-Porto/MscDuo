using MSCLoader;

namespace MscDuo.Tools
{
    internal class ComputerProbeCommand : ConsoleCommand
    {
        static readonly string[] Paths =
        {
            "YARD/Building/BEDROOM1/COMPUTER",
            "Database/Case",
            "Database/Monitor",
            "Database/Peripherals",
        };

        public override string Name => "duo_pc";
        public override string Help => "duo_pc  levanta tudo sobre o computador do quarto e salva em dumps/computador.txt";

        public override void Run(string[] args) => DynoProbeCommand.Probe(args.Length > 0 ? args : Paths, "computador");
    }
}
