using System.Collections.Generic;
using System.IO;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class ItemRegistry
    {
        const float MatchDistance = 2f;
        const string ReportFile = "objetos.txt";

        static readonly string[] Tags = { "PART", "ITEM" };
        static readonly List<Rigidbody> items = new List<Rigidbody>();
        static readonly List<string> paths = new List<string>();
        static readonly List<Vector3> origins = new List<Vector3>();
        static readonly HashSet<Rigidbody> known = new HashSet<Rigidbody>();
        static readonly HashSet<string> clonesAdded = new HashSet<string>();
        static readonly Dictionary<ushort, ushort> fromRemote = new Dictionary<ushort, ushort>();
        static string[] pendingPaths;
        static Vector3[] pendingOrigins;

        internal static int Count => items.Count;
        internal static bool Final { get; private set; }
        internal static int Matched => fromRemote.Count;
        internal static int RemoteCount { get; private set; }
        internal static string Status { get; private set; } = "esperando o outro jogo";

        internal static Rigidbody Get(ushort id) => id < items.Count ? items[id] : null;

        internal static bool TryLocal(ushort remoteId, out ushort localId) => fromRemote.TryGetValue(remoteId, out localId);

        internal static void Build()
        {
            items.Clear();
            paths.Clear();
            origins.Clear();
            known.Clear();
            clonesAdded.Clear();
            Final = false;
            Forget();
            var excluded = new List<Transform>();
            var car = GameObject.Find("SATSUMA(557kg, 248)");
            if (car != null) excluded.Add(car.transform);
            if (Duplication.SatsumaCloner.Clone != null) excluded.Add(Duplication.SatsumaCloner.Clone.transform);
            var player = GameObject.Find("PLAYER");
            if (player != null) excluded.Add(player.transform);
            Add(Object.FindObjectsOfType<Rigidbody>(), excluded);
        }

        internal static void AddClone(GameObject car, params GameObject[] roots)
        {
            var bodies = new List<Rigidbody>();
            foreach (var root in roots)
                if (root != null && clonesAdded.Add(root.name)) bodies.AddRange(root.GetComponentsInChildren<Rigidbody>(true));
            Add(bodies.ToArray(), new List<Transform> { car != null ? car.transform : null });
            Final = true;
            Status = "esperando o outro jogo";
            if (pendingPaths != null) Match(pendingPaths, pendingOrigins);
        }

        internal static void Forget()
        {
            fromRemote.Clear();
            pendingPaths = null;
            pendingOrigins = null;
            RemoteCount = 0;
            Status = Final ? "esperando o outro jogo" : "esperando o registro local";
            ItemSync.Enabled = false;
        }

        internal static void WriteInfo(BinaryWriter w)
        {
            w.Write(Final);
            w.Write(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                w.Write(paths[i]);
                w.Write(origins[i].x);
                w.Write(origins[i].y);
                w.Write(origins[i].z);
            }
        }

        internal static bool ReadInfo(BinaryReader r)
        {
            if (!r.ReadBoolean()) return false;
            var count = r.ReadInt32();
            var remotePaths = new string[count];
            var remoteOrigins = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                remotePaths[i] = r.ReadString();
                remoteOrigins[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            }
            if (!Final)
            {
                pendingPaths = remotePaths;
                pendingOrigins = remoteOrigins;
                Status = "esperando o registro local";
                return false;
            }
            Match(remotePaths, remoteOrigins);
            return true;
        }

        static void Match(string[] remotePaths, Vector3[] remoteOrigins)
        {
            fromRemote.Clear();
            RemoteCount = remotePaths.Length;
            var byPath = new Dictionary<string, List<int>>();
            for (int i = 0; i < paths.Count; i++)
            {
                List<int> list;
                if (!byPath.TryGetValue(paths[i], out list)) byPath[paths[i]] = list = new List<int>();
                list.Add(i);
            }

            var used = new HashSet<int>();
            var remoteOnly = new List<string>();
            for (int r = 0; r < remotePaths.Length; r++)
            {
                List<int> candidates;
                var best = -1;
                var bestDistance = MatchDistance * MatchDistance;
                if (byPath.TryGetValue(remotePaths[r], out candidates))
                    foreach (var l in candidates)
                    {
                        if (used.Contains(l)) continue;
                        var d = (origins[l] - remoteOrigins[r]).sqrMagnitude;
                        if (d <= bestDistance) { bestDistance = d; best = l; }
                    }
                if (best < 0) { remoteOnly.Add(Describe(remotePaths[r], remoteOrigins[r])); continue; }
                used.Add(best);
                fromRemote[(ushort)r] = (ushort)best;
            }

            var localOnly = new List<string>();
            for (int i = 0; i < paths.Count; i++)
                if (!used.Contains(i)) localOnly.Add(Describe(paths[i], origins[i]));

            ItemSync.Enabled = fromRemote.Count > 0;
            Status = $"{fromRemote.Count} em comum, {localOnly.Count} só aqui, {remoteOnly.Count} só no outro";
            WriteReport(localOnly, remoteOnly);
            if (localOnly.Count == 0 && remoteOnly.Count == 0)
                ModConsole.Print($"<color=orange>[MSC Duo]</color> objetos soltos iguais nos dois jogos ({fromRemote.Count}); sincronização de objetos ligada.");
            else
                ModConsole.Warning($"[MSC Duo] objetos soltos: {Status}. Os em comum serão sincronizados; lista das diferenças em {ReportFile}.");
        }

        static void WriteReport(List<string> localOnly, List<string> remoteOnly)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# {Status}");
                sb.AppendLine($"## só neste jogo ({localOnly.Count})");
                foreach (var line in localOnly) sb.AppendLine(line);
                sb.AppendLine($"## só no outro jogo ({remoteOnly.Count})");
                foreach (var line in remoteOnly) sb.AppendLine(line);
                sb.AppendLine($"## todos deste jogo ({paths.Count})");
                for (int i = 0; i < paths.Count; i++) sb.AppendLine(Describe(paths[i], origins[i]));
                File.WriteAllText(Path.Combine(MscDuo.Instance.SettingsFolder, ReportFile), sb.ToString(), Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                ModConsole.Warning($"[MSC Duo] não foi possível gravar {ReportFile}: {e.Message}");
            }
        }

        static string Describe(string path, Vector3 origin) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}  ({1:0.0}, {2:0.0}, {3:0.0})", path, origin.x, origin.y, origin.z);

        static void Add(Rigidbody[] bodies, List<Transform> excluded)
        {
            var layer = LayerMask.NameToLayer("Parts");
            foreach (var body in bodies)
            {
                if (body == null || known.Contains(body) || items.Count >= ushort.MaxValue) continue;
                var go = body.gameObject;
                if (go.layer != layer && System.Array.IndexOf(Tags, go.tag) < 0) continue;
                var skip = false;
                foreach (var root in excluded)
                    if (root != null && body.transform.IsChildOf(root)) { skip = true; break; }
                if (skip) continue;
                known.Add(body);
                items.Add(body);
                paths.Add(PathOf(body.transform));
                origins.Add(body.position);
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {items.Count} objetos soltos registrados para sincronizar.");
        }

        static string PathOf(Transform t)
        {
            var chain = new List<string>();
            for (var c = t; c != null; c = c.parent) chain.Add(c.name);
            chain.Reverse();
            return string.Join("/", chain.ToArray());
        }
    }
}
