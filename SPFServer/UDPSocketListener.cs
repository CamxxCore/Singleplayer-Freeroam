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
using SPFServer.Types;
using SPFServer.Weather;
using ASUPService;

namespace SPFServer
{
    public class GameClient
    {
        public ClientInfo Info { get; }
        public ClientState State { get; private set; }
        public TimeSpan Ping { get; set; }
        public TimeSpan TimeDiff { get; set; }
        public List<TimeSpan> AvgPing;
        public int LastUpd { get; set; }
        public int LastSync { get; set; }
        public int LastCMD { get; set; }
        public int LastVehicleUpd { get; set; }
        public bool WaitForRespawn;

        public GameClient(ClientInfo info, EndPoint endpoint)
        {
            Info = info;
            State = new ClientState(info.UID, info.Name);
            AvgPing = new List<TimeSpan>();
        }

        public void UpdateState(ClientState state, int updTime)
        {
            State.Name = null;
            State.Position = state.Position;
            State.Velocity = state.Velocity;
            State.Angles = state.Angles;
            State.Rotation = state.Rotation;
            State.Seat = state.Seat;
            State.MovementFlags = state.MovementFlags;
            State.ActiveTask = state.ActiveTask;
            State.PedID = state.PedID;
            State.WeaponID = state.WeaponID;
            State.InVehicle = state.InVehicle;
            State.VehicleState = state.VehicleState;
            LastUpd = updTime;
        }

        public void SetHealth(int health)
        {
            if (health > short.MaxValue)
                return;

            State.Health = (short)health;
        }
    }

    public class UDPSocketListener
    {
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

        private Dictionary<EndPoint, GameClient> clientList = new Dictionary<EndPoint, GameClient>();

        private Dictionary<EndPoint, GameClient> removalQueue = new Dictionary<EndPoint, GameClient>();

        private Dictionary<EndPoint, DateTime> stressMitigation = new Dictionary<EndPoint, DateTime>();

        private List<int> pendingCallbacks = new List<int>();

        private List<int> nativeCallbacks = new List<int>();

        private ThreadQueue threadQueue = new ThreadQueue(24);

        private object syncObj = new object();
        private byte[] byteData = new byte[1024];

        private int serverTime = 0;
        private int lastMasterUpdate = 0;

        Socket serverSocket;

        Stopwatch sw = new Stopwatch();

        SessionState state = new SessionState();

        /// <summary>
        /// Ctor
        /// </summary>
        public UDPSocketListener(int sessionID, int maxPlayers, int maxPing, DateTime igTime, WeatherType weatherType)
        {
            SessionID = sessionID;
            timeMgr = new TimeCycleManager(igTime);
            weatherMgr = new WeatherManager(weatherType);
            weatherMgr.OnServerWeatherChanged += OnServerWeatherChanged;
            commands.Add("status", GetStatus);
            commands.Add("forcesync", ForceSync);
            commands.Add("invoke", InvokeNative);
            commands.Add("setweather", SetWeather);
            commands.Add("settime", SetTime);
            commands.Add("kick", Kick);
            commands.Add("getpos", GetPosition);
            commands.Add("getposition", GetPosition);
            commands.Add("vstatus", GetVehicleStatus);
            commands.Add("help", ShowHelp);
            commands.Add("?", ShowHelp);
            serverVars = new ServerVarCollection<int>(
                new ServerVar<int>("sv_maxplayers", maxPlayers),
                new ServerVar<int>("sv_maxping", maxPing),
                new ServerVar<int>("sv_tickrate", 10),
                new ServerVar<int>("sv_mtrpingrate", 10000));
        /*    weatherMgr.SetAllowedWeatherTypes(WeatherType.Clear, 
                WeatherType.Rain,
                WeatherType.Thunder,
                WeatherType.Smog,
                WeatherType.Foggy,
                WeatherType.ExtraSunny,
                WeatherType.Overcast);*/
                weatherMgr.ForceWeatherChange();
        }

        public UDPSocketListener(int sessionID) : this(sessionID, 12, 999, DateTime.Now, WeatherType.Clear)
        { }

        /// <summary>
        /// Start listening for client requests asynchronously.
        /// </summary>
        public void StartListening()
        {
            sw.Start();

            // Create a UDP socket.
            Console.WriteLine("Starting server...");

            serverSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);

            serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

            IPEndPoint svEndpoint = new IPEndPoint(IPAddress.Any, 27852);

            try
            {
                serverSocket.Bind(svEndpoint);

                Console.WriteLine("Listening for clients...");

                EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);

                serverSocket.BeginReceiveFrom(byteData, 0, byteData.Length, SocketFlags.None, ref epSender, new AsyncCallback(OnReceive), byteData);

#if DEBUG
                sw1.Start();
#endif

                Thread thread = new Thread(() => Main());

                thread.Start();

                Console.ReadLine();
            }

            catch (Exception e)
            {
                threadQueue.AddTask(() => Program.WriteToConsole(string.Format("Exception:\n{0}", e.ToString())));
            }
        }

        int validStates;

        KeyValuePair<DateTime, ClientState[]>[] stateBuffer = new KeyValuePair<DateTime, ClientState[]>[20];

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
                if (sw.ElapsedMilliseconds >= TimerInterval)
                {
                    serverTime += 1;

                    state.Clients = GetClientStates();

                    for (int i = stateBuffer.Length - 1; i > 0; i--)
                        stateBuffer[i] = stateBuffer[i - 1];

                    var timeNow = PreciseDatetime.Now;

                    stateBuffer[0] = new KeyValuePair<DateTime, ClientState[]>(timeNow, state.Clients);

                    validStates = Math.Min(validStates + 1, stateBuffer.Length);

                    lock (syncObj)
                    {
                        foreach (var client in clientList)
                        {
                            if (client.Value.LastUpd != 0 && (serverTime - client.Value.LastUpd) > 1000)
                            {
                                removalQueue.Add(client.Key, client.Value);
                                continue;
                            }

                            if (client.Value.AvgPing.Count < MinTimeSamples && serverTime - client.Value.LastSync > 2)
                            {
                                threadQueue.AddTask(() => SendSynchronizationRequest(client.Key));                             
                                threadQueue.AddTask(() => Program.WriteToConsole("send again"));
                                client.Value.LastSync = serverTime;
                            }

                            if (client.Value.State.MovementFlags.HasFlag(ClientFlags.Dead) && !client.Value.WaitForRespawn)
                            {
                                client.Value.WaitForRespawn = true;

                                threadQueue.AddTask(() =>
                                {
                                    Thread.Sleep(8900); //8.7 seconds to allow respawn
                                    client.Value.SetHealth(100);
                                    client.Value.WaitForRespawn = false;
                                });
                            }

                            client.Value.State.PktID = ++client.Value.State.PktID;
                            client.Value.State.PktID %= int.MaxValue;

                         //   try
                        //    {
                                state.Timestamp = timeNow.Subtract(client.Value.TimeDiff);
                        //    }

                          //  catch (ArgumentOutOfRangeException)
                          //  { }

                            var msgBytes = state.ToByteArray();

                            serverSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, client.Key,
                                new AsyncCallback(OnSend), msgBytes);
                        }
                    }

                    foreach (var client in removalQueue)
                    {
                        threadQueue.AddTask(() => Program.WriteToConsole(string.Format("User \"{0}\" timed out.", client.Value.Info.Name)));
                        RaiseClientEvent(client.Value, EventType.PlayerLogout);
                        RemoveClient(client.Key);
                    }

                    if (serverTime > lastMasterUpdate + PingInterval)
                    {
                        var clientCount = clientList.Count;

                        var maxPlayers = serverVars.GetVar<int>("sv_maxplayers");

                        var sessionInfo = new SessionUpdate()
                        {
                            ClientCount = clientList.Count,
                            MaxClients = maxPlayers,
                            ServerID = SessionID
                        };

                        threadQueue.AddTask(() => SendSessionUpdate(sessionInfo));
                        lastMasterUpdate = serverTime;
                    }

                    removalQueue.Clear();

                    sw.Reset();
                    sw.Start();
#if DEBUG
                    Console.WriteLine(((float)sw1.ElapsedMilliseconds / 1000f).ToString());

                    sw1.Reset();
                    sw1.Start();
#endif        
                }
            }
        }

        /// <summary>
        /// Callback method for sent data.
        /// </summary>
        /// <param name="ar"></param>
        private void OnSend(IAsyncResult ar)
        {
            try
            {
                serverSocket.EndSend(ar);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Send error. \n" + ex.ToString());
            }
        }

        /// <summary>
        /// Process and handle any data received from the client.
        /// </summary>
        /// <param name="ar"></param>
        private void OnReceive(IAsyncResult ar)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                serverSocket.EndReceiveFrom(ar, ref sender);

                var msg = (NetMessage)byteData[0];

                    switch (msg)
                    {
                        case NetMessage.ClientState:
                            HandleClientStateUpdate(sender, new ClientState(byteData));
                            break;
                        case NetMessage.ChatMessage:
                            threadQueue.AddTask(new ThreadStart(() => HandleChatMessage(sender, new MessageInfo(byteData))));
                            break;
                        case NetMessage.ServerCommand:
                            threadQueue.AddTask(new ThreadStart(() => HandleServerCommand(sender, new ServerCommand(byteData))));
                            break;
                        case NetMessage.TimeSync:
                            threadQueue.AddTask(new ThreadStart(() => HandleTimeSync(sender, new TimeSync(byteData))));
                            break;
                        case NetMessage.NativeCallback:
                            threadQueue.AddTask(new ThreadStart(() => HandleNativeCallback(sender, new NativeCallback(byteData))));
                            break;
                        case NetMessage.WeaponHit:
                            threadQueue.AddTask(new ThreadStart(() => HandleBulletImpact(sender, new WeaponHit(byteData))));
                            break;              
                }
            }

            catch (SocketException e)
            {
                GameClient client;
                if (clientList.TryGetValue(sender, out client))
                {
                    threadQueue.AddTask(() => Program.WriteToConsole(string.Format("User \"{0}\" timed out.\n\nException:\n{1}", client.Info.Name, e.ToString())));
                    RaiseClientEvent(client, EventType.PlayerLogout);
                    RemoveClient(sender);
                }
            }

            finally
            {
                listen:
                try
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    serverSocket.BeginReceiveFrom(byteData, 0, byteData.Length, SocketFlags.None, ref ep, new AsyncCallback(OnReceive), byteData);
                }

                catch (SocketException)
                {
                    Console.WriteLine("listen failed. retrying...");
                    goto listen;
                }
            }
        }

        private bool StressMitigationCheck(EndPoint sender, int msInterval)
        {
            DateTime lastReceived;

            if (!stressMitigation.TryGetValue(sender, out lastReceived))
            {
                stressMitigation.Add(sender, PreciseDatetime.Now);
                return false;
            }

            else
            {
                var value = (PreciseDatetime.Now - lastReceived < TimeSpan.FromMilliseconds(msInterval));

                if (!value) stressMitigation[sender] = PreciseDatetime.Now;

                return value;
            }
        }

        private void OnServerWeatherChanged(WeatherType lastWeather, WeatherType newWeather)
        {
            foreach (var cl in clientList)
            {
                InvokeClientNative(cl.Key, "_SET_WEATHER_TYPE_OVER_TIME", newWeather.ToString(), 120f);
            }

            Console.WriteLine("Weather Changing... prev: " + lastWeather.ToString() + " new: " + newWeather.ToString());
        }

        internal void SendSessionUpdate(SessionUpdate sessionInfo)
        {
            try
            {
                Program.SessionProvider.SendHeartbeat(sessionInfo);
            }

            catch (Exception)
            {
                threadQueue.AddTask(() =>
                Program.WriteToConsole(string.Format("Failed while sending master server heartbeat.")));
            }
        }

        /// <summary>
        /// Send a time syncronization request to the client.
        /// </summary>
        /// <param name="client"></param>
        internal void SendSynchronizationRequest(EndPoint client)
        {
            var req = new TimeSync();
            req.ServerTime = PreciseDatetime.Now;
            var packedData = req.ToByteArray();
            serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);
        }

        /// <summary>
        /// Send a server hello to the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cmd"></param>
        internal void SendServerHello(EndPoint client, int netID, string message = "")
        {
            var hello = new ServerHello(netID, message);
            var packedData = hello.ToByteArray();
            serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);
            AddPendingCallback(client, packedData, hello.NetID);
        }

        /// <summary>
        /// Send a generic acknowledgment to the client.
        /// </summary>
        /// <param name="client"></param>
        internal void SendGenericAck(EndPoint client, int netID)
        {
            var ack = new GenericCallback(netID);
            var packedData = ack.ToByteArray();
            serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);
        }

        internal void AddPendingCallback(EndPoint client, byte[] data, int netID)
        {
            var timer = new Timer(new TimerCallback(x =>
            {
                // keep sending until we receive a callback
                if (pendingCallbacks.Contains(netID))
                    serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, client,
                    new AsyncCallback(OnSend), data);
            }),
              null, 1000, 1000);

            pendingCallbacks.Add(netID);
        }

        /// <summary>
        /// Raise an event for the specified client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="type"></param>
        internal void RaiseClientEvent(GameClient client, EventType type, bool sendToOthers = true)
        {
            UserEvent uEvent = new UserEvent();
            uEvent.SenderID = client.Info.UID;
            uEvent.SenderName = client.Info.Name;
            uEvent.EventType = type;

            var msgBytes = uEvent.ToByteArray();

            if (sendToOthers)
            {
                foreach (var cl in clientList)
                {
                    if (cl.Value.Info.UID != client.Info.UID)
                    {
                        serverSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, cl.Key,
                            new AsyncCallback(OnSend), msgBytes);
                        AddPendingCallback(cl.Key, msgBytes, uEvent.NetID);
                    }
                }

            }

            else
            {
                var cl = clientList.FirstOrDefault(x => x.Value.Info.UID == client.Info.UID);
                if (cl.Key != null)
                {
                    serverSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, cl.Key,
                        new AsyncCallback(OnSend), msgBytes);
                    AddPendingCallback(cl.Key, msgBytes, uEvent.NetID);
                }
            }
        }
        

        /// <summary>
        /// Invoke a native on the client with return type void.
        /// </summary>
        /// <param name="client"></param>
        internal void InvokeClientNative(EndPoint client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            var packedData = native.ToByteArray();

            serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);

            var timer = new Timer(new TimerCallback(x =>
            {
                // keep sending until we receive a callback
                if (nativeCallbacks.Contains(native.NetID))
                    serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                    new AsyncCallback(OnSend), packedData);
            }),
                    null, 1000, 1000);
            nativeCallbacks.Add(native.NetID);
        }

        /// <summary>
        /// Invoke a native on the client with a return type.
        /// </summary>
        /// <param name="client"></param>
        internal void InvokeClientNative<T>(EndPoint client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);
            var packedData = native.ToByteArray();
            serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);

            var timer = new Timer(new TimerCallback(x =>
            {
                // keep sending until we receive a callback
                if (nativeCallbacks.Contains(native.NetID))
                    serverSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                    new AsyncCallback(OnSend), packedData);
            }), null, 1000, 1000);
            nativeCallbacks.Add(native.NetID);
        }

       /* /// <summary>
        /// Sends a server notification to all clients
        /// </summary>
        /// <param name="client"></param>
        /// <param name="type"></param>
        internal void SendServerNotification(string text)
        {
            ServerNotification sNotify = new ServerNotification();
            sNotify.Message = text;
            sNotify.Timestamp = DateTime.UtcNow;

            var msgBytes = sNotify.ToByteArray();

            lock (syncObj)
            {
                foreach (var cl in clientList)
                {
                    serverSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, cl.Key,
                        new AsyncCallback(OnSend), msgBytes);
                }
            }
        } }*/

        /// <summary>
        /// Get a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool GetClient(EndPoint endpoint, out GameClient client)
        {
            return clientList.TryGetValue(endpoint, out client);
        }

        /// <summary>
        /// Get a client by its user ID.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        internal bool GetClient(int clientID, out GameClient client)
        {
            var item = clientList.Where(x => x.Value.Info.UID == clientID).FirstOrDefault();

            if (item.Value != null)
            {
                client = item.Value;
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
            return clientList.Select(x => x.Value.Info.UID).Contains(clientID);
        }

        /// <summary>
        /// Remove a client by endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(EndPoint endpoint)
        {
            lock (syncObj)
            clientList.Remove(endpoint);
        }

        /// <summary>
        /// Remove a client by client ID.
        /// </summary>
        /// <param name="endpoint"></param>
        internal void RemoveClient(int clientID)
        {
            var client = clientList.Where(x => x.Value.Info.UID == clientID).FirstOrDefault();
            if (client.Key != null)
                RemoveClient(client.Key);
        }

        /// <summary>
        /// Return an array of active vehicle states.
        /// </summary>
        /// <returns></returns>
        internal VehicleState[] GetVehicleStates()
        {
            return clientList.Values.Select(x => x.State.VehicleState).Where(y => y != null).ToArray();
        }

        /// <summary>
        /// Return an array of active client states.
        /// </summary>
        /// <returns></returns>
        internal ClientState[] GetClientStates()
        {
            return clientList.Values.Select(x => x.State).ToArray();
        }


        #region received msgs

        /// <summary>
        /// Handle a chat message sent by the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="msg"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleChatMessage(EndPoint sender, MessageInfo msg)
        {
            if (StressMitigationCheck(sender, 1500)) return;

            SendGenericAck(sender, msg.NetID);

            GameClient client;

            if (GetClient(sender, out client))
            {
                msg.SenderName = client.Info.Name;
                msg.SenderUID = client.Info.UID;

                var msgBytes = msg.ToByteArray();

                foreach (var cl in clientList)
                {
                    if (cl.Key != sender)
                    {
                        serverSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, cl.Key,
                            new AsyncCallback(OnSend), msgBytes);
                    }
                }

                threadQueue.AddTask(() => Program.WriteToConsole(string.Format("{0}: {1} [{2}]", client.Info.Name, msg.Message, PreciseDatetime.Now.ToString())));
            }

            else
            {
                Program.WriteToConsole("Cannot send a message, the sender doesn't exist.");
            }        
        }

        /// <summary>
        /// Handle a state update sent by a client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleClientStateUpdate(EndPoint sender, ClientState state)
        {
            try
            {
                GameClient client;
                if (GetClient(sender, out client))
                {
                    clientList[sender].UpdateState(state, serverTime);
                }
            }

            catch (Exception e)
            {
                threadQueue.AddTask(() => Program.WriteToConsole(string.Format("Update state failed.\n\nException:\n{0}", e.ToString())));
            }
        }

        /// <summary>
        /// Handle commands invoked by the client. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cmd"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleServerCommand(EndPoint sender, ServerCommand cmd)
        {
            if (StressMitigationCheck(sender, 5000)) return;

            GameClient client;

            switch (cmd.Command)
            {
                case CommandType.Login:
                    if (!ClientExists(cmd.UID))
                    {
                        // RemoveClient(cmd.UID);

                        if (clientList.Count > serverVars.GetVar<int>("sv_maxplayers"))
                            return;

                        var newClient = new GameClient(new ClientInfo(cmd.UID, cmd.Name), sender);

                        //  add client to active participants
                        clientList.Add(sender, newClient);

                        // notify other clients in the session
                        RaiseClientEvent(newClient, EventType.PlayerLogon);

                        // setup local world -->
                        InvokeClientNative(sender, "SET_WEATHER_TYPE_NOW", new NativeArg(weatherMgr.CurrentWeather.ToString()));

                        var svTime = timeMgr.CurrentTime;

                        InvokeClientNative(sender, "NETWORK_OVERRIDE_CLOCK_TIME", new NativeArg(svTime.Hour),
                            new NativeArg(svTime.Minute),
                            new NativeArg(svTime.Second));

                        SendServerHello(sender, cmd.NetID);

                        // request time sync
                        SendSynchronizationRequest(sender);

                        newClient.LastCMD = serverTime;

                        threadQueue.AddTask(() =>
                        Program.WriteToConsole(string.Format("User \"{0}\" joined the session with user ID \'{1}\'. Client IP Address: {2}\nIn- game time: {3}", cmd.Name, cmd.UID, (sender as IPEndPoint).Address.ToString(), svTime.ToShortTimeString())));
                    }
                    break;


                case CommandType.Logout:

                    if (GetClient(sender, out client))
                    {
                        if (clientList.TryGetValue(sender, out client))
                        {
                            threadQueue.AddTask(() => Program.WriteToConsole(string.Format("User \"{0}\" left the session.", client.Info.Name)));
                            RemoveClient(sender);
                        }

                        RaiseClientEvent(client, EventType.PlayerLogout);
               
                        threadQueue.AddTask(() => Program.WriteToConsole(string.Format("User \"{0}\" left the session.", cmd.Name, cmd.UID)));

                    }

                      //  threadQueue.AddTask(() => Console.WriteLine("No client exists with the specified endpoint."));
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
        private void HandleTimeSync(EndPoint sender, TimeSync req)
        {
            var currentTime = PreciseDatetime.Now;

            GameClient client;

            if (GetClient(sender, out client))
            {
                // total time for the roundtrip (server > client > server)
                client.Ping = (currentTime - req.ServerTime);

                var timeDiff = (currentTime - req.ClientTime) - TimeSpan.FromMilliseconds(client.Ping.TotalMilliseconds / 2);

                if (client.TimeDiff == null)
                {
                    // clock offset based on the local client time and half the ping time.
                    client.TimeDiff = timeDiff;
                    threadQueue.AddTask(() => Program.WriteToConsole("init time diff"));
                }

                client.AvgPing.Add(timeDiff);

                //    if (currentTime + client.TimeDiff > currentTime)
                //        client.TimeDiff = new TimeSpan(Math.Abs(client.TimeDiff.Ticks));

                if (client.AvgPing.Count >= MinTimeSamples)
                {
                    client.TimeDiff = Helpers.CalculateAverage(ref client.AvgPing, MinTimeSamples);

                    if (client.TimeDiff.TotalMilliseconds > 10000)
                    {
                        threadQueue.AddTask(() => Program.WriteToConsole("bad ping.. retrying"));
                        client.AvgPing.Clear();
                    }

                    else
                    {
                        RaiseClientEvent(client, EventType.PlayerSynced, false);
                        threadQueue.AddTask(() => Program.WriteToConsole(string.Format("Synced time for user {0} at {1}. Difference: {2}", client.Info.Name, currentTime.ToString(), client.TimeDiff.ToString())));
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
        private void HandleBulletImpact(EndPoint sender, WeaponHit wh)
        {
            SendGenericAck(sender, wh.NetID);

            GameClient killer, target;
            if (GetClient(sender, out killer))
            {
                var playbackTime = wh.Timestamp.Add(killer.TimeDiff);

                // get the game state from when this bullet was fired.
                var recentStates = stateBuffer.Where(x => x.Key <= playbackTime).FirstOrDefault();

                for (int i = 0; i < recentStates.Value.Length; i++)
                {
                    if (recentStates.Value[i].ID == killer.Info.UID)
                    {
                        foreach (var client in recentStates.Value)
                        {
                            //  dont want the killer
                            if (client.ID == recentStates.Value[i].ID)
                                continue;

                            // check distance
                            var dist = client.Position.DistanceTo(wh.HitCoords);

                            // lazy anti- cheat
                            if (dist > 5f) continue;

                            // make sure the target exists
                            if (GetClient(client.ID, out target))
                            {
                                if (target.State.Health < 0) return;

                                // modify health
                                target.State.Health -= wh.WeaponDamage;
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
        private void HandleNativeCallback(EndPoint sender, NativeCallback cb)
        {
            var callback = nativeCallbacks.Find(x => x == cb.NetID);

            if (callback != 0)
            {
                nativeCallbacks.Remove(callback);
            }
        }

        /// <summary>
        /// Handle a callback from the client.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="cb"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleGenericCallback(EndPoint sender, GenericCallback cb)
        {
            var callback = pendingCallbacks.Find(x => x == cb.NetID);

            if (callback != 0)
            {
                pendingCallbacks.Remove(callback);
            }
        }

        #endregion

        #region command line functions

        /// <summary>
        /// Execute a console command by its string alias defined in this.commands
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public bool ExecuteCommandString(string cmd)
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

        public string ShowHelp(params string[] args)
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
        public string GetStatus(params string[] args)
        {
            var states = GetClientStates();

            var builder = new System.Text.StringBuilder("Active Clients:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | UID: {1} Name: {2} Vehicle Seat: {3}", i, states[i].ID, states[i].Name, states[i].Seat == VehicleSeat.None ? "N/A" : states[i].Seat.ToString());

            builder.AppendLine();

            Console.Write(builder.ToString());

            return null;
        }

        /// <summary>
        /// Get a list of active vehicles and print it to the console.
        /// </summary>
        public string GetVehicleStatus(params string[] args)
        {
            var states = GetVehicleStates();

            var builder = new System.Text.StringBuilder("Active Vehicles:");

            for (int i = 0; i < states.Length; i++)
                builder.AppendFormat("\nID: {0} | Vehicle ID: {1} Flags: {2} Position: {3} {4} {5} Radio Station: {6}", i, states[i].ID, states[i].Flags, states[i].Position.X, states[i].Position.Y, states[i].Position.Z, states[i].RadioStation);

            builder.AppendLine();

            Console.Write(builder.ToString());

            return null;
        }

        /// <summary>
        /// Invoke a client native.
        /// </summary>
        public string InvokeNative(params string[] args)
        {
            var endpoint = clientList.ElementAt(Convert.ToInt32(args[0])).Key;
            string funcName = args[1];

            //skip user index and function name to get args
            var funcArgs = args.Skip(2).ToArray();

            List<NativeArg> nativeArgs = new List<NativeArg>();

            foreach (var arg in funcArgs)
            {
                nativeArgs.Add(new NativeArg(arg));
            }

            InvokeClientNative(endpoint, funcName, nativeArgs.ToArray());
            return null;
        }

        /// <summary>
        /// Set weather for all players
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        public string SetWeather(params string[] args)
        {
            var weatherArgs = args[0];

            foreach (var cl in clientList)
            {
                InvokeClientNative(cl.Key, "SET_OVERRIDE_WEATHER", new NativeArg(weatherArgs));
            }
           

            return null;
        }

        /// <summary>
        /// Set time for all players
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        public string SetTime(params string[] args)
        {
            int hours = Convert.ToInt32(args[0]);
            int minutes = Convert.ToInt32(args[1]);
            int seconds = Convert.ToInt32(args[2]);

            timeMgr.CurrentTime = new DateTime();
            timeMgr.CurrentTime += new TimeSpan(hours, minutes, seconds);

            foreach (var cl in clientList)
            {
                InvokeClientNative(cl.Key, "NETWORK_OVERRIDE_CLOCK_TIME", hours, minutes, seconds);
            }

            return null;
        }


        /// <summary>
        /// Kick a client from the session.
        /// </summary>
        /// <param name="hours">hours</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        public string Kick(params string[] args)
        {
            var client = clientList.ElementAt(Convert.ToInt32(args[0]));

            if (client.Key != null)
            {
                RaiseClientEvent(client.Value, EventType.PlayerKicked);

                clientList.Remove(client.Key);

                Program.WriteToConsole(string.Format("User \'{0}\' was kicked from the server.", client.Value.Info.Name));
            }

            return null;
        }

        /// <summary>
        /// Set weather for all players
        /// </summary>
        /// <param name="weather">Weather type as string</param>
        public string GetPosition(params string[] args)
        {
            var client = clientList.ElementAt(Convert.ToInt32(args[0])).Value;

            Console.WriteLine("{0}'s position: {1} {2} {3}", client.Info.Name,
                client.State.Position.X,
                client.State.Position.Y,
                client.State.Position.Z);

            return null;
        }

        /// <summary>
        /// Force a time sync for all clients.
        /// </summary>
        public string ForceSync(params string[] args)
        {
            foreach (var client in clientList)
            {
                SendSynchronizationRequest(client.Key);
            }

            return null;
        }

        #endregion
    }
}

