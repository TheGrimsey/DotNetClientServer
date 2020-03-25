using System;
using System.Net.Sockets;
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

        public SimpleClient()
        {
            //Do this.
            do
            {
                Console.WriteLine("Please enter the server address as such: address:port");
                Console.WriteLine("If no port is specified we will use 2002.");

                //Read client input.
                string rawInput = Console.ReadLine();
                //Split the raw input by the : to get address and port separate.
                string[] input = rawInput.Split(':');

                //Check so we have atleast one.
                if(input.Length > 0)
                {
                    //Get address from first part of input.
                    string address = input[0];
                    //Set port to default value.
                    int port = DefaultPort;

                    //If we have more than one then the second one should be the port.
                    if(input.Length > 1)
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
            } //Until we connect. 
            while (_tcpClient == null || _tcpClient.Connected == false);

            Console.WriteLine("Connected to server!");

            //At this point we have connected. Let's send some messages whilst we still are.
            while(true)
            {
                //Read message from console.
                string MessageToSend = Console.ReadLine();

                //Send to server. If it fails break the loop.
                if(!SendMessageToServer(MessageToSend))
                {
                    break;
                }
            }

            Console.WriteLine("Disconnected from server. Press ENTER to exit.");
            Console.ReadLine();
        }

        /*
         * Attempts to send message to server.
         * Returns true if it succeeded without error.
         */
        bool SendMessageToServer(string message)
        {
            //Convert message to byte array.
            byte[] MessageAsBytes = System.Text.Encoding.Unicode.GetBytes(message);

            //Create packet array with one extra space for the packettype.
            byte[] Packet = new byte[MessageAsBytes.Length + 1];
            
            //Copy message into packet at a one index offset. 
            Array.Copy(MessageAsBytes, 0, Packet, 1, MessageAsBytes.Length);

            //Assign packet type.
            Packet[0] = (byte)EPacketType.ChatMessage;
            try
            {
                NetworkStream tcpStream = _tcpClient.GetStream();

                //Try to send message.
                tcpStream.Write(Packet, 0, Packet.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception when trying to send message to server: " + e.Message);
                return false;
            }

            return true;
        }

        /*
         * Attempt to connect to server on address with port.
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
