using System.IO;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Player
{
    internal static class PlayerContext
    {
        const string Player2File = "player2.txt";

        static readonly PlayerProfile[] profiles = new PlayerProfile[3];
        static Transform player;
        static bool hooked;
        static float nextAutosave;
        const float AutosaveSeconds = 30f;

        internal static int Active { get; private set; } = 1;

        static string Player2Path => Path.Combine(MscDuo.Instance.SettingsFolder, Player2File);

        internal static void Init()
        {
            Active = 1;
            player = null;
            hooked = false;
            nextAutosave = 0f;
            profiles[1] = null;
            profiles[2] = PlayerProfile.Load(Player2Path);
            HookSave();
            var session = Net.NetSession.Current;
            if (session != null && !session.Transport.IsHost && session.Connected) JoinAsGuest();
            GiveHomeKeys();
        }

        static void GiveHomeKeys()
        {
            var van = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmInt("PlayerKeyHayosiko");
            if (van != null) van.Value = 1;
        }

        internal static void Switch(int target)
        {
            if (target == Active) { ModConsole.Print($"[MSC Duo] você já é o jogador {target}."); return; }
            if (!FindPlayer()) { ModConsole.Error("[MSC Duo] PLAYER não encontrado"); return; }

            profiles[Active] = PlayerProfile.Capture(player);
            if (Active == 2) profiles[2].Save(Player2Path);

            if (profiles[target] == null) profiles[target] = NewPlayer2(profiles[1]);
            profiles[target].Apply();
            Active = target;
            if (profiles[target].HasPosition) Teleport(profiles[target].Position, profiles[target].Yaw);

            GiveHomeKeys();
            if (Active == 2) profiles[2].Save(Player2Path);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> agora você é o jogador {Active} ({profiles[Active].Get("PlayerName")}), dinheiro {profiles[Active].Get("PlayerMoney")}.");
        }

        internal static void JoinAsGuest()
        {
            if (Active == 2) return;
            ModConsole.Print("<color=orange>[MSC Duo]</color> conectado ao anfitrião: você joga como jogador 2, na casa 2 com a Satsuma 2.");
            Switch(2);
        }

        internal static void Tick()
        {
            if (Active != 2 || Time.realtimeSinceStartup < nextAutosave) return;
            nextAutosave = Time.realtimeSinceStartup + AutosaveSeconds;
            SaveActive();
        }

        internal static void SaveActive()
        {
            if (Active != 2 || !FindPlayer()) return;
            profiles[2] = PlayerProfile.Capture(player);
            profiles[2].Save(Player2Path);
        }

        internal static string Describe(int index)
        {
            var profile = index == Active && FindPlayer() ? PlayerProfile.Capture(player) : profiles[index];
            if (profile == null) return $"jogador {index}: ainda não criado";
            return $"jogador {index}: {profile.Get("PlayerName")}  dinheiro {Short(profile.Get("PlayerMoney"))}  fome {Short(profile.Get("PlayerHunger"))}  sede {Short(profile.Get("PlayerThirst"))}  fadiga {Short(profile.Get("PlayerFatigue"))}  stress {Short(profile.Get("PlayerStress"))}";
        }

        static string Short(string value)
        {
            float f;
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f.ToString("0.##") : value;
        }

        static PlayerProfile NewPlayer2(PlayerProfile basis)
        {
            var fresh = Copy(basis ?? PlayerProfile.Capture(player));
            fresh.Set("PlayerFirstName", "Jogador");
            fresh.Set("PlayerLastName", "2");
            fresh.Set("PlayerName", "Jogador 2");
            foreach (var need in new[] { "PlayerHunger", "PlayerThirst", "PlayerStress", "PlayerUrine", "PlayerFatigue", "PlayerDirtiness", "PlayerDrunk", "PlayerDrunkAdjusted", "PlayerBurns", "PlayerFines", "PlayerFinesDay" })
                fresh.Set(need, "0");

            var spawn = Player2Spawn();
            if (spawn.HasValue)
            {
                fresh.Position = spawn.Value;
                fresh.Yaw = HouseCloner.Clone != null ? HouseCloner.Clone.transform.eulerAngles.y : 0f;
                fresh.HasPosition = true;
            }
            return fresh;
        }

        static PlayerProfile Copy(PlayerProfile source)
        {
            var copy = new PlayerProfile();
            foreach (var name in PlayerProfile.Variables)
            {
                var value = source.Get(name);
                if (value != null) copy.Set(name, value);
            }
            return copy;
        }

        static Vector3? Player2Spawn()
        {
            if (HouseCloner.Clone == null) return null;
            var door = Find(HouseCloner.Clone.transform, "DoorFront");
            var center = HouseCloner.Clone.transform.position;
            if (door == null) return center + Vector3.up;
            var outward = door.position - center;
            outward.y = 0f;
            return door.position + outward.normalized * 2f + Vector3.up * 0.5f;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = Find(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void Teleport(Vector3 position, float yaw)
        {
            player.position = position;
            player.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static bool FindPlayer()
        {
            if (player != null) return true;
            var go = GameObject.Find("PLAYER");
            if (go == null) return false;
            player = go.transform;
            return true;
        }

        static void HookSave()
        {
            if (hooked) return;
            var items = GameObject.Find("ITEMS");
            var fsm = items != null ? items.GetComponent<PlayMakerFSM>() : null;
            if (fsm == null || !fsm.FsmInject("Save game", BeforeGameSave, false, 0))
            {
                ModConsole.Warning("[MSC Duo] não foi possível interceptar o save do jogo; evite salvar como jogador 2.");
                return;
            }
            hooked = true;
        }

        static void BeforeGameSave()
        {
            if (Active != 2) return;
            ModConsole.Print("<color=orange>[MSC Duo]</color> o jogo vai salvar: voltando para o jogador 1 antes, o jogador 2 foi salvo no arquivo do mod.");
            Switch(1);
        }
    }
}
