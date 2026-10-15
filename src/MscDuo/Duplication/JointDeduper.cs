using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
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
                    if (root != null) removed += Dedupe(root) + Reconnect(root) + ReconnectLoose(root);
            }
            MatchMass();
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
            if (joints.Length < 2) return 0;
            var keep = joints.Length - 1;
            for (int i = joints.Length - 1; i >= 0; i--)
                if (joints[i].connectedBody != null) { keep = i; break; }
            for (int i = 0; i < joints.Length; i++)
            {
                if (i == keep) continue;
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: trava {typeof(T).Name} duplicada removida de '{go.name}'.");
                Destroy(joints[i]);
            }
            return joints.Length - 1;
        }

        static int ReconnectLoose(GameObject root)
        {
            var map = SatsumaCloner.Map;
            if (map == null) return 0;
            var reverse = new Dictionary<Object, Object>();
            foreach (var pair in map) if (pair.Value != null) reverse[pair.Value] = pair.Key;

            var fixedCount = 0;
            foreach (var joint in root.GetComponentsInChildren<Joint>(true))
            {
                if (joint.connectedBody != null) continue;
                Object original;
                if (!reverse.TryGetValue(joint.gameObject, out original) || original == null) continue;
                foreach (var joint1 in ((GameObject)original).GetComponents(joint.GetType()))
                {
                    var body1 = ((Joint)joint1).connectedBody;
                    Object mapped;
                    if (body1 == null || !map.TryGetValue(body1, out mapped)) continue;
                    joint.connectedBody = (Rigidbody)mapped;
                    var copied = CopySettings((Joint)joint1, joint);
                    ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: junta de '{joint.name}' estava presa ao mundo; ligada a '{mapped.name}' com {copied} ajustes copiados da Satsuma 1.");
                    fixedCount++;
                    break;
                }
            }
            return fixedCount;
        }

        static readonly HashSet<string> SkippedProperties = new HashSet<string> { "connectedBody", "name", "tag", "hideFlags" };

        static int CopySettings(Joint from, Joint to)
        {
            var copied = 0;
            foreach (var property in from.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || SkippedProperties.Contains(property.Name) || property.GetIndexParameters().Length > 0) continue;
                try
                {
                    property.SetValue(to, property.GetValue(from, null), null);
                    copied++;
                }
                catch { }
            }
            return copied;
        }

        static void MatchMass()
        {
            var locals = SatsumaCloner.Variables;
            NamedVariable local;
            if (locals == null || !locals.TryGetValue("CarMass", out local)) return;
            var global = FsmVariables.GlobalVariables.FindFsmFloat("CarMass");
            var mass = (FsmFloat)local;
            if (global == null || mass.Value >= global.Value) return;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: massa ajustada de {mass.Value:0.#} para {global.Value:0.#} kg, igual à Satsuma 1.");
            mass.Value = global.Value;
        }
    }
}
