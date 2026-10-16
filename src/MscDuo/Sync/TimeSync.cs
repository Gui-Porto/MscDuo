using HutongGames.PlayMaker;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class TimeSync
    {
        static PlayMakerFSM color;

        internal static int? ClockTime
        {
            get
            {
                if (color == null) color = Fsm("SUN", "Color");
                var time = color != null ? color.FsmVariables.FindFsmInt("Time") : null;
                return time != null ? time.Value : (int?)null;
            }
        }

        static PlayMakerFSM Fsm(string objectName, string fsmName)
        {
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (fsm != null && fsm.gameObject.name == objectName && fsm.FsmName == fsmName) return fsm;
            return null;
        }
    }
}
