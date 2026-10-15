using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class EarlyHook : MonoBehaviour
    {
        static EarlyHook instance;

        internal static void Install()
        {
            if (instance != null) return;
            var go = new GameObject("MscDuo_EarlyHook");
            Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<EarlyHook>();
        }

        void OnLevelWasLoaded(int level)
        {
            if (Application.loadedLevelName != "GAME") return;
            try { SatsumaCloner.SpawnEarly(); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] Satsuma 2 (cedo) falhou: {e}"); }
        }
    }
}
