using System.Collections.Generic;
using System.Globalization;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using MscDuo.Net;
using MscDuo.Sync;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class AdminPanel
    {
        const float ClaimSeconds = 2f;
        const float FuelFallback = 30f;

        static readonly string[] Needs = { "PlayerHunger", "PlayerThirst", "PlayerFatigue", "PlayerStress", "PlayerUrine", "PlayerDirtiness" };
        static readonly string[] NeedLabels = { "Fome", "Sede", "Cansaço", "Estresse", "Mijo", "Sujeira" };
        static readonly string[] Tabs = { "Jogador", "Veículos", "Teleporte", "Mundo" };
        static readonly string[] Blocks = { "06-08", "10-12", "14-16", "18-20", "22-00", "02-04" };
        static readonly string[] BlockLabels = { "6h", "10h", "14h", "18h", "22h", "2h" };
        static readonly string[] WeatherLabels = { "Sol", "Chuva", "Tempestade" };

        static readonly bool[] frozen = new bool[Needs.Length];
        static bool open;
        static int tab;
        static string moneyText = "";
        static string status = "";
        static Rect window = new Rect(60f, 60f, 430f, 520f);
        static Vector2 scroll;
        static FsmBool inMenu;
        static bool menuWas;

        internal static bool Open => open;

        internal static void Toggle()
        {
            if (ModLoader.CurrentScene != CurrentScene.Game) return;
            open = !open;
            if (inMenu == null) inMenu = FsmVariables.GlobalVariables.FindFsmBool("PlayerInMenu");
            if (open)
            {
                menuWas = inMenu != null && inMenu.Value;
                status = "";
            }
            else
            {
                if (inMenu != null) inMenu.Value = menuWas;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        internal static void Tick()
        {
            for (int i = 0; i < Needs.Length; i++)
            {
                if (!frozen[i]) continue;
                var need = FsmVariables.GlobalVariables.FindFsmFloat(Needs[i]);
                if (need != null) need.Value = 0f;
            }
            if (!open) return;
            if (ModLoader.CurrentScene != CurrentScene.Game) { open = false; return; }
            if (inMenu != null) inMenu.Value = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        internal static void Draw()
        {
            if (!open) return;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            window = GUI.Window(0x4D5344, window, DrawWindow, "MSC Duo  admin");
        }

        static void DrawWindow(int id)
        {
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: PlayerTab(); break;
                case 1: VehiclesTab(); break;
                case 2: TeleportTab(); break;
                case 3: WorldTab(); break;
            }
            GUILayout.EndScrollView();
            if (status.Length > 0) GUILayout.Label(status);
            GUILayout.Label("Feche com a mesma tecla que abriu.");
            GUI.DragWindow();
        }

        static void PlayerTab()
        {
            GUILayout.Label("Necessidades travadas em zero");
            for (int i = 0; i < Needs.Length; i++)
                frozen[i] = GUILayout.Toggle(frozen[i], " " + NeedLabels[i]);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Travar todas")) for (int i = 0; i < frozen.Length; i++) frozen[i] = true;
            if (GUILayout.Button("Soltar todas")) for (int i = 0; i < frozen.Length; i++) frozen[i] = false;
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            var money = FsmVariables.GlobalVariables.FindFsmFloat("PlayerMoney");
            GUILayout.Label($"Dinheiro: {(money != null ? money.Value.ToString("0") : "-")} mk");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100")) AddMoney(money, 100f);
            if (GUILayout.Button("+1000")) AddMoney(money, 1000f);
            if (GUILayout.Button("-100")) AddMoney(money, -100f);
            if (GUILayout.Button("-1000")) AddMoney(money, -1000f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            moneyText = GUILayout.TextField(moneyText, GUILayout.Width(120f));
            float value;
            if (GUILayout.Button("Definir") && money != null && float.TryParse(moneyText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                money.Value = Mathf.Max(0f, value);
                status = $"dinheiro definido em {money.Value:0} mk";
            }
            GUILayout.EndHorizontal();
        }

        static void AddMoney(FsmFloat money, float amount)
        {
            if (money == null) return;
            money.Value = Mathf.Max(0f, money.Value + amount);
            status = $"dinheiro: {money.Value:0} mk";
        }

        static void VehiclesTab()
        {
            var player = Player();
            if (player == null) { GUILayout.Label("Jogador não encontrado."); return; }
            var current = CurrentVehicle(player);
            GUILayout.Label("Ir até leva você para o lado do veículo; trazer coloca o veículo ao seu lado.");
            if (current != null && GUILayout.Button("Encher o tanque do veículo atual")) Refuel(current);
            foreach (var link in CarSync.Links)
            {
                if (!link.Ready || link.Root == null) continue;
                var distance = (link.Root.position - player.position).magnitude;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{link.Label}  ({distance:0} m)", GUILayout.Width(200f));
                if (GUILayout.Button("Ir até")) GoTo(player, link.Root);
                if (GUILayout.Button("Trazer")) Bring(player, link);
                GUILayout.EndHorizontal();
            }
        }

        static void GoTo(Transform player, Transform target)
        {
            if (InVehicle(player)) { status = "saia do veículo antes de teleportar."; return; }
            var right = target.right;
            right.y = 0f;
            right.Normalize();
            player.position = target.position + right * 3f + Vector3.up * 1.5f;
            status = $"teleportado para perto de {target.name}";
        }

        static void Bring(Transform player, CarSync.CarLink link)
        {
            if (player.IsChildOf(link.Root)) { status = "você está dentro desse veículo."; return; }
            var vehicle = link.Root.gameObject;
            CarSync.Claim(link, ClaimSeconds);
            if (!Placement.Beside(vehicle, player.gameObject))
            {
                var forward = player.forward;
                forward.y = 0f;
                vehicle.transform.position = player.position + forward.normalized * 5f + Vector3.up;
                vehicle.transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
                VehicleCloner.Snap(vehicle);
            }
            var body = vehicle.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            status = $"{link.Label} trazido para perto de você";
        }

        static void Refuel(Transform vehicle)
        {
            var filled = 0;
            foreach (var fsm in vehicle.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != "Data") continue;
                var level = fsm.FsmVariables.FindFsmFloat("FuelLevel");
                if (level == null) continue;
                var max = fsm.FsmVariables.FindFsmFloat("MaxCapacity") ?? fsm.FsmVariables.FindFsmFloat("FuelCapacity") ?? fsm.FsmVariables.FindFsmFloat("Capacity");
                level.Value = max != null && max.Value > 0f ? max.Value : Mathf.Max(level.Value, FuelFallback);
                filled++;
            }
            status = filled > 0 ? $"tanque cheio ({filled})" : "tanque não encontrado nesse veículo";
        }

        static void TeleportTab()
        {
            var player = Player();
            if (player == null) { GUILayout.Label("Jogador não encontrado."); return; }
            if (GUILayout.Button("Casa 1") && HouseCloner.Original != null) Teleport(player, HouseCloner.Original.transform.position, "casa 1");
            if (GUILayout.Button("Casa 2") && SatsumaCloner.Clone != null) Teleport(player, SatsumaCloner.Clone.transform.position + SatsumaCloner.Clone.transform.right * 2.5f, "casa 2");
            var remote = NetSession.Current != null ? NetSession.Current.RemotePosition : null;
            GUI.enabled = remote.HasValue;
            if (GUILayout.Button("Até o outro jogador") && remote.HasValue) Teleport(player, remote.Value + Vector3.right * 1.5f, "o outro jogador");
            GUI.enabled = true;
        }

        static void Teleport(Transform player, Vector3 target, string label)
        {
            if (InVehicle(player)) { status = "saia do veículo antes de teleportar."; return; }
            player.position = target + Vector3.up * 1.5f;
            status = $"teleportado para {label}";
        }

        static void WorldTab()
        {
            var session = NetSession.Current;
            var guest = session != null && session.Connected && !session.Transport.IsHost;
            if (guest) GUILayout.Label("Só o anfitrião muda a hora e o clima.");
            GUI.enabled = !guest;
            GUILayout.Label($"Hora (agora: {TimeSync.CurrentBlock})");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Blocks.Length; i++)
                if (GUILayout.Button(BlockLabels[i])) status = TimeSync.SetBlock(Blocks[i]) ? $"hora: {BlockLabels[i]}" : "relógio não encontrado";
            GUILayout.EndHorizontal();
            GUILayout.Label("Clima");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < WeatherLabels.Length; i++)
                if (GUILayout.Button(WeatherLabels[i])) status = TimeSync.SetWeather(i) ? $"clima: {WeatherLabels[i]}" : "nuvens não encontradas";
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        static Transform Player()
        {
            var go = GameObject.Find("PLAYER");
            return go != null ? go.transform : null;
        }

        static bool InVehicle(Transform player)
        {
            var current = FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle");
            return current != null && !string.IsNullOrEmpty(current.Value);
        }

        static Transform CurrentVehicle(Transform player)
        {
            if (!InVehicle(player)) return null;
            Transform best = null;
            var bestDistance = float.MaxValue;
            foreach (var link in CarSync.Links)
            {
                if (!link.Ready || link.Root == null) continue;
                var d = (link.Root.position - player.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = link.Root; }
            }
            return best;
        }
    }
}
