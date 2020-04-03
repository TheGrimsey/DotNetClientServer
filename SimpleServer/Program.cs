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
            //Set up console.
            Console.Title = "SimpleServer";
            Console.InputEncoding = System.Text.Encoding.Unicode;
            Console.OutputEncoding = System.Text.Encoding.Unicode;

            SimpleServer server = new SimpleServer();
            ServerPacketHandler serverPacketHandler = new ServerPacketHandler(server);
            MySQLLoggingPacketHandler mySQLLoggingPacketHandler = new MySQLLoggingPacketHandler(server);

            server.Run();
        }
    }

    public class SimpleServer
    {
        bool _isRunning;

        TcpListener _tcpListener;

        //Connections to clients.
        List<TcpClient> _connectedClients;

        //All recieved and not dealt with packets.
        ConcurrentQueue<Packet> _packetsRecieved;

        //Packets to send out to clients.
        ConcurrentQueue<Packet> _packetsToSend;
        
        //Cancellation token used for async tasks.
        CancellationTokenSource _cancellationTokenSource;

        //Packet handlers.
        Dictionary<EPacketType, List<HandlePacketDelegate>> _packetHandlers;

        public SimpleServer()
        {
            _isRunning = true;

            //Create tcpListener.
            IPAddress iPAddress = IPAddress.Parse("127.0.0.1"); //Localhost
            _tcpListener = new TcpListener(iPAddress, Defaults.DEFAULTPORT);
            _tcpListener.Start();

            _connectedClients = new List<TcpClient>();
            _packetsRecieved = new ConcurrentQueue<Packet>();
            _packetsToSend = new ConcurrentQueue<Packet>();

            _cancellationTokenSource = new CancellationTokenSource();

            _packetHandlers = new Dictionary<EPacketType, List<HandlePacketDelegate>>();
        }

        /*
         * Run Server.
         * Starts networking and main loop.
         */
        public void Run()
        {
            Console.WriteLine("Server has started.");

            //Accept new clients in async.
            _ = AcceptConnectionsAsync();

            while (_isRunning)
            {
                Packet packet;

                //Handle recieved packets.
                while (_packetsRecieved.TryDequeue(out packet))
                {
                    //Grab packet handler if it exists.
                    List<HandlePacketDelegate> packetDelegate;
                    if (_packetHandlers.TryGetValue(packet.PacketType, out packetDelegate))
                    {
                        foreach(HandlePacketDelegate pDel in packetDelegate)
                        {
                            pDel.Invoke(packet);
                        }
                    }
                }

                //Send packets we have queued up to send.
                while (_packetsToSend.TryDequeue(out packet))
                {
                    foreach (TcpClient tcpClient in _connectedClients)
                    {
                        byte[] packetByteArray = PacketConverter.CreatePacketByteArray(packet.PacketType, packet.Data);
                        tcpClient.GetStream().Write(packetByteArray, 0, packetByteArray.Length);
                    }
                }

                Thread.Sleep(50);

            }

            CloseServer();
        }

        /*
         * Closes the server. 
         * Cancelling async operations & closing sockets.
         * There is no comming back from this.
         */
        void CloseServer()
        {
            //We exited the IsRunning loop so we are closing down the server.
            Console.WriteLine("Closing server...");

            //Cancel all async operations.
            _cancellationTokenSource.Cancel();

            //End all connections.
            foreach (TcpClient client in _connectedClients)
            {
                client.Close();
            }
        }

        async Task AcceptConnectionsAsync()
        {
            while (_isRunning)
            {
                TcpClient newClient = await _tcpListener.AcceptTcpClientAsync();
                _connectedClients.Add(newClient);

                OnClientConnected(newClient);

                //Start accepting packets from client in async.
                _ = AcceptClientPacketAsync(newClient, _cancellationTokenSource.Token);
            }
        }

        async Task AcceptClientPacketAsync(TcpClient client, CancellationToken cancellationToken)
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
                //Read actual packet.
                stream.Read(packetData, 0, packetData.Length);

                //Queue this packet up to be handled.
                _packetsRecieved.Enqueue(PacketConverter.ReadPacketFromByteArray(client, packetData));
            }
        }

        public void RegisterPacketHandler(EPacketType packetType, HandlePacketDelegate packetDelegate)
        {
            List<HandlePacketDelegate> existingDelegate;
            if (_packetHandlers.TryGetValue(packetType, out existingDelegate))
            {
                existingDelegate.Add(packetDelegate);
            }
            else
            {
                List<HandlePacketDelegate> newList = new List<HandlePacketDelegate>();
                newList.Add(packetDelegate);
                _packetHandlers.Add(packetType, new List<HandlePacketDelegate>(newList));
            }
        }

        /*
         * Queues a packet to be sent to all clients.
         */
        public void QueuePacket(Packet packet)
        {
            _packetsToSend.Enqueue(packet);
        }

        void OnClientConnected(TcpClient newClient)
        {
            Console.WriteLine("Client {0} connected!", newClient.Client.Handle);
        }

        void OnClientDisconnected(TcpClient client)
        {
            Console.WriteLine("Client {0} disconnected!", client.Client.Handle);
        }
    }
}
