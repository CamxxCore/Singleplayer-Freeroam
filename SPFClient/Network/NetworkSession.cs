#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;
using SPFClient.Entities;
using SPFClient.UIManagement;
using SPFClient.Types;
using ASUPService;
using System.Globalization;
using System.Diagnostics;
using System.Net;

namespace SPFClient.Network
{
    public class NetworkSession : Script
    {
        #region var

        /// <summary>
        /// Update rate for local packets sent to the server.
        /// </summary>
        private const int ClUpdateRate = 30;

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

        public static SessionClient CurrentSession { get { return currentSession; } }
        private static SessionClient currentSession;
        
        private static Queue<NativeCall> nativeQueue = new Queue<NativeCall>();

        private static DateTime disconnectTimeout = new DateTime(),
            clUpdateTimer = new DateTime();

        private static Queue<MessageInfo> msgQueue =
            new Queue<MessageInfo>();

        public NetworkSession()
        {
            UIChat.MessageSent += MessageSent;
            Tick += OnTick;
        }

        /// <summary>
        /// Join an active session.
        /// </summary>
        /// <param name="session"></param>
        public static void JoinActiveSession(ActiveSession session)
        {
            if (currentSession != null) Close();

            currentSession = new SessionClient(new IPAddress(session.Address), Port);

            currentSession.ChatEvent += ChatEvent;
            currentSession.SessionStateEvent += SessionStateEvent;
            currentSession.NativeInvoked += NativeInvoked;
            currentSession.UserEvent += UserEvent;
            currentSession.ServerHelloEvent += ServerHello;
            currentSession.Login(UID, Username);

            if (currentSession.StartListening())

            {
                World.GetAllEntities().ToList().ForEach(x => {
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
                UI.Notify(string.Format("~r~Connection error. The server is offline or UDP port ~y~{0} ~r~is not properly forwarded.", Port));
                Initialized = false;
            }
        }

        private static void ServerHello(EndPoint sender, ServerHello e)
        {
            UIManager.UINotifyProxy("Server says: " + e.Message);

        }

        /// <summary>
        /// Close the session gracefully. 
        /// Unsubscribe from server events and remove all entities from the world.
        /// </summary>
        public static void Close()
        {
            Initialized = false;

            if (currentSession != null)
            {
                currentSession.ChatEvent -= ChatEvent;
                currentSession.SessionStateEvent -= SessionStateEvent;
                currentSession.UserEvent -= UserEvent;
                currentSession.NativeInvoked -= NativeInvoked;
                currentSession.StopListening();
                currentSession = null;
            }

            // remove all clients and any vehicles from the world.
            World.GetAllEntities().ToList().ForEach(x => {
                if ((x is Ped || x is Vehicle) &&
                x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
                { x.Delete(); }
            });

            // restore regular game world
            Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);

            Function.Call(Hash.PAUSE_CLOCK, false);

            // reset timers to zero
            disconnectTimeout = new DateTime();
            clUpdateTimer = new DateTime();
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
                NetworkManager.QueueClientUpdate(client, e.Timestamp, client.ID == UID);
            }

            disconnectTimeout = DateTime.Now + TimeSpan.FromMilliseconds(ClTimeout);
        }

        /// <summary>
        /// Chat event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ChatEvent(EndPoint sender, MessageInfo e)
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
            currentSession.Say(message);
        }

        /// <summary>
        /// User event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UserEvent(EndPoint sender, UserEvent e)
        {
            var client = NetworkManager.PlayerFromID(e.SenderID);

            switch (e.EventType)
            {
                case EventType.PlayerLogon:
                    UIManager.UINotifyProxy(e.SenderName + " joined.");
                    break;

                case EventType.PlayerLogout:
                    if (client != null)
                    {
                        if (client.ActiveVehicle != null)
                            NetworkManager.RemoveVehicle(client.ActiveVehicle);
                        NetworkManager.RemoveClient(client);
                    }
                    UIManager.UINotifyProxy(e.SenderName + " left.");
                    break;

                case EventType.PlayerKicked:
                    if (client != null) NetworkManager.RemoveClient(client);
                    UIManager.UINotifyProxy(e.SenderName + " was kicked.");
                    break;

                case EventType.PlayerSynced:
                    UIManager.UINotifyProxy(e.SenderName + " synced.");
                    // start timer and begin sending data.
                    clUpdateTimer = DateTime.Now + TimeSpan.FromMilliseconds(ClUpdateRate);
                    Initialized = true;  
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
            if (Function.Call<bool>((Hash)0xD55DDFB47991A294, Game.Player.Handle) || !Initialized) return;

            if (disconnectTimeout.Ticks > 0 && DateTime.Now > disconnectTimeout)
            {
                UI.Notify("~r~Lost connection to the server.");
                Close();
            }

            if ((clUpdateTimer.Ticks > 0 && DateTime.Now > clUpdateTimer))
            {
                // Send local information to the server

                var localPlayer = NetworkManager.LocalPlayer;

                try
                {
                    var state = new ClientState();

                    // dont serialize this information unless we have to
                    if (!localPlayer.Ped.IsInVehicle())
                    {
                        state.Position = localPlayer.Ped.Position.Serialize();
                        state.Velocity = localPlayer.Ped.Velocity.Serialize();
                        state.Angles = GameplayCamera.Rotation.Serialize();
                        state.Rotation = localPlayer.Ped.Quaternion.Serialize();
                        state.MovementFlags = localPlayer.ClientFlags;
                        state.ActiveTask = (ActiveTask)MemoryAccess.ReadInt16(localPlayer.EntityAddress + Offsets.CLocalPlayer.Stance);
                        state.Health = Convert.ToInt16(localPlayer.Ped.Health);
                        state.WeaponID = localPlayer.GetWeaponID();
                        state.PedID = localPlayer.GetPedID();
                    }

                    else
                    {
                        var vehicle = localPlayer.Vehicle;

                        if ((int)localPlayer.GetVehicleSeat() == -1)
                        {
                            // if the local player is in a vehicle that doesn't exist in the active list...
                            if ((vehicle == null || localPlayer.Ped.CurrentVehicle.Handle != vehicle.Handle))
                            {
                                vehicle = NetworkManager.VehicleFromLocalHandle(localPlayer.Ped.CurrentVehicle.Handle);
                                if (vehicle == null)
                                    vehicle = new NetworkVehicle(localPlayer.Ped.CurrentVehicle, SPFLib.Helpers.GenerateUniqueID());
                                localPlayer.SetCurrentVehicle(vehicle);
                            }

                            else
                            {
                                var v = new Vehicle(vehicle.Handle);

                                state.VehicleState = new VehicleState(vehicle.ID,
                                    vehicle.Position.Serialize(),
                                    vehicle.Velocity.Serialize(),
                                    vehicle.Quaternion.Serialize(),
                                    v.CurrentRPM,
                                    vehicle.GetWheelRotation(),
                                    0, Convert.ToInt16(v.Health),
                                    (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                                    localPlayer.GetRadioStation(),
                                    localPlayer.GetVehicleID());

                                state.VehicleState.Flags |= VehicleFlags.Driver;

                                if (v.IsDead) state.VehicleState.Flags |= VehicleFlags.Exploded;

                                if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, vehicle.Model.Hash))
                                {
                                    if (v.LeftHeadLightBroken)
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.LHeadlightBroken;

                                    if (v.RightHeadLightBroken)
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.RHeadlightBroken;

                                    if (v.IsDoorBroken(VehicleDoor.FrontLeftDoor))
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.LDoorBroken;

                                    if (v.IsDoorBroken(VehicleDoor.FrontRightDoor))
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.RDoorBroken;

                                    if (v.IsDoorBroken(VehicleDoor.BackLeftDoor))
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.BLDoorBroken;

                                    if (v.IsDoorBroken(VehicleDoor.BackRightDoor))
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.BRDoorBroken;

                                    if (v.IsDoorBroken(VehicleDoor.Hood))
                                        state.VehicleState.ExtraFlags |= (ushort)VDamageFlags.HoodBroken;
                                }

                                else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, vehicle.Model.Hash))
                                {
                                    if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehicleFlyUnderCarriage))
                                    {
                                        var lgState = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, vehicle.Handle);
                                        state.VehicleState.ExtraFlags = (ushort)lgState;
                                    }

                                    if (Game.IsControlPressed(0, Control.VehicleFlyAttack) || Game.IsControlPressed(0, Control.VehicleFlyAttack2))
                                    {
                                        var outArg = new OutputArgument();
                                        if (Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, localPlayer.Ped.Handle, outArg))
                                        {
                                            unchecked
                                            {
                                                switch ((WeaponHash)outArg.GetResult<int>())
                                                {
                                                    case (WeaponHash)0xCF0896E0:
                                                        state.VehicleState.Flags |= VehicleFlags.PlaneShoot;
                                                        break;

                                                    case (WeaponHash)0xE2822A29:
                                                        state.VehicleState.Flags |= VehicleFlags.PlaneGun;
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }

                                else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, vehicle.Model.Hash))
                                {
                                    if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehiclePushbikePedal))
                                    {
                                        if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                                            state.VehicleState.ExtraFlags = (ushort)BicycleState.TuckPedaling;
                                        else state.VehicleState.ExtraFlags = (ushort)BicycleState.Pedaling;
                                    }

                                    else
                                    {
                                        if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                                            state.VehicleState.ExtraFlags = (ushort)BicycleState.TuckCruising;
                                        else state.VehicleState.ExtraFlags = (ushort)BicycleState.Cruising;
                                    }
                                }
                            }
                        }

                        else
                        {
                            vehicle = NetworkManager.VehicleFromLocalHandle(localPlayer.Ped.CurrentVehicle.Handle);
                            if (vehicle != null)
                            {
                                state.VehicleState = new VehicleState(vehicle.ID);
                            }
                        }

                        state.InVehicle = true;

                     //   state.Health = Convert.ToInt16(localPlayer.Ped.Health);
                        state.Seat = localPlayer.GetVehicleSeat();
                        state.PedID = localPlayer.GetPedID();
                        state.WeaponID = localPlayer.GetWeaponID();
                        UI.Notify(state.Seat.ToString());
                        vehicle.UpdateSent(state.VehicleState);
                    }

                    currentSession.UpdateUserData(state);

                    //reset local user command
                    localPlayer.ResetClientFlags();
                }

                catch
                { }

                clUpdateTimer = DateTime.Now + TimeSpan.FromMilliseconds(ClUpdateRate);
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

                if (Enum.TryParse(native.FunctionName, out result))
                {
                    ExecuteNativeWithArgs(result, native.Args, native.ReturnType, native.NetID);
                }

                else if (long.TryParse(native.FunctionName, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out nResult))
                {
                    ExecuteNativeWithArgs((Hash)nResult, native.Args, native.ReturnType, native.NetID);
                }
            }

            #endregion

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
            //  Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);
        }

        private void ExecuteNativeWithArgs(Hash fHash, NativeArg[] args, DataType returnType, int callbackID)
        {
            var argsList = new List<InputArgument>();

            foreach (var arg in args)
            {
                switch (arg.Type)
                {
                    case DataType.Bool:
                        argsList.Add(new InputArgument(Convert.ToBoolean(arg.Value)));
                        break;
                    case DataType.Int:
                        argsList.Add(new InputArgument(Convert.ToInt32(arg.Value)));
                        break;
                    case DataType.String:
                        argsList.Add(new InputArgument(Convert.ToString(arg.Value)));
                        break;
                    case DataType.Float:
                        argsList.Add(new InputArgument(Convert.ToSingle(arg.Value)));
                        break;
                    case DataType.Double:
                        argsList.Add(new InputArgument(Convert.ToDouble(arg.Value)));
                        break;
                }
            }

            var fArgs = argsList.ToArray();

            object value = null;

            switch (returnType)
            {
                case DataType.Bool:
                    value = Function.Call<bool>(fHash, fArgs);
                    break;
                case DataType.Int:
                    value = Function.Call<int>(fHash, fArgs);
                    break;
                case DataType.String:
                    value = Function.Call<string>(fHash, fArgs);
                    break;
                case DataType.Float:
                    value = Function.Call<float>(fHash, fArgs);
                    break;
                case DataType.Double:
                    value = Function.Call<double>(fHash, fArgs);
                    break;
                default:
                case DataType.None:
                    Function.Call(fHash, fArgs);
                    break;
            }

            currentSession.SendNativeCallback(new NativeCallback(callbackID, value));
        }

        /// <summary>
        /// Dispose the script and related resources.
        /// </summary>
        /// <param name="A_0"></param>
        protected override void Dispose(bool A_0)
        {
            World.GetAllEntities().ToList().ForEach(x => {
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

        private static Queue<ClientState> lClientQueue =
        new Queue<ClientState>();

        private static Queue<KeyValuePair<ClientState, DateTime>> clientQueue =
            new Queue<KeyValuePair<ClientState, DateTime>>();

        private static List<NetworkPlayer> activeClients =
            new List<NetworkPlayer>();

        private static Queue<NetworkPlayer> clientDeletionQueue =
          new Queue<NetworkPlayer>();

        private static Queue<NetworkVehicle> vehicleDeletionQueue =
      new Queue<NetworkVehicle>();

        private static List<NetworkVehicle> activeVehicles =
         new List<NetworkVehicle>();

        public NetworkManager()
        {
            localPlayer = new LocalPlayer();
            Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!NetworkSession.Initialized) return;

            while (clientQueue.Count > 0)
            {
                // dequeue the client that needs an update.
                var remoteClient = clientQueue.Dequeue();

                var clientState = remoteClient.Key;
                NetworkPlayer client = PlayerFromID(clientState.ID);

                // the client doesn't exist
                if (client == null)
                {
                    client = new NetworkPlayer(clientState);
                    AddClient(client);
                }

                else
                {
                    if (clientState.PedID != 0 && client.GetPedID() != clientState.PedID ||
                        clientState.Health > 0 && client.Health <= 0)
                    {
                        ForceRemoveClient(client);
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

                            if (vehicle == null && vehicleState.Flags.HasFlag(VehicleFlags.Driver))
                            {

                                vehicle = AddVehicle(vehicleState);
                                client.ActiveVehicle = vehicle;
                                Function.Call(Hash.TASK_ENTER_VEHICLE, client.Handle, vehicle.Handle, -1, (int)clientState.Seat,
                                  0.0f, 3, 0);
                            }
                        }

                        if (!Function.Call<bool>((Hash)0xAE31E7DF9B5B132E, vehicle.Handle))
                        {
                            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, vehicle.Handle, true, true, 0);
                        }

                        if (!client.LastState.InVehicle)
                        {
                            UI.ShowSubtitle("not in vehicle last.. entering..");
                            Function.Call(Hash.TASK_ENTER_VEHICLE, client.Handle, vehicle.Handle, -1, (int)clientState.Seat, 1.0f, 3, 0);
                        }

                        if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, vehicle.Model.Hash))
                        {
                            client.SetBicycleState((BicycleState)vehicleState.ExtraFlags);
                        }

                        if (vehicleState.Flags.HasFlag(VehicleFlags.Driver))
                        vehicle.HandleUpdatePacket(vehicleState, clientState.PktID, remoteClient.Value);

                    }

                    // update entity location data.
                    client.HandleUpdatePacket(clientState, remoteClient.Value);
                }
            }

            while (lClientQueue.Count > 0)
            {
                var clientUpd = lClientQueue.Dequeue();

                if (clientUpd.Health <= 0 && Game.Player.Character.Health > -1)
                    Game.Player.Character.Health = -1;

                else
                {
                    if (Game.Player.Character.Health > clientUpd.Health)
                    {
                        Function.Call(Hash.APPLY_DAMAGE_TO_PED, Game.Player.Character.Handle,
                            Game.Player.Character.Health - clientUpd.Health, true);
                    }
                }
            }

            while (clientDeletionQueue.Count > 0)
            {
                var client = clientDeletionQueue.Dequeue();
                ForceRemoveClient(client);
            }

            while (vehicleDeletionQueue.Count > 0)
            {
                var vehicle = vehicleDeletionQueue.Dequeue();
                ForceRemoveVehicle(vehicle);
            }

            if (localPlayer != null)
            {
                localPlayer.Update();
            }

            foreach (NetworkPlayer client in activeClients)
            {
                client.Update();
            }

            foreach (NetworkVehicle vehicle in activeVehicles)
            {
                vehicle.Update();
            }
        }
    
      //  static Stopwatch sw = new Stopwatch();

        internal static void QueueClientUpdate(ClientState state, DateTime serverTime, bool isLocal)
        {
            if (isLocal) lClientQueue.Enqueue(state);
            else
                clientQueue.Enqueue(new KeyValuePair<ClientState, DateTime>(state, serverTime));
        }

        internal static List<NetworkPlayer> GetClients()
        {
            return activeClients;
        }

        internal static void AddClient(NetworkPlayer client)
        {
            activeClients.Add(client);
        }

        internal static NetworkPlayer PlayerFromID(int id)
        {
            return activeClients.Find(x => x.ID == id);
        }

        internal static NetworkPlayer PlayerFromLocalHandle(int handle)
        {
            return activeClients.Find(x => x.Handle == handle);
        }

        internal static NetworkVehicle VehicleFromID(int id)
        {
            return activeVehicles.FirstOrDefault(x => x.ID == id);
        }

        internal static NetworkVehicle VehicleFromLocalHandle(int handle)
        {
            return activeVehicles.FirstOrDefault(x => x.Handle == handle);
        }

        internal static NetworkVehicle AddVehicle(VehicleState state)
        {
            var hash = (int)Helpers.VehicleIDToHash(state.VehicleID);

            NetworkVehicle vehicle;

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
            {
                vehicle = new NetCar(state);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
            {
                vehicle = new NetHeli(state);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
            {
                vehicle = new NetPlane(state);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
            {
                vehicle = new NetBicycle(state);
            }

            else vehicle = new NetworkVehicle(state);

            activeVehicles.Add(vehicle);

            return vehicle;
        }

        internal static void RemoveClient(NetworkPlayer client)
        {
            clientDeletionQueue.Enqueue(client);
        }

        internal static void RemoveVehicle(NetworkVehicle vehicle)
        {
            vehicleDeletionQueue.Enqueue(vehicle);
        }

        internal static void ForceRemoveClient(NetworkPlayer client, bool removeFromWorld = true)
        {
            if (removeFromWorld)
            {
                client.MarkAsNoLongerNeeded();
                client.Remove();
            }
            activeClients.Remove(client);
        }

        internal static void ForceRemoveVehicle(NetworkVehicle vehicle, bool removeFromWorld = true)
        {
            if (removeFromWorld)
            {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Remove();
            }
            activeVehicles.Remove(vehicle);
        }

        internal static void ForceRemoveAllClients()
        {
            foreach (var client in activeClients)
            {
                ForceRemoveClient(client);
            }

            activeClients.Clear();
        }

        internal static void ForceRemoveAllVehicles()
        {
            foreach (var vehicle in activeVehicles)
            {
                ForceRemoveVehicle(vehicle);
            }

            activeVehicles.Clear();
        }
    }
}
