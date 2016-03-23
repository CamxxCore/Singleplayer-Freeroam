using System;
using System.ServiceModel;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using Assembly = System.Reflection.Assembly;
using System.Threading;

namespace SPFServer
{
    public class Server
    {
        public static SessionServer ActiveSession { get { return activeSession; } }

        private static SessionServer activeSession;

        internal static ASUPServiceClient SessionProvider;

        internal static ScriptManager ScriptManager;

        static void Main(string[] args)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            Console.WriteLine("Singleplayer Freeroam Server | BETA Build # " +
                Assembly.GetExecutingAssembly().GetName().Version.ToString() + 
                Environment.NewLine);

            int sessionID = AnnounceSessionToService();

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
                LogErrorToConsole("Failed while initializing the server. \n\nAborting...");
                Environment.Exit(0);
            }
        }

        internal static int AnnounceSessionToService()
        {
            SessionProvider = new ASUPServiceClient(
            new WSHttpBinding(SecurityMode.Transport),
            new EndpointAddress("https://camx.me/asupstatsvc/asupservice.svc"));

            bool success = false;

            try
            {
                int sessionID = SessionProvider.AnnounceSession("session1");
                success = true;
                return sessionID;
            }

            catch (EndpointNotFoundException)
            {
                LogErrorToConsole("Could not connect to the master server endpoint. Ensure you are connected to the internet and have allowed the program through your firewall \n\nAborting...");
            }

            catch (Exception ex)
            {
                LogErrorToConsole("Failed initializing the server. \nException Data: " + ex.Message);
            }

            finally
            {
                if (!success)
                {
                    System.Threading.Thread.Sleep(4000);
                    Environment.Exit(0);
                }
            }

            return -1;
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

        public static IPAddress GetExternAddress()
        {
            string externalIP = "";
            externalIP = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
            externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(externalIP)[0].ToString();
            return IPAddress.Parse(externalIP);
        }

        internal static void WriteToConsole(string message = "")
        {
            if (message.Length > 0)
            {
                Console.WriteLine("\nServer @ {0}>  " + message, DateTime.Now.ToLongTimeString());
            }
        }

        internal static string ReadFromConsole(string promptMessage = "")
        {
            // Show a prompt, and get input:
            Console.Write("Server> " + promptMessage);
            return Console.ReadLine();
        }

        internal static void LogErrorToConsole(string message = "")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[ERROR]\0");
            Console.ResetColor();
            Console.Write(message);
            Console.WriteLine();
        }
    }
}
