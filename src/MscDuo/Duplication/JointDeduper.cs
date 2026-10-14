using System.Collections;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class JointDeduper : MonoBehaviour
    {
        static readonly float[] Checks = { 1f, 3f, 6f, 10f };

        internal GameObject[] Roots;

        IEnumerator Start()
        {
            var removed = 0;
            var elapsed = 0f;
            foreach (var at in Checks)
            {
                yield return new WaitForSeconds(at - elapsed);
                elapsed = at;
                foreach (var root in Roots)
                    if (root != null) removed += Dedupe(root);
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {removed} travas duplicadas removidas.");
        }

        static int Dedupe(GameObject root)
        {
            var removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                removed += Keep<FixedJoint>(t.gameObject);
                removed += Keep<HingeJoint>(t.gameObject);
            }
            return removed;
        }

        static int Keep<T>(GameObject go) where T : Joint
        {
            var joints = go.GetComponents<T>();
            for (int i = 0; i < joints.Length - 1; i++) Destroy(joints[i]);
            return Mathf.Max(0, joints.Length - 1);
        }
    }
}
