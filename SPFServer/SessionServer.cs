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
        public Vector3 Position { get { return ReceivedStates[0].Position; } }
        public Quaternion Rotation { get { return ReceivedStates[0].Rotation; } }
        public short Health { get; private set; }
        internal int ReceivedStatesCount;
        internal List<TimeSpan> AvgPing;
        internal TimeSpan TimeDiff;
        internal DateTime LastUpd;
        internal DateTime LastSync;
        internal bool WaitForRespawn;
        internal ClientState[] ReceivedStates { get; private set; }

        public GameClient(NetConnection connection, ClientInfo info)
        {
            Connection = connection;
            Info = info;
            ReceivedStates = new ClientState[100];
            ReceivedStates[0] = new ClientState();
            AvgPing = new List<TimeSpan>();
            Health = 100;
        }

        internal void UpdateState(ClientState state, DateTime currentTime)
        {
            for (int i = ReceivedStates.Length - 1; i > 0; i--)
                ReceivedStates[i] = ReceivedStates[i - 1];
            ReceivedStates[0] = state;
            ReceivedStates[0].ClientID = Info.UID;
            ReceivedStatesCount = Math.Min(ReceivedStatesCount + 1, ReceivedStates.Length);
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
        public WeatherManager WeatherManager {  get { return weatherMgr; } }

        public GameClient[] ActiveClients {  get { return activeClients.ToArray(); } }

        private NetServer server;

        private WeatherManager weatherMgr;

        private TimeCycleManager timeMgr;

        private ServerVarCollection<int> serverVars;

        private const int SVTickRate = 10; // updates / sec
        private const int SVPingRate = 10000; // master server ping interval (10 sec)
        private const int MinTimeSamples = 5;

        private const int TimerInterval = 1000 / SVTickRate;

        private readonly int PingInterval = SVPingRate / TimerInterval;

        private readonly int SessionID;

        private Dictionary<string, Func<string[], string>> commands = new Dictionary<string, Func<string[], string>>();

        private List<GameClient> activeClients = new List<GameClient>();

        private List<GameClient> removalList = new List<GameClient>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private List<int> pendingCallbacks = new List<int>();

        private List<int> nativeCallbacks = new List<int>();

        private ThreadQueue threadQueue = new ThreadQueue(24);

        private object syncObj = new object();
        private byte[] byteData = new byte[1024];

        private DateTime lastMasterUpdate = new DateTime();

        private readonly NetPeerConfiguration Config;

        SessionState state = new SessionState();

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
            timeMgr = new TimeCycleManager(igTime);
            weatherMgr = new WeatherManager(weatherType);
            weatherMgr.OnServerWeatherChanged += OnServerWeatherChanged;
            commands.Add("status", GetStatus);
            commands.Add("vstatus", GetVehicleStatus);
            commands.Add("forcesync", ForceSync);
            commands.Add("invoke", InvokeNative);
            commands.Add("setweather", SetWeather);
            commands.Add("settime", SetTime);
            commands.Add("kick", KickClient);
            commands.Add("getpos", GetPosition);
            commands.Add("getposition", GetPosition);
            commands.Add("help", ShowHelp);
            commands.Add("?", ShowHelp);
            serverVars = new ServerVarCollection<int>(
                new ServerVar<int>("sv_maxplayers", maxPlayers),
                new ServerVar<int>("sv_maxping", maxPing),
                new ServerVar<int>("sv_tickrate", 10),
                new ServerVar<int>("sv_mtrpingrate", 10000));
        }

        public SessionServer(int sessionID) : this(sessionID, 12, 999, DateTime.Now, WeatherType.Clear)
        { }

        /// <summary>
        /// Start listening for client requests asynchronously.
        /// </summary>
        internal void StartListening()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            server.Start();

            server.RegisterReceivedCallback(new SendOrPostCallback(OnReceive), SynchronizationContext.Current);

            Console.WriteLine("[INIT] Listening for clients\n");

            Thread thread = new Thread(() => Main());

            thread.Start();
        }

        int validStates;

        SessionState[] stateBuffer = new SessionState[20];

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
                Server.ScriptManager?.DoTick();

                state.Clients = GetClientStates();

                for (int i = stateBuffer.Length - 1; i > 0; i--)
                    stateBuffer[i] = stateBuffer[i - 1];

                var timeNow = NetworkTime.Now;

                stateBuffer[0] = state;

                validStates = Math.Min(validStates + 1, stateBuffer.Length);

                lock (syncObj)
                {
                    foreach (var client in activeClients)
                    {
                        if (client.AvgPing.Count < MinTimeSamples && 
                            NetworkTime.Now - client.LastSync > TimeSpan.FromMilliseconds(100))
                        {
                            SendSynchronizationRequest(client.Connection);
                            client.LastSync = NetworkTime.Now;
                        }

                        if (client.LastUpd.Ticks <= 0) continue;

                        if ((NetworkTime.Now - client.LastUpd) > TimeSpan.FromMilliseconds(5000))
                        {
                            removalList.Add(client);
                            continue;
                        }

                        state.Timestamp = timeNow.Subtract(client.TimeDiff);

                        if (client.ReceivedStatesCount > 0)
                        {
                            if (client.ReceivedStates[0].MovementFlags.HasFlag(ClientFlags.Dead) && !client.WaitForRespawn)
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

                        server.SendMessage(message, client.Connection, NetDeliveryMethod.UnreliableSequenced);
                    }
                }

                foreach (var client in removalList)
                {
                    threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" timed out.", client.Info.Name)));
                    RaiseSessionEvent(client, EventType.PlayerLogout);
                    RemoveClient(client);
                }

                if (NetworkTime.Now > lastMasterUpdate + TimeSpan.FromMilliseconds(PingInterval))
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

                removalList.Clear();

                Thread.Sleep(TimerInterval);

#if DEBUG
                    Console.WriteLine(((float)sw1.ElapsedMilliseconds / 1000f).ToString());

                    sw1.Reset();
                    sw1.Start();
#endif

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
                case NetIncomingMessageType.ConnectionApproval:
                    message.SenderConnection.Approve();
                    message.ReadByte();
                    HandleSessionCommand(message.SenderConnection, message.ReadSessionCommand());
                    break;
                case NetIncomingMessageType.Data:
                    HandleIncomingDataMessage(message);
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
            DateTime lastReceived;

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
            }
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

            server.SendMessage(msg, client, NetDeliveryMethod.Unreliable);
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

            nativeCallbacks.Add(native.NetID);
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
        internal bool ClientExists(int clientID)
        {
            return activeClients.Select(x => x.Info.UID).Contains(clientID);
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
            return activeClients.Select(x => x.ReceivedStates[0].VehicleState).Where(y => y != null).ToArray();
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        private ClientState[] GetClientStates()
        {
            return activeClients.Select(x => x.ReceivedStates[0]).ToArray();
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

         //   SendGenericAck(sender, msg.NetID);

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
                case CommandType.Login:
                    if (!GetClient(sender, out client))
                    {
                        if (activeClients.Count > serverVars.GetVar<int>("sv_maxplayers")) return;

                        client = new GameClient(sender, new ClientInfo(cmd.UID, cmd.Name));

                        activeClients.Add(client);

                        // notify other clients in the session
                        RaiseSessionEvent(client, EventType.PlayerLogon);   
                    }
                    break;


                case CommandType.Logout:
                    if (GetClient(sender, out client))
                    {
                        threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" left the session.", client.Info.Name)));

                        RemoveClient(client);

                        RaiseSessionEvent(client, EventType.PlayerLogout);

                        Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                        threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" left the session.", cmd.Name, cmd.UID)));
                    }            
                    break;
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
          //  SendGenericAck(sender, wh.NetID);

            GameClient killer, target;
            if (GetClient(sender, out killer))
            {
                var playbackTime = wh.Timestamp.Add(killer.TimeDiff);

                // get the game state from when this bullet was fired.
                var recentStates = stateBuffer.Where(x => x.Timestamp <= playbackTime).FirstOrDefault();

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

        private static bool ValidateSequence(uint s1, uint s2, uint max)
        {
            return (s1 > s2) && (s1 - s2 <= max / 2) || (s2 > s1) && (s2 - s1 > max / 2);
        }

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
        /// Set weather for all players
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        internal string GetPosition(params string[] args)
        {
            var client = activeClients.ElementAt(Convert.ToInt32(args[0]));

            Console.WriteLine("{0}'s position: {1} {2} {3}", client.Info.Name,
                client.ReceivedStates[0].Position.X,
                client.ReceivedStates[0].Position.Y,
                client.ReceivedStates[0].Position.Z);

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

