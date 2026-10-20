using System.Collections.Generic;
using System.IO;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class P2Joints : MonoBehaviour
    {
        static readonly float[] Passes = { 3f, 6f, 10f, 20f, 40f };
        const float Interval = 10f;

        float started;
        int pass;
        float next;
        int orphans;
        int relinked;
        int sounds;

        internal static void Watch()
        {
            var old = GameObject.Find("MscDuo_P2Joints");
            if (old != null) Destroy(old);
            new GameObject("MscDuo_P2Joints").AddComponent<P2Joints>();
        }

        void Start()
        {
            started = Time.time;
            next = started + Passes[0];
        }

        void Update()
        {
            if (Time.time < next) return;
            if (SatsumaCloner.Clone == null) { Destroy(gameObject); return; }
            pass++;
            next = pass < Passes.Length ? started + Passes[pass] : Time.time + Interval;
            var report = pass == 1 ? new StringBuilder() : null;
            var before = orphans + relinked + sounds;
            Sweep(report);
            if (report != null) Save(report);
            if (orphans + relinked + sounds > before || pass == 1)
                ModConsole.Print($"<color=orange>[MSC Duo]</color> juntas da Satsuma 2: {orphans} órfãs removidas, {relinked} religadas da Satsuma 1 para a 2, {sounds} sons presos desligados.");
        }

        static IEnumerable<GameObject> Roots()
        {
            if (SatsumaCloner.Clone != null) yield return SatsumaCloner.Clone;
            if (SatsumaCloner.Parts != null) yield return SatsumaCloner.Parts;
            if (SatsumaCloner.Database != null) yield return SatsumaCloner.Database;
        }

        static IEnumerable<GameObject> OriginalRoots()
        {
            if (SatsumaCloner.Original != null) yield return SatsumaCloner.Original;
            if (SatsumaCloner.OriginalDatabase != null) yield return SatsumaCloner.OriginalDatabase;
            var parts = SatsumaCloner.FindRoot("CARPARTS");
            if (parts != null) yield return parts;
        }

        static bool Inside(Transform t, IEnumerable<GameObject> roots)
        {
            foreach (var root in roots)
                if (root != null && t.IsChildOf(root.transform)) return true;
            return false;
        }

        void Sweep(StringBuilder report)
        {
            var referenced = new HashSet<Joint>();
            foreach (var root in Roots())
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    foreach (var variable in fsm.FsmVariables.ObjectVariables)
                    {
                        var joint = variable.Value as Joint;
                        if (joint != null) referenced.Add(joint);
                    }

            var map = SatsumaCloner.Map;
            foreach (var root in Roots())
                foreach (var joint in root.GetComponentsInChildren<Joint>(true))
                {
                    if (joint == null) continue;
                    var body = joint.connectedBody;
                    var p1 = body != null && Inside(body.transform, OriginalRoots());
                    var siblings = joint.GetComponents<FixedJoint>().Length;
                    var used = referenced.Contains(joint);
                    report?.AppendLine($"{PathOf(joint.transform)} | {joint.GetType().Name} | ligada a {(body != null ? PathOf(body.transform) : "mundo")} | {(p1 ? "SATSUMA 1" : "ok")} | referenciada={used} | fixas no objeto={siblings}");

                    if (p1)
                    {
                        Object mapped;
                        if (map != null && map.TryGetValue(body, out mapped) && mapped is Rigidbody)
                        {
                            joint.connectedBody = (Rigidbody)mapped;
                            relinked++;
                        }
                        continue;
                    }
                }

            var seen = new HashSet<GameObject>();
            foreach (var root in Roots())
                foreach (var joint in root.GetComponentsInChildren<FixedJoint>(true))
                {
                    if (joint == null || !seen.Add(joint.gameObject)) continue;
                    var all = joint.GetComponents<FixedJoint>();
                    var left = all.Length;
                    foreach (var candidate in all)
                    {
                        if (left <= 1) break;
                        if (referenced.Contains(candidate)) continue;
                        Destroy(candidate);
                        left--;
                        orphans++;
                    }
                }

            foreach (var root in Roots())
                foreach (var audio in root.GetComponentsInChildren<AudioSource>())
                {
                    if (audio.gameObject.name != "PouringSound" || !audio.gameObject.activeSelf) continue;
                    var trigger = audio.transform.parent != null ? audio.transform.parent.GetComponent<PlayMakerFSM>() : null;
                    if (trigger != null && trigger.ActiveStateName == "Pouring") continue;
                    audio.gameObject.SetActive(false);
                    sounds++;
                }
        }

        static void Save(StringBuilder report)
        {
            try
            {
                var folder = Path.Combine(MscDuo.Instance.SettingsFolder, "dumps");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "juntas-p2.txt"), report.ToString());
            }
            catch (IOException) { }
        }

        static string PathOf(Transform t)
        {
            var path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
