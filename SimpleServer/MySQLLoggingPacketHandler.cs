using MySql.Data.MySqlClient;
using SimpleCommon;
using System;

namespace SimpleServer
{
    public class MySQLLoggingPacketHandler : PacketHandler
    {
        SimpleServer _server;

        //Connection to the Database.
        MySqlConnection _mySqlConnection;

        //Temp hardcoded connection info.
        string _dbAddress = "localhost";
        string _dbDatabase = "dotnetserverclient";
        string _dbUser = "root";
        string _dbPassword = "root";

        public MySQLLoggingPacketHandler(SimpleServer server) : base()
        {
            _server = server;
            _server.RegisterPacketHandler(EPacketType.ChatMessage, HandleChatMessagePacket);

            string connectionString = string.Format("Server={0};Database={1};Uid={2};Pwd={3}", _dbAddress, _dbDatabase, _dbUser, _dbPassword);
            _mySqlConnection = new MySqlConnection(connectionString);
            _mySqlConnection.Open();
        }

        void HandleChatMessagePacket(Packet packet)
        {
            //Parse data into message.
            string Message = System.Text.Encoding.Unicode.GetString(packet.Data, 0, packet.Data.Length - 1);

            MySqlCommand command = _mySqlConnection.CreateCommand();

            try
            {
                command.CommandText = "INSERT INTO chatlogs (ClientId, Message) VALUES (@ClientId, @Message)";
                command.Parameters.AddWithValue("ClientId", packet.Sender.Client.Handle);
                command.Parameters.AddWithValue("Message", Message);

                command.ExecuteNonQuery();
            }
            catch(Exception e)
            {
                Console.WriteLine("MySQL error: " + e.Message);
            }
        }
    }
}
