using SimpleCommon;
using System;

namespace SimpleServer
{
    /*
     * Packet Handler for the Server.
     */
    class ServerPacketHandler : PacketHandler
    {
        SimpleServer _server;

        public ServerPacketHandler(SimpleServer server) : base()
        {
            _server = server;

            _server.RegisterPacketHandler(EPacketType.ChatMessage, HandleChatMessagePacket);
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

            string ClientMessage = "Client " + packet.Sender.Client.Handle + ": " + Message;
            Console.WriteLine(ClientMessage);

            //Packet we send out to clients.
            Packet returnPacket = new Packet(packet.Sender, EPacketType.ChatMessage, System.Text.Encoding.Unicode.GetBytes(ClientMessage));

            _server.QueuePacket(returnPacket);
        }

    }
}
