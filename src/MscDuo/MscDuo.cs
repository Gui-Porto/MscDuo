using MSCLoader;
using MscDuo.Duplication;
using MscDuo.Net;
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
            SetupFunction(Setup.PostLoad, OnPostLoad);
            SetupFunction(Setup.Update, OnUpdate);
            SetupFunction(Setup.OnGUI, OnGui);
        }

        void ModSettings()
        {
            dumpKey = Keybind.Add("dump", "Exportar YARD, Satsuma e globais", KeyCode.F8);
        }

        void OnGameLoad()
        {
            DevCommands.Register();
            SteamInvites.Listen();
            ModConsole.Print("<color=orange>[MSC Duo]</color> carregado. Comandos: duo_net, duo_house, duo_tp, duo_dump, duo_map, duo_shots, duo_probe.");
        }

        void OnPostLoad()
        {
            HouseCloner.Spawn();
        }

        void OnGui()
        {
            NetHud.Draw();
        }

        void OnUpdate()
        {
            try
            {
                NetSession.Current?.Tick();
            }
            catch (System.Exception e)
            {
                ModConsole.Error($"[MSC Duo] erro na rede, sessão encerrada: {e}");
                NetSession.Stop();
            }
            if (dumpKey != null && dumpKey.GetKeybindDown())
                SceneDumper.DumpDefault();
        }

        internal string SettingsFolder => ModLoader.GetModSettingsFolder(this);
    }
}
