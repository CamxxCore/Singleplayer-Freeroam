using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPFLib.Types;

namespace SPFServer.Main
{
    public class ServerCommands
    {
        #region command line functions

        internal static string ShowHelp(params string[] args)
        {
            var builder = new StringBuilder("\nValid Commands:\n\n");
            builder.AppendLine("status - get a list of all active clients. params: N/A");
            builder.AppendLine("vstatus - get a list of all active vehicles. params: N/A");
            builder.AppendLine("screl - Reload all server scripts");
            builder.AppendLine("invoke - invoke a client native. params: clientIndex, functionName, args");
            builder.AppendLine("setweather - set the in- game weather for all clients. params: weatherType");
            builder.AppendLine("settime - set the in- game time for all clients. params: hours, minutes, sec");
            builder.AppendLine("getpos / getposition - get in in- game world position for the client");
            builder.AppendLine("kick - kick a client from the server by index");
            builder.AppendLine("help / ? - display help.\n");
            Console.Write(builder.ToString());
            return null;
        }

        internal static string GetVar(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            string varName = args[0];

            int value = 0;

            if (Server.ActiveSession.ServerVars.TryGetValue(varName, out value))
            {
                Console.WriteLine("{0} = {1}", varName, value);
            }

            else Console.WriteLine("Specified var was not found.");

            return null;
        }

        internal static string GetString(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            string stringName = args[0];

            string value = "";

            if (Server.ActiveSession.ServerStrings.TryGetValue(stringName, out value))
            {
                Console.WriteLine("{0} = {1}", stringName, value);
            }

            else Console.WriteLine("Specified string was not found.");

            return null;
        }

        internal static string SetVar(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            string varName = args[0];

            int value = 0;

            if (int.TryParse(args[1], out value))
            {
                if (Server.ActiveSession.ServerVars.ContainsKey(varName))
                {
                    Server.ActiveSession.ServerVars[varName] = value;
                    Console.WriteLine("\nSuccess");
                }

                else Console.WriteLine("Specified var was not found.");
            }

            else Console.WriteLine("Value '" + args[1] + "' was in an invalid format.");

            return null;
        }

        /// <summary>
        /// Invoke a client native.
        /// </summary>
        internal static string InvokeNative(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var client = Server.ActiveSession.GameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            if (client != null)
            {
                string funcName = args[1];

                var funcArgs = args.Skip(2).ToArray();

                List<NativeArg> nativeArgs = new List<NativeArg>();

                foreach (var arg in funcArgs)
                {
                    nativeArgs.Add(new NativeArg(arg));
                }

                Server.ActiveSession.InvokeClientFunction(client, funcName, nativeArgs.ToArray());
            }

            return null;
        }

        /// <summary>
        /// Set weather for all players
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        internal static string SetWeather(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var weatherArgs = args[0];

            Enums.WeatherType weather;

            if (Enum.TryParse(weatherArgs, true, out weather))
            {              
                Server.ActiveSession.CurrentWeather = weather;
            }

            return null;
        }

        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal static string KickClient(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var client = Server.ActiveSession.GameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            if (client != null)
            {
                Server.ActiveSession.KickClient(client);          
            }

            return null;
        }

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal static string SetTime(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            int hours = Convert.ToInt32(args[0]);
            int minutes = Convert.ToInt32(args[1]);
            int seconds = Convert.ToInt32(args[2]);

            Server.ActiveSession.CurrentTime = new DateTime();
            Server.ActiveSession.CurrentTime += new TimeSpan(hours, minutes, seconds);

            foreach (var client in Server.ActiveSession.ActiveClients)
            {
                NativeFunctions.SetClock(client, hours, minutes, seconds);
            }

            return null;
        }

        internal static string TeleportClient(params string[] args)
        {
            if (Server.ActiveSession == null) return null; 

            var client = Server.ActiveSession.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            var posX = Convert.ToSingle(args[1]);
            var posY = Convert.ToSingle(args[2]);
            var posZ = Convert.ToSingle(args[3]);

            NativeFunctions.SetPosition(client, new Vector3(posX, posY, posZ));

            return null;
        }

        internal static string TeleportToClient(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var client = Server.ActiveSession.GameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            var client1 = Server.ActiveSession.GameManager.ActiveClients.ElementAt(Convert.ToInt32(args[1]));

            NativeFunctions.SetPosition(client, client1);

            return null;
        }

        internal static string GetPosition(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var client = Server.ActiveSession.GameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));
            var state = client.State;

            Console.WriteLine("{0}'s position: {1} {2} {3}", client.Info.Name,
                state.Position.X,
                state.Position.Y,
                state.Position.Z);

            return null;
        }

        /// <summary>
        /// Get a list of active clients and print it to the console.
        /// </summary>
        internal static string GetStatus(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 10)));
            var builder = new System.Text.StringBuilder("Active Clients:");
            for (int i = 0; i < Server.ActiveSession.GameManager.ActiveClients.Count; i++)
                builder.AppendFormat("\nID: {0} | UID: {1} Name: {2} Health: {3} Ping: {4}ms",
                    i,
                    Server.ActiveSession.GameManager.ActiveClients[i].Info.UID,
                    Server.ActiveSession.GameManager.ActiveClients[i].Info.Name,
                    Server.ActiveSession.GameManager.ActiveClients[i].Health,
                    Server.ActiveSession.GameManager.ActiveClients[i].Ping.TotalMilliseconds);

            builder.AppendLine();

            Console.Write(builder.ToString());

            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 15)));

            return null;
        }

        /// <summary>
        /// Get a list of active vehicles and print it to the console.
        /// </summary>
        internal static string GetVehicleStatus(params string[] args)
        {
            if (Server.ActiveSession == null) return null;

            var states = Server.ActiveSession.GameManager.GetVehicleStates();

            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 10)));

            var builder = new System.Text.StringBuilder("Active Vehicles:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | Model: {1} Health: {2} Flags: {3} Position: {4} {5} {6} Radio Station: {7}",
                    i, SPFLib.Helpers.VehicleIDToHash(states[i].ModelID).ToString(), states[i].Health, states[i].Flags,
                    states[i].Position.X, states[i].Position.Y, states[i].Position.Z, states[i].RadioStation);

            builder.AppendLine();

            Console.Write(builder.ToString());

            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 15)));

            return null;
        }

        internal static string ScriptReload(params string[] args)
        {
            Server.ScriptManager.Reload();
            return null;
        }

        #endregion
    }
}

