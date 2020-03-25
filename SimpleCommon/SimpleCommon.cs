using System;
using System.Net.Sockets;

namespace SimpleCommon
{
    public enum EPacketType
    {
        ChatMessage
    }

    public struct Packet
    {
        private readonly TcpClient _sender;
        public TcpClient Sender => _sender;

        private readonly EPacketType _packetType;
        public EPacketType PacketType => _packetType;

        private readonly byte[] _data;
        public byte[] Data => _data;
        public Packet(TcpClient sender, EPacketType packetType, byte[] data)
        {
            _sender = sender;
            _packetType = packetType;
            _data = data;
        }
    }
}
