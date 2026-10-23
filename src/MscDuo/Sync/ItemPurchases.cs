using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Sync
{
    internal static class ItemPurchases
    {
        const string SpawnEvent = "SPAWNITEM";

        static readonly Dictionary<string, PlayMakerFSM> spawners = new Dictionary<string, PlayMakerFSM>();
        static readonly Dictionary<string, Queue<ushort>> mirrors = new Dictionary<string, Queue<ushort>>();
        static Action<string, ushort> announce;
        static Action<ushort, ushort> link;

        internal static void Bind(Action<string, ushort> sendAnnounce, Action<ushort, ushort> sendLink)
        {
            announce = sendAnnounce;
            link = sendLink;
        }

        internal static void Patch()
        {
            spawners.Clear();
            mirrors.Clear();
            int hooked = 0;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.gameObject.hideFlags != HideFlags.None || !fsm.gameObject.name.StartsWith("Create")) continue;
                try
                {
                    Duplication.FsmTools.EnsureLoaded(fsm);
                    if (!Array.Exists(fsm.FsmEvents, e => e != null && e.Name == SpawnEvent)) continue;
                    var key = PathOf(fsm.transform) + ":" + fsm.FsmName;
                    if (spawners.ContainsKey(key)) continue;
                    var found = false;
                    foreach (var state in fsm.FsmStates)
                    {
                        for (int i = 0; i < state.Actions.Length; i++)
                        {
                            var create = state.Actions[i] as CreateObject;
                            if (create == null || create.storeObject == null) continue;
                            var action = create;
                            var spawnerKey = key;
                            fsm.FsmInject(state.Name, () => OnCreated(spawnerKey, action), false, i + 1);
                            found = true;
                            hooked++;
                            break;
                        }
                    }
                    if (found) spawners[key] = fsm;
                }
                catch (Exception e)
                {
                    ModConsole.Warning($"[MSC Duo] gerador de itens '{fsm.gameObject.name}/{fsm.FsmName}' não preparado: {e.Message}");
                }
            }
            ModConsole.Print($"<color=orange>[MSC Duo]</color> compras: {spawners.Count} geradores de itens acompanhados ({hooked} pontos de criação).");
        }

        static void OnCreated(string key, CreateObject action)
        {
            var created = action.storeObject != null ? action.storeObject.Value : null;
            if (created == null) return;
            var body = created.GetComponent<Rigidbody>() ?? created.GetComponentInChildren<Rigidbody>();
            if (body == null) return;
            var local = ItemRegistry.AddDynamic(body);
            Queue<ushort> queue;
            if (mirrors.TryGetValue(key, out queue) && queue.Count > 0)
            {
                var remote = queue.Dequeue();
                ItemRegistry.Link(remote, local);
                if (link != null) link(remote, local);
                return;
            }
            if (announce != null) announce(key, local);
        }

        internal static void ReadAnnounce(BinaryReader r)
        {
            var key = r.ReadString();
            var remote = r.ReadUInt16();
            PlayMakerFSM fsm;
            if (!spawners.TryGetValue(key, out fsm) || fsm == null)
            {
                ModConsole.Warning($"[MSC Duo] o outro jogador comprou um item de um gerador que não existe aqui ({key}).");
                return;
            }
            Queue<ushort> queue;
            if (!mirrors.TryGetValue(key, out queue)) mirrors[key] = queue = new Queue<ushort>();
            queue.Enqueue(remote);
            fsm.SendEvent(SpawnEvent);
        }

        internal static void ReadLink(BinaryReader r)
        {
            var senderId = r.ReadUInt16();
            var ourId = r.ReadUInt16();
            ItemRegistry.Link(senderId, ourId);
        }

        static string PathOf(Transform t)
        {
            var p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
