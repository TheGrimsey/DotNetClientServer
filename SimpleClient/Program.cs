﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommon;

namespace SimpleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //Set up console.
            Console.Title = "SimpleClient";
            Console.InputEncoding = System.Text.Encoding.Unicode;
            Console.OutputEncoding = System.Text.Encoding.Unicode;

            //Create client.
            SimpleClient simpleClient = new SimpleClient();
            ClientPacketHandler packetHandler = new ClientPacketHandler(simpleClient);

            //Run client.
            simpleClient.Run();
        }
    }
    public class SimpleClient
    {
        //Our connection to the server.
        TcpClient _tcpClient;

        //Queue of packets to send to server.
        ConcurrentQueue<Packet> _packetsToSend;

        //Queue of packets recieved from the server.
        ConcurrentQueue<Packet> _packetsRecieved;

        CancellationTokenSource _cancellationTokenSource;

        //Packet handlers.
        Dictionary<EPacketType, HandlePacketDelegate> _packetHandlers;

        public SimpleClient()
        {
            _packetsToSend = new ConcurrentQueue<Packet>();
            _packetsRecieved = new ConcurrentQueue<Packet>();

            _cancellationTokenSource = new CancellationTokenSource();

            _packetHandlers = new Dictionary<EPacketType, HandlePacketDelegate>();
        }

        ~SimpleClient()
        {
            //Clean up connection.
            if(_tcpClient != null && _tcpClient.Connected)
            {
                _tcpClient.Close();
            }
        }

        /*
         * Handles reading Console input and parsing it for packets.
         * Ran on a background thread.
         */
        void HandleInput()
        {
            //Read input until we disconnect.
            while(_tcpClient.Connected)
            {
                string input =  Console.ReadLine();

                //Check if this is a command. (Command start with a slash)
                if(input.StartsWith("/"))
                {
                    //This is a command. Let's deal with it.
                    Console.WriteLine(string.Format("Command {0} Not implemented."), input); //Dealt with it.
                }
                else
                {
                    //Nothing special with this so let's just treat it as a normal message.
                    SendChatMessageToServer(input);
                }
            }
        }

        /*
         * Registers a packet handler.
         */
        public void RegisterPacketHandler(EPacketType packetType, HandlePacketDelegate packetDelegate)
        {
            HandlePacketDelegate existingDelegate;
            if (_packetHandlers.TryGetValue(packetType, out existingDelegate))
            {
                existingDelegate += packetDelegate;
            }
            else
            {
                _packetHandlers.Add(packetType, packetDelegate);
            }
        }

        /*
         * Run the client.
         * Starts server connection and packet sending.
         */
        public void Run()
        {
            SetUpServerConnection();
            Console.WriteLine("Connected to server!");

            //Start listening to input on secondary thread.
            Thread inputThread = new Thread(new ThreadStart(HandleInput));
            inputThread.Start();

            //Accept packets from server in async.
            _ = AcceptServerPacketAsync(_tcpClient, _cancellationTokenSource.Token);

            Console.WriteLine("Starting packet sending..");

            //At this point we have connected. Let's send some messages until we aren't.
            while (_tcpClient.Connected)
            {
                //Go through all PacketsToSend and send them.
                Packet packet;
                NetworkStream stream = _tcpClient.GetStream();

                while (_packetsToSend.TryDequeue(out packet))
                {
                    //Convert packet to byte array.
                    byte[] packetBuffer = PacketConverter.CreatePacketByteArray(packet.PacketType, packet.Data);
                    //Write to stream.
                    stream.Write(packetBuffer, 0, packetBuffer.Length);
                }

                while (_packetsRecieved.TryDequeue(out packet))
                {
                    //Grab packet handler if it exists.
                    HandlePacketDelegate packetDelegate;
                    if (_packetHandlers.TryGetValue(packet.PacketType, out packetDelegate))
                    {
                        packetDelegate.Invoke(packet);
                    }
                }

                Thread.Sleep(50);
            }

            Console.WriteLine("Disconnected from server. Press ENTER to exit.");
            Console.ReadLine();
        }

        async Task AcceptServerPacketAsync(TcpClient client, CancellationToken cancellationToken)
        {
            //Accept client packets until we are cancelled.
            while (!cancellationToken.IsCancellationRequested)
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

        /*
         * Attempts to send message to server.
         * Returns true if it succeeded without error.
         */
        void SendChatMessageToServer(string message)
        {
            //Convert message to byte array.
            byte[] messageAsBytes = System.Text.Encoding.Unicode.GetBytes(message);

            //Queue packet
            _packetsToSend.Enqueue(new Packet(_tcpClient, EPacketType.ChatMessage, messageAsBytes));
        }

        /*
         * Takes in user input and parses it to a server address and port.
         */
        private void SetUpServerConnection()
        {
            /*
             * Connect to server.
             */
            do
            {
                Console.WriteLine("Please enter the server address as such: address:port");
                Console.WriteLine("If no port is specified we will use 2002.");

                //Read client input.
                string rawInput = Console.ReadLine();
                //Split the raw input by the : to get address and port separate.
                string[] input = rawInput.Split(':');

                //Check so we have atleast one.
                if (input.Length > 0)
                {
                    //Get address from first part of input.
                    string address = input[0];
                    //Set port to default value.
                    int port = Defaults.DEFAULTPORT;

                    //If we have more than one then the second one should be the port.
                    if (input.Length > 1)
                    {
                        //Try to parse integer into port. If this fails we will stay at the defaultport.
                        Int32.TryParse(input[1], out port);
                    }

                    //Attempt to connect to the server.
                    ConnectToServer(address, port);
                }
                else
                {
                    //If we have nothing then let's clear the console and restart.
                    Console.Clear();
                    Console.WriteLine("Invalid input: " + rawInput);
                    continue;
                }
            } //Repeat until we connect. 
            while (_tcpClient == null || _tcpClient.Connected == false);
        }

        /*
         * Connect to server on address with port.
         */
        void ConnectToServer(string address, int port)
        {
            Console.WriteLine("Attempting to connect to server.");

            //Create new client.
            _tcpClient = new TcpClient();

            //Try to connect to server.
            try
            {
                _tcpClient.Connect(address, port);

            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't connect to server at " + address + ":" + port);
                Console.WriteLine("Exception thrown: " + e.Message);
            }        
        }
    }
}
