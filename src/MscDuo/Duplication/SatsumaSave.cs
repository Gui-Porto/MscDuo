using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SatsumaSave
    {
        const string GameSave = "defaultES2File.txt";
        const string Marker = "MscDuoSeeded";

        internal static void Seed(string saveFile)
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
            var removed = 0;
            foreach (var tag in tags)
            {
                if (!tag.ToLowerInvariant().EndsWith("transform")) continue;
                ES2.Delete(saveFile + "?tag=" + tag);
                removed++;
            }
            ES2.Save(true, saveFile + "?tag=" + Marker);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: save próprio criado a partir do save da Satsuma 1 ({tags.Length} etiquetas, {removed} posições retiradas).");
        }
    }
}
