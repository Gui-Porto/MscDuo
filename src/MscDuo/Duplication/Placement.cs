using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class Placement
    {
        const float MaxStep = 0.8f;
        const float Headroom = 2.6f;
        const float Margin = 0.6f;

        static readonly Vector2[] Candidates =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(-1.8f, 0f), new Vector2(1.8f, 0f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(0f, 1.8f), new Vector2(0f, -1.8f),
            new Vector2(-2.6f, 0f), new Vector2(2.6f, 0f), new Vector2(0f, 2.6f), new Vector2(0f, -2.6f),
        };

        internal static bool Beside(GameObject vehicle, GameObject anchor, params GameObject[] ignore)
        {
            if (vehicle == null || anchor == null) return false;
            var all = new GameObject[ignore.Length + 2];
            all[0] = vehicle;
            all[1] = anchor;
            ignore.CopyTo(all, 2);

            float baseGround;
            if (!Ground.TryHeight(anchor.transform.position, 0.3f, out baseGround, all)) return false;
            var right = anchor.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();
            var forward = Vector3.Cross(right, Vector3.up);

            Vector3 half;
            var size = Size(vehicle, out half);
            var anchorHalf = Size(anchor, out _) * 0.5f;
            var stepX = anchorHalf.x + half.x + Margin;
            var stepZ = anchorHalf.z + half.z + Margin;

            var yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            foreach (var c in Candidates)
            {
                var spot = anchor.transform.position + right * (c.x * stepX) + forward * (c.y * stepZ);
                float ground;
                if (!Free(spot, right, forward, half, baseGround, out ground, all)) continue;
                vehicle.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                vehicle.transform.position = new Vector3(spot.x, ground, spot.z);
                vehicle.transform.position += Vector3.up * (0.05f - VehicleCloner.BottomOffset(vehicle));
                var body = vehicle.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                return true;
            }
            return false;
        }

        static Vector3 Size(GameObject go, out Vector3 half)
        {
            var rotation = go.transform.rotation;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            var found = false;
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (!found) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            go.transform.rotation = rotation;
            var size = found ? bounds.size : new Vector3(2f, 2f, 4.5f);
            size.x = Mathf.Clamp(size.x, 0.8f, 4f);
            size.z = Mathf.Clamp(size.z, 1.5f, 10f);
            half = size * 0.5f;
            return size;
        }

        static bool Free(Vector3 spot, Vector3 right, Vector3 forward, Vector3 half, float baseGround, out float ground, GameObject[] ignore)
        {
            ground = float.MinValue;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    var point = spot + right * (half.x * i) + forward * (half.z * j);
                    point.y = baseGround;
                    float y;
                    if (!Ground.TryHeight(point, MaxStep, out y, ignore) || Mathf.Abs(y - baseGround) > MaxStep) return false;
                    if (Blocked(new Vector3(point.x, y + 0.3f, point.z), ignore)) return false;
                    ground = Mathf.Max(ground, y);
                }
            return true;
        }

        static bool Blocked(Vector3 from, GameObject[] ignore)
        {
            foreach (var hit in Physics.RaycastAll(from, Vector3.up, Headroom))
            {
                if (hit.collider.isTrigger) continue;
                var mine = false;
                foreach (var root in ignore)
                    if (root != null && hit.collider.transform.IsChildOf(root.transform)) mine = true;
                if (!mine) return true;
            }
            return false;
        }
    }
}
