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
using SPFServer.Types;
using SPFServer.WCF;
using SPFServer.Natives;
using Lidgren.Network;

namespace SPFServer.Main
{
    public sealed class SessionServer
    {
        public WeatherManager WeatherManager { get { return weatherManager; } }

        public GameClient[] ActiveClients { get { return gameManager.ActiveClients.ToArray(); } }

        public AIClient[] ActiveAI { get { return gameManager.ActiveAI.ToArray(); } }

        private NetServer server;

        private GameStateManager gameManager;

        private WeatherManager weatherManager;

        private TimeCycleManager timeManager;

        private readonly int SessionID;

        private Dictionary<string, Func<string[], string>> commands = new Dictionary<string, Func<string[], string>>();

        private List<GameClient> removalList = new List<GameClient>();
        private List<AIClient> aiRemovalList = new List<AIClient>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private ThreadQueue threadQueue = new ThreadQueue(42);

        private byte[] byteData = new byte[1024];

        private DateTime lastMasterUpdate = new DateTime();

        private readonly NetPeerConfiguration NetConfig;

        private SessionState state = new SessionState();

        private uint tickCount;

        private ServerVarCollection<string> serverStrings = new ServerVarCollection<string>()
        {
            { "sv_hostname", "Default Server Title" }
        };

        private ServerVarCollection<int> serverVars = new ServerVarCollection<int>()
        {
            { "sv_maxplayers", 12 },
            { "sv_maxping", 999 },
            { "sv_tickrate", 12 },
            { "sv_pingrate", 10000 },
            { "sv_cltimeout", 2000 },
            { "sv_mintimesamples", 5 },
            { "scr_kill_xp", 100 },
            { "scr_ai_xp", 50 },
            { "scr_death_xp", 0 },
            { "scr_xpscale", 1 },
        };

        /// <summary>
        /// Ctor
        /// </summary>
        public SessionServer(int sessionID, DateTime initialTime, WeatherType weatherType)
        {
            SessionID = sessionID;
            NetConfig = new NetPeerConfiguration("spfsession");
            NetConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            NetConfig.Port = 27852;
            server = new NetServer(NetConfig);
            commands.Add("status", GetStatus);
            commands.Add("vstatus", GetVehicleStatus);
            commands.Add("aistatus", GetAIStatus);
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
            commands.Add("getvar", GetVar);
            commands.Add("setvar", SetVar);
            commands.Add("setv", SetVar);
            commands.Add("set", SetVar);
            commands.Add("getstring", GetString);
            commands.Add("help", ShowHelp);
            commands.Add("?", ShowHelp);
            commands.Add("screload", ScriptReload);
            timeManager = new TimeCycleManager(initialTime);
            weatherManager = new WeatherManager(weatherType);
            weatherManager.OnServerWeatherChanged += OnServerWeatherChanged;
            gameManager = new GameStateManager();
            Config.GetOverrideVarsInt(AppDomain.CurrentDomain.BaseDirectory + "server.cfg", ref serverVars);
            Config.GetOverrideVarsString(AppDomain.CurrentDomain.BaseDirectory + "server.cfg", ref serverStrings);
        }

        public SessionServer(int sessionID) : this(sessionID, DateTime.Now, WeatherType.Clear)
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
                // increment tick count for packet sequencing.
                tickCount += 1;
                tickCount %= uint.MaxValue;

                var timeNow = NetworkTime.Now;

                var aiHost = gameManager.GetActiveAIHost();

                state.Sequence = tickCount;
                state.Clients = gameManager.GetClientStates();
                state.Timestamp = timeNow;

                gameManager.SaveGameState(state);

                lock (gameManager.SyncObj)
                {
                    for (int i = 0; i < gameManager.ActiveClients.Count; i++)
                    {
                        GameClient client = gameManager.ActiveClients[i];

                        // send a full AI update to everyone but the client hosting the AI.
                        // also send an update if the client has just joined the server.
                        if (client != aiHost || client.LastSequence <= 0)
                        {
                            state.AI = gameManager.GetAIStates();
                        }

                        // let the client know if he is hosting the server AI.
                        state.AIHost = (client == aiHost);

                        state.Timestamp = timeNow.Subtract(client.TimeDiff);

                        if (client.LastUpd.Ticks > 0 && (timeNow - client.LastUpd).TotalMilliseconds > serverVars["sv_cltimeout"])
                        {
                            // we haven't received an update from this client for > 1200ms, so queue him to be removed.
                            removalList.Add(client);
                            continue;
                        }

                        if (client.AvgPing.Count < serverVars["sv_mintimesamples"] &&  timeNow - client.LastSync > TimeSpan.FromMilliseconds(100))
                        {
                            // Client will continue to execute this until we have received the needed amount of time samples to start syncing.
                            SendSynchronizationRequest(client.Connection);
                            client.LastSync = timeNow;
                            continue;
                        }

                        if (client.State.MovementFlags.HasFlag(ClientFlags.Dead) && !client.WaitForRespawn)
                        {
                            client.WaitForRespawn = true;

                            threadQueue.AddTask(() =>
                            {
                                Thread.Sleep(8900); //8.7 seconds to allow respawn
                                client.Health = 100;
                                client.WaitForRespawn = false;
                            });
                        }

                        var message = server.CreateMessage();
                        message.Write((byte)NetMessage.SessionUpdate);
                        message.Write(state, client.LastSequence <= 0);
                        server.SendMessage(message, client.Connection, NetDeliveryMethod.ReliableUnordered);
                    }

                    for (int i = 0; i < gameManager.ActiveAI.Count; i++)
                    {
                        AIClient client = gameManager.ActiveAI[i];

                        if (client.State.Health < 0)
                        {
                            aiRemovalList.Add(client);
                        }
                    }
                }

                // remove any clients queued for removal and notify all participants they have left the session.
                foreach (var client in removalList)
                {
                    threadQueue.AddTask(() => Server.WriteToConsole(string.Format("User \"{0}\" timed out.\n", client.Info.Name)));
                    RaiseSessionEvent(client, EventType.PlayerLogout);
                    gameManager.RemoveClient(client);
                }

                removalList.Clear();

                // remove any clients queued for removal and notify all participants they have left the session.
                foreach (var client in aiRemovalList)
                {
                    gameManager.RemoveAI(client);
                }

                aiRemovalList.Clear();

                // handle master server sync, if needed.

                if (NetworkTime.Now > lastMasterUpdate +
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

                Thread.Sleep(1000 / serverVars["sv_tickrate"]);
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

            if (message.MessageType == NetIncomingMessageType.Data)
                HandleIncomingDataMessage(message);

            else if (message.MessageType == NetIncomingMessageType.ConnectionApproval &&
                message.ReadByte() == (byte)NetMessage.LoginRequest)
            {
                message.SenderConnection.Approve();
                HandleLoginRequest(message.SenderConnection, message.ReadLoginRequest());
            }

            else if (message.MessageType == NetIncomingMessageType.WarningMessage ||
                message.MessageType == NetIncomingMessageType.ErrorMessage)
                Console.WriteLine(message.ReadString());
        }

        private void HandleIncomingDataMessage(NetIncomingMessage message)
        {
            if (message.LengthBits <= 0) return;

            var dataType = (NetMessage)message.ReadByte();

            if (dataType == NetMessage.ClientState)
                HandleClientStateUpdate(message.SenderConnection, 
                    message.ReadUInt32(), 
                    message.ReadClientState());

            if (dataType == NetMessage.ClientStateAI)
                HandleClientStateUpdate(message.SenderConnection, 
                    message.ReadUInt32(), 
                    message.ReadClientState(), 
                    message.GetAIStates(message.ReadInt32()).ToArray());

            else if (dataType == NetMessage.AckWorldSync)
                HandleClientInit(message.SenderConnection);

            else if (dataType == NetMessage.SessionMessage)
                threadQueue.AddTask(() => HandleChatMessage(message.SenderConnection, message.ReadSessionMessage()));

            else if (dataType == NetMessage.SessionCommand)
                threadQueue.AddTask(() => HandleSessionCommand(message.SenderConnection, message.ReadSessionCommand()));

            else if (dataType == NetMessage.SessionSync)
                threadQueue.AddTask(() => HandleSessionSync(message.SenderConnection, message.ReadSessionSync()));

            else if (dataType == NetMessage.NativeCallback)
                threadQueue.AddTask(() => HandleNativeCallback(message.SenderConnection, message.ReadNativeCallback()));

            else if (dataType == NetMessage.WeaponData)
                threadQueue.AddTask(() => HandleBulletImpact(message.SenderConnection, message.ReadWeaponData()));
        }

        private void OnServerWeatherChanged(WeatherType lastWeather, WeatherType newWeather)
        {
            foreach (var cl in gameManager.ActiveClients)
            {
                NativeFunctions.SetWeatherTypeNow(cl, newWeather);
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

        public AIClient CreateAI(string name, PedType type, Vector3 position, Quaternion rotation)
        {
            return gameManager.AddAI(name, type, position, rotation);
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
            SessionEvent sEvent = new SessionEvent();
            sEvent.SenderID = client.Info.UID;
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
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            var msg = server.CreateMessage();
            msg.Write((byte)NetMessage.NativeCall);
            msg.Write(native);
            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Invoke a native on the client with return type.
        /// </summary>
        /// <typeparam name="T">Native function return type</typeparam>
        /// <param name="client">Target client</param>
        /// <param name="func">Native function name or hash</param>
        /// <param name="args">Native arguments</param>
        public void InvokeClientNative<T>(GameClient client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);

            var msg = server.CreateMessage();
            msg.WriteAllProperties(native);

            server.SendMessage(msg, client.Connection, NetDeliveryMethod.ReliableOrdered);
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

                threadQueue.AddTask(() => Server.WriteToConsole(string.Format("{0}: {1} [{2}]", client.Info.Name, message.Message, NetworkTime.Now.ToString())));
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
            if (StressMitigationCheck(sender, 5000)) return;

            GameClient client;

            switch (cmd.Command)
            {
                case CommandType.Logout:
                    if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
                    {
                        gameManager.RemoveClient(client);

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
                gameManager.ActiveClients.Count > serverVars["sv_maxplayers"] ||
                req == null ||
                req.Username == null ||
                req.UID == 0)
                return;

            GameClient client;

            if (!server.Connections.Contains(sender))
            {
                gameManager.AddClient(sender, req.UID, req.Username);

                // check if the player exists in the master database
                if (!Server.NetworkService.UserExists(req.UID))
                {
                    Server.NetworkService.CreateUser(req.UID, req.Username);
                }
            }

            else
            {
                if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
                {
                    gameManager.RemoveClient(client);
                }
            }
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientInit(NetConnection sender)
        {
            GameClient client;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out client))
            {
                Server.ScriptManager.DoClientConnect(client, NetworkTime.Now);

                threadQueue.AddTask(() =>
                Server.WriteToConsole(string.Format("User \"{0}\" joined the session with user ID \'{1}\'. Client IP Address: {2}\nIn- game time: {3}", 
                client.Info.Name, client.Info.UID, 
                (sender.RemoteEndPoint).Address.ToString(), 
                timeManager.CurrentTime.ToShortTimeString())));
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

                if (client.AvgPing.Count >= serverVars["sv_mintimesamples"])
                {
                    client.TimeDiff = Helpers.CalculateAverage(ref client.AvgPing, serverVars["sv_mintimesamples"]);

                    if (client.TimeDiff.TotalMilliseconds > 10000)
                    {
                        threadQueue.AddTask(() => Server.WriteToConsole("Unusually bad ping result.. retrying: " +
                            req.ClientTime.ToString() + " " +
                            req.ServerTime.ToString()));

                        client.AvgPing.Clear();
                    }

                    else
                    {
                        // here we setup the remote client and let them know that we are ready to start syncing positions.
                        // the client will start sending position updates to the server when this is called.
                        // post-connect tasks are handled in the HandleClientConnect method when the client sends a connection ack.
                        RaiseSessionEvent(client, EventType.PlayerLogon);

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
        private void HandleBulletImpact(NetConnection sender, WeaponData wh)
        {
            GameClient killer, target;

            if (gameManager.ClientFromEndpoint(sender.RemoteEndPoint, out killer))
            {
                var playbackTime = wh.Timestamp.Subtract(killer.TimeDiff);

                // get the game state from when this bullet was fired.
                var gameState = gameManager.FindBufferedState(playbackTime);

                for (int i = 0; i < gameState.Clients.Length; i++)
                {
                    if (gameState.Clients[i].ClientID == killer.Info.UID)
                    {
                        foreach (var client in gameState.Clients)
                        {
                            //  dont want the killer
                            if (client.ClientID == gameState.Clients[i].ClientID)
                                continue;
                            // check distance
                            var dist = client.InVehicle ?
                              client.VehicleState.Position.DistanceTo(wh.HitCoords) :
                              client.Position.DistanceTo(wh.HitCoords);

                            if (dist > 2f) continue;

                            // make sure the target exists
                            if (gameManager.ClientFromID(client.ClientID, out target))
                            {
                                if (target.State.Health < 0) return;

                                // modify health
                                target.Health = (short)Helpers.Clamp((target.Health - wh.WeaponDamage), -1, 100);

                                if (target.Health < 0)
                                {
                                    Say(string.Format("<font size =\"12\"><b>{0}</b></size></font> killed <font size=\"12\"><b>{1}</b></font>",
                                        killer.Info.Name, target.Info.Name));

                                    var xpToGrant = serverVars["scr_kill_xp"] * serverVars["scr_xpscale"];

                                    var currentXP = Server.NetworkService.GetPlayerStat(killer.Info.UID, "totalExp");

                                    SendRankData(killer.Connection, Ranks.RankTables.GetRankIndex(currentXP), currentXP, xpToGrant);

                                    Server.NetworkService.UpdatePlayerExp(killer.Info.UID, xpToGrant);

                                    return;
                                }
                            }
                        }
                    }
                }

                AIClient aiTarget;

                for (int i = 0; i < gameState.AI.Length; i++)
                {
                    if (gameState.AI[i].Position.DistanceTo(wh.HitCoords) < 2f)
                    {
                        // make sure the target exists
                        if (gameManager.AIFromID(gameState.AI[i].ClientID, out aiTarget))
                        {
                            if (aiTarget.State.Health < 0) return;

                            // modify health
                            aiTarget.State.Health = (short)Helpers.Clamp((aiTarget.State.Health - wh.WeaponDamage), -1, 100);

                            if (aiTarget.State.Health < 0)
                            {
                                Say(string.Format("<font size =\"12\"><b>{0}</b></size></font> killed <font size=\"12\"><b>{1}</b></font>",
                                    killer.Info.Name, aiTarget.Name));

                                var xpToGrant = serverVars["scr_ai_xp"] * serverVars["scr_xpscale"];

                                var currentXP = Server.NetworkService.GetPlayerStat(killer.Info.UID, "totalExp");

                                SendRankData(killer.Connection, Ranks.RankTables.GetRankIndex(currentXP), currentXP, xpToGrant);

                                Server.NetworkService.UpdatePlayerExp(killer.Info.UID, xpToGrant);

                                return;
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
            string cMessage = cb.Type == DataType.None ?
                "Executed Successfully." : string.Format("Returned value from native: {0}", cb.Value);
            threadQueue.AddTask(() => Console.WriteLine(cMessage));
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientStateUpdate(NetConnection sender, uint sequence, ClientState state, AIState[] ai)
        {
            gameManager.HandleClientUpdate(sender, sequence, state, ai);
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientStateUpdate(NetConnection sender, uint sequence, ClientState state)
        {
            gameManager.HandleClientUpdate(sender, sequence, state);
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

            var builder = new System.Text.StringBuilder("Active Vehicles:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | Vehicle ID: {1} Flags: {2} Position: {3} {4} {5} Radio Station: {6}", i, states[i].VehicleID, states[i].Flags, states[i].Position.X, states[i].Position.Y, states[i].Position.Z, states[i].RadioStation);

            builder.AppendLine();

            Console.Write(builder.ToString());

            return null;
        }

        /// <summary>
        /// Get a list of active AI and print it to the console.
        /// </summary>
        internal string GetAIStatus(params string[] args)
        {
            var states = gameManager.GetAIStates();

            var builder = new System.Text.StringBuilder("Active AI:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | Name: {1} Type: {2} Health: {3} Position: {4} {5} {6}", i, states[i].Name, states[i].PedType, states[i].Health, states[i].Position.X, states[i].Position.Y, states[i].Position.Z);

            builder.AppendLine();

            Console.Write(builder.ToString());

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
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        internal string KickClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            if (client.Connection != null)
            {
                RaiseSessionEvent(client, EventType.PlayerKicked);

                gameManager.ActiveClients.Remove(client);

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
            var item = gameManager.ActiveClients.Where(x => x.Info.UID == client.Info.UID).FirstOrDefault();

            if (item != null)
            {
                gameManager.RemoveClient(item);
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
        internal string TeleportClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));

            var posX = Convert.ToSingle(args[1]);
            var posY = Convert.ToSingle(args[2]);
            var posZ = Convert.ToSingle(args[3]);

            NativeFunctions.SetPosition(client, new Vector3(posX, posY, posZ));

            return null;
        }

        /// <summary>
        /// Teleport a client.
        /// </summary>
        internal string TeleportToClient(params string[] args)
        {
            var client = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[0]));
            var client1 = gameManager.ActiveClients.ElementAt(Convert.ToInt32(args[1]));

            NativeFunctions.SetPosition(client, client1);

            return null;
        }

        /// <summary>
        /// Get a player position
        /// </summary>
        /// <param name="weather">Weather type as string</param>
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
        /// Force a time sync for all clients.
        /// </summary>
        internal string ForceSync(params string[] args)
        {
            foreach (var client in gameManager.ActiveClients)
            {
                SendSynchronizationRequest(client.Connection);
            }

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

