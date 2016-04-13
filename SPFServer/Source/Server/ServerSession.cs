//#define DEBUG
using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using SPFLib;
using SPFLib.Types;
using SPFLib.Enums;
using SPFServer.Enums;
using SPFServer.Types;
using SPFServer.WCF;
using Lidgren.Network;

namespace SPFServer.Main
{
    public sealed class NetworkSession
    {
        public readonly int SessionID;

        public List<GameClient> ActiveClients { get { return gameManager.ActiveClients; } }

        public List<GameVehicle> ActiveVehicles { get { return gameManager.ActiveVehicles; } }

        internal ServerManager GameManager { get { return gameManager; } }

        private Dictionary<string, Func<string[], string>> commands = new Dictionary<string, Func<string[], string>>();

        private readonly CallbackManager<object> callbackHandler = new CallbackManager<object>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private ThreadQueue threadQueue = new ThreadQueue(42);

        private ServerManager gameManager;

        private ServerWeather serverWeather;
        private ServerTime serverTime;

        private ServerSocket serverSocket;

        private Queue<Tuple<GameClient, NativeCall>> nativeQueue =
            new Queue<Tuple<GameClient, NativeCall>>();

        public ServerVarCollection<int> ServerVars
        {
            get { return serverVars; }
        }

        public ServerVarCollection<string> ServerStrings
        {
            get { return serverStrings; }
        }

        public DateTime CurrentTime
        {
            get { return serverTime.CurrentTime; }
            set { serverTime.SetTime(TimeSpan.FromTicks(value.Ticks)); }
        }

        public WeatherType CurrentWeather
        {
            get { return serverWeather.CurrentWeather; }
            set { serverWeather.SetWeather(value); }
        }

        private DateTime lastMasterUpdate = new DateTime();

        private DateTime lastNativeCall;

        private uint sentPackets;
        /// <summary>
        /// Ctor
        /// </summary>
        public NetworkSession(int sessionID, DateTime initialTime, WeatherType weatherType)
        {
            SessionID = sessionID;
            serverSocket = new ServerSocket();
            serverSocket.OnMessageReceived += OnMessageReceived;
            commands.Add("status", ServerCommands.GetStatus);
            commands.Add("vstatus", ServerCommands.GetVehicleStatus);
            commands.Add("invoke", ServerCommands.InvokeNative);
            commands.Add("setweather", ServerCommands.SetWeather);
            commands.Add("settime", ServerCommands.SetTime);
            commands.Add("kick", ServerCommands.KickClient);
            commands.Add("getpos", ServerCommands.GetPosition);
            commands.Add("getposition", ServerCommands.GetPosition);
            commands.Add("tp", ServerCommands.TeleportClient);
            commands.Add("teleport", ServerCommands.TeleportClient);
            commands.Add("tp2", ServerCommands.TeleportToClient);
            commands.Add("tpto", ServerCommands.TeleportToClient);
            commands.Add("getvar", ServerCommands.GetVar);
            commands.Add("setvar", ServerCommands.SetVar);
            commands.Add("setv", ServerCommands.SetVar);
            commands.Add("set", ServerCommands.SetVar);
            commands.Add("getstring", ServerCommands.GetString);
            commands.Add("help", ServerCommands.ShowHelp);
            commands.Add("?", ServerCommands.ShowHelp);
            commands.Add("screload", ServerCommands.ScriptReload);
            commands.Add("screl", ServerCommands.ScriptReload);
            serverTime = new ServerTime(initialTime);
            serverTime.OnTimeChanged += OnTimeChanged;
            serverWeather = new ServerWeather(weatherType);
            serverWeather.OnWeatherChanged += OnWeatherChanged;
            gameManager = new ServerManager();
            Config.GetOverrideVarsInt(AppDomain.CurrentDomain.BaseDirectory + "server.cfg", ref serverVars);
            Config.GetOverrideVarsString(AppDomain.CurrentDomain.BaseDirectory + "server.cfg", ref serverStrings);
        }

        public NetworkSession(int sessionID) : this(sessionID, DateTime.Now, WeatherType.Clear)
        { }

        /// <summary>
        /// Start listening for client requests.
        /// </summary>
        internal void StartListening()
        {
            serverSocket.Start();

            Console.WriteLine("[INIT] Listening for clients\n");

            new Thread(() => Main()).Start();
        }

        internal void SendSessionUpdate(SessionUpdate sessionInfo)
        {
            try
            {
                Server.NetworkService.SendHeartbeat(sessionInfo);
            }

            catch (Exception)
            {
                threadQueue.AddTask(() =>
                Server.WriteToConsole(string.Format("Failed while sending master server heartbeat.")));
            }
        }

        /// <summary>
        /// Main tick event. Handles user session updates, sent at the rate defined by var SV_TickRate
        /// </summary>
        /// <param name="data"></param>
        private void Main()
        {
            while (true)
            {
                var timeNow = NetworkTime.Now;

                SessionState state = new SessionState(sentPackets, timeNow);

                lock (gameManager.SyncObj)
                {
                    for (int i = 0; i < gameManager.ActiveClients.Count; i++)
                    {
                        GameClient client = gameManager.ActiveClients[i];

                        state.LocalHealth = client.Health;

                        //only get the other clients.
                        state.Vehicles = (client.State.InVehicle && client.State.VehicleSeat != VehicleSeat.Driver) ? 
                            gameManager.GetVehicleStates(false) : gameManager.GetVehicleStates()
                            .Where(x => x.ID != client.State.VehicleID).ToArray();

                        state.Clients = gameManager.GetClientStates(false).Where(x => x.ClientID != client.Info.UID).ToArray();

                        state.Timestamp = timeNow.Subtract(client.TimeDiff);

                        if (client.LastUpd.Ticks > 0 && (timeNow - client.LastUpd).TotalMilliseconds > serverVars["sv_timeout"])
                        {
                            client.Connection.Disconnect("NC_TIMEOUT");

                            gameManager.RemoveClient(client);

                            RaiseSessionEvent(client, SessionEventType.PlayerTimeout);

                            Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                            threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" timed out.\n", client.Info.Name)));
                            continue;
                        }

                        if (client.IdleTimer.Ticks > 0 && (timeNow - client.IdleTimer).TotalMilliseconds > serverVars["sv_idlekick"])
                        {
                            client.Connection.Disconnect("NC_IDLEKICK");

                            gameManager.RemoveClient(client);

                            RaiseSessionEvent(client, SessionEventType.PlayerKicked);
                            Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);
                            threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" kicked for idle.\n", client.Info.Name)));
                            continue;
                        }

                        //  if the client just joined send a full update of 
                        //  client states and vehicles.
                        if (client.LastSequence <= 0)
                        {        
                            state.Clients = gameManager.GetClientStates(false);
                            state.Vehicles = gameManager.GetVehicleStates(false);
                        }

                        if (client.AvgPing.Count < serverVars["sv_mintimesamples"] && timeNow - client.LastSync > TimeSpan.FromMilliseconds(100))
                        {
                            // client should execute this until we 
                            // receive the needed amount of time samples to start syncing.
                            SendSynchronizationRequest(client.Connection);
                            client.LastSync = timeNow;
                            continue;
                        }

                        if ((client.State.MovementFlags.HasFlag(ClientFlags.Dead)) &&
                            !client.Respawning)
                        {
                            client.Respawning = true;

                            threadQueue.AddTask(() =>
                            {
                                var dt = NetworkTime.Now + TimeSpan.FromMilliseconds(4300);

                                while (NetworkTime.Now < dt)
                                {
                                    Thread.Sleep(0);
                                }

                                client.Health = 100;
                                client.Respawning = false;
                                Console.WriteLine("set health");

                            });
                        }

                        if ((client.State.ActiveTask == ActiveTask.Idle ||
                            client.State.ActiveTask == ActiveTask.IdleCrouch))
                        {
                            if (!client.WaitForKick)
                            {
                                client.IdleTimer = NetworkTime.Now;
                                client.WaitForKick = true;
                            }
                        }

                        else
                        {
                            if (client.WaitForKick)
                            {
                                client.WaitForKick = false;
                                client.IdleTimer = default(DateTime);
                            }
                        }

                        var message = serverSocket.CreateMessage();

                        message.Write(NetMessage.SessionUpdate);
                        message.Write(state, client.DoNameSync || client.LastSequence <= 0);

                        serverSocket.SendUnorderedMessage(message, client.Connection);

                        if (client.DoNameSync) client.DoNameSync = false;
                    }

                    for (int i = 0; i < gameManager.ActiveVehicles.Count; i++)
                    {
                        if (NetworkTime.Now.Subtract(gameManager.ActiveVehicles[i].LastUpd) > 
                            TimeSpan.FromMinutes(4))
                            gameManager.RemoveVehicle(gameManager.ActiveVehicles[i]);
                    }

                    while (nativeQueue.Count > 0)
                    {
                        if (lastNativeCall != null &&
                        NetworkTime.Now.Subtract(lastNativeCall) <
                        TimeSpan.FromMilliseconds(10)) break;

                        var nc = nativeQueue.Dequeue();

                        var message = serverSocket.CreateMessage();
                        message.Write((byte)NetMessage.NativeCall);
                        message.Write(nc.Item2);

                        serverSocket.SendReliableMessage(message, nc.Item1.Connection);

                        nc.Item1.PendingNatives.Add(nc.Item2);

                        lastNativeCall = NetworkTime.Now;
                    }
                }

                // handle master server sync, if needed.
                if (serverVars["sv_broadcast"] > 0 && NetworkTime.Now > lastMasterUpdate +
                    TimeSpan.FromMilliseconds(serverVars["sv_pingrate"]))
                {
                    var maxPlayers = serverVars["sv_maxplayers"];

                    var sessionInfo = new SessionUpdate()
                    {
                        ServerID = SessionID,
                        ClientCount = gameManager.ActiveClients.Count,
                        MaxClients = maxPlayers,
                    };

                    threadQueue.AddTask(() => SendSessionUpdate(sessionInfo));
                    lastMasterUpdate = NetworkTime.Now;
                }

                state.Clients = gameManager.GetClientStates();
                state.Vehicles = gameManager.GetVehicleStates();

                gameManager.SaveGameState(state);

                Server.ScriptManager?.DoTick();

                sentPackets += 1;
                sentPackets %= uint.MaxValue;

                Thread.Sleep(1000 / serverVars["sv_tickrate"]);
            }
        }

        private void OnTimeChanged(DateTime oldTime, DateTime newTime)
        {
            foreach (var client in gameManager.ActiveClients)
            {
                NativeFunctions.SetClock(client, newTime.Hour, newTime.Minute, newTime.Second);
            }
        }

        private void OnWeatherChanged(WeatherType lastWeather, WeatherType newWeather)
        {
            foreach (var client in gameManager.ActiveClients)
            {
                NativeFunctions.SetWeatherTypeOverTime(client, newWeather, 60f);
            }

            Console.WriteLine("Weather Changing... Previous: {0} New: {1}\n", lastWeather, newWeather);
        }

        /// <summary>
        /// Broadcast a server wide message.
        /// </summary>
        /// <param name="msg"></param>
        public void Say(string msg)
        {
            if (msg.Length <= 0 || msg.Length >= 100) return;

            SessionMessage sMessage = new SessionMessage();
            sMessage.SenderName = "Server";
            sMessage.Timestamp = NetworkTime.Now;
            sMessage.Message = msg;

            foreach (var client in gameManager.ActiveClients)
            {
                var message = serverSocket.CreateMessage();
                message.Write((byte)NetMessage.SessionMessage);
                message.Write(sMessage);
                serverSocket.SendReliableMessage(message, client.Connection);
            }
        }

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="time">Time to set.</param>
        public void SetTime(DateTime time)
        {
            serverTime.SetTime(TimeSpan.FromTicks(time.Ticks));

            foreach (var cl in gameManager.ActiveClients)
            {
                InvokeClientFunction(cl, "NETWORK_OVERRIDE_CLOCK_TIME", time.Hour, time.Minute, time.Second);
            }
        }

        /// <summary>
        /// Create a vehicle on the server for all players.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="modelID"></param>
        /// <returns></returns>
        public GameVehicle CreateVehicle(Vector3 position, Quaternion rotation, VehicleHash modelID)
        {
            return gameManager.AddVehicle(SPFLib.Helpers.GenerateUniqueID(), modelID, position, rotation);
        }

        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param client="client to kick.">hours</param>
        public bool KickClient(GameClient client)
        {
            var item = gameManager.ActiveClients.Where(x => x.Info.UID == client.Info.UID).FirstOrDefault();

            if (item != null)
            {
                client.Connection.Disconnect("NC_GENERICKICK");

                gameManager.RemoveClient(client);

                RaiseSessionEvent(client, SessionEventType.PlayerKicked);

                Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                Server.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Info.Name));
                return true;
            }

            else return false;
        }

        /// <summary>
        /// Process and handle any data received from the client.
        /// </summary>
        /// <param name="ar"></param>
        private void OnMessageReceived(NetConnection sender, NetIncomingMessage message)
        {
            if (message.MessageType == NetIncomingMessageType.Data)
            {
                if (message.LengthBits < 8) return;

                var dataType = (NetMessage)message.ReadByte();

                if (dataType == NetMessage.ClientState)
                    HandleClientUpdate(message.SenderConnection, message.ReadUInt32(), message.ReadClientState());

                else if (dataType == NetMessage.VehicleState)
                    HandleClientUpdate(message.SenderConnection, message.ReadUInt32(), message.ReadClientState(), message.ReadVehicleState());

                else if (dataType == NetMessage.WeaponData)
                    HandleBulletImpact(message.SenderConnection, message.ReadImpactData());

                else if (dataType == NetMessage.Acknowledgement)
                    HandleClientAck(message.SenderConnection, message.ReadSessionAck());

                else if (dataType == NetMessage.SessionMessage)
                    HandleChatMessage(message.SenderConnection, message.ReadSessionMessage());

                else if (dataType == NetMessage.SessionCommand)
                    HandleSessionCommand(message.SenderConnection, message.ReadSessionCommand());

                else if (dataType == NetMessage.SessionSync)
                    HandleSessionSync(message.SenderConnection, message.ReadSessionSync());

                else if (dataType == NetMessage.NativeCallback)
                    HandleNativeCallback(message.SenderConnection, message.ReadNativeCallback());
            }

            else if (message.MessageType == NetIncomingMessageType.ConnectionApproval &&
                message.ReadByte() == (byte)NetMessage.LoginRequest)
            {
                HandleLoginRequest(message.SenderConnection, message.ReadLoginRequest());
            }

            else if (message.MessageType == NetIncomingMessageType.WarningMessage ||
                message.MessageType == NetIncomingMessageType.ErrorMessage)
                Console.WriteLine(message.ReadString());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleLoginRequest(NetConnection sender, LoginRequest req)
        {
            if (StressMitigationCheck(sender, 5000))
            {
                sender.Deny("NC_TOOFREQUENT");
                return;
            }

            if (req.Revision != serverVars["revision"])
            {
                sender.Deny("NC_REVMISMATCH");
                return;
            }

            if (gameManager.ActiveClients.Count > serverVars["sv_maxplayers"])
            {
                sender.Deny("NC_LOBBYFULL");
                return;
            }

            if (req == null || req.Username == null || req.UID == 0)
            {
                sender.Deny("NC_INVALID");
                return;
            }

            GameClient client;

            if (gameManager.ClientFromID(req.UID, out client))
            {
                gameManager.RemoveClient(client);

                RaiseSessionEvent(client, SessionEventType.PlayerLogout);

                Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);
            }

            gameManager.AddClient(sender, req.UID, req.Username);

            if (!Server.NetworkService.UserExists(req.UID))
            {
                Server.NetworkService.CreateUser(req.UID, req.Username);
            }

            sender.Approve();
        }

        /// <summary>
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client">Target client</param>
        internal void SendSynchronizationRequest(NetConnection client)
        {
            var req = new SessionSync();
            req.ServerTime = NetworkTime.Now;

            var message = serverSocket.CreateMessage();
            message.Write((byte)NetMessage.SessionSync);
            message.Write(req.ServerTime.Ticks);
            message.Write(req.ClientTime.Ticks);

            serverSocket.SendUnorderedMessage(message, client);
        }

        /// <summary>
        /// Invoke a native on the client with return type.
        /// </summary>
        /// <typeparam name="T">Native function return type</typeparam>
        /// <param name="client">Target client</param>
        /// <param name="func">Native function name or hash</param>
        /// <param name="args">Native arguments</param>
        public void InvokeClientFunction<T>(GameClient client, string func, ReturnedResult<object> callback, params NativeArg[] args)
        {
            if (nativeQueue.Count > 20) return;
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);
            nativeQueue.Enqueue(new Tuple<GameClient, NativeCall>(client, native));
            callbackHandler.AddCallback(native.NetID, callback);
        }

        /// <summary>
        /// Invoke a native on the client with return type void.
        /// </summary>
        /// <param name="client">Target client</param>
        /// <param name="func">Native function name or hash</param>
        /// <param name="args">Native arguments</param>
        public void InvokeClientFunction(GameClient client, string func, params NativeArg[] args)
        {
            if (nativeQueue.Count > 20) return;
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            nativeQueue.Enqueue(new Tuple<GameClient, NativeCall>(client, native));
        }

        /// <summary>
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client">Target client</param>
        internal void SendRankData(NetConnection client, int rankIndex, int rankXP, int newXP)
        {
            var rData = new RankData();
            rData.RankIndex = rankIndex;
            rData.RankXP = rankXP;
            rData.NewXP = newXP;
            var message = serverSocket.CreateMessage();
            message.Write((byte)NetMessage.RankData);
            message.Write(rData);
            serverSocket.SendReliableMessage(message, client);
        }

        /// <summary>
        /// Raise a server wide event for the specified client.
        /// </summary>
        /// <param name="client">Target client.</param>
        /// <param name="type">Type of message.</param>
        internal void RaiseSessionEvent(GameClient client, SessionEventType type)
        {
            SessionEvent sEvent = new SessionEvent();
            sEvent.ID = client.Info.UID;
            sEvent.SenderName = client.Info.Name;
            sEvent.EventType = type;

            foreach (var cl in gameManager.ActiveClients)
            {
                var message = serverSocket.CreateMessage();
                message.Write((byte)NetMessage.SessionEvent);
                message.Write(sEvent);
                serverSocket.SendReliableMessage(message, cl.Connection);
            }
        }

        /// <summary>
        /// Handle a chat message sent by the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="msg"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleChatMessage(NetConnection sender, SessionMessage msg)
        {
            if (StressMitigationCheck(sender, 1500)) return;

            GameClient client;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                Server.ScriptManager.DoMessageReceived(client, msg.Message);

                if (msg.Message[0] == '/')
                    return;

                foreach (var cl in gameManager.ActiveClients)
                {
                    if (cl.Connection == sender) continue;
                    var message = serverSocket.CreateMessage();
                    message.Write((byte)NetMessage.SessionMessage);
                    message.Write(message);
                    serverSocket.SendReliableMessage(message, cl.Connection);
                }

                Server.WriteToConsole(string.Format("{0}: {1} [{2}]", client.Info.Name, msg.Message, NetworkTime.Now.ToString()));
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
        private void HandleClientUpdate(NetConnection sender, uint sequence, ClientState state)
        {
            gameManager.HandleClientUpdate(sender, sequence, state, null);
        }

        /// <summary>
        /// Handle a state and vehicle update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientUpdate(NetConnection sender, uint sequence, ClientState state, VehicleState vehicle)
        {
            gameManager.HandleClientUpdate(sender, sequence, state, vehicle);
        }

        /// <summary>
        /// Handle commands invoked by the client. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cmd"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleSessionCommand(NetConnection sender, SessionCommand cmd)
        {
            GameClient client;

            switch (cmd.Command)
            {
                case CommandType.Logout:

                    if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
                    {
                        gameManager.RemoveClient(client);

                        RaiseSessionEvent(client, SessionEventType.PlayerLogout);

                        Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);
                    }
                    break;
                case CommandType.GetClientNames:

                    if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
                    {
             
                        client.DoNameSync = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientAck(NetConnection sender, SessionAck ack)
        {
            GameClient client;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                if (ack.Type == AckType.WorldSync)
                {
                    threadQueue.AddTask(() =>
                    Server.WriteToConsole(string.Format("User \"{0}\" joined the session with user ID \'{1}\'. Client IP Address: {2}\nIn- game time: {3}",
                    client.Info.Name, client.Info.UID,
                    (sender.RemoteEndPoint).Address.ToString(),
                    serverTime.CurrentTime.ToShortTimeString())));

                    gameManager.ActiveClients.ForEach(x => x.DoNameSync = true);

                    Server.ScriptManager.DoClientConnect(client, NetworkTime.Now);
                }

                else if (ack.Type == AckType.PlayerSpawn)
                {
                    client.Respawning = false;
                    Console.WriteLine("got ack");
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

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                // total time for the roundtrip (server > client > server)
                client.Ping = (currentTime - req.ServerTime);
                // clock offset based on local client time and half the ping time.
                var timeDiff = (currentTime - req.ClientTime) - TimeSpan.FromMilliseconds(client.Ping.TotalMilliseconds / 2);

                if (ReferenceEquals(client.TimeDiff, null))
                {
                    client.TimeDiff = timeDiff;
                }

                client.AvgPing.Add(timeDiff);

                var minSamples = serverVars["sv_mintimesamples"];

                if (client.AvgPing.Count >= minSamples)
                {
                    client.TimeDiff = Helpers.CalculateAverage(ref client.AvgPing, minSamples);

                    if (client.TimeDiff.TotalMilliseconds > 10000)
                    {
                        threadQueue.AddTask(() => Server.WriteToConsole(
                            string.Format("Unusually bad ping result. (Client time = {0}, Server time = {1}) retrying...",
                            req.ClientTime,
                            req.ServerTime)));

                        client.AvgPing.Clear();
                    }

                    else
                    {
                        RaiseSessionEvent(client, SessionEventType.PlayerSynced);

                        NativeFunctions.SetClock(client, serverTime.CurrentTime.Hour,
                            serverTime.CurrentTime.Minute,
                            serverTime.CurrentTime.Second);

                        NativeFunctions.SetWeatherTypeNow(client, serverWeather.CurrentWeather);                          
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
        private void HandleBulletImpact(NetConnection sender, ImpactData wh)
        {
            GameClient killer;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out killer))
            {
                GameClient clientTarget;
                bool wasKilled = false;

                if (gameManager.VerifyClientImpact(killer, wh.HitCoords, wh.Timestamp, wh.WeaponDamage, 
                    wh.TargetID, out clientTarget, out wasKilled) && wasKilled)
                {
                    Say(string.Format("<font size =\"12\"><b>{0}</b></size></font> killed <font size=\"12\"><b>{1}</b></font>", 
                        killer.Info.Name, clientTarget.Info.Name));

                    var xpToGrant = serverVars["scr_kill_xp"] * serverVars["scr_xpscale"];
                    var currentXP = Server.NetworkService.GetPlayerStat(killer.Info.UID, "totalExp");

                    SendRankData(killer.Connection, ServerRanks.GetRankIndex(currentXP), currentXP, xpToGrant);

                    Server.NetworkService.UpdatePlayerExp(killer.Info.UID, xpToGrant);
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
            GameClient client; NativeCall native;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                if ((native = client.PendingNatives.Find(x => x.NetID == cb.NetID)) != null)
                {
                    callbackHandler.InvokeCallbackByID(cb.NetID, cb.Value);
                    client.PendingNatives.Remove(native);

                    //Console.WriteLine("Client " + client.Info.Name + " executed " + native.FunctionName + " successfully.");
                    
                    if (cb.Type != DataType.None)
                    {
                        Console.WriteLine("Value returned: " + cb.Value.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether the client is sending messages too frequently
        /// </summary>
        /// <param name="sender">The connection sending the messages.</param>
        /// <param name="msInterval">Minimum allowed message interval to trigger the check.</param>
        /// <returns></returns>
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

        private ServerVarCollection<string> serverStrings = new ServerVarCollection<string>()
        {
            { "sv_hostname", "Default Server Title" }
        };

        private ServerVarCollection<int> serverVars = new ServerVarCollection<int>()
        {
            { "revision", Server.RevisionNumber},
            { "sv_maxplayers", 12 },
            { "sv_maxping", 999 },
            { "sv_tickrate", 12 },
            { "sv_pingrate", 10000 },
            { "sv_timeout", 6800 },
            { "sv_idlekick", 300000 },
            { "sv_broadcast", 1 },
            { "sv_mintimesamples", 5 },
            { "scr_kill_xp", 100 },
            { "scr_death_xp", 0 },
            { "scr_xpscale", 1 },
        };

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
    }
}

