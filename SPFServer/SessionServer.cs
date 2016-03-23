//#define DEBUG
using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using SPFLib;
using SPFLib.Types;
using SPFLib.Enums;
using ASUPService;
using Lidgren.Network;

namespace SPFServer
{
    public class GameClient
    {
        public ClientInfo Info { get; }      
        public TimeSpan Ping { get; set; }
        public NetConnection Connection { get; set; }
        public short Health { get; private set; }
        internal List<TimeSpan> AvgPing;
        internal TimeSpan TimeDiff;
        internal DateTime LastUpd;
        internal DateTime LastSync;
        internal bool WaitForRespawn;
        internal ClientState State { get; private set; }

        public GameClient(NetConnection connection, ClientInfo info)
        {
            Connection = connection;
            Info = info;
            State = new ClientState();
            AvgPing = new List<TimeSpan>();
            Health = 100;
        }

        internal void UpdateState(ClientState state, DateTime currentTime)
        {
            State = state;
            State.ClientID = Info.UID;
            State.Health = Health;
            LastUpd = currentTime;
        }

        public void SetHealth(int health)
        {
            if (health > short.MaxValue)
                return;

            Health = (short)health;
        }
    }

    public sealed class SessionServer
    {
        public WeatherManager WeatherManager { get { return weatherMgr; } }

        public GameClient[] ActiveClients { get { return activeClients.ToArray(); } }

        private NetServer server;

        private WeatherManager weatherMgr;

        private TimeCycleManager timeMgr;

        private ServerVarCollection<int> serverVars;

        private const int SVTickRate = 12; // updates / sec
        private const int SVPingRate = 10000; // master server ping interval (10 sec)
        private const int MinTimeSamples = 5;

        private readonly int SessionID;

        private Dictionary<string, Func<string[], string>> commands = new Dictionary<string, Func<string[], string>>();

        private List<GameClient> activeClients = new List<GameClient>();

        private List<GameClient> removalList = new List<GameClient>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private ThreadQueue threadQueue = new ThreadQueue(42);

        private object syncObj = new object();
        private byte[] byteData = new byte[1024];

        private DateTime lastMasterUpdate = new DateTime();

        private readonly NetPeerConfiguration Config;

        private SessionState state = new SessionState();

        private uint tickCount;

        /// <summary>
        /// Ctor
        /// </summary>
        public SessionServer(int sessionID, int maxPlayers, int maxPing, DateTime igTime, WeatherType weatherType)
        {
            SessionID = sessionID;
            Config = new NetPeerConfiguration("spfsession");
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.Port = 27852;
            server = new NetServer(Config);
            serverVars = new ServerVarCollection<int>(
                new ServerVar<int>("sv_maxplayers", maxPlayers),
                new ServerVar<int>("sv_maxping", maxPing),
                new ServerVar<int>("sv_tickrate", SVTickRate),
                new ServerVar<int>("sv_pingrate", SVPingRate),
                new ServerVar<int>("sv_mintimesamples", MinTimeSamples)
                 );
            commands.Add("status", GetStatus);
            commands.Add("vstatus", GetVehicleStatus);
            commands.Add("forcesync", ForceSync);
            commands.Add("invoke", InvokeNative);
            commands.Add("setweather", SetWeather);
            commands.Add("settime", SetTime);
            commands.Add("kick", KickClient);
            commands.Add("getpos", GetPosition);
            commands.Add("getposition", GetPosition);
            commands.Add("tp", TeleportClient);
            commands.Add("teleport", TeleportClient);
            commands.Add("tp2", TeleportToClient);
            commands.Add("tpto", TeleportToClient);
            commands.Add("help", ShowHelp);
            commands.Add("?", ShowHelp);
            timeMgr = new TimeCycleManager(igTime);
            weatherMgr = new WeatherManager(weatherType);
            weatherMgr.OnServerWeatherChanged += OnServerWeatherChanged;
        }

        public SessionServer(int sessionID) : this(sessionID, 12, 999, DateTime.Now, WeatherType.Clear)
        { }

        /// <summary>
        /// Start listening for client requests asynchronously.
        /// </summary>
        internal void StartListening()
        {
            server.Start();

            server.RegisterReceivedCallback(new SendOrPostCallback(OnReceive), SynchronizationContext.Current);

            Console.WriteLine("[INIT] Listening for clients\n");

            new Thread(() => Main()).Start();
        }

        int validStates;

        SessionState[] historyBuffer = new SessionState[20];

#if DEBUG
        Stopwatch sw1 = new Stopwatch();
#endif

        /// <summary>
        /// Main tick event. Handles user session updates, sent at the rate defined by SVTickRate
        /// </summary>
        /// <param name="data"></param>
        private void Main()
        {
            while (true)
            {
                tickCount += 1;
                tickCount %= uint.MaxValue;

                Server.ScriptManager?.DoTick();

                state.Sequence = tickCount;

                state.Clients = GetClientStates();

                for (int i = historyBuffer.Length - 1; i > 0; i--)
                    historyBuffer[i] = historyBuffer[i - 1];

                var timeNow = NetworkTime.Now;

                historyBuffer[0] = state;

                validStates = Math.Min(validStates + 1, historyBuffer.Length);

                lock (syncObj)
                {
                    foreach (var client in activeClients)
                    {
                        if (client.LastUpd.Ticks > 0 && (NetworkTime.Now - client.LastUpd) > TimeSpan.FromMilliseconds(1000))
                        {
                            removalList.Add(client);
                            continue;
                        }

                        if (client.AvgPing.Count < MinTimeSamples &&
                            NetworkTime.Now - client.LastSync > TimeSpan.FromMilliseconds(100))
                        {
                            SendSynchronizationRequest(client.Connection);
                            client.LastSync = NetworkTime.Now;
                            continue;
                        }

                        state.Timestamp = timeNow.Subtract(client.TimeDiff);

                        if (client.State != null)
                        {
                            if (client.State.MovementFlags.HasFlag(ClientFlags.Dead) && !client.WaitForRespawn)
                            {
                                client.WaitForRespawn = true;

                                threadQueue.AddTask(() =>
                                {
                                    Thread.Sleep(8900); //8.7 seconds to allow respawn
                                        client.SetHealth(100);
                                    client.WaitForRespawn = false;
                                });
                            }
                        }

                        var message = server.CreateMessage();

                        message.Write((byte)NetMessage.SessionUpdate);

                        message.Write(state);

                        server.SendMessage(message, client.Connection, NetDeliveryMethod.ReliableSequenced);
                    }
                }

                foreach (var client in removalList)
                {
                    threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" timed out.", client.Info.Name)));
                    RaiseSessionEvent(client, EventType.PlayerLogout);
                    RemoveClient(client);
                }

                removalList.Clear();

                if (NetworkTime.Now > lastMasterUpdate + 
                    TimeSpan.FromMilliseconds(serverVars.GetVar<int>("sv_mtrpingrate")))
                {
                    var maxPlayers = serverVars.GetVar<int>("sv_maxplayers");

                    var sessionInfo = new SessionUpdate()
                    {
                        ServerID = SessionID,
                        ClientCount = activeClients.Count,
                        MaxClients = maxPlayers,
                    };

                    threadQueue.AddTask(() => SendSessionUpdate(sessionInfo));
                    lastMasterUpdate = NetworkTime.Now;
                }
#if DEBUG
                    Console.WriteLine(((float)sw1.ElapsedMilliseconds / 1000f).ToString());

                    sw1.Reset();
                    sw1.Start();
#endif

                Thread.Sleep(1000 / serverVars.GetVar<int>("sv_tickrate"));
            }
        }

        NetIncomingMessage message;

        /// <summary>
        /// Process and handle any data received from the client.
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceive(object state)
        {
            message = server.ReadMessage();

            switch (message.MessageType)
            {
                case NetIncomingMessageType.Data:
                    HandleIncomingDataMessage(message);
                    break;
                case NetIncomingMessageType.ConnectionApproval:
                    if (message.ReadByte() == (byte)NetMessage.LoginRequest)
                    {
                        message.SenderConnection.Approve();
                        HandleLoginRequest(message.SenderConnection, message.ReadLoginRequest());
                    }
                    break;
                case NetIncomingMessageType.StatusChanged:
                    // Console.WriteLine(message.SenderConnection.ToString() + " status changed. " + (NetConnectionStatus)message.SenderConnection.Status);                         
                    break;
                case NetIncomingMessageType.WarningMessage:
                case NetIncomingMessageType.ErrorMessage:
                    Console.WriteLine(message.ReadString());
                    break;
                default:
                    Console.WriteLine("Unhandled type: " + message.MessageType);
                    break;
            }
        }

        private void HandleIncomingDataMessage(NetIncomingMessage message)
        {
            if (message.LengthBits <= 0) return;

            var dataType = (NetMessage)message.ReadByte();

            switch (dataType)
            {
                case NetMessage.ClientState:
                    HandleClientStateUpdate(message.SenderConnection, message.ReadClientState());
                    break;
                case NetMessage.SessionMessage:
                    threadQueue.AddTask(() => HandleChatMessage(message.SenderConnection, message.ReadSessionMessage()));
                    break;
                case NetMessage.SessionCommand:
                    threadQueue.AddTask(() => HandleSessionCommand(message.SenderConnection, message.ReadSessionCommand()));
                    break;
                case NetMessage.SessionSync:
                    threadQueue.AddTask(() => HandleSessionSync(message.SenderConnection, message.ReadSessionSync()));
                    break;
                case NetMessage.NativeCallback:
                    threadQueue.AddTask(() => HandleNativeCallback(message.SenderConnection, message.ReadNativeCallback()));
                    break;
                case NetMessage.WeaponData:
                    threadQueue.AddTask(() => HandleBulletImpact(message.SenderConnection, message.ReadWeaponData()));
                    break;
            }
        }

        private bool StressMitigationCheck(NetConnection sender, int msInterval)
        {
            /*  DateTime lastReceived;

              if (!stressMitigation.TryGetValue(sender.RemoteEndPoint, out lastReceived))
              {
                  stressMitigation.Add(sender.RemoteEndPoint, NetworkTime.Now);
                  return false;
              }

              else
              {
                  var value = (NetworkTime.Now - lastReceived < TimeSpan.FromMilliseconds(msInterval));

                  if (!value) stressMitigation[sender.RemoteEndPoint] = NetworkTime.Now;

                  return value;
              }*/

            return false;
        }

        private void OnServerWeatherChanged(WeatherType lastWeather, WeatherType newWeather)
        {
            foreach (var cl in activeClients)
            {
                InvokeClientNative(cl, "_SET_WEATHER_TYPE_OVER_TIME", newWeather.ToString(), 120f);
            }

            Console.WriteLine("Weather Changing... Previous: " + lastWeather.ToString() + " New: " + newWeather.ToString() + "\n");
        }

        internal void SendSessionUpdate(SessionUpdate sessionInfo)
        {
            try
            {
                Server.SessionProvider.SendHeartbeat(sessionInfo);
            }

            catch (Exception)
            {
                threadQueue.AddTask(() =>
                Server.WriteToConsole(string.Format("Failed while sending master server heartbeat.")));
            }
        }

        /// <summary>
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client"></param>
        internal void SendSynchronizationRequest(NetConnection client)
        {
            var req = new SessionSync();
            req.ServerTime = NetworkTime.Now;

            var msg = server.CreateMessage();
            msg.Write((byte)NetMessage.SessionSync);
            msg.Write(req.ServerTime.Ticks);
            msg.Write(req.ClientTime.Ticks);

            server.SendMessage(msg, client, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Raise an event for the specified client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="type"></param>
        internal void RaiseSessionEvent(GameClient client, EventType type, bool sendToOthers = true)
        {
            SessionEvent sEvent = new SessionEvent();
            sEvent.SenderID = client.Info.UID;
            sEvent.SenderName = client.Info.Name;
            sEvent.EventType = type;

            if (sendToOthers)
            {
                foreach (var cl in activeClients)
                {
                    if (cl.Info.UID != client.Info.UID)
                    {
                        var msg = server.CreateMessage();
                        msg.Write((byte)NetMessage.SessionEvent);
                        msg.Write(sEvent);
                        server.SendMessage(msg, cl.Connection, NetDeliveryMethod.ReliableOrdered);
                    }
                }
            }

            else
            {
                var cl = activeClients.FirstOrDefault(x => x.Info.UID == client.Info.UID);
                if (cl.Connection != null)
                {
                    var msg = server.CreateMessage();
                    msg.Write((byte)NetMessage.SessionEvent);
                    msg.Write(sEvent);
                    server.SendMessage(msg, cl.Connection, NetDeliveryMethod.ReliableOrdered);
                }
            }
        }

        /// <summary>
        /// Invoke a native on the client with return type void.
        /// </summary>
        /// <param name="client"></param>
        public void InvokeClientNative(GameClient client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            var msg = server.CreateMessage();
            msg.Write((byte)NetMessage.NativeCall);
            msg.Write(native);
            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Invoke a native on the client with a return type.
        /// </summary>
        /// <param name="client"></param>
        public void InvokeClientNative<T>(GameClient client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);

            var msg = server.CreateMessage();
            msg.WriteAllProperties(native);

            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Get a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool GetClient(NetConnection connection, out GameClient client)
        {
            client = activeClients.Find(x => x.Connection.GetHashCode().Equals(connection.GetHashCode()));
            return client != null;
        }

        /// <summary>
        /// Get a client by its user ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool GetClient(int clientID, out GameClient client)
        {
            var item = activeClients.Where(x => x.Info.UID == clientID).FirstOrDefault();

            if (item != null)
            {
                client = item;
                return true;
            }

            else
            {
                client = null;
                return false;
            }
        }

        /// <summary>
        /// Returns true if the client exists.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool ClientExists(NetConnection conn)
        {
            return server.Connections.Contains(conn);
        }

        /// <summary>
        /// Remove a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(GameClient client)
        {
            lock (syncObj)
            {
                activeClients.Remove(client);
            }
        }

        /// <summary>
        /// Remove a client by client ID.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(int clientID)
        {
            var client = activeClients.Where(x => x.Info.UID == clientID).FirstOrDefault();
            if (client.Connection != null)
                RemoveClient(client);
        }

        /// <summary>
        /// Return an array of active vehicle states.
        /// </summary>
        /// <returns></returns>
        internal VehicleState[] GetVehicleStates()
        {
            return activeClients.Select(x => x.State.VehicleState).Where(y => y != null).ToArray();
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        private ClientState[] GetClientStates()
        {
            return activeClients.Select(x => x.State).ToArray();
        }

        #region received msgs

        /// <summary>
        /// Handle a chat message sent by the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="msg"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleChatMessage(NetConnection sender, SessionMessage message)
        {
            if (StressMitigationCheck(sender, 1500)) return;

            GameClient client;

            if (GetClient(sender, out client))
            {
                message.SenderName = client.Info.Name;
                message.SenderUID = client.Info.UID;

                var msg = server.CreateMessage();
                msg.WriteAllProperties(message);

                foreach (var cl in activeClients)
                {
                    if (cl.Connection != sender)
                    {
                        server.SendMessage(msg, cl.Connection, NetDeliveryMethod.ReliableOrdered);

                    }
                }

                Server.ScriptManager.DoMessageReceived(client, message.Message);

                threadQueue.AddTask(() => Server.WriteToConsole(string.Format("{0}: {1} [{2}]", client.Info.Name, message.Message, NetworkTime.Now.ToString())));
            }

            else
            {
                Server.WriteToConsole("Cannot send a message, the sender doesn't exist.");
            }
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientStateUpdate(NetConnection sender, ClientState state)
        {
            try
            {
                GameClient client;
                if (GetClient(sender, out client))
                {
                    client.UpdateState(state, NetworkTime.Now);
                }
            }

            catch (Exception e)
            {
                threadQueue.AddTask(() => Server.WriteToConsole(string.Format("Update state failed.\n\nException:\n{0}", e.ToString())));
            }
        }

        /// <summary>
        /// Handle commands invoked by the client. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cmd"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleSessionCommand(NetConnection sender, SessionCommand cmd)
        {
            if (StressMitigationCheck(sender, 5000)) return;

            GameClient client;

            switch (cmd.Command)
            {          
                case CommandType.Logout:
                    if (GetClient(sender, out client))
                    {
                        RemoveClient(client);

                        RaiseSessionEvent(client, EventType.PlayerLogout);

                        Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                        threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" left the session.", cmd.Name, cmd.UID)));
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleLoginRequest(NetConnection sender, LoginRequest req)
        {
            if (StressMitigationCheck(sender, 5000) ||
                activeClients.Count > serverVars.GetVar<int>("sv_maxplayers") ||
                req == null ||
                req.Username == null ||
                req.UID == 0)
                return;

            Console.WriteLine(req.UID + "name: " + req.Username);

            GameClient client;

            if (!ClientExists(sender))
            {
                client = new GameClient(sender, new ClientInfo(req.UID, req.Username));

                client.LastUpd = NetworkTime.Now;

                client.LastSync = NetworkTime.Now;

                activeClients.Add(client);
            }

            else
            {
                if (GetClient(sender, out client))
                {
                    RemoveClient(client);
                }
            }
        }
    

        /// <summary>
        /// This method handles the time synchronization echo from the client.
        /// The time offset is computed based on the clients local time and packet latency 
        /// </summary>
        /// <param name="sender">The client that sent the echo</param>
        /// <param name="req">Time data returned from the client</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleSessionSync(NetConnection sender, SessionSync req)
        {
            var currentTime = NetworkTime.Now;

            GameClient client;



            if (GetClient(sender, out client))
            {
                // total time for the roundtrip (server > client > server)
                client.Ping = (currentTime - req.ServerTime);

                // clock offset based on local client time and half the ping time.
                var timeDiff = (currentTime - req.ClientTime) - TimeSpan.FromMilliseconds(client.Ping.TotalMilliseconds / 2);
                Console.WriteLine("ss " + timeDiff.ToString());
                if (client.TimeDiff == null)
                {               
                    client.TimeDiff = timeDiff;
                }

                client.AvgPing.Add(timeDiff);

                if (client.AvgPing.Count >= MinTimeSamples)
                {
                    client.TimeDiff = Helpers.CalculateAverage(ref client.AvgPing, MinTimeSamples);

                    if (client.TimeDiff.TotalMilliseconds > 10000)
                    {
                        threadQueue.AddTask(() => Server.WriteToConsole("Unusually bad ping result.. retrying: " + 
                            req.ClientTime.ToString() + " " + 
                            req.ServerTime.ToString()));

                        client.AvgPing.Clear();
                    }

                    else
                    {
                        Server.ScriptManager.DoClientConnect(client, NetworkTime.Now);

                        RaiseSessionEvent(client, EventType.PlayerSynced, false);

                        RaiseSessionEvent(client, EventType.PlayerLogon);

                        Server.ScriptManager.DoClientConnect(client, NetworkTime.Now);
                        // notify other clients in the session
      
                        // setup local world -->
                        InvokeClientNative(client, "SET_WEATHER_TYPE_NOW", new NativeArg(weatherMgr.CurrentWeather.ToString()));

                        InvokeClientNative(client, "NETWORK_OVERRIDE_CLOCK_TIME", new NativeArg(timeMgr.CurrentTime.Hour),
                            new NativeArg(timeMgr.CurrentTime.Minute),
                            new NativeArg(timeMgr.CurrentTime.Second));

                        threadQueue.AddTask(() =>
                        Server.WriteToConsole(string.Format("User \"{0}\" joined the session with user ID \'{1}\'. Client IP Address: {2}\nIn- game time: {3}", client.Info.Name, client.Info.UID, (sender.RemoteEndPoint).Address.ToString(), timeMgr.CurrentTime.ToShortTimeString())));
                        // threadQueue.AddTask(() => Server.WriteToConsole(string.Format("Synced time for user {0} at {1}. Difference: {2}", client.Info.Name, currentTime.ToString(), client.TimeDiff.ToString())));
                    }
                }
            }
        }

        /// <summary>
        /// Handle a weapon hit sent by the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="wh"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleBulletImpact(NetConnection sender, WeaponData wh)
        {
            GameClient killer, target;
            if (GetClient(sender, out killer))
            {
                var playbackTime = wh.Timestamp.Add(killer.TimeDiff);

                // get the game state from when this bullet was fired.
                var recentStates = historyBuffer.Where(x => x.Timestamp <= playbackTime).FirstOrDefault();


                for (int i = 0; i < recentStates.Clients.Length; i++)
                {
                    if (recentStates.Clients[i].ClientID == killer.Info.UID)
                    {
                        foreach (var client in recentStates.Clients)
                        {
                            //  dont want the killer
                            if (client.ClientID == recentStates.Clients[i].ClientID)
                                continue;
                            // check distance
                            //  var dist = client.Position.DistanceTo(wh.HitCoords);

                            // lazy anti- cheat
                            //  if (dist > 5f) continue;

                            // make sure the target exists
                            if (GetClient(client.ClientID, out target))
                            {
                                // modify health
                                target.SetHealth(target.Health - wh.WeaponDamage);

                                Console.WriteLine(target.Health);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle a native callback from the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cb"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleNativeCallback(NetConnection sender, NativeCallback cb)
        {
            /* var callback = nativeCallbacks.Find(x => x == cb.NetID);

             if (callback != 0)
             {
                 nativeCallbacks.Remove(callback);
             }*/
            threadQueue.AddTask(() => Console.WriteLine("callback"));
        }

        #endregion

        #region command line functions

        /// <summary>
        /// Execute a console command by its string alias defined in this.commands
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        internal bool ExecuteCommandString(string cmd)
        {
            var stringArray = Regex.Split(cmd,
              "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            string command = stringArray[0].ToLower();

            string[] args = stringArray.Skip(1).ToArray();

            Func<string[], string> func = null;

            if (commands.TryGetValue(command, out func))
            {
                func?.Invoke(args);
                return true;
            }

            else
            {
                return false;
            }
        }

        internal string ShowHelp(params string[] args)
        {
            var builder = new System.Text.StringBuilder("\nValid Commands:\n\n");
            builder.AppendLine("status - get a list of all active clients. params: N/A");
            builder.AppendLine("vstatus - get a list of all active vehicles. params: N/A");
            // builder.AppendLine("forcesync - force a time- sync for all clients. params: N/A");
            builder.AppendLine("invoke - invoke a client native. params: clientIndex, functionName, args");
            builder.AppendLine("setweather - set the in- game weather for all clients. params: weatherType");
            builder.AppendLine("settime - set the in- game time for all clients. params: hours, minutes, sec");
            builder.AppendLine("getpos / getposition - get in in- game world position for the client");
            builder.AppendLine("kick - kick a client from the server by index");
            builder.AppendLine("help / ? - display help.\n");
            Console.Write(builder.ToString());
            return null;
        }

        /// <summary>
        /// Get a list of active clients and print it to the console.
        /// </summary>
        internal string GetStatus(params string[] args)
        {
            var states = GetClientStates();

            var builder = new System.Text.StringBuilder("Active Clients:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | UID: {1} Name: {2} Vehicle Seat: {3}", i, states[i].ClientID, states[i].Name, states[i].VehicleSeat == VehicleSeat.None ? "N/A" : states[i].VehicleSeat.ToString());

            builder.AppendLine();

            Console.Write(builder.ToString());

            return null;
        }

        /// <summary>
        /// Get a list of active vehicles and print it to the console.
        /// </summary>
        internal string GetVehicleStatus(params string[] args)
        {
            var states = GetVehicleStates();

            var builder = new System.Text.StringBuilder("Active Vehicles:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | Vehicle ID: {1} Flags: {2} Position: {3} {4} {5} Radio Station: {6}", i, states[i].VehicleID, states[i].Flags, states[i].Position.X, states[i].Position.Y, states[i].Position.Z, states[i].RadioStation);

            builder.AppendLine();

            Console.Write(builder.ToString());

            return null;
        }

        /// <summary>
        /// Invoke a client native.
        /// </summary>
        internal string InvokeNative(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));

            if (client != null)
            {
                string funcName = args[1];

                //skip user index and function name to get args
                var funcArgs = args.Skip(2).ToArray();

                List<NativeArg> nativeArgs = new List<NativeArg>();

                foreach (var arg in funcArgs)
                {
                    nativeArgs.Add(new NativeArg(arg));
                }

                InvokeClientNative(client, funcName, nativeArgs.ToArray());
            }

            return null;
        }

        /// <summary>
        /// Set weather for all players
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        internal string SetWeather(params string[] args)
        {
            var weatherArgs = args[0];

            foreach (var cl in activeClients)
            {
                InvokeClientNative(cl, "SET_OVERRIDE_WEATHER", new NativeArg(weatherArgs));
            }
            return null;
        }

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal string SetTime(params string[] args)
        {
            int hours = Convert.ToInt32(args[0]);
            int minutes = Convert.ToInt32(args[1]);
            int seconds = Convert.ToInt32(args[2]);

            timeMgr.CurrentTime = new DateTime();
            timeMgr.CurrentTime += new TimeSpan(hours, minutes, seconds);

            foreach (var cl in activeClients)
            {
                InvokeClientNative(cl, "NETWORK_OVERRIDE_CLOCK_TIME", hours, minutes, seconds);
            }

            return null;
        }

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="time">Time to set.</param>
        public void SetTime(DateTime time)
        {
            timeMgr.CurrentTime = time;

            foreach (var cl in activeClients)
            {
                InvokeClientNative(cl, "NETWORK_OVERRIDE_CLOCK_TIME", time.Hour, time.Minute, time.Second);
            }
        }

        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal string KickClient(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));

            if (client.Connection != null)
            {
                RaiseSessionEvent(client, EventType.PlayerKicked);

                activeClients.Remove(client);

                Server.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Info.Name));
            }

            return null;
        }

        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param client="client to kick.">hours</param>
        public bool KickClient(GameClient client)
        {
            var item = activeClients.Where(x => x.Info.UID == client.Info.UID).FirstOrDefault();

            if (item != null)
            {
                activeClients.Remove(item);
                Server.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Info.Name));
                return true;
            }

            else
            {
                return false;
            }
        }

        /// <summary>
        /// Teleport a client.
        /// </summary>
        public void TeleportClient(GameClient client, Vector3 position)
        {
            InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET", new NativeArg(DataType.LocalHandle), position.X, position.Y, position.Z, 1, 1, 1);
        }

        /// <summary>
        /// Teleport a client to another.
        /// </summary>
        public void TeleportClient(GameClient client, GameClient destClient)
        {
            var dest = destClient.State.Position;
            InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET", new NativeArg(DataType.LocalHandle), dest.X, dest.Y, dest.Z, 1, 1, 1);
        }

        /// <summary>
        /// Teleport a client.
        /// </summary>
        internal string TeleportClient(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));

            var posX = Convert.ToSingle(args[1]);
            var posY = Convert.ToSingle(args[2]);
            var posZ = Convert.ToSingle(args[3]);

            TeleportClient(client, new Vector3(posX, posY, posZ));

            return null;
        }

        /// <summary>
        /// Teleport a client.
        /// </summary>
        internal string TeleportToClient(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));
            var client1 = activeClients.ElementAt(Convert.ToInt32(args[1]));

            TeleportClient(client, client1);

            return null;
        }

        /// <summary>
        /// Get a player position
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        internal string GetPosition(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));

            Console.WriteLine("{0}'s position: {1} {2} {3}", client.Info.Name,
                client.State.Position.X,
                client.State.Position.Y,
                client.State.Position.Z);

            return null;
        }

        /// <summary>
        /// Force a time sync for all clients.
        /// </summary>
        internal string ForceSync(params string[] args)
        {
            foreach (var client in activeClients)
            {
                SendSynchronizationRequest(client.Connection);
            }

            return null;
        }

        #endregion
    }
}

