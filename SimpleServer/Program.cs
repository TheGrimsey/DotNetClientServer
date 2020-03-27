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

            SimpleServer server = new SimpleServer();
        }
    }

    class SimpleServer
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

            Console.WriteLine("Server has started.");

            //Accept new clients in async.
            _ = AcceptConnectionsAsync();

            while (_isRunning)
            {
                Packet packet;

                //Handle recieved packets.
                while(_packetsRecieved.TryDequeue(out packet))
                {
                    //Determine which packet type it is so we can parse it.
                    switch (packet.PacketType)
                    {
                        case EPacketType.ChatMessage:
                            HandleChatmessage(packet);
                            break;
                    }
                }

                //Send packets we have queued up to send.
                while(_packetsToSend.TryDequeue(out packet))
                {
                    foreach(TcpClient tcpClient in _connectedClients)
                    {
                        byte[] packetByteArray = PacketConverter.CreatePacketByteArray(packet.PacketType, packet.Data);
                        tcpClient.GetStream().Write(packetByteArray, 0, packetByteArray.Length);
                    }
                }
                
                Thread.Sleep(50);

            }
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

        private void HandleChatmessage(Packet packet)
        {
            //Convert message to unicode string
            string Message = System.Text.Encoding.Unicode.GetString(packet.Data, 0, packet.Data.Length-1);

            //Remove newline at the end of string if it exists.
            if (Message.EndsWith(Environment.NewLine))
            {
                Message = Message.Substring(0, Message.LastIndexOf(Environment.NewLine));
            }

            string ClientMessage = "Client " + packet.Sender.Client.Handle + ": " + Message;
            Console.WriteLine(ClientMessage);

            //Packet we send out to clients.
            Packet returnPacket = new Packet(packet.Sender, EPacketType.ChatMessage, System.Text.Encoding.Unicode.GetBytes(ClientMessage));

            _packetsToSend.Enqueue(returnPacket);
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
                stream.Read(packetData, 0, packetData.Length);

                _packetsRecieved.Enqueue(PacketConverter.ReadPacketFromByteArray(client, packetData));
            }
        }

        void OnClientConnected(TcpClient newClient)
        {
            //TODO EVENT
            Console.WriteLine("Client {0} connected!", newClient.Client.Handle);
        }

        void OnClientDisconnected(TcpClient client)
        {
            Console.WriteLine("Client {0} disconnected!", client.Client.Handle);

        }
    }
}
