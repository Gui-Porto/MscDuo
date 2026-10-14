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
                    if (root != null) removed += Dedupe(root) + Reconnect(root);
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {removed} travas duplicadas removidas ou religadas.");
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

        static int Reconnect(GameObject root)
        {
            var map = SatsumaCloner.Map;
            if (map == null) return 0;
            var fixedCount = 0;
            foreach (var joint in root.GetComponentsInChildren<Joint>(true))
            {
                var body = joint.connectedBody;
                Object mapped;
                if (body == null || !map.TryGetValue(body, out mapped)) continue;
                joint.connectedBody = (Rigidbody)mapped;
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: junta de '{joint.name}' estava presa em '{body.name}' da Satsuma 1; religada à Satsuma 2.");
                fixedCount++;
            }
            return fixedCount;
        }

        static int Keep<T>(GameObject go) where T : Joint
        {
            var joints = go.GetComponents<T>();
            for (int i = 0; i < joints.Length - 1; i++)
            {
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: trava {typeof(T).Name} duplicada removida de '{go.name}'.");
                Destroy(joints[i]);
            }
            return Mathf.Max(0, joints.Length - 1);
        }
    }
}
