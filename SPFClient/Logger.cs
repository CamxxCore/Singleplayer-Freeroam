using System;
using System.IO;

namespace SPFClient
{
    /// <summary>
    /// Static logger class that allows direct logging of anything to a text file
    /// </summary>
    internal static class Logger
    {
        public static void Log(object message)
        {
            try
            {
                File.AppendAllText("SPFClient.log", DateTime.Now + " : " + message + Environment.NewLine);
            }

            catch { }
        }

        /*    public static void Log(params string[] message)
            {
                StringBuilder sb = new StringBuilder();

                foreach (var item in message)
                    sb.Append(item);

                try
                {
                    File.AppendAllText("SPFServer.log", DateTime.Now + " : " + String.Join("", message) + Environment.NewLine);
                }

                catch { }
            }*/
    }
}