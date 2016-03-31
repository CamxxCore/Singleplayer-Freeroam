#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using GTA;
using GTA.Native;
using ASUPService;
using SPFLib.Types;
using SPFClient.Entities;
using SPFClient.Types;
using System.Threading;

using SPFClient.UI;

namespace SPFClient.Network
{
    public class NetworkSession : Script
    {
        #region var

        /// <summary>
        /// Update rate for local packets sent to the server.
        /// </summary>
        private const int ClUpdateRate = 15;

        /// <summary>
        /// Total idle time before the client is considered to have timed out from the server.
        /// </summary>
        private const int ClTimeout = 5000;

        /// <summary>
        /// Default port used for server connections (default: 27852)
        /// </summary>
        private const int Port = 27852;

        #endregion

        private static int UID = Guid.NewGuid().GetHashCode();

        public static bool Initialized { get; private set; } = false;

        private static bool isSynced = false;
        private static int lastSync = 0;

        public static SessionClient Current { get { return current; } }
        private static SessionClient current;

        private static Queue<NativeCall> queuedNativeCalls = new Queue<NativeCall>();

        private static Queue<RankData> queuedRankData = new Queue<RankData>();

        private static DateTime disconnectTimeout = new DateTime();

        private static Queue<SessionMessage> msgQueue =
            new Queue<SessionMessage>();

        private static uint lastSequence;

        public uint SentPacketSequence { get; private set; }

        public NetworkSession()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            UIChat.MessageSent += MessageSent;
            Tick += OnTick;
        }

        /// <summary>
        /// Join the specified session.
        /// </summary>
        /// <param name="session"></param>
        public static void JoinActiveSession(ActiveSession session)
        {
            if (current != null) current.Close();

            current = new SessionClient(new IPAddress(session.Address), Port);
            current.ChatEvent += ChatEvent;
            current.SessionStateEvent += SessionStateEvent;
            current.NativeInvoked += NativeInvoked;
            current.SessionEvent += SessionEvent;
            current.RankDataEvent += RankDataEvent;

            if (current.Inititialize(UID, Game.Player.Name ?? "Unknown Player"))
            {
                current.Login();

                //GTA.UI.ShowSubtitle("Initialized");

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

        private static void RankDataEvent(EndPoint sender, RankData e)
        {
            queuedRankData.Enqueue(e);
        }

        /// <summary>
        /// Close the session gracefully. 
        /// Unsubscribe from server events and remove all entities from the world.
        /// </summary>
        public static void Close()
        {
            UIManager.UINotifyProxy("Leaving session...");

            Initialized = false;

            if (current != null)
            {
                current.ChatEvent -= ChatEvent;
                current.SessionStateEvent -= SessionStateEvent;
                current.SessionEvent -= SessionEvent;
                current.NativeInvoked -= NativeInvoked;
                current.RankDataEvent -= RankDataEvent;
                current.Dispose();
                current = null;
            }     

            NetworkManager.DeleteAllEntities();

            // restore regular game world
            Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);

            //     Function.Call(Hash.PAUSE_CLOCK, false);

            Function.Call(Hash._DISABLE_AUTOMATIC_RESPAWN, true);

            disconnectTimeout = new DateTime();

            lastSequence = 0;

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

            if (SPFLib.Helpers.ValidateSequence(e.Sequence, lastSequence, uint.MaxValue))
            {
                foreach (var client in e.Clients)
                {
                    NetworkManager.QueueClientUpdate(client, e.Timestamp, client.ClientID == UID);
                }

                lastSequence = e.Sequence;
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
            if (!Initialized) return;
            if (e.SenderName.Equals("Server", StringComparison.InvariantCultureIgnoreCase))
                UIManager.UINotifyProxy(e.Message);
            else
                msgQueue.Enqueue(e);
        }

        /// <summary>
        /// Message sent handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void MessageSent(UIChat sender, string message)
        {
            if (!Initialized) return;
            current.Say(message);
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
                    UIManager.UINotifyProxy("Successfully Connected.");
                    break;

                case EventType.PlayerLogon:
                    if (e.SenderID == UID) return;
                    UIManager.UINotifyProxy(e.SenderName + " joined. " + e.SenderID.ToString());
                    break;

                case EventType.PlayerLogout:
                    if (e.SenderID == UID) return;
                    if (client != null)
                    {
                        NetworkManager.DeleteClient(client);
                        if (client.ActiveVehicle != null)
                            NetworkManager.DeleteVehicle(client.ActiveVehicle);           
                    }
                    UIManager.UINotifyProxy(e.SenderName + " left. " + e.SenderID.ToString());
                    break;

                case EventType.PlayerKicked:
                    if (e.SenderID == UID) return;
                    if (client != null) NetworkManager.DeleteClient(client);
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
            queuedNativeCalls.Enqueue(e);
        }

        /// <summary>
        /// Main tick event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTick(object sender, EventArgs e)
        {
            if (!Initialized) return;

            if (disconnectTimeout.Ticks > 0 && DateTime.Now >= disconnectTimeout)
            {
                GTA.UI.Notify("Lost connection to the server.");
                Close();
            }

            if (isSynced && Game.GameTime - lastSync >= ClUpdateRate)
            {
                SentPacketSequence++;

                SentPacketSequence %= uint.MaxValue;

                var localPlayer = NetworkManager.LocalPlayer;

                var state = localPlayer.GetClientState();

                current.UpdateUserData(state, SentPacketSequence);

                lastSync = Game.GameTime;
            }

            while (msgQueue.Count > 0)
            {
                var message = msgQueue.Dequeue();
                UIChat.AddFeedMessage(message.SenderName, message.Message);
            }

            #region native invocation

            while (queuedNativeCalls.Count > 0)
            {
                var native = queuedNativeCalls.Dequeue();

                NativeCallback callback = NativeHandler.ExecuteLocalNativeWithArgs(native);

                if (callback != null)
                    current.SendNativeCallback(callback);
            }

            while (queuedRankData.Count > 0)
            {
                var rData = queuedRankData.Dequeue();
                UIManager.RankBar.ShowRankBar(rData.RankIndex, rData.RankXP, rData.NewXP, 116, 3000, 2000);
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

        public static List<NetworkPlayer> ActivePlayers { get { return activePlayers; } }

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

                var clientState = remoteClient.Key;

                // make sure the client position is valid
                if (clientState == null ||
                    (!clientState.InVehicle && 
                    clientState.Position != null &&
                    clientState.Position.X == 0 &&
                    clientState.Position.Y == 0 &&
                    clientState.Position.Z == 0))
                {
                    continue;
                }

                NetworkPlayer client = PlayerFromID(clientState.ClientID);

                // the client doesn't exist
                if (client == null)
                {
                    client = new NetworkPlayer(clientState);
                    AddClient(client);
                }

                else
                {
                    if (clientState.PedID != 0 && client.GetPedID() != clientState.PedID ||
                        clientState.Health > 0 && client.Health <= 0 || !client.Exists())
                    {
                        DeleteClient(client);
                        continue;
                    }

                    // vehicle exists. queue for update.
                    if (clientState.InVehicle && clientState.VehicleState != null)
                    {
                        var vehicleState = clientState.VehicleState;

                        NetworkVehicle vehicle;

                        // network player is in our vehicle, so use the reference in LocalPlayer instead
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
                            }
                        }

                        if (!Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, client.Handle, vehicle.Handle, true))
                        {
                            Function.Call(Hash.TASK_ENTER_VEHICLE, client.Handle, vehicle.Handle, -1, (int)clientState.VehicleSeat, 0.0f, client.LastState.InVehicle ? 16 : 3, 0);

                            var dt = DateTime.Now + TimeSpan.FromMilliseconds(1800);

                            while (DateTime.Now < dt)
                                Yield();
                        }

                        if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, vehicle.Model.Hash))
                        {
                            client.SetBicycleState((BicycleState)vehicleState.ExtraFlags);
                        }

                        // if it isnt the driver, we don't need to handle the update.
                        if (clientState.VehicleSeat == SPFLib.Enums.VehicleSeat.Driver)
                            vehicle.HandleUpdatePacket(vehicleState, remoteClient.Value);
                    }

                    client.HandleUpdatePacket(clientState, remoteClient.Value);

                }
            }

            while (localClientQueue.Count > 0)
            {
                var clientUpdate = localClientQueue.Dequeue();

                if (clientUpdate.Health <= 0 && Game.Player.Character.Health > -1)
                    Game.Player.Character.Health = -1;

                else if (clientUpdate.Health != Game.Player.Character.Health)
                {
                    Game.Player.Character.Health = clientUpdate.Health;
                }             
            }

            if (localPlayer != null)
            {
                localPlayer.Update();
            }

            while (clientDeletionQueue.Count > 0)
            {
                var client = clientDeletionQueue.Dequeue();
                if (client.Value)
                {
                    client.Key.MarkAsNoLongerNeeded();
                    client.Key.Dispose();
                }

                activePlayers.Remove(client.Key);
            }

            while (vehicleDeletionQueue.Count > 0)
            {
                var vehicle = vehicleDeletionQueue.Dequeue();
                if (vehicle.Value)
                {
                    vehicle.Key.MarkAsNoLongerNeeded();
                    vehicle.Key.Dispose();
                }

                activeVehicles.Remove(vehicle.Key);
            }

            foreach (NetworkPlayer client in activePlayers)
            {
                client.Update();
            }

            foreach (NetworkVehicle vehicle in activeVehicles)
            {
                if (!vehicle.Exists() || !vehicle.IsAlive)
                    DeleteVehicle(vehicle, false);
                vehicle.Update();
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
                vehicle = new NetworkCar(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
                vehicle = new NetworkHeli(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
                vehicle = new NetworkPlane(state);

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
                vehicle = new NetworkBicycle(state);

            else vehicle = new NetworkVehicle(state);

            activeVehicles.Add(vehicle);

            return vehicle;
        }

        /// <summary>
        /// Queue a client for deletion next game loop.
        /// </summary>
        /// <param name="client"></param>
        internal static void DeleteClient(NetworkPlayer client, bool removeFromWorld = true)
        {
            clientDeletionQueue.Enqueue(
            new KeyValuePair<NetworkPlayer, bool>(client, removeFromWorld));
        }

        /// <summary>
        /// Queue a vehicle for deletion next game loop.
        /// </summary>
        /// <param name="vehicle"></param>
        internal static void DeleteVehicle(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            vehicleDeletionQueue.Enqueue(
                new KeyValuePair<NetworkVehicle, bool>(vehicle, removeFromWorld));
        }


        internal static void DeleteAllEntities(bool removeFromWorld = true)
        {
            activePlayers.ForEach(x => DeleteClient(x, removeFromWorld));
            activeVehicles.ForEach(x => DeleteVehicle(x, removeFromWorld));
        }
    }
}
