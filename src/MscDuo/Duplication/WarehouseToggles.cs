using System;
using System.Collections.Generic;
using System.IO;

namespace MscDuo.Duplication
{
    internal static class WarehouseToggles
    {
        class Entry
        {
            internal int Value;
            internal Action<int> Apply;
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        static Action<string, int> sender;

        internal static void Bind(Action<string, int> send) => sender = send;

        internal static void Clear() => entries.Clear();

        internal static void Register(string key, int initial, Action<int> apply)
        {
            entries[key] = new Entry { Value = initial, Apply = apply };
            apply(initial);
        }

        internal static int Get(string key)
        {
            Entry e;
            return entries.TryGetValue(key, out e) ? e.Value : 0;
        }

        internal static void Set(string key, int value)
        {
            Entry e;
            if (!entries.TryGetValue(key, out e)) return;
            e.Value = value;
            e.Apply(value);
            if (sender != null) sender(key, value);
        }

        internal static void Read(BinaryReader r)
        {
            var key = r.ReadString();
            var value = r.ReadInt32();
            Entry e;
            if (!entries.TryGetValue(key, out e)) return;
            e.Value = value;
            e.Apply(value);
        }

        internal static void WriteAll(Action<Action<BinaryWriter>> send)
        {
            foreach (var pair in entries)
            {
                var key = pair.Key;
                var value = pair.Value.Value;
                send(w => { w.Write(key); w.Write(value); });
            }
        }
    }
}
