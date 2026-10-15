using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SatsumaSave
    {
        const string GameSave = "defaultES2File.txt";
        const string Marker = "MscDuoSeeded3";

        internal static void Seed(string saveFile, Matrix4x4 move, Quaternion turn)
        {
            var folder = Application.persistentDataPath;
            var source = Path.Combine(folder, GameSave);
            var target = Path.Combine(folder, saveFile);
            if (File.Exists(target) && ES2.Exists(saveFile + "?tag=" + Marker))
            {
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: usando o save próprio ({saveFile}).");
                return;
            }
            if (File.Exists(target))
            {
                var backup = target + ".old";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(target, backup);
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: save sem marca do mod guardado em {saveFile}.old; criando um novo.");
            }
            if (!File.Exists(source))
            {
                ModConsole.Warning($"[MSC Duo] Satsuma 2: save do jogo não encontrado em {source}; ela vai nascer sem apertos.");
                return;
            }

            File.Copy(source, target);
            var tags = ES2.GetTags(saveFile);
            var moved = 0;
            var probe = new GameObject("MscDuo_SaveProbe").transform;
            try
            {
                foreach (var tag in tags)
                {
                    if (!tag.ToLowerInvariant().EndsWith("transform")) continue;
                    var id = saveFile + "?tag=" + tag;
                    try { ES2.Load<Transform>(id, probe); }
                    catch { continue; }
                    probe.position = move.MultiplyPoint3x4(probe.position);
                    probe.rotation = turn * probe.rotation;
                    ES2.Save(probe, id);
                    moved++;
                }
            }
            finally { Object.Destroy(probe.gameObject); }
            ES2.Save(true, saveFile + "?tag=" + Marker);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: save próprio criado a partir do save da Satsuma 1 ({tags.Length} etiquetas, {moved} posições levadas da casa 1 para a casa 2).");
        }
    }
}
