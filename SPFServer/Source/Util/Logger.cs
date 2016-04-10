using System;
using System.IO;

namespace SPFServer
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
                File.AppendAllText("SPFServer.log", DateTime.Now + " : " + message + Environment.NewLine);
            }

            catch { }
        }
    }
}