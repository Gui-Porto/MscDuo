using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class LookCommand : ConsoleCommand
    {
        public override string Name => "duo_olhar";
        public override string Help => "duo_olhar [alcance = 5]  lista tudo o que o raio da câmera atravessa e o estado de interação do jogador";

        public override void Run(string[] args)
        {
            var reach = 5f;
            if (args.Length > 0 && !DevCommands.TryFloat(args[0], out reach)) { ModConsole.Error(Help); return; }
            var camera = Camera.main;
            if (camera == null) { ModConsole.Error("[MSC Duo] câmera não encontrada"); return; }

            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            sb.AppendLine(string.Format(inv, "# câmera '{0}' em ({1:0.##}, {2:0.##}, {3:0.##}) olhando ({4:0.##}, {5:0.##}, {6:0.##}), alcance {7}",
                Path(camera.transform), ray.origin.x, ray.origin.y, ray.origin.z, ray.direction.x, ray.direction.y, ray.direction.z, reach));

            var hits = Physics.RaycastAll(ray, reach, ~0);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            sb.AppendLine($"## {hits.Length} colisores no raio");
            foreach (var hit in hits)
            {
                var c = hit.collider;
                var names = new List<string>();
                foreach (var component in c.GetComponents<Component>())
                    if (component != null) names.Add(component.GetType().Name);
                var fsms = new List<string>();
                foreach (var fsm in c.GetComponents<PlayMakerFSM>())
                    fsms.Add(fsm.FsmName + "=" + fsm.ActiveStateName);
                sb.AppendLine(string.Format(inv, "{0:0.00} m | camada {1} ({2}) | trigger={3} | {4} | [{5}] | fsm: {6}",
                    hit.distance, c.gameObject.layer, LayerMask.LayerToName(c.gameObject.layer), c.isTrigger, Path(c.transform),
                    string.Join(", ", names.ToArray()), string.Join(", ", fsms.ToArray())));
            }

            sb.AppendLine("## sons tocando a até 8 m");
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (!audio.isPlaying) continue;
                var distance = Vector3.Distance(audio.transform.position, ray.origin);
                if (distance > 8f) continue;
                sb.AppendLine(string.Format(inv, "{0:0.0} m | {1} | clip {2} | volume {3:0.00} | loop={4}",
                    distance, Path(audio.transform), audio.clip != null ? audio.clip.name : "null", audio.volume, audio.loop));
            }

            sb.AppendLine("## globais do jogador");
            foreach (var v in FsmVariables.GlobalVariables.BoolVariables)
                if (v.Value) sb.AppendLine($"bool {v.Name} = true");
            foreach (var v in FsmVariables.GlobalVariables.StringVariables)
                if (v.Name.StartsWith("GUI") || v.Name.StartsWith("Player")) sb.AppendLine($"string {v.Name} = '{v.Value}'");
            foreach (var v in FsmVariables.GlobalVariables.GameObjectVariables)
                if (v.Name.StartsWith("Player") || v.Name.StartsWith("Hand") || v.Name.StartsWith("Picked")) sb.AppendLine($"gameobject {v.Name} = {(v.Value != null ? Path(v.Value.transform) : "null")}");

            var folder = System.IO.Path.Combine(MscDuo.Instance.SettingsFolder, "dumps");
            Directory.CreateDirectory(folder);
            var file = System.IO.Path.Combine(folder, "olhar-" + DateTime.Now.ToString("HHmmss") + ".txt");
            File.WriteAllText(file, sb.ToString());
            for (int i = 0; i < hits.Length && i < 6; i++)
                ModConsole.Print(string.Format(inv, "{0:0.00} m  {1}  (camada {2})", hits[i].distance, Path(hits[i].collider.transform), LayerMask.LayerToName(hits[i].collider.gameObject.layer)));
            ModConsole.Print($"<color=orange>[MSC Duo]</color> olhar salvo em {file}");
        }

        static string Path(Transform t)
        {
            var path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
