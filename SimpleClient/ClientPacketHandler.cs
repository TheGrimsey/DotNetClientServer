using SimpleCommon;
using System;

namespace SimpleClient
{
    public class ClientPacketHandler : PacketHandler
    {
        SimpleClient _client;

        public ClientPacketHandler(SimpleClient client)
        {
            _client = client;

            _client.RegisterPacketHandler(EPacketType.ChatMessage, HandleChatMessagePacket);
        }

        void HandleChatMessagePacket(Packet packet)
        {
            //Convert message to unicode string
            string Message = System.Text.Encoding.Unicode.GetString(packet.Data, 0, packet.Data.Length - 1);

            //Remove newline at the end of string if it exists.
            if (Message.EndsWith(Environment.NewLine))
            {
                Message = Message.Substring(0, Message.LastIndexOf(Environment.NewLine));
            }

            Console.WriteLine(Message);
        }
    }
}
