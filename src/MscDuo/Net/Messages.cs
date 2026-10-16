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
        CarState = 11,
    }

    internal static class Protocol
    {
        internal const ushort Version = 3;
    }

    internal struct PlayerStateMessage
    {
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

    internal struct CarStateMessage
    {
        internal byte Car;
        internal uint Sequence;
        internal float Time;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal Vector3 Velocity;
        internal Vector3 AngularVelocity;
        internal bool Driving;

        internal void Write(BinaryWriter w)
        {
            w.Write(Car);
            w.Write(Sequence);
            w.Write(Time);
            Write(w, Position);
            w.Write(Rotation.x);
            w.Write(Rotation.y);
            w.Write(Rotation.z);
            w.Write(Rotation.w);
            Write(w, Velocity);
            Write(w, AngularVelocity);
            w.Write(Driving);
        }

        internal static CarStateMessage Read(BinaryReader r) => new CarStateMessage
        {
            Car = r.ReadByte(),
            Sequence = r.ReadUInt32(),
            Time = r.ReadSingle(),
            Position = ReadVector(r),
            Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            Velocity = ReadVector(r),
            AngularVelocity = ReadVector(r),
            Driving = r.ReadBoolean(),
        };

        static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        static Vector3 ReadVector(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
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
