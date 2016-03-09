using System;
using System.ServiceModel;
using System.Net;
using System.Text.RegularExpressions;

namespace SPFServer
{
    class Program
    {
        private static UDPSocketListener listener;

        private static ASUPServiceClient sProvider;

        public static ASUPServiceClient SessionProvider
        {
            get
            {
                return sProvider;
            }
        }

        object sMainFib = null;
        object sScriptFib = null;

        static void Main(string[] args)
        {
            

            int sessionID = AnnounceSessionToService();

            listener = new UDPSocketListener(sessionID);
            listener.StartListening();

            Run();
        }

        static int AnnounceSessionToService()
        {
            sProvider = new ASUPServiceClient(
            new WSHttpBinding(SecurityMode.Transport),
            new EndpointAddress("https://camx.me/asupstatsvc/asupservice.svc"));

            bool success = false;

            try
            {
                int sessionID = sProvider.AnnounceSession("session1");
                success = true;
                return sessionID;
            }

            catch (EndpointNotFoundException)
            {
                Console.WriteLine("\n[Error] Could not connect to the master server endpoint. Ensure you are connected to the internet and have allowed the program through your firewall \n\nAborting...");
            }

            catch (Exception ex)
            {
                WriteToConsole("[Error] Failed initializing the server. \nException Data: " + ex.Message);
            }

            finally
            {
                if (!success)
                {
                    System.Threading.Thread.Sleep(4000);
                    Environment.Exit(0);
                }
            }

            return 0;
        }

        static void Run()
        {
            while (true)
            {
                var consoleInput = ReadFromConsole();
                if (string.IsNullOrWhiteSpace(consoleInput)) continue;

                try
                {
                    if (!listener.ExecuteCommandString(consoleInput))
                        WriteToConsole("Command not recognized '" + consoleInput + "'");
                }

                catch (Exception ex)
                {
                    WriteToConsole(ex.Message);
                }
            }
        }

        static IPAddress GetExternAddress()
        {
            string externalIP = "";
            externalIP = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
            externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(externalIP)[0].ToString();
            return IPAddress.Parse(externalIP);
        }

        public static void WriteToConsole(string message = "")
        {
            if (message.Length > 0)
            {
                Console.WriteLine("\n[Server @ {0}]  " + message, DateTime.Now.ToLongTimeString());
            }
        }

        const string _readPrompt = "Server> ";
        public static string ReadFromConsole(string promptMessage = "")
        {
            // Show a prompt, and get input:
            Console.Write(_readPrompt + promptMessage);
            return Console.ReadLine();
        }
    }
}
