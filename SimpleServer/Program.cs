using SimpleCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "SimpleServer";
            Console.InputEncoding = System.Text.Encoding.Unicode;
            Console.OutputEncoding = System.Text.Encoding.Unicode;

            SimpleServer server = new SimpleServer(new MySQLConnectionInfo("localhost", "root", "root"));
        }
    }

    struct MySQLConnectionInfo
    {
        readonly string _address;
        readonly string _user;
        readonly string _password;

        public MySQLConnectionInfo(string address = "localhost", string user = "root", string password = "root")
        {
            _address = address;
            _user = user;
            _password = password;
        }
    }

    class SimpleServer
    {
        const int DefaultPort = 2002;

        //Information used to connect to the database.
        MySQLConnectionInfo _mySQLconnectionInfo;

        bool isRunning;

        TcpListener tcpListener;

        //Connections to clients.
        List<TcpClient> connectedClients;
        //All recieved and not dealt with packets.
        ConcurrentQueue<Packet> recievedPackets;

        CancellationTokenSource cancellationTokenSource;
        public SimpleServer(MySQLConnectionInfo mySQLConnectionInfo)
        {
            _mySQLconnectionInfo = mySQLConnectionInfo;

            isRunning = true;

            //Create tcpListener.
            IPAddress iPAddress = IPAddress.Parse("127.0.0.1"); //Localhost
            tcpListener = new TcpListener(iPAddress, DefaultPort);
            tcpListener.Start();

            connectedClients = new List<TcpClient>();
            recievedPackets = new ConcurrentQueue<Packet>();

            cancellationTokenSource = new CancellationTokenSource();

            Console.WriteLine("Server has started.");

            //Try to accept new client.
            try
            {
                //Accept new client.
                TcpClient newClient = tcpListener.AcceptTcpClient();
                connectedClients.Add(newClient);

                _ = AcceptClientPacket(newClient, cancellationTokenSource.Token);

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception when accepting new client: " + e.Message);
            }

            while (isRunning)
            {
                Packet packet = new Packet();
                while(recievedPackets.TryDequeue(out packet))
                {
                    //Determine which packet type it is so we can parse it.
                    switch (packet.PacketType)
                    {
                        case EPacketType.ChatMessage:
                            HandleChatmessage(packet);
                            break;
                    }
                }
                
                Thread.Sleep(50);

            }
            //We exited the IsRunning loop so we are closing down the server.
            Console.WriteLine("Closing server...");

            //Cancel all async operations.
            cancellationTokenSource.Cancel();

            //End all connections.
            foreach (TcpClient client in connectedClients)
            {
                client.Close();
            }
        }

        private static void HandleChatmessage(Packet packet)
        {
            //Convert message to unicode string
            string Message = System.Text.Encoding.Unicode.GetString(packet.Data, 0, packet.Data.Length-1);

            //Remove newline at the end of string if it exists.
            if (Message.EndsWith(Environment.NewLine))
            {
                Message = Message.Substring(0, Message.LastIndexOf(Environment.NewLine));
            }

            Console.WriteLine("Client " + packet.Sender.Client.Handle + " says: " + Message);
        }

        async Task AcceptClientPacket(TcpClient client, CancellationToken cancellationToken)
        {
            //Accept client packets until we are cancelled.
            while(!cancellationToken.IsCancellationRequested)
            {
                NetworkStream stream = client.GetStream();

                //Read packetSize.
                byte[] packetSizeBuffer = new byte[sizeof(int)];
                await stream.ReadAsync(packetSizeBuffer, 0, sizeof(int));

                //Create databuffer with packetSize.
                byte[] packetData = new byte[BitConverter.ToInt32(packetSizeBuffer, 0)];
                stream.Read(packetData, 0, packetData.Length);

                recievedPackets.Enqueue(PacketConverter.ReadPacketFromByteArray(client, packetData));
            }
        }

    }
}
