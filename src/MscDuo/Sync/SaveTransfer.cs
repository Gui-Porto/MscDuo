using System;
using System.Collections.Generic;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class SaveTransfer
    {
        static readonly string[] Files = { "defaultES2File.txt", "items.txt", "graveyard.txt", "trophies.txt", "MscDuo_P2.txt", Duplication.VehicleCloner.HomeFlag, Duplication.VehicleCloner.CopiesFlag };
        const string BackupName = "save_original";
        const string MarkerName = "arquivos.txt";
        const float RetrySeconds = 5f;

        enum Stage { Idle, Requesting, Ready, Failed }

        static Stage stage;
        static float nextRequest;
        static string message;
        static GUIStyle style;

        internal static bool InMenu => ModLoader.CurrentScene == CurrentScene.MainMenu;

        static string SaveFolder => Application.persistentDataPath;
        static string BackupFolder => Path.Combine(MscDuo.Instance.SettingsFolder, BackupName);
        static string Marker => Path.Combine(BackupFolder, MarkerName);

        internal static void OnHostHello(Action request)
        {
            if (stage != Stage.Idle) return;
            stage = Stage.Requesting;
            nextRequest = Time.realtimeSinceStartup + RetrySeconds;
            message = "Conectado ao anfitrião, baixando o save dele...";
            request();
        }

        internal static void Tick(Action request)
        {
            if (stage != Stage.Requesting || Time.realtimeSinceStartup < nextRequest) return;
            nextRequest = Time.realtimeSinceStartup + RetrySeconds;
            request();
        }

        internal static void WriteSave(BinaryWriter w)
        {
            var present = new List<string>();
            foreach (var name in Files)
                if (File.Exists(Path.Combine(SaveFolder, name))) present.Add(name);
            w.Write(present.Count);
            foreach (var name in present)
            {
                var bytes = File.ReadAllBytes(Path.Combine(SaveFolder, name));
                w.Write(name);
                w.Write(bytes.Length);
                w.Write(bytes);
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> save enviado ao convidado ({present.Count} arquivos).");
        }

        internal static void ReadSave(BinaryReader r)
        {
            var count = r.ReadInt32();
            var received = new Dictionary<string, byte[]>();
            for (int i = 0; i < count; i++)
            {
                var name = r.ReadString();
                var bytes = r.ReadBytes(r.ReadInt32());
                if (Array.IndexOf(Files, name) >= 0) received[name] = bytes;
            }
            if (stage != Stage.Requesting) return;
            if (!InMenu)
            {
                stage = Stage.Failed;
                message = null;
                ModConsole.Warning("[MSC Duo] o save do anfitrião chegou com o jogo já carregado; para usar o save dele, volte ao menu e entre de novo.");
                return;
            }
            if (!received.ContainsKey("defaultES2File.txt"))
            {
                stage = Stage.Failed;
                message = "O anfitrião não tem save. Ele precisa salvar o jogo uma vez.";
                return;
            }
            try
            {
                Backup();
                foreach (var name in Files)
                {
                    var path = Path.Combine(SaveFolder, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                foreach (var pair in received) File.WriteAllBytes(Path.Combine(SaveFolder, pair.Key), pair.Value);
                stage = Stage.Ready;
                message = "Save do anfitrião recebido. Clique em CONTINUE para entrar no mundo dele.\nO seu save volta sozinho quando você sair para o menu.";
                ModConsole.Print("<color=orange>[MSC Duo]</color> save do anfitrião instalado; o seu foi guardado e volta quando você sair para o menu.");
            }
            catch (Exception e)
            {
                Restore();
                stage = Stage.Failed;
                message = "Não foi possível instalar o save do anfitrião: " + e.Message;
                ModConsole.Error($"[MSC Duo] {message}");
            }
        }

        static void Backup()
        {
            if (File.Exists(Marker)) return;
            Directory.CreateDirectory(BackupFolder);
            var present = new List<string>();
            foreach (var name in Files)
            {
                var source = Path.Combine(SaveFolder, name);
                if (!File.Exists(source)) continue;
                File.Copy(source, Path.Combine(BackupFolder, name), true);
                present.Add(name);
            }
            File.WriteAllLines(Marker, present.ToArray());
        }

        internal static void Restore()
        {
            stage = Stage.Idle;
            message = null;
            if (!File.Exists(Marker)) return;
            try
            {
                foreach (var name in Files)
                {
                    var path = Path.Combine(SaveFolder, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                foreach (var name in File.ReadAllLines(Marker))
                {
                    if (Array.IndexOf(Files, name) < 0) continue;
                    File.Copy(Path.Combine(BackupFolder, name), Path.Combine(SaveFolder, name), true);
                }
                Directory.Delete(BackupFolder, true);
                ModConsole.Print("<color=orange>[MSC Duo]</color> o seu save original foi restaurado.");
            }
            catch (Exception e)
            {
                ModConsole.Error($"[MSC Duo] não foi possível restaurar o seu save; ele continua guardado em {BackupFolder}: {e.Message}");
            }
        }

        internal static void Draw()
        {
            if (message == null || !InMenu) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                style.normal.textColor = Color.white;
            }
            var rect = new Rect(Screen.width * 0.15f, 20f, Screen.width * 0.7f, 80f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, message, style);
        }
    }
}
