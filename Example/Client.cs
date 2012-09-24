﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using NTCPMSG.Client;
using NTCPMSG.Event;

namespace Example
{
    class Client
    {
        /// <summary>
        /// DataReceived event will be called back when server get message from client which connect to.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void ReceiveEventHandler(object sender, ReceiveEventArgs args)
        {
            switch ((Event)args.Event)
            {
                case Event.PushMessage:
                    //Get OneWay message from server
                    if (args.Data != null)
                    {
                        try
                        {
                            Console.WriteLine(Encoding.UTF8.GetString(args.Data));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                    break;
            }

        }

        public static void Run(string[] args)
        {
            Console.Write("Please input server IP Address [127.0.0.1]:");
            string ipAddress = Console.ReadLine().Trim().ToLower();

            if (ipAddress == "")
            {
                ipAddress = "127.0.0.1";
            }

            try
            {
                //************** SingConnection Example **********************

                Console.Write("Press any key to start single connection example");
                Console.ReadKey();

                //Create a SingleConnection instanace that will try to connect to host specified in 
                //ipAddress and port (2500).
                SingleConnection client =
                    new SingleConnection(new IPEndPoint(IPAddress.Parse(ipAddress), 2500));
                client.ReceiveEventHandler += new EventHandler<ReceiveEventArgs>(ReceiveEventHandler);

                client.Connect();

                Console.WriteLine("ASend: Hello world! I am Single");

                //Send an asynchronously message to server
                client.ASend((UInt32)Event.OneWay, Encoding.UTF8.GetBytes("Hello world! I am Single"));

                int number = 0;

                try
                {
                    Console.WriteLine("SSend {0}", number);

                    //send a synchronously message to server
                    //send a number with event: Event.Return to server and get the response from server 
                    //with the number increased.
                    byte[] retData = client.SSend((UInt32)Event.Return, BitConverter.GetBytes(number));

                    number = BitConverter.ToInt32(retData, 0);

                    Console.WriteLine("Get {0}", number);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                Console.Write("Waitting for 10 seconds to finish simple connection example.");
                System.Threading.Thread.Sleep(10000);

                client.Close();

                //************* SingleConnectionCable Example *****************
                Console.Write("Press any key to start single connection cable example");
                Console.ReadKey();

                //Create a SingleConnectionCable instance that will try to connect to host specified in 
                //ipAddress and port (2500).
                //by default, SingleConnectionCable will try to connect automatically and including 6 tcp connections.
                SingleConnectionCable clientCable =
                    new SingleConnectionCable(new IPEndPoint(IPAddress.Parse(ipAddress), 2500));
                
                clientCable.ReceiveEventHandler += new EventHandler<ReceiveEventArgs>(ReceiveEventHandler);
                clientCable.Connect();

                Console.WriteLine("ASend: Hello world! I am Cable");
                //Send a one way message to server
                clientCable.ASend((UInt32)Event.OneWay, Encoding.UTF8.GetBytes("Hello world! I am Cable"));

                while (true)
                {
                    Console.WriteLine("SSend {0}", number);

                    try
                    {
                        //send a number with event: Event.Return to server and get the response from server 
                        //with the number increased.
                        byte[] retData = clientCable.SSend((UInt32)Event.Return, BitConverter.GetBytes(number));

                        number = BitConverter.ToInt32(retData, 0);

                        Console.WriteLine("Get {0}", number);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    Console.WriteLine("Quit when you press ESC. Else continue SSend.");

                    //Quit when you press ESC
                    if (Console.ReadKey().KeyChar == 0x1B)
                    {
                        clientCable.Close();
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadLine();
            }
        }
    


    }
}
