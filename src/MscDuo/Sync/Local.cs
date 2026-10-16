using MscDuo.Net;
using MscDuo.Player;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class Local
    {
        static Transform player;

        internal static PlayerStateMessage? Capture()
        {
            if (player == null)
            {
                var go = GameObject.Find("PLAYER");
                if (go == null) return null;
                player = go.transform;
            }

            var camera = Camera.main;
            var forward = camera != null ? camera.transform.forward : player.forward;
            var yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            return new PlayerStateMessage
            {
                Position = player.position,
                Yaw = yaw,
                Pitch = pitch,
                Flags = PlayerContext.Active == 2 ? PlayerStateMessage.Player2Flag : (byte)0,
            };
        }
    }
}
