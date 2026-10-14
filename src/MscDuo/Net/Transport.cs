using System.Collections.Generic;

namespace MscDuo.Net
{
    internal enum Channel : byte
    {
        Unreliable = 0,
        Reliable = 1,
    }

    internal struct Packet
    {
        internal readonly ulong From;
        internal readonly byte[] Data;
        internal readonly Channel Channel;

        internal Packet(ulong from, byte[] data, Channel channel)
        {
            From = from;
            Data = data;
            Channel = channel;
        }
    }

    internal interface ITransport
    {
        ulong LocalId { get; }
        IEnumerable<ulong> Peers { get; }
        string Name { get; }
        void Send(ulong to, byte[] data, Channel channel);
        void Poll(List<Packet> received);
        void Shutdown();
    }
}
