using System.Collections.Generic;
using System.Text;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class ItemRegistry
    {
        static readonly string[] Tags = { "PART", "ITEM" };
        static readonly List<Rigidbody> items = new List<Rigidbody>();
        static readonly Dictionary<Rigidbody, ushort> ids = new Dictionary<Rigidbody, ushort>();
        static readonly HashSet<string> clonesAdded = new HashSet<string>();

        internal static int Count => items.Count;
        internal static int Hash { get; private set; }
        internal static bool Final { get; private set; }
        internal static event System.Action Finalized;
        static int? remoteCount;
        static int remoteHash;

        internal static Rigidbody Get(ushort id) => id < items.Count ? items[id] : null;

        internal static void Build()
        {
            items.Clear();
            ids.Clear();
            clonesAdded.Clear();
            Final = false;
            remoteCount = null;
            Hash = 17;
            var excluded = new List<Transform>();
            var car = GameObject.Find("SATSUMA(557kg, 248)");
            if (car != null) excluded.Add(car.transform);
            var player = GameObject.Find("PLAYER");
            if (player != null) excluded.Add(player.transform);
            Add(Collect(Object.FindObjectsOfType<Rigidbody>(), excluded));
        }

        internal static void AddClone(GameObject car, params GameObject[] roots)
        {
            var bodies = new List<Rigidbody>();
            foreach (var root in roots)
                if (root != null && clonesAdded.Add(root.name)) bodies.AddRange(root.GetComponentsInChildren<Rigidbody>(true));
            Add(Collect(bodies.ToArray(), new List<Transform> { car != null ? car.transform : null }));
            Final = true;
            Compare();
            Finalized?.Invoke();
        }

        internal static void Remote(int count, int hash, bool final)
        {
            if (!final) return;
            remoteCount = count;
            remoteHash = hash;
            Compare();
        }

        internal static void Forget() => remoteCount = null;

        static void Compare()
        {
            if (!Final || !remoteCount.HasValue) return;
            var same = remoteCount.Value == Count && remoteHash == Hash;
            ItemSync.Enabled = same;
            if (same) ModConsole.Print($"<color=orange>[MSC Duo]</color> objetos soltos iguais nos dois jogos ({Count}); sincronização de objetos ligada.");
            else ModConsole.Warning($"[MSC Duo] os saves são diferentes ({Count} objetos aqui, {remoteCount.Value} no outro jogo): objetos soltos NÃO serão sincronizados. Copie o save do anfitrião de novo.");
        }

        static List<KeyValuePair<string, Rigidbody>> Collect(Rigidbody[] bodies, List<Transform> excluded)
        {
            var parts = LayerMask.NameToLayer("Parts");
            var found = new List<KeyValuePair<string, Rigidbody>>();
            foreach (var body in bodies)
            {
                if (body == null || ids.ContainsKey(body)) continue;
                var go = body.gameObject;
                if (go.layer != parts && System.Array.IndexOf(Tags, go.tag) < 0) continue;
                var skip = false;
                foreach (var root in excluded)
                    if (root != null && body.transform.IsChildOf(root)) { skip = true; break; }
                if (skip) continue;
                found.Add(new KeyValuePair<string, Rigidbody>(Key(body.transform), body));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return found;
        }

        static void Add(List<KeyValuePair<string, Rigidbody>> found)
        {
            foreach (var pair in found)
            {
                if (items.Count >= ushort.MaxValue) break;
                ids[pair.Value] = (ushort)items.Count;
                items.Add(pair.Value);
                foreach (var c in pair.Key.Substring(0, pair.Key.IndexOf('|'))) Hash = Hash * 31 + c;
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {items.Count} objetos soltos registrados para sincronizar.");
        }

        static string Key(Transform t)
        {
            var chain = new List<string>();
            for (var c = t; c != null; c = c.parent)
                chain.Add(c.parent != null ? c.name + "#" + c.GetSiblingIndex() : c.name);
            chain.Reverse();
            var sb = new StringBuilder(string.Join("/", chain.ToArray()));
            var p = t.position;
            sb.Append('|').Append(Mathf.RoundToInt(p.x * 2f)).Append(',').Append(Mathf.RoundToInt(p.y * 2f)).Append(',').Append(Mathf.RoundToInt(p.z * 2f));
            return sb.ToString();
        }
    }
}
