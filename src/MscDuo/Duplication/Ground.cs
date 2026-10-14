using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class Ground
    {
        internal const float ProbeHeight = 1.5f;

        internal static bool TryHeight(Vector3 position, float above, out float y, params GameObject[] ignore)
        {
            y = float.MinValue;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * above, Vector3.down, 50f))
            {
                if (hit.collider.isTrigger || hit.point.y <= y || Ignored(hit.collider.transform, ignore)) continue;
                y = hit.point.y;
            }
            return y > float.MinValue;
        }

        internal static bool TryClearance(Transform t, float above, out float clearance, params GameObject[] ignore)
        {
            clearance = 0f;
            float y;
            if (!TryHeight(t.position, above, out y, ignore)) return false;
            clearance = t.position.y - y;
            return true;
        }

        static bool Ignored(Transform t, GameObject[] ignore)
        {
            foreach (var root in ignore)
                if (root != null && t.IsChildOf(root.transform)) return true;
            return false;
        }
    }
}
