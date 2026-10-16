using System.IO;
using UnityEngine;

namespace MscDuo.Net
{
    internal enum MessageType : byte
    {
        Hello = 1,
        Ping = 2,
        Pong = 3,
        PlayerState = 10,
    }

    internal static class Protocol
    {
        internal const ushort Version = 1;
    }

    internal struct PlayerStateMessage
    {
        internal const byte Player2Flag = 1;

        internal uint Sequence;
        internal float Time;
        internal Vector3 Position;
        internal float Yaw;
        internal float Pitch;
        internal byte Flags;

        internal void Write(BinaryWriter w)
        {
            w.Write(Sequence);
            w.Write(Time);
            w.Write(Position.x);
            w.Write(Position.y);
            w.Write(Position.z);
            w.Write(Angle16(Yaw));
            w.Write(Angle16(Pitch));
            w.Write(Flags);
        }

        internal static PlayerStateMessage Read(BinaryReader r) => new PlayerStateMessage
        {
            Sequence = r.ReadUInt32(),
            Time = r.ReadSingle(),
            Position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            Yaw = FromAngle16(r.ReadUInt16()),
            Pitch = FromAngle16(r.ReadUInt16()),
            Flags = r.ReadByte(),
        };

        static ushort Angle16(float degrees) => (ushort)Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 360f * 65535f);

        static float FromAngle16(ushort value) => value / 65535f * 360f;
    }

    internal static class MessageWriter
    {
        internal static byte[] Build(MessageType type, System.Action<BinaryWriter> body)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)type);
                body?.Invoke(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
