using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HutongGames.PlayMaker;
using UnityEngine;

namespace MscDuo.Player
{
    internal class PlayerProfile
    {
        internal static readonly string[] Variables =
        {
            "PlayerMoney", "PlayerHunger", "PlayerThirst", "PlayerStress", "PlayerStressRate",
            "PlayerUrine", "PlayerFatigue", "PlayerFatigueRate", "PlayerDirtiness", "PlayerDrunk",
            "PlayerDrunkAdjusted", "PlayerBurns", "PlayerAllergy", "PlayerFines", "PlayerFinesDay",
            "PlayerWeight", "PlayerCigarettes", "PlayerBerryPickSkill", "PlayerNetIncome", "PlayerWanted",
            "PlayerFirstName", "PlayerLastName", "PlayerName",
            "PlayerKeyHome", "PlayerKeySatsuma", "PlayerKeyFerndale", "PlayerKeyGifu", "PlayerKeyHayosiko", "PlayerKeyRuscko",
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        internal Vector3 Position { get; set; }
        internal float Yaw { get; set; }
        internal bool HasPosition { get; set; }

        internal static PlayerProfile Capture(Transform player)
        {
            var profile = new PlayerProfile();
            foreach (var name in Variables)
            {
                var v = FsmVariables.GlobalVariables.GetVariable(name);
                if (v != null) profile.values[name] = Serialize(v);
            }
            if (player != null)
            {
                profile.Position = player.position;
                profile.Yaw = player.eulerAngles.y;
                profile.HasPosition = true;
            }
            return profile;
        }

        internal void Apply()
        {
            foreach (var pair in values)
            {
                var v = FsmVariables.GlobalVariables.GetVariable(pair.Key);
                if (v != null) Deserialize(v, pair.Value);
            }
        }

        internal void Set(string name, string value) => values[name] = value;

        internal string Get(string name)
        {
            string value;
            return values.TryGetValue(name, out value) ? value : null;
        }

        internal void Save(string path)
        {
            var sb = new StringBuilder();
            if (HasPosition)
                sb.AppendLine(string.Format(Inv, "@position={0} {1} {2} {3}", Position.x, Position.y, Position.z, Yaw));
            foreach (var pair in values) sb.AppendLine(pair.Key + "=" + pair.Value.Replace("\n", " "));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        internal static PlayerProfile Load(string path)
        {
            if (!File.Exists(path)) return null;
            var profile = new PlayerProfile();
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq);
                var value = line.Substring(eq + 1);
                if (key == "@position")
                {
                    var p = value.Split(' ');
                    profile.Position = new Vector3(float.Parse(p[0], Inv), float.Parse(p[1], Inv), float.Parse(p[2], Inv));
                    profile.Yaw = float.Parse(p[3], Inv);
                    profile.HasPosition = true;
                    continue;
                }
                profile.values[key] = value;
            }
            return profile;
        }

        static string Serialize(NamedVariable v)
        {
            if (v is FsmFloat f) return f.Value.ToString("R", Inv);
            if (v is FsmInt i) return i.Value.ToString(Inv);
            if (v is FsmBool b) return b.Value ? "1" : "0";
            if (v is FsmString s) return s.Value ?? "";
            throw new NotSupportedException(v.GetType().Name);
        }

        static void Deserialize(NamedVariable v, string value)
        {
            if (v is FsmFloat f) f.Value = float.Parse(value, Inv);
            else if (v is FsmInt i) i.Value = int.Parse(value, Inv);
            else if (v is FsmBool b) b.Value = value == "1";
            else if (v is FsmString s) s.Value = value;
        }
    }
}
