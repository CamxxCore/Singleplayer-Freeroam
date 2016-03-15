using System;
using System.ServiceModel;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;

namespace SPFServer
{
    public class Program
    {
        private static UDPSocketListener currentSession;

        internal static ASUPServiceClient SProvider;

        internal static Scripting.ScriptDomain Domain;

        static void Main(string[] args)
        {       
            int sessionID = AnnounceSessionToService();

            currentSession = new UDPSocketListener(sessionID);
            currentSession.StartListening();

            if (StartScriptDomain())
                System.Threading.ThreadPool.QueueUserWorkItem(x => Console.WriteLine("Script Domain started."));

            Run();
        }

        static int AnnounceSessionToService()
        {
            SProvider = new ASUPServiceClient(
            new WSHttpBinding(SecurityMode.Transport),
            new EndpointAddress("https://camx.me/asupstatsvc/asupservice.svc"));

            bool success = false;

            try
            {
                int sessionID = SProvider.AnnounceSession("session1");
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
                    if (!currentSession.ExecuteCommandString(consoleInput))
                        WriteToConsole("Command not recognized '" + consoleInput + "'");
                }

                catch (Exception ex)
                {
                    WriteToConsole(ex.Message);
                }
            }
        }

       static bool StartScriptDomain()
        {
            Domain = Scripting.ScriptDomain.Load(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "scripts"));

            if (Domain == null)
            {
                return false;
            }

            Domain.Start();

            return true;
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
                Console.WriteLine("\n[Server @ {0}]  " + message, DateTime.Now.ToLongTimeString());
            }
        }

        const string _readPrompt = "Server> ";
        internal static string ReadFromConsole(string promptMessage = "")
        {
            // Show a prompt, and get input:
            Console.Write(_readPrompt + promptMessage);
            return Console.ReadLine();
        }
    }
}
