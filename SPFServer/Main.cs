﻿using System;
using System.ServiceModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using SPFServer.Session;


namespace SPFServer
{
    public class Server
    {
        public static SessionServer ActiveSession { get { return activeSession; } }
        private static SessionServer activeSession;

        internal static NetworkService NetworkService;

        internal static ScriptManager ScriptManager;

        static void Main(string[] args)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            Console.WriteLine("Singleplayer Freeroam Server | BETA Build # " +
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + 
                Environment.NewLine);

            NetworkService = new NetworkService();

            NetworkService.Initialize();

            int sessionID = NetworkService.AnnounceSession("session1");

            if (sessionID != -1)
            {
                Console.WriteLine("[INIT] Starting session server...\n");

                activeSession = new SessionServer(sessionID);
                activeSession.StartListening();

                ScriptManager = new ScriptManager(AppDomain.CurrentDomain.BaseDirectory + "scripts");
                ScriptManager.StartThreads();

                Run();
            }

            else
            {
                WriteErrorToConsole("Failed while initializing the server. \n\nAborting...");
                Environment.Exit(0);
            }
        }   

        internal static void Run()
        {
            Console.WriteLine();

            while (true)
            {
                var consoleInput = ReadFromConsole();
                if (string.IsNullOrWhiteSpace(consoleInput)) continue;

                try
                {
                    if (!activeSession.ExecuteCommandString(consoleInput))
                        WriteToConsole("Command not recognized '" + consoleInput + "'");
                }

                catch (Exception ex)
                {
                    WriteToConsole(ex.Message);
                }
            }
        }

        internal static string ReadFromConsole(string promptMessage = "")
        {
            Console.Write("Server> " + promptMessage);
            return Console.ReadLine();
        }

        internal static void WriteToConsole(string message = "")
        {
            if (message.Length > 0)
            {
                Console.WriteLine("\nServer @ {0}>  " + message, DateTime.Now.ToLongTimeString());
            }
        }

        internal static void WriteErrorToConsole(string message = "")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[ERROR]\0");
            Console.ResetColor();
            Console.Write(message);
            Console.WriteLine();
        }

        public static IPAddress GetExternAddress()
        {
            string externalIP = "";
            externalIP = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
            externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(externalIP)[0].ToString();
            return IPAddress.Parse(externalIP);
        }
    }
}
