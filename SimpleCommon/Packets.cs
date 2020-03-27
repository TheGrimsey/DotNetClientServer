using System;
using System.IO;
using System.Net.Sockets;

namespace SimpleCommon
{
    public enum EPacketType
    {
        None,
        ChatMessage,
        ClientJoined,
        ClientDisconnected
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

    public static class PacketConverter
    {
        /*
         * Creates a byte array from data.
         * Packet returnes is in the following format: [Length][PacketType][Data]
         */
        public static byte[] CreatePacketByteArray(EPacketType packetType, byte[] Data)
        {
            int size = sizeof(EPacketType) + Data.Length;
            MemoryStream stream = new MemoryStream(1 + size);

            //Write length
            stream.Write(BitConverter.GetBytes(size), 0, sizeof(int));
            //Write Packettype
            stream.Write(BitConverter.GetBytes((int)packetType), 0, sizeof(EPacketType));
            //Write Data
            stream.Write(Data, 0, Data.Length);

            return stream.ToArray();
        }

        /*
         * Reads data into Packet.
         * This assumes the byte array is structured as follows: [PacketType][Data]
         */
        public static Packet ReadPacketFromByteArray(TcpClient Sender, byte[] Data)
        {
            Packet packet = new Packet();

            //Make sure we have some data.
            if(Data.Length > 0)
            {
                //Stream to read data.
                MemoryStream stream = new MemoryStream(Data);

                //Read packetType.
                byte[] packetTypeBuffer = new byte[sizeof(EPacketType)];
                stream.Read(packetTypeBuffer, 0, sizeof(EPacketType));
                int packetTypeRaw = BitConverter.ToInt32(packetTypeBuffer, 0);
                EPacketType packetType = EPacketType.None;

                //Check so it is a valid packet type and if it is let's use it.
                if(Enum.IsDefined(typeof(EPacketType), packetTypeRaw))
                {
                    packetType = (EPacketType)packetTypeRaw;
                }

                //The rest is just data so let's read that.
                byte[] packetData = new byte[stream.Length - stream.Position + 1];
                stream.Read(packetData, 0, packetData.Length);

                //Now create the packet.
                packet = new Packet(Sender, packetType, packetData);
            }

            return packet;
        }
    }
}
