using MSCLoader;
using MscDuo.Tools;
using UnityEngine;

namespace MscDuo
{
    public class MscDuo : Mod
    {
        public override string ID => "MscDuo";
        public override string Name => "MSC Duo";
        public override string Version => "0.1.0";
        public override string Author => "Kauan";
        public override string Description =>
            "Multiplayer P2P com progresso individual. Contains AI-generated code (Claude), reviewed by the author.";

        internal static MscDuo Instance { get; private set; }

        SettingsKeybind dumpKey;

        public override void ModSetup()
        {
            Instance = this;
            SetupFunction(Setup.ModSettings, ModSettings);
            SetupFunction(Setup.OnLoad, OnGameLoad);
            SetupFunction(Setup.Update, OnUpdate);
        }

        void ModSettings()
        {
            dumpKey = Keybind.Add("dump", "Exportar YARD, Satsuma e globais", KeyCode.F8);
        }

        void OnGameLoad()
        {
            DumpCommand.Register();
            ModConsole.Print($"<color=orange>[MSC Duo]</color> carregado. F8 ou 'duo_dump' exporta a cena.");
        }

        void OnUpdate()
        {
            if (dumpKey != null && dumpKey.GetKeybindDown())
                SceneDumper.DumpDefault();
        }

        internal string SettingsFolder => ModLoader.GetModSettingsFolder(this);
    }
}
