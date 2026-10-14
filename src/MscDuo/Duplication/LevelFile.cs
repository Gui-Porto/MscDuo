using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class LevelFile : IDisposable
    {
        const int MeshClassId = 43;
        const int ChannelCount = 8;

        readonly FileStream stream;
        readonly BinaryReader reader;
        readonly Dictionary<string, long> meshOffsets = new Dictionary<string, long>();

        internal LevelFile(string path)
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(stream);
            ReadIndex();
        }

        public void Dispose()
        {
            reader.Close();
        }

        internal bool Has(string meshName) => meshOffsets.ContainsKey(meshName);

        void ReadIndex()
        {
            var header = reader.ReadBytes(16);
            var version = BigEndian(header, 8);
            var dataOffset = BigEndian(header, 12);
            if (version != 14)
                throw new NotSupportedException($"formato de arquivo {version} não suportado");

            stream.Position = 20;
            ReadCString();
            reader.ReadInt32();
            var typeTree = reader.ReadByte() != 0;
            if (typeTree)
                throw new NotSupportedException("arquivo com type tree não suportado");

            var typeCount = reader.ReadInt32();
            for (int i = 0; i < typeCount; i++)
            {
                var classId = reader.ReadInt32();
                stream.Position += classId < 0 ? 32 : 16;
            }

            var objectCount = reader.ReadInt32();
            var entries = new List<long>();
            for (int i = 0; i < objectCount; i++)
            {
                Align();
                reader.ReadInt64();
                var byteStart = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadInt32();
                var classId = reader.ReadUInt16();
                reader.ReadInt16();
                if (classId == MeshClassId) entries.Add(byteStart + dataOffset);
            }

            foreach (var offset in entries)
            {
                stream.Position = offset;
                var name = ReadString();
                if (name.StartsWith("Combined Mesh", StringComparison.Ordinal) && !meshOffsets.ContainsKey(name))
                    meshOffsets.Add(name, offset);
            }
        }

        internal CombinedMesh Read(string meshName)
        {
            long offset;
            if (!meshOffsets.TryGetValue(meshName, out offset)) return null;
            stream.Position = offset;

            var mesh = new CombinedMesh { Name = ReadString() };

            var subCount = reader.ReadInt32();
            mesh.SubMeshes = new SubMesh[subCount];
            for (int i = 0; i < subCount; i++)
            {
                var sub = new SubMesh
                {
                    FirstByte = reader.ReadUInt32(),
                    IndexCount = reader.ReadUInt32(),
                    Topology = reader.ReadInt32(),
                };
                reader.ReadUInt32();
                reader.ReadUInt32();
                var center = ReadVector3();
                var extent = ReadVector3();
                sub.Bounds = new Bounds(center, extent * 2f);
                mesh.SubMeshes[i] = sub;
            }

            SkipBlendShapes();
            SkipArray(64);
            SkipArray(4);
            reader.ReadUInt32();

            var compression = reader.ReadByte();
            stream.Position += 3;
            if (compression != 0)
                throw new NotSupportedException($"{meshName}: malha comprimida ({compression})");

            var indexBytes = reader.ReadBytes(reader.ReadInt32());
            Align();
            mesh.Indices = new ushort[indexBytes.Length / 2];
            Buffer.BlockCopy(indexBytes, 0, mesh.Indices, 0, mesh.Indices.Length * 2);

            SkipArray(32);

            reader.ReadUInt32();
            var vertexCount = (int)reader.ReadUInt32();
            var channelCount = reader.ReadInt32();
            var channels = new Channel[channelCount];
            for (int i = 0; i < channelCount; i++)
                channels[i] = new Channel(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            var data = reader.ReadBytes(reader.ReadInt32());

            Decode(mesh, channels, vertexCount, data);
            return mesh;
        }

        static void Decode(CombinedMesh mesh, Channel[] channels, int vertexCount, byte[] data)
        {
            var streamCount = 0;
            foreach (var c in channels) if (c.Dimension > 0) streamCount = Math.Max(streamCount, c.Stream + 1);

            var strides = new int[streamCount];
            foreach (var c in channels)
                if (c.Dimension > 0)
                    strides[c.Stream] = Math.Max(strides[c.Stream], c.Offset + c.Size);

            var starts = new int[streamCount];
            var position = 0;
            for (int s = 0; s < streamCount; s++)
            {
                starts[s] = position;
                position += strides[s] * vertexCount;
                position = (position + 15) & ~15;
            }

            mesh.VertexCount = vertexCount;
            for (int ch = 0; ch < channels.Length && ch < ChannelCount; ch++)
            {
                var c = channels[ch];
                if (c.Dimension == 0) continue;
                var values = new float[vertexCount * c.Dimension];
                for (int v = 0; v < vertexCount; v++)
                {
                    var at = starts[c.Stream] + strides[c.Stream] * v + c.Offset;
                    for (int d = 0; d < c.Dimension; d++)
                        values[v * c.Dimension + d] = ReadComponent(data, at, c.Format, d);
                }
                mesh.Channels[ch] = values;
                mesh.Dimensions[ch] = c.Dimension;
            }
        }

        static float ReadComponent(byte[] data, int at, int format, int index)
        {
            switch (format)
            {
                case 0: return BitConverter.ToSingle(data, at + index * 4);
                case 1: return HalfToFloat(BitConverter.ToUInt16(data, at + index * 2));
                case 2: return data[at + index] / 255f;
                default: return data[at + index];
            }
        }

        static float HalfToFloat(ushort h)
        {
            var sign = (h >> 15) & 1;
            var exp = (h >> 10) & 0x1F;
            var mant = h & 0x3FF;
            float value;
            if (exp == 0) value = mant * Mathf.Pow(2f, -24f);
            else if (exp == 31) value = mant == 0 ? float.PositiveInfinity : float.NaN;
            else value = (1f + mant / 1024f) * Mathf.Pow(2f, exp - 15);
            return sign == 1 ? -value : value;
        }

        void SkipBlendShapes()
        {
            SkipArray(40);
            SkipArray(12);
            var channelCount = reader.ReadInt32();
            for (int i = 0; i < channelCount; i++)
            {
                ReadString();
                stream.Position += 12;
            }
            SkipArray(4);
        }

        void SkipArray(int elementSize)
        {
            var count = reader.ReadInt32();
            stream.Position = stream.Position + (long)count * elementSize;
        }

        string ReadString()
        {
            var length = reader.ReadInt32();
            var s = Encoding.UTF8.GetString(reader.ReadBytes(length));
            Align();
            return s;
        }

        string ReadCString()
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0) bytes.Add(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        Vector3 ReadVector3() => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        void Align()
        {
            stream.Position = (stream.Position + 3) & ~3L;
        }

        static uint BigEndian(byte[] b, int at) =>
            (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);

        struct Channel
        {
            internal readonly int Stream;
            internal readonly int Offset;
            internal readonly int Format;
            internal readonly int Dimension;

            internal Channel(byte stream, byte offset, byte format, byte dimension)
            {
                Stream = stream;
                Offset = offset;
                Format = format;
                Dimension = dimension & 0x0F;
            }

            internal int Size => Dimension * (Format == 0 ? 4 : Format == 1 ? 2 : 1);
        }
    }

    internal class CombinedMesh
    {
        internal const int Position = 0;
        internal const int Normal = 1;
        internal const int Color = 2;
        internal const int Uv0 = 3;
        internal const int Uv1 = 4;
        internal const int Tangent = 7;

        internal string Name;
        internal SubMesh[] SubMeshes;
        internal ushort[] Indices;
        internal int VertexCount;
        internal readonly float[][] Channels = new float[8][];
        internal readonly int[] Dimensions = new int[8];
    }

    internal struct SubMesh
    {
        internal uint FirstByte;
        internal uint IndexCount;
        internal int Topology;
        internal Bounds Bounds;
    }
}
