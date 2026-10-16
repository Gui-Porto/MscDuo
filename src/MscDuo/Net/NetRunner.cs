using MSCLoader;
using MscDuo.Sync;
using MscDuo.Tools;
using UnityEngine;

namespace MscDuo.Net
{
    internal class NetRunner : MonoBehaviour
    {
        static NetRunner instance;

        internal static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("MscDuo_Net");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NetRunner>();
        }

        void Update()
        {
            try
            {
                NetSession.Current?.Tick();
            }
            catch (System.Exception e)
            {
                ModConsole.Error($"[MSC Duo] erro na rede, sessão encerrada: {e}");
                NetSession.Stop();
            }
        }

        void OnGUI()
        {
            NetHud.Draw();
            SaveTransfer.Draw();
        }

        void OnApplicationQuit()
        {
            NetSession.Stop();
            SaveTransfer.Restore();
        }
    }
}
