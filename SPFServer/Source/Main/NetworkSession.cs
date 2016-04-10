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
        public List<GameClient> ActiveClients { get { return gameManager.ActiveClients; } }

        public List<GameVehicle> ActiveVehicles { get { return gameManager.ActiveVehicles; } }

        internal GameManager GameManager {  get { return gameManager; } }

        private NetServer server;

        private GameManager gameManager;

        private WeatherFactory weatherManager;

        private TimeCycle timeManager;

        private readonly int SessionID;

        private Dictionary<string, Func<string[], string>> commands = new Dictionary<string, Func<string[], string>>();

        private readonly CallbackManager<object> callbackHandler = new CallbackManager<object>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private ThreadQueue threadQueue = new ThreadQueue(42);

        private Queue<Tuple<GameClient, NativeCall>> nativeSendQueue = 
            new Queue<Tuple<GameClient, NativeCall>>();

        private byte[] byteData = new byte[1024];

        private DateTime lastMasterUpdate = new DateTime();

        private DateTime lastNativeCall;

        private NetIncomingMessage message;

        private readonly NetPeerConfiguration NetConfig;

        private SessionState state = new SessionState();

        private uint sentPackets;

        private ServerVarList<string> serverStrings = new ServerVarList<string>()
        {
            { "sv_hostname", "Default Server Title" }
        };

        private ServerVarList<int> serverVars = new ServerVarList<int>()
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
        /// Ctor
        /// </summary>
        public NetworkSession(int sessionID, DateTime initialTime, WeatherType weatherType)
        {
            SessionID = sessionID;
            NetConfig = new NetPeerConfiguration("spfsession");
            NetConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            NetConfig.Port = 27852;
            server = new NetServer(NetConfig);
            commands.Add("status", GetStatus);
            commands.Add("vstatus", GetVehicleStatus);
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
            commands.Add("getvar", GetVar);
            commands.Add("setvar", SetVar);
            commands.Add("setv", SetVar);
            commands.Add("set", SetVar);
            commands.Add("getstring", GetString);
            commands.Add("help", ShowHelp);
            commands.Add("?", ShowHelp);
            commands.Add("screload", ScriptReload);
            commands.Add("screl", ScriptReload);
            timeManager = new TimeCycle(initialTime);
            weatherManager = new WeatherFactory(weatherType);
            weatherManager.OnServerWeatherChanged += OnServerWeatherChanged;
            gameManager = new GameManager();
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
            server.Start();

            server.RegisterReceivedCallback(new SendOrPostCallback(OnReceive), SynchronizationContext.Current);

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
   
                state.Clients = gameManager.GetClientStates();

                state.Vehicles = gameManager.GetVehicleStates();

                state.Sequence = sentPackets;
                state.Timestamp = timeNow;
      
                gameManager.SaveGameState(state);

                lock (gameManager.SyncObj)
                {
                    for (int i = 0; i < gameManager.ActiveClients.Count; i++)
                    {
                        GameClient client = gameManager.ActiveClients[i];

                        state.Timestamp = timeNow.Subtract(client.TimeDiff);

                        if (client.LastUpd.Ticks > 0 && (timeNow - client.LastUpd).TotalMilliseconds > serverVars["sv_timeout"])
                        {
                            client.Connection.Disconnect("NC_TIMEOUT");

                            gameManager.RemoveClient(client);

                            RaiseSessionEvent(client, EventType.PlayerTimeout);
                            Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                            threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" timed out.\n", client.Info.Name)));
                            continue;
                        }

                        if (client.IdleTimer.Ticks > 0 && (timeNow - client.IdleTimer).TotalMilliseconds > serverVars["sv_idlekick"])
                        {
                            client.Connection.Disconnect("NC_IDLEKICK");

                            gameManager.RemoveClient(client);

                            RaiseSessionEvent(client, EventType.PlayerKicked);
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
                            //  client should execute this until we 
                            //  receive the needed amount of time samples to start syncing.
                            SendSynchronizationRequest(client.Connection);
                            client.LastSync = timeNow;
                            continue;
                        }

                        if ((client.State.MovementFlags.HasFlag(ClientFlags.Dead) ||
                            client.Health < 0) &&
                            !client.Respawning)
                        {
                            //  client sent a ClientFlags.Dead flag, so wait for local
                            //  respawn and set health back to 100
                            client.Respawning = true;

                            threadQueue.AddTask(() =>
                            {
                                Thread.Sleep(4000); //8.7 seconds to allow respawn
                                client.Health = 100;
                                client.Respawning = false;
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

                        var message = server.CreateMessage();

                        message.Write((byte)NetMessage.SessionUpdate);
                        message.Write(state, client.DoNameSync || client.LastSequence <= 0);

                        server.SendMessage(message, client.Connection, NetDeliveryMethod.ReliableUnordered);

                        if (client.DoNameSync) client.DoNameSync = false;
                    }

                    for (int i = 0; i < gameManager.ActiveVehicles.Count; i++)
                    {
                        if (NetworkTime.Now.Subtract(gameManager.ActiveVehicles[i].LastUpd) > 
                            TimeSpan.FromMinutes(4))
                            gameManager.RemoveVehicle(gameManager.ActiveVehicles[i]);
                    }

                    while (nativeSendQueue.Count > 0)
                    {
                        if (lastNativeCall != null &&
                        NetworkTime.Now.Subtract(lastNativeCall) <
                        TimeSpan.FromMilliseconds(10)) break;

                        var nc = nativeSendQueue.Dequeue();

                        var msg = server.CreateMessage();
                        msg.Write((byte)NetMessage.NativeCall);
                        msg.Write(nc.Item2);

                        server.SendMessage(msg, nc.Item1.Connection, NetDeliveryMethod.ReliableSequenced);

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

                Server.ScriptManager?.DoTick();

                sentPackets += 1;
                sentPackets %= uint.MaxValue;

                Thread.Sleep(1000 / serverVars["sv_tickrate"]);
            }
        }

        /// <summary>
        /// Process and handle any data received from the client.
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceive(object state)
        {
            message = server.ReadMessage();

            if (message.MessageType == NetIncomingMessageType.Data)
                HandleIncomingDataMessage(message);

            else if (message.MessageType == NetIncomingMessageType.ConnectionApproval &&
                message.ReadByte() == (byte)NetMessage.LoginRequest)
            {
                HandleLoginRequest(message.SenderConnection, message.ReadLoginRequest());
            }

            else if (message.MessageType == NetIncomingMessageType.WarningMessage ||
                message.MessageType == NetIncomingMessageType.ErrorMessage)
                Console.WriteLine(message.ReadString());
        }

        private void HandleIncomingDataMessage(NetIncomingMessage message)
        {
            if (message.LengthBits < 8) return;

            var dataType = (NetMessage)message.ReadByte();

            if (dataType == NetMessage.ClientState)
            {
                HandleClientUpdate(message.SenderConnection,
                    message.ReadUInt32(), message.ReadClientState());
            }

            else if (dataType == NetMessage.VehicleState)
            {
                HandleClientUpdate(message.SenderConnection,
                    message.ReadUInt32(), message.ReadClientState(), message.ReadVehicleState());
            }

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

        private void OnServerWeatherChanged(WeatherType lastWeather, WeatherType newWeather)
        {
            foreach (var cl in gameManager.ActiveClients)
            {
                NativeFunctions.SetWeatherTypeOverTime(cl, newWeather, 60f);
            }

            Console.WriteLine("Weather Changing... Previous: " + lastWeather.ToString() + " New: " + newWeather.ToString() + "\n");
        }

        /// <summary>
        /// Broadcast a server wide message.
        /// </summary>
        /// <param name="message"></param>
        public void Say(string message)
        {
            if (message.Length <= 0 || message.Length >= 100) return;

            SessionMessage sMessage = new SessionMessage();
            sMessage.SenderName = "Server";
            sMessage.Timestamp = NetworkTime.Now;
            sMessage.Message = message;

            foreach (var client in gameManager.ActiveClients)
            {
                var msg = server.CreateMessage();
                msg.Write((byte)NetMessage.SessionMessage);
                msg.Write(sMessage);
                server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableOrdered);
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
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client">Target client</param>
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
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client">Target client</param>
        internal void SendRankData(NetConnection client, int rankIndex, int rankXP, int newXP)
        {
            var rData = new RankData();
            rData.RankIndex = rankIndex;
            rData.RankXP = rankXP;
            rData.NewXP = newXP;

            var msg = server.CreateMessage();
            msg.Write((byte)NetMessage.RankData);
            msg.Write(rData);
            server.SendMessage(msg, client, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Raise an event for the specified client.
        /// </summary>
        /// <param name="client">Target client.</param>
        /// <param name="type">Type of message.</param>
        internal void RaiseSessionEvent(GameClient client, EventType type)
        {
            ClientEvent sEvent = new ClientEvent();
            sEvent.ID = client.Info.UID;
            sEvent.SenderName = client.Info.Name;
            sEvent.EventType = type;

            foreach (var cl in gameManager.ActiveClients)
            {
                var msg = server.CreateMessage();
                msg.Write((byte)NetMessage.SessionEvent);
                msg.Write(sEvent);
                server.SendMessage(msg, cl.Connection, NetDeliveryMethod.ReliableOrdered);
            }
        }

        /// <summary>
        /// Invoke a native on the client with return type void.
        /// </summary>
        /// <param name="client">Target client</param>
        /// <param name="func">Native function name or hash</param>
        /// <param name="args">Native arguments</param>
        public void InvokeClientNative(GameClient client, string func, params NativeArg[] args)
        {
            if (nativeSendQueue.Count > 20) return;
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            nativeSendQueue.Enqueue(new Tuple<GameClient, NativeCall>(client, native));
        }

        /// <summary>
        /// Invoke a native on the client with return type.
        /// </summary>
        /// <typeparam name="T">Native function return type</typeparam>
        /// <param name="client">Target client</param>
        /// <param name="func">Native function name or hash</param>
        /// <param name="args">Native arguments</param>
        public void InvokeClientNative<T>(GameClient client, string func, ReturnedResult<object> callback, params NativeArg[] args)
        {
            if (nativeSendQueue.Count > 20) return;
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);
            nativeSendQueue.Enqueue(new Tuple<GameClient, NativeCall>(client, native));
            callbackHandler.AddCallback(native.NetID, callback);
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

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                Server.ScriptManager.DoMessageReceived(client, message.Message);

                if (message.Message[0] == '/')
                    return;

                foreach (var cl in gameManager.ActiveClients)
                {
                    if (cl.Connection == sender) continue;
                    var msg = server.CreateMessage();
                    msg.Write((byte)NetMessage.SessionMessage);
                    msg.Write(message);
                    server.SendMessage(msg, cl.Connection, NetDeliveryMethod.ReliableOrdered);
                }

                Server.WriteToConsole(string.Format("{0}: {1} [{2}]", client.Info.Name, message.Message, NetworkTime.Now.ToString()));
            }

            else
            {
                Server.WriteToConsole("Cannot send a message, the sender doesn't exist.");
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
            GameClient client;

            switch (cmd.Command)
            {
                case CommandType.Logout:

                    if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
                    {
                        gameManager.RemoveClient(client);

                        RaiseSessionEvent(client, EventType.PlayerLogout);

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

                RaiseSessionEvent(client, EventType.PlayerLogout);

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
                    timeManager.CurrentTime.ToShortTimeString())));

                    gameManager.ActiveClients.ForEach(x => x.DoNameSync = true);

                    Server.ScriptManager.DoClientConnect(client, NetworkTime.Now);
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

                if (client.TimeDiff == null)
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
                        // setup the remote client and let them know that we are ready to start syncing positions
                        // The client will start sending position updates to the server when this is called.
                        RaiseSessionEvent(client, EventType.PlayerSynced);

                        NativeFunctions.SetClock(client, timeManager.CurrentTime.Hour,
                            timeManager.CurrentTime.Minute,
                            timeManager.CurrentTime.Second);

                        NativeFunctions.SetWeatherTypeNow(client, weatherManager.CurrentWeather);                          
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

                    SendRankData(killer.Connection, Ranks.RankTables.GetRankIndex(currentXP), currentXP, xpToGrant);

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
                }
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

        #endregion

        /// <summary>
        /// Set server weather for all clients.
        /// </summary>
        /// <param name="weather"></param>
        public void SetWeather(WeatherType weather)
        {
            weatherManager.SetWeatherType(weather);

            foreach (var cl in gameManager.ActiveClients)
            {
                NativeFunctions.SetWeatherTypeNow(cl, weather);
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

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="time">Time to set.</param>
        public void SetTime(DateTime time)
        {
            timeManager.CurrentTime = time;

            foreach (var cl in gameManager.ActiveClients)
            {
                InvokeClientNative(cl, "NETWORK_OVERRIDE_CLOCK_TIME", time.Hour, time.Minute, time.Second);
            }
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

                RaiseSessionEvent(client, EventType.PlayerKicked);

                Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                Server.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Info.Name));
                return true;
            }

            else return false;
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

        internal string GetVar(params string[] args)
        {
            string varName = args[0];

            int value = 0;

            if (serverVars.TryGetValue(varName, out value))
            {
                Console.WriteLine("{0} = {1}", varName, value);
            }

            else Console.WriteLine("Specified var was not found.");

            return null;
        }

        internal string GetString(params string[] args)
        {
            string stringName = args[0];

            string value = "";

            if (serverStrings.TryGetValue(stringName, out value))
            {
                Console.WriteLine("{0} = {1}", stringName, value);
            }

            else Console.WriteLine("Specified string was not found.");

            return null;
        }

        internal string SetVar(params string[] args)
        {
            string varName = args[0];

            int value = 0;

            if (int.TryParse(args[1], out value))
            {
                if (serverVars.ContainsKey(varName))
                {
                    serverVars[varName] = value;
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
        internal string InvokeNative(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            if (client != null)
            {
                string funcName = args[1];

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

            WeatherType weatherType;

            if (Enum.TryParse(weatherArgs, true, out weatherType))
            {
                foreach (var cl in gameManager.ActiveClients)
                {
                    InvokeClientNative(cl, "SET_OVERRIDE_WEATHER", weatherArgs);
                }

                weatherManager.SetWeatherType(weatherType);
            }

            return null;
        }

        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal string KickClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            if (client.Connection != null)
            {
                client.Connection.Disconnect("NC_GENERICKICK");

                gameManager.ActiveClients.Remove(client);

                RaiseSessionEvent(client, EventType.PlayerKicked);

                Server.ScriptManager.DoClientDisconnect(client, NetworkTime.Now);

                Server.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Info.Name));
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

            timeManager.CurrentTime = new DateTime();
            timeManager.CurrentTime += new TimeSpan(hours, minutes, seconds);

            foreach (var cl in gameManager.ActiveClients)
            {
                NativeFunctions.SetClock(cl, hours, minutes, seconds);
            }

            return null;
        }

        internal string TeleportClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            var posX = Convert.ToSingle(args[1]);
            var posY = Convert.ToSingle(args[2]);
            var posZ = Convert.ToSingle(args[3]);

            NativeFunctions.SetPosition(client, new Vector3(posX, posY, posZ));

            return null;
        }

        internal string TeleportToClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            var client1 = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[1]));

            NativeFunctions.SetPosition(client, client1);

            return null;
        }

        internal string GetPosition(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            Console.WriteLine("{0}'s position: {1} {2} {3}", client.Info.Name,
                client.State.Position.X,
                client.State.Position.Y,
                client.State.Position.Z);

            return null;
        }

        /// <summary>
        /// Get a list of active clients and print it to the console.
        /// </summary>
        internal string GetStatus(params string[] args)
        {
            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 10)));
            var builder = new System.Text.StringBuilder("Active Clients:");
            for (int i = 0; i < gameManager.ActiveClients.Count; i++)
                builder.AppendFormat("\nID: {0} | UID: {1} Name: {2} Health: {3} Ping: {4}ms",
                    i,
                    gameManager.ActiveClients[i].Info.UID,
                    gameManager.ActiveClients[i].Info.Name,
                    gameManager.ActiveClients[i].Health,
                    gameManager.ActiveClients[i].Ping.TotalMilliseconds);

            builder.AppendLine();

            Console.Write(builder.ToString());

            Console.WriteLine(string.Concat(Enumerable.Repeat('-', 15)));

            return null;
        }

        /// <summary>
        /// Get a list of active vehicles and print it to the console.
        /// </summary>
        internal string GetVehicleStatus(params string[] args)
        {
            var states = gameManager.GetVehicleStates();

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

        internal string ScriptReload(params string[] args)
        {
            Server.ScriptManager.Reload();
            return null;
        }

        #endregion
    }
}

