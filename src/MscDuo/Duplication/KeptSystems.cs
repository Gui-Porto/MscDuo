using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class KeptSystems
    {
        internal static bool Keeps(Transform t) => IsDoorPart(t);

        static bool IsDoorPart(Transform t)
        {
            for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
            {
                if (t.name != "Pivot" || t.parent == null) continue;
                return t.parent.name.StartsWith("Door");
            }
            return false;
        }
    }
}
