using System;
using System.Collections.Concurrent;
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
            Console.Title = "SimpleClient";
            Console.InputEncoding = System.Text.Encoding.Unicode;
            Console.OutputEncoding = System.Text.Encoding.Unicode;

            //Create client.
            SimpleClient simpleClient = new SimpleClient();
        }
    }
    class SimpleClient
    {
        //Our connection to the server.
        TcpClient _tcpClient;

        const int DefaultPort = 2002;

        //Queue of packets to send to server.
        ConcurrentQueue<byte[]> PacketsToSend;

        //CancellationTokenSource used for cancelling HandleInput()
        CancellationTokenSource cancellationTokenSource;

        public SimpleClient()
        {
            SetUpServerConnection();

            PacketsToSend = new ConcurrentQueue<byte[]>();
            cancellationTokenSource = new CancellationTokenSource();

            Console.WriteLine("Connected to server!");

            //Start listening to input on secondary thread.
            Thread inputThread = new Thread(new ThreadStart(HandleInput));
            inputThread.Start();

            Console.WriteLine("Starting packet sending..");

            //At this point we have connected. Let's send some messages until we aren't.
            while (_tcpClient.Connected)
            {
                Thread.Sleep(50);

                //Go through all PacketsToSend and send them.
                byte[] Packet;
                NetworkStream stream = _tcpClient.GetStream();

                while (PacketsToSend.TryDequeue(out Packet))
                {
                    stream.Write(Packet, 0, Packet.Length);
                }

                //TODO: Read packets.

                //Note: We could in theory send multiple packets at once and save some overhead but that requires work on splitting them up on the server.
            }

            Console.WriteLine("Disconnected from server. Press ENTER to exit.");
            Console.ReadLine();
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
                Console.WriteLine("TEST");

                Task<string> readTask =  Console.In.ReadLineAsync();
                readTask.Wait();

                string Input = readTask.Result;

                //Check if this is a command. (Command start with a slash)
                if(Input.StartsWith("/"))
                {
                    //This is a command. Let's deal with it.
                    Console.WriteLine(string.Format("Command {0} Not implemented."), Input); //Dealt with it.
                }
                else
                {
                    //Nothing special with this so let's just treat it as a normal message.
                    SendChatMessageToServer(Input);
                }
            }
        }

        /*
         * Attempts to send message to server.
         * Returns true if it succeeded without error.
         */
        void SendChatMessageToServer(string message)
        {
            //Convert message to byte array.
            byte[] MessageAsBytes = System.Text.Encoding.Unicode.GetBytes(message);

            //Create packet array with one extra space for the packettype.
            byte[] Packet = new byte[MessageAsBytes.Length + 1];
            
            //Copy message into packet at a one index offset. 
            Array.Copy(MessageAsBytes, 0, Packet, 1, MessageAsBytes.Length);

            //Assign packet type.
            Packet[0] = (byte)EPacketType.ChatMessage;

            PacketsToSend.Enqueue(Packet);
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
                    int port = DefaultPort;

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
