#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Net;
using GTA;
using GTA.Native;
using ASUPService;
using SPFLib.Types;
using SPFLib.Enums;
using SPFClient.Entities;
using SPFClient.Types;
using SPFClient.UI;

namespace SPFClient.Network
{
    public class NetworkSession : Script
    {
        #region var

        /// <summary>
        /// Update rate for local packets sent to the server.
        /// </summary>
        private const int ClUpdateRate = 10;

        /// <summary>
        /// Total idle time before the client is considered to have timed out from the server.
        /// </summary>
        private const int ClTimeout = 5000;

        /// <summary>
        /// Default port used for server connections (default: 27852)
        /// </summary>
        private const int Port = 27852;

        #endregion

        private const string Username = "client";

        private static int UID = Guid.NewGuid().GetHashCode();

        public static bool Initialized { get; private set; } = false;

        private static bool isSynced = false;

        public static SessionClient Client { get { return client; } }
        private static SessionClient client;

        private static Queue<NativeCall> nativeQueue = new Queue<NativeCall>();

        private static DateTime disconnectTimeout = new DateTime();

        private static Queue<SessionMessage> msgQueue =
            new Queue<SessionMessage>();

        public NetworkSession()
        {
            UIChat.MessageSent += MessageSent;
            Tick += OnTick;
        }

        /// <summary>
        /// Join the specified session.
        /// </summary>
        /// <param name="session"></param>
        public static void JoinActiveSession(ActiveSession session)
        {
            if (client != null) client.Close();

            client = new SessionClient(new IPAddress(session.Address), Port);
            client.ChatEvent += ChatEvent;
            client.SessionStateEvent += SessionStateEvent;
            client.NativeInvoked += NativeInvoked;
            client.SessionEvent += SessionEvent;

            if (client.StartListening())
            {
                client.Login(UID, Username);

                World.GetAllEntities().ToList().ForEach(x =>
                    {
                        if ((x is Ped || x is Vehicle) &&
                        x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
                        { x.Delete(); }
                    });

                Function.Call((Hash)0x231C8F89D0539D8F, 0, 1);
                NetworkManager.LocalPlayer.Setup();
                // wait for server callback and handle initialization there.
            }

            else
            {
                GTA.UI.Notify(string.Format("~r~Connection error. The server is offline or UDP port ~y~{0} ~r~is not properly forwarded.", Port));
                Initialized = false;
            }
        }

        /// <summary>
        /// Close the session gracefully. 
        /// Unsubscribe from server events and remove all entities from the world.
        /// </summary>
        public static void Close()
        {
            UIManager.UINotifyProxy("Leaving session...");

            Initialized = false;

            if (client != null)
            {
                client.ChatEvent -= ChatEvent;
                client.SessionStateEvent -= SessionStateEvent;
                client.SessionEvent -= SessionEvent;
                client.NativeInvoked -= NativeInvoked;
                client.Dispose();
                client = null;
            }

            // remove all clients and any vehicles from the world.
            World.GetAllEntities().ToList().ForEach(x =>
            {
                if ((x is Ped || x is Vehicle) &&
                x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
                { x.Delete(); }
            });

            // restore regular game world
            Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);

            Function.Call(Hash.PAUSE_CLOCK, false);

            // reset timers to zero
            disconnectTimeout = new DateTime();
            isSynced = false;
        }

        /// <summary>
        /// Session state received handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SessionStateEvent(EndPoint sender, SessionState e)
        {
            if (!Initialized) return;

            foreach (var client in e.Clients)
            {
                NetworkManager.QueueClientUpdate(client, e.Timestamp, client.ClientID == UID);
            }

            disconnectTimeout = DateTime.Now + TimeSpan.FromMilliseconds(ClTimeout);
        }


        /// <summary>
        /// Chat event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ChatEvent(EndPoint sender, SessionMessage e)
        {
            if (!Initialized || e.SenderUID == UID) return;
            msgQueue.Enqueue(e);
        }

        /// <summary>
        /// Message sent handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void MessageSent(UIChat sender, string message)
        {
            if (!Initialized)
                return;
            client.Say(message);
        }

        /// <summary>
        /// User event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SessionEvent(EndPoint sender, SessionEvent e)
        {
            var client = NetworkManager.PlayerFromID(e.SenderID);
            switch (e.EventType)
            {
                case EventType.PlayerSynced:
                    isSynced = true;
                    Initialized = true;
                    break;

                case EventType.PlayerLogon:
                    if (e.SenderID == UID) return;
                    UIManager.UINotifyProxy(e.SenderName + " joined. " + e.SenderID.ToString());
                    break;

                case EventType.PlayerLogout:
                    if (e.SenderID == UID) return;
                    if (client != null)
                    {
                        if (client.ActiveVehicle != null)
                            NetworkManager.RemoveVehicle(client.ActiveVehicle);
                        NetworkManager.RemoveClient(client);
                    }
                    UIManager.UINotifyProxy(e.SenderName + " left.");
                    break;

                case EventType.PlayerKicked:
                    if (e.SenderID == UID) return;
                    if (client != null) NetworkManager.RemoveClient(client);
                    UIManager.UINotifyProxy(e.SenderName + " was kicked.");
                    break;
            }
        }

        /// <summary>
        /// Native invocation handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void NativeInvoked(EndPoint sender, NativeCall e)
        {
            if (!Initialized) return;
            nativeQueue.Enqueue(e);
        }

        /// <summary>
        /// Main tick event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTick(object sender, EventArgs e)
        {
            client?.OnReceive();

            if (!Initialized) return;

            if (disconnectTimeout.Ticks > 0 && DateTime.Now > disconnectTimeout)
            {
                GTA.UI.Notify("Lost connection to the server.");
                Close();
            }

            if (isSynced)
            {
                var localPlayer = NetworkManager.LocalPlayer;

                var state = localPlayer.GetClientState();

                client.UpdateUserData(state);

                localPlayer.ResetClientFlags();
            }

            while (msgQueue.Count > 0)
            {
                var message = msgQueue.Dequeue();
                UIChat.AddFeedMessage(message.SenderName, message.Message);
            }

            #region native invocation

            while (nativeQueue.Count > 0)
            {
                var native = nativeQueue.Dequeue();

                Hash result; long nResult;
                NativeCallback nCallback;

                if (Enum.TryParse(native.FunctionName, out result))
                {
                  nCallback = NativeHandler.ExecuteSerializedNativeWithArgs(result, 
                      native.Args, 
                      native.ReturnType, 
                      native.NetID);

                    client.SendNativeCallback(nCallback);
                }

                else if (long.TryParse(native.FunctionName, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out nResult))
                {
                    nCallback = NativeHandler.ExecuteSerializedNativeWithArgs((Hash)nResult, 
                        native.Args, 
                        native.ReturnType, 
                        native.NetID);

                    client.SendNativeCallback(nCallback);
                }
            }

            #endregion

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
        }

        /// <summary>
        /// Dispose the script and related resources.
        /// </summary>
        /// <param name="A_0"></param>
        protected override void Dispose(bool A_0)
        {
            World.GetAllEntities().ToList().ForEach(x =>
            {
                if ((x is Ped || x is Vehicle) &&
                x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
                { x.Delete(); }
            });
            base.Dispose(A_0);
        }
    }

    public class NetworkManager : Script
    {
        private static LocalPlayer localPlayer;
        public static LocalPlayer LocalPlayer { get { return localPlayer; } }

        public static List<NetworkPlayer> ActivePlayers {  get { return activePlayers; } }

        private static Queue<KeyValuePair<ClientState, DateTime>> updateQueue =
            new Queue<KeyValuePair<ClientState, DateTime>>();

        private static Queue<ClientState> localClientQueue =
            new Queue<ClientState>();

        private static List<NetworkPlayer> activePlayers =
            new List<NetworkPlayer>();

        private static List<NetworkVehicle> activeVehicles = 
            new List<NetworkVehicle>();

        private static Queue<KeyValuePair<NetworkPlayer, bool>> clientDeletionQueue =
            new Queue<KeyValuePair<NetworkPlayer, bool>>();

        private static Queue<KeyValuePair<NetworkVehicle, bool>> vehicleDeletionQueue =
            new Queue<KeyValuePair<NetworkVehicle, bool>>();

        public NetworkManager()
        {
            localPlayer = new LocalPlayer();
            Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!NetworkSession.Initialized) return;

            while (updateQueue.Count > 0)
            {
                // dequeue the client that needs an update.
                var remoteClient = updateQueue.Dequeue();

                // make sure the client position is valid
                if ((!remoteClient.Key.InVehicle && remoteClient.Key.Position?.X == 0 &&
                        remoteClient.Key.Position.Y == 0 &&
                        remoteClient.Key.Position.Z == 0))
                {
                    continue;
                }

                var clientState = remoteClient.Key;

                NetworkPlayer client = PlayerFromID(clientState.ClientID);

                // the client doesn't exist
                if (client == null)
                {
                    client = new NetworkPlayer(clientState);
                    AddClient(client);
                }

                if (clientState.PedID != 0 && client.GetPedID() != clientState.PedID ||
                    clientState.Health > 0 && client.Health <= 0)
                {
                    ForceRemoveClient(client);
                    continue;
                }

                // vehicle exists. queue for update.
                if (clientState.InVehicle && clientState.VehicleState != null)
                {

                    var vehicleState = clientState.VehicleState;

                    NetworkVehicle vehicle;

                    if (vehicleState.ID == localPlayer.Vehicle?.ID)
                    {
                        vehicle = localPlayer.Vehicle;
                    }

                    else
                    {
                        vehicle = VehicleFromID(vehicleState.ID);

                        if (vehicle == null)
                        {
                            vehicle = CreateAndAddVehicle(vehicleState);
                            client.ActiveVehicle = vehicle;

                            Function.Call(Hash.TASK_ENTER_VEHICLE,
                                client.Handle, vehicle.Handle, -1, (int)clientState.VehicleSeat, 0.0f, 16, 0);

                            var dt = DateTime.Now + TimeSpan.FromMilliseconds(1800);

                            while (DateTime.Now < dt)
                                Yield();
                        }
                    }

                    if (!Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, client.Handle, vehicle.Handle, true))
                    {
                        Function.Call(Hash.TASK_ENTER_VEHICLE,
                       client.Handle, vehicle.Handle, -1, (int)clientState.VehicleSeat, 0.0f, 3, 0);

                        var dt = DateTime.Now + TimeSpan.FromMilliseconds(1800);

                        while (DateTime.Now < dt)
                            Yield();

                        Function.Call(Hash.TASK_ENTER_VEHICLE, client.Handle, vehicle.Handle, -1, (int)clientState.VehicleSeat, 0.0f, 16, 0);
                    }

                    if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, vehicle.Model.Hash))
                    {
                        client.SetBicycleState((BicycleState)vehicleState.ExtraFlags);
                    }

                   // if (vehicleState.Flags.HasFlag(VehicleFlags.Driver))
                        vehicle.HandleUpdatePacket(vehicleState, remoteClient.Value);
                }

                client.HandleUpdatePacket(clientState, remoteClient.Value);

            }

            while (localClientQueue.Count > 0)
            {
                var clientUpdate = localClientQueue.Dequeue();

                if (clientUpdate.Health <= 0 && Game.Player.Character.Health > -1)
                    Game.Player.Character.Health = -1;

                else
                {
                    if (Game.Player.Character.Health > clientUpdate.Health)
                    {
                        Function.Call(Hash.APPLY_DAMAGE_TO_PED, Game.Player.Character.Handle,
                            Game.Player.Character.Health - clientUpdate.Health, true);
                    }
                }
            }

            if (localPlayer != null)
            {
                localPlayer.Update();
            }

            foreach (NetworkPlayer client in activePlayers)
            {
                client.Update();
            }

            foreach (NetworkVehicle vehicle in activeVehicles)
            {
                vehicle.Update();
            }

            while (clientDeletionQueue.Count > 0)
            {
                var client = clientDeletionQueue.Dequeue();
                ForceRemoveClient(client.Key, client.Value);
            }

            while (vehicleDeletionQueue.Count > 0)
            {
                var vehicle = vehicleDeletionQueue.Dequeue();
                ForceRemoveVehicle(vehicle.Key, vehicle.Value);
            }
        }

        /// <summary>
        /// Add an already created NetworkPlayer object to the active list of entities.
        /// </summary>
        /// <param name="client"></param>
        internal static void AddClient(NetworkPlayer client)
        {
            if (activePlayers.Contains(client)) activePlayers.Remove(client);
            activePlayers.Add(client);
        }

        /// <summary>
        /// Queue a client update from a client state object sent over the network.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="serverTime"></param>
        /// <param name="isLocal"></param>
        internal static void QueueClientUpdate(ClientState state, DateTime serverTime, bool isLocal)
        {
            if (isLocal) localClientQueue.Enqueue(state);
            else
                updateQueue.Enqueue(new KeyValuePair<ClientState, DateTime>(state, serverTime));
        }
     
        /// <summary>
        /// Get a NetworkPlayer from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static NetworkPlayer PlayerFromID(int id)
        {
            return activePlayers.Find(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkPlayer from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal static NetworkPlayer PlayerFromLocalHandle(int handle)
        {
            return activePlayers.Find(x => x.Handle == handle);
        }

        /// <summary>
        /// Get a NetworkVehicle from its respective ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static NetworkVehicle VehicleFromID(int id)
        {
            return activeVehicles.FirstOrDefault(x => x.ID == id);
        }

        /// <summary>
        /// Get a NetworkVehicle from its handle in the local game world.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        internal static NetworkVehicle VehicleFromLocalHandle(int handle)
        {
            return activeVehicles.FirstOrDefault(x => x.Handle == handle);
        }

        /// <summary>
        /// Takes a vehicle state object sent over the network and create its representation in the local world.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        internal static NetworkVehicle CreateAndAddVehicle(VehicleState state)
        {
            NetworkVehicle vehicle;

            var hash = (int)Helpers.VehicleIDToHash(state.VehicleID);

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
                vehicle = new NetCar(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
                vehicle = new NetHeli(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
                vehicle = new NetPlane(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
                vehicle = new NetBicycle(state);

            else vehicle = new NetworkVehicle(state);

            activeVehicles.Add(vehicle);

            return vehicle;
        }

        /// <summary>
        /// Queue a client for deletion next game loop.
        /// </summary>
        /// <param name="client"></param>
        internal static void RemoveClient(NetworkPlayer client, bool removeFromWorld = true)
        {
            clientDeletionQueue.Enqueue(
            new KeyValuePair<NetworkPlayer, bool>(client, removeFromWorld));
        }

        /// <summary>
        /// Queue a vehicle for deletion next game loop.
        /// </summary>
        /// <param name="vehicle"></param>
        internal static void RemoveVehicle(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            vehicleDeletionQueue.Enqueue(
                new KeyValuePair<NetworkVehicle, bool>(vehicle, removeFromWorld));
        }

        /// <summary>
        /// Forces a client to be removed from the list of active entites, and optionally from the local world.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="removeFromWorld"></param>
        internal static void ForceRemoveClient(NetworkPlayer client, bool removeFromWorld = true)
        {
            if (removeFromWorld)
            {
                client.MarkAsNoLongerNeeded();
                client.Dispose();
            }

            activePlayers.Remove(client);
        }

        /// <summary>
        /// Forces a vehicle to be removed from the list of active entites, and optionally from the local world.
        /// </summary>
        /// <param name="vehicle"></param>
        /// <param name="removeFromWorld"></param>
        internal static void ForceRemoveVehicle(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            if (removeFromWorld)
            {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Dispose();
            }

            activeVehicles.Remove(vehicle);
        }

        /// <summary>
        /// Forces all active clients to be removed from the list of active entities and the local world.
        /// </summary>
        internal static void ForceRemoveAllClients()
        {
            foreach (var client in activePlayers)
                ForceRemoveClient(client);

            activePlayers.Clear();
        }

        /// <summary>
        /// Forces all active vehicles to be removed from the list of active entities and the local world.
        /// </summary>
        internal static void ForceRemoveAllVehicles()
        {
            foreach (var vehicle in activeVehicles)
                ForceRemoveVehicle(vehicle);

            activeVehicles.Clear();
        }
    }
}
