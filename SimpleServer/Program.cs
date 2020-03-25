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

                AcceptClientPacket(newClient, cancellationTokenSource.Token);

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
                
                Thread.Sleep(100);

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
            //Convert message to unicode string. Skip index 0 because that is where the packet type is
            string Message = System.Text.Encoding.Unicode.GetString(packet.Data, 1, packet.Data.Length-1);

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
                //Data Buffer.
                byte[] Data = new byte[1024];
                //Grab data into buffer in async.
                int DataSize = await client.GetStream().ReadAsync(Data, 0, Data.Length);

                //Convert buffer to raw byte array.
                byte[] Packet = new byte[DataSize];
                Array.Copy(Data, 0, Packet, 0, DataSize);

                //Check so we have data.
                if (Packet.Length > 0)
                {
                    //Grab first byte. This is our PacketType.
                    int PacketType = Packet[0];

                    //Make sure it is a valid packettype.
                    if (Enum.IsDefined(typeof(EPacketType), PacketType))
                    {
                        //Put it in the recieved packets queue to be dealt with on the main thread.
                        recievedPackets.Enqueue(new SimpleCommon.Packet(client, (EPacketType)PacketType, Packet));
                    }
                }
            }
        }

    }
}
