using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class ProbeCommand : ConsoleCommand
    {
        public override string Name => "duo_probe";
        public override string Help => "duo_probe [raio = 30]  lista renderers, colliders e malhas em volta do jogador";

        public override void Run(string[] args)
        {
            var player = DevCommands.Player;
            if (player == null) { ModConsole.Error("PLAYER não encontrado"); return; }
            var radius = 30f;
            if (args.Length > 0 && !DevCommands.TryFloat(args[0], out radius)) { ModConsole.Error(Help); return; }

            var center = player.position;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(inv, "# probe em ({0:0.##}, {1:0.##}, {2:0.##}) raio {3}", center.x, center.y, center.z, radius));
            sb.AppendLine("kind,dist,x,y,z,size_x,size_y,size_z,active,static_batch,mesh,mesh_vertices,readable,components,path");

            var seen = new HashSet<int>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r.hideFlags != HideFlags.None) continue;
                if (!Intersects(r.bounds, center, radius)) continue;
                seen.Add(r.gameObject.GetInstanceID());
                var filter = r.GetComponent<MeshFilter>();
                Append(sb, "renderer", r.gameObject, r.bounds, center, r.isPartOfStaticBatch, filter != null ? filter.sharedMesh : null);
            }
            foreach (var c in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (c.hideFlags != HideFlags.None || seen.Contains(c.gameObject.GetInstanceID())) continue;
                if (!c.enabled || !Intersects(c.bounds, center, radius)) continue;
                var mc = c as MeshCollider;
                Append(sb, "collider", c.gameObject, c.bounds, center, false, mc != null ? mc.sharedMesh : null);
            }

            var folder = Path.Combine(MscDuo.Instance.SettingsFolder, "probes");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
            File.WriteAllText(file, sb.ToString());
            ModConsole.Print($"<color=orange>[MSC Duo]</color> probe salvo em {file}");
        }

        static bool Intersects(Bounds b, Vector3 center, float radius)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(center.x - b.center.x) - b.extents.x);
            var dz = Mathf.Max(0f, Mathf.Abs(center.z - b.center.z) - b.extents.z);
            return dx * dx + dz * dz <= radius * radius && b.size.magnitude < 5000f;
        }

        static void Append(StringBuilder sb, string kind, GameObject go, Bounds b, Vector3 center, bool batched, Mesh mesh)
        {
            var inv = CultureInfo.InvariantCulture;
            var p = go.transform.position;
            var comps = new List<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c != null && !(c is Transform)) comps.Add(c.GetType().Name);

            var readable = "";
            var vertexCount = 0;
            if (mesh != null)
            {
                vertexCount = mesh.vertexCount;
                readable = IsReadable(mesh) ? "1" : "0";
            }

            var path = go.name;
            var t = go.transform.parent;
            for (int depth = 0; t != null && depth < 6; depth++, t = t.parent) path = t.name + "/" + path;

            sb.AppendLine(string.Format(inv, "{0},{1:0.#},{2:0.##},{3:0.##},{4:0.##},{5:0.#},{6:0.#},{7:0.#},{8},{9},\"{10}\",{11},{12},\"{13}\",\"{14}\"",
                kind, Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(center.x, 0f, center.z)),
                p.x, p.y, p.z, b.size.x, b.size.y, b.size.z,
                go.activeInHierarchy ? 1 : 0, batched ? 1 : 0,
                mesh != null ? mesh.name.Replace("\"", "'") : "", vertexCount, readable,
                string.Join(" ", comps.ToArray()), path.Replace("\"", "'")));
        }

        static bool IsReadable(Mesh mesh)
        {
            try
            {
                return mesh.vertexCount > 0 && mesh.vertices.Length == mesh.vertexCount;
            }
            catch
            {
                return false;
            }
        }
    }
}
