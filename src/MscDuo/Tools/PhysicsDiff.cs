using System.Collections.Generic;
using System.IO;
using System.Text;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal static class PhysicsDiff
    {
        internal static void Run(string folder)
        {
            var map = SatsumaCloner.Map;
            var car1 = SatsumaCloner.Original;
            var car2 = SatsumaCloner.Clone;
            if (map == null || car1 == null || car2 == null) { ModConsole.Error("Satsuma 2 não está ativa"); return; }

            var text = new StringBuilder();
            var reverse = new Dictionary<Object, Object>();
            foreach (var pair in map) if (pair.Value != null) reverse[pair.Value] = pair.Key;

            int differences = 0, onlyIn2 = 0;
            foreach (var t2 in car2.GetComponentsInChildren<Transform>(true))
            {
                Object o1;
                if (!reverse.TryGetValue(t2.gameObject, out o1) || o1 == null)
                {
                    if (t2.GetComponent<Rigidbody>() != null || t2.GetComponent<Joint>() != null)
                    {
                        text.AppendLine($"SÓ NA 2: {Path(t2, car2.transform)} {Describe(t2.gameObject, car2)}");
                        onlyIn2++;
                    }
                    continue;
                }
                var g1 = (GameObject)o1;
                var d1 = Describe(g1, car1);
                var d2 = Describe(t2.gameObject, car2);
                var p1 = car1.transform.InverseTransformPoint(g1.transform.position);
                var p2 = car2.transform.InverseTransformPoint(t2.position);
                var moved = (p1 - p2).magnitude > 0.02f;
                if (d1 == d2 && !moved) continue;
                if (d1 == "" && d2 == "" ) continue;
                text.AppendLine($"{Path(t2, car2.transform)}");
                text.AppendLine($"   1: {d1} pos={Fmt(p1)}");
                text.AppendLine($"   2: {d2} pos={Fmt(p2)}");
                differences++;
            }

            foreach (var t1 in car1.GetComponentsInChildren<Transform>(true))
            {
                if (map.ContainsKey(t1.gameObject)) continue;
                if (t1.GetComponent<Rigidbody>() == null && t1.GetComponent<Joint>() == null) continue;
                text.AppendLine($"SÓ NA 1: {Path(t1, car1.transform)} {Describe(t1.gameObject, car1)}");
            }

            Directory.CreateDirectory(folder);
            var file = System.IO.Path.Combine(folder, "physics_diff.txt");
            File.WriteAllText(file, text.ToString());
            ModConsole.Print($"<color=orange>[MSC Duo]</color> física comparada: {differences} diferenças, {onlyIn2} corpos só na Satsuma 2. Arquivo: {file}");
        }

        static string Describe(GameObject go, GameObject car)
        {
            var parts = new List<string>();
            var body = go.GetComponent<Rigidbody>();
            if (body != null) parts.Add($"rb(kin={body.isKinematic} massa={body.mass:0.#} grav={body.useGravity} ativo={go.activeInHierarchy})");
            foreach (var joint in go.GetComponents<Joint>())
            {
                var target = joint.connectedBody == null ? "mundo" : joint.connectedBody.transform.IsChildOf(car.transform) ? joint.connectedBody.name : "FORA:" + joint.connectedBody.transform.root.name + "/" + joint.connectedBody.name;
                parts.Add($"{joint.GetType().Name}(->{target} ativo={joint.enableCollision} quebra={joint.breakForce:0})");
            }
            var col = go.GetComponents<Collider>();
            if (body != null || parts.Count > 0) parts.Add($"camada={go.layer} colisores={col.Length}");
            return string.Join(" ", parts.ToArray());
        }

        static string Path(Transform t, Transform root) => t == root || t.parent == null ? t.name : Path(t.parent, root) + "/" + t.name;
        static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
