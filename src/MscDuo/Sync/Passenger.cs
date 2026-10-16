using HutongGames.PlayMaker;
using UnityEngine;

namespace MscDuo.Sync
{
    // banco do carona: o jogador fica preso ao banco olhando em volta, sem andar e sem os comandos do veículo
    internal static class Passenger
    {
        const float Reach = 0.9f;
        const float BikeBack = 0.4f;
        const float StepOut = 1.1f;
        const string Hint = "PASSAGEIRO (ENTER)";
        static readonly string[] SingleSeat = { "kekmet", "combine", "boat", "flatbed" };
        static readonly string[] Movement = { "CharacterMotor", "FPSInputController" };

        static Transform player;
        static Transform vehicle;
        static Vector3 seat;
        static Vector3 localPlayer;
        static FsmString interaction;
        static bool hinting;
        static readonly System.Collections.Generic.Dictionary<Transform, Transform> pivots = new System.Collections.Generic.Dictionary<Transform, Transform>();

        internal static bool Active => vehicle != null;

        internal static void Tick(bool pressed)
        {
            if (player == null)
            {
                var go = GameObject.Find("PLAYER");
                if (go == null) return;
                player = go.transform;
            }
            var camera = Camera.main;
            if (camera == null) return;

            if (Active)
            {
                player.localPosition = localPlayer;
                if (pressed) Leave();
                return;
            }
            if (vehicle == null && player.parent != null && localPlayer != Vector3.zero) Release();

            Transform near = null;
            var nearSeat = Vector3.zero;
            if (player.parent == null)
                foreach (var link in CarSync.Links)
                {
                    Vector3 local;
                    if (link.Root == null || !SeatOf(link, out local)) continue;
                    var world = link.Root.TransformPoint(local);
                    if ((world - camera.transform.position).sqrMagnitude > Reach * Reach) continue;
                    near = link.Root;
                    nearSeat = local;
                    break;
                }
            ShowHint(near != null);
            if (near != null && pressed) Enter(near, nearSeat, camera.transform);
        }

        static bool SeatOf(CarSync.CarLink link, out Vector3 local)
        {
            local = Vector3.zero;
            foreach (var single in SingleSeat)
                if (link.Label.StartsWith(single)) return false;
            Transform pivot;
            if (!pivots.TryGetValue(link.Root, out pivot)) pivots[link.Root] = pivot = Find(link.Root, "DriverHeadPivot");
            if (pivot == null) return false;
            local = link.Root.InverseTransformPoint(pivot.position);
            if (link.Label.StartsWith("jonnez")) local.z -= BikeBack;
            else local.x = -local.x;
            return true;
        }

        static void Enter(Transform target, Vector3 local, Transform camera)
        {
            ShowHint(false);
            vehicle = target;
            seat = local;
            SetMovement(false);
            player.parent = vehicle;
            player.rotation = Quaternion.LookRotation(vehicle.forward, vehicle.up);
            player.position += vehicle.TransformPoint(seat) - camera.position;
            localPlayer = player.localPosition;
        }

        static void Leave()
        {
            var side = (seat.x >= 0f ? 1f : -1f) * vehicle.right;
            var outside = vehicle.TransformPoint(seat) + side * StepOut;
            RaycastHit hit;
            if (Physics.Raycast(outside + Vector3.up * 1.5f, Vector3.down, out hit, 6f)) outside = hit.point + Vector3.up * 0.3f;
            var yaw = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            Release();
            player.position = outside;
            player.rotation = yaw;
        }

        static void Release()
        {
            vehicle = null;
            localPlayer = Vector3.zero;
            if (player.parent != null) player.parent = null;
            SetMovement(true);
        }

        static void SetMovement(bool on)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = on;
            foreach (var name in Movement)
            {
                var behaviour = player.GetComponent(name) as Behaviour;
                if (behaviour != null) behaviour.enabled = on;
            }
        }

        static void ShowHint(bool on)
        {
            if (!on && !hinting) return;
            if (interaction == null) interaction = FsmVariables.GlobalVariables.FindFsmString("GUIinteraction");
            if (interaction == null) return;
            if (on) interaction.Value = Hint;
            else if (interaction.Value == Hint) interaction.Value = "";
            hinting = on;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
