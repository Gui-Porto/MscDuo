using System.IO;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SatsumaSave
    {
        const string Marker = "MscDuoNewGame1";

        internal static void Seed(string saveFile)
        {
            var target = Path.Combine(Application.persistentDataPath, saveFile);
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
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: save antigo guardado em {saveFile}.old.");
            }
            ES2.Save(true, saveFile + "?tag=" + Marker);
            ModConsole.Print("<color=orange>[MSC Duo]</color> Satsuma 2: save próprio novo e vazio; ela começa como num jogo novo.");
        }
    }
}
