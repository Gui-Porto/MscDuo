using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class SleepSync
    {
        const string WaitState = "MscDuo Wait";
        const string Go = "MSCDUO_GO";
        const string Cancel = "MSCDUO_CANCEL";
        const float ResendSeconds = 1f;

        static readonly List<PlayMakerFSM> beds = new List<PlayMakerFSM>();
        static GUIStyle style;
        static bool sentWaiting;
        static float nextResend;

        internal static bool RemoteWaiting { get; private set; }
        internal static bool LocalWaiting { get; private set; }

        internal static void Patch()
        {
            beds.Clear();
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.gameObject.name != "SleepTrigger" || fsm.FsmName != "Activate") continue;
                try
                {
                    FsmTools.EnsureLoaded(fsm);
                    if (fsm.GetState(WaitState) != null) { beds.Add(fsm); continue; }
                    var sleep = fsm.GetState("Sleep");
                    if (sleep == null || fsm.GetState("Smoking") == null || fsm.GetState("Wake up 2") == null) continue;
                    var finished = sleep.GetTransition("FINISHED");
                    if (finished == null) continue;
                    fsm.AddEvent(Go);
                    fsm.AddEvent(Cancel);
                    var wait = fsm.AddState(WaitState);
                    wait.AddTransition(Go, "Smoking");
                    wait.AddTransition(Cancel, "Wake up 2");
                    finished.ToState = WaitState;
                    beds.Add(fsm);
                }
                catch (System.Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] cama '{fsm.transform.root.name}' não preparada para dormir junto: {e.Message}");
                }
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {beds.Count} camas preparadas para dormir junto.");
        }

        internal static void Tick(float now, bool connected, System.Action<bool> send)
        {
            PlayMakerFSM waiting = null;
            foreach (var bed in beds)
                if (bed != null && bed.ActiveStateName == WaitState) { waiting = bed; break; }
            LocalWaiting = waiting != null;
            if (!connected) RemoteWaiting = false;

            if (connected && (LocalWaiting != sentWaiting || now >= nextResend))
            {
                sentWaiting = LocalWaiting;
                nextResend = now + ResendSeconds;
                send(LocalWaiting);
            }

            if (waiting == null) return;
            if (!connected || RemoteWaiting)
            {
                waiting.SendEvent(Go);
                return;
            }
            if (MscDuo.Instance.WakeKey != null && MscDuo.Instance.WakeKey.GetKeybindDown())
            {
                var time = waiting.FsmVariables.FindFsmInt("TimeOfDay");
                var clock = TimeSync.ClockTime;
                if (time != null && clock.HasValue) time.Value = clock.Value;
                waiting.SendEvent(Cancel);
            }
        }

        internal static void Receive(bool waiting) => RemoteWaiting = waiting;

        internal static void Reset()
        {
            RemoteWaiting = false;
            sentWaiting = false;
        }

        internal static void Draw()
        {
            if (!LocalWaiting) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = Color.white;
            }
            var key = MscDuo.Instance.WakeKey != null ? MscDuo.Instance.WakeKey.GetKeyValue.ToString() : "?";
            GUI.Label(new Rect(0f, Screen.height * 0.75f, Screen.width, 60f), $"Esperando o outro jogador deitar para dormir...\n[{key}] levantar", style);
        }
    }
}
