#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using GTA;
using GTA.Native;
using ASUPService;
using SPFLib.Types;
using SPFLib.Enums;
using System.Threading;
using SPFClient.UI;

namespace SPFClient.Network
{
    public class ClientSession : Script
    {
        #region var

        /// <summary>
        /// Default port used for server connections (default: 27852)
        /// </summary>
        private const int ServerPort = 27852;

        /// <summary>
        /// Update rate for local packets sent to the server.
        /// </summary>
        private const int SendRate = 14;

        /// <summary>
        /// Total idle time before the client is considered to have timed out from the server.
        /// </summary>
        private const int ClientTimeout = 5000;

        #endregion

        private static int UID;

        public static bool Initialized { get; private set; } = false;

        private static int lastSync = 0;

        private static bool firstSync = true;

        public static Client Current { get { return current; } }
        private static Client current;

        private static Queue<NativeCall> queuedNativeCalls = new Queue<NativeCall>();

        private static Queue<RankData> queuedRankData = new Queue<RankData>();

        private static DateTime connectionTimeout = new DateTime();

        private static Queue<SessionMessage> msgQueue =
            new Queue<SessionMessage>();

        public static uint LastSequence { get; private set; }

        public static uint PacketsSent { get; private set; }

        public ClientSession()
        {
            UID = Guid.NewGuid().GetHashCode(); //TempID.GetUniqueMachineID();
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
            if (current != null) Close();

            current = new Client(new IPAddress(session.Address), ServerPort);
            current.ChatEvent += ChatEvent;
            current.SessionStateEvent += SessionStateEvent;
            current.NativeInvoked += NativeInvoked;
            current.SessionEvent += SessionEvent;
            current.RankDataEvent += RankDataEvent;
            current.OnDisconnect += OnDisconnected;

            if (current.Inititialize(UID, Game.Player.Name ?? "Unknown Player"))
            {
                current.Login();

                World.GetAllEntities().ToList().ForEach(x =>
                    {
                        if ((x is Ped || x is Vehicle) &&
                        x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
                        { x.Delete(); }
                    });

                Function.Call((Hash)0x231C8F89D0539D8F, 0, 1);

                Function.Call(Hash.IGNORE_NEXT_RESTART, true);

                ClientManager.LocalPlayer.Setup();
                // wait for server callback and handle initialization there.
            }

            else
            {
                GTA.UI.Notify(string.Format("~r~Connection error. The server is offline or UDP port ~y~{0} ~r~is not properly forwarded.", ServerPort));
                Initialized = false;
            }
        }

        /// <summary>
        /// Join a session directly.
        /// </summary>
        /// <param name="session"></param>
        public static void JoinSessionDirect(IPAddress address)
        {
            ActiveSession session = new ActiveSession()
            {
                Address = address.GetAddressBytes()
            };

            JoinActiveSession(session);
        }

        /// <summary>
        /// Close the session gracefully. 
        /// Unsubscribe from server events and remove all entities from the world.
        /// </summary>
        public static void Close()
        {
            UIManager.ShowNotification("Leaving session...");

            Initialized = false;

            ClientManager.DeleteAll();
            ClientManager.Enabled = false;

            VehicleManager.DeleteAll();
            VehicleManager.Enabled = false;

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

            connectionTimeout = new DateTime();

            LastSequence = 0;

            firstSync = true;
        }

        /// <summary>
        /// Session state received handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SessionStateEvent(EndPoint sender, SessionState e)
        {
            if (!Initialized) return;

            if ((e.Sequence > LastSequence && 
                e.Sequence - lastSync <= uint.MaxValue / 2 || 
                LastSequence > e.Sequence && 
                LastSequence - e.Sequence > uint.MaxValue / 2) ||
                LastSequence == 0)
            {
                for (int i = 0; i < e.Vehicles.Length; i++)
                {
                    VehicleManager.QueueUpdate(e.Vehicles[i], e.Timestamp);
                }

                for (int i = 0; i < e.Clients.Length; i++)
                {
                    ClientManager.QueueUpdate(e.Clients[i], e.Timestamp);
                }

                ClientManager.QueueLocalUpdate(e.LocalHealth);

                LastSequence = e.Sequence;
            }

            connectionTimeout = DateTime.Now + 
                TimeSpan.FromMilliseconds(ClientTimeout);
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
                UIManager.ShowNotification(e.Message);
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
        /// Rank data handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void RankDataEvent(EndPoint sender, RankData e)
        {
            queuedRankData.Enqueue(e);
        }

        /// <summary>
        /// User event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SessionEvent(EndPoint sender, SessionEvent e)
        {
            switch (e.EventType)
            {
                case SessionEventType.PlayerKicked:
                case SessionEventType.PlayerTimeout:
                case SessionEventType.PlayerLogout:
                    var player = ClientManager.PlayerFromID(e.ID);
                    if (e.ID == UID) return;
                    if (player != null)
                    {
                        ClientManager.Delete(player);
                        if (player.ActiveVehicle != null)
                            VehicleManager.Delete(player.ActiveVehicle);
                    }

                    UIManager.ShowNotification(e.SenderName + Helpers.GetSessionEventString(e.EventType));
                    break;
                case SessionEventType.PlayerSynced:
                    if (e.ID == UID)
                    {
                        ClientManager.Enabled = true;
                        VehicleManager.Enabled = true;
                        Initialized = true;
                        UIManager.ShowNotification("Successfully Connected.");
                    }
                    UIManager.ShowNotification(e.SenderName + " joined. " + e.ID);
                    break;
                case SessionEventType.VehicleDeleted:
                    var vehicle = VehicleManager.VehicleFromID(e.ID);
                    if (vehicle != null) VehicleManager.Delete(vehicle);
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
            if (!Initialized || current == null) return;

            if (connectionTimeout.Ticks > 0 && DateTime.Now >= connectionTimeout)
            {
                GTA.UI.Notify("Lost connection to the server.");
                Close();
            }

            try
            {
                if (Game.GameTime - lastSync >= SendRate)
                {
                    PacketsSent++;
                    PacketsSent %= uint.MaxValue;

                    var localPlayer = ClientManager.LocalPlayer;

                    var state = localPlayer.GetClientState();

                    state.ClientID = UID;

                    if (state != null)
                    {
                        current.UpdateUserData(state, localPlayer.GetVehicleState(), PacketsSent);
                    }

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

                    var callback = NativeHelper.ExecuteLocalNativeWithArgs(native);

                    if (callback != null)
                        current.SendNativeCallback(callback);
                }

                while (queuedRankData.Count > 0)
                {
                    var rData = queuedRankData.Dequeue();
                    UIManager.RankBar.ShowRankBar(rData.RankIndex, rData.RankXP, rData.NewXP, 116, 3000, 2000);
                }

                #endregion

                if (LastSequence > 0 && firstSync)
                {
                    if (current != null)
                    {
                        current.RequestNameSync();
                        current.SendAck(AckType.WorldSync, null);
                    }

                    firstSync = false;
                }

                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_WANTED_LEVEL_MULTIPLIER, 0f);
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);

                Game.Player.WantedLevel = 0;
            }

            catch (Exception ex)

            {// Get stack trace for the exception with source file information
                var st = new System.Diagnostics.StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();
                Logger.Log(ex.StackTrace.ToString() + " line: " + line);
                GTA.UI.Notify(ex.ToString() + " line: " + line);
            }
        }

        private static void OnDisconnected(EndPoint sender, string message)
        {
            switch (message)
            {
                case "NC_TOOFREQUENT":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~The connection was refused.\nYou are connecting too frequently.");
                    break;
                case "NC_REVMISMATCH":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~Server refused the connection.\nClient/ server version mismatch.");
                    break;
                case "NC_INVALID":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~Server refused the connection. Failed to login the user.");
                    break;
                case "NC_LOBBYFULL":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~The lobby is full.");
                    break;
                case "NC_GENERICKICK":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~Youhave been kicked.");
                    break;
                case "NC_TIMEOUT":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~Connection timed out.");
                    break;
                case "NC_IDLEKICK":
                    UIManager.ShowNotification("~y~Lost connection to the session.\n~w~You were kicked for being idle too long.");
                    break;
            }
        }

        /// <summary>
        /// Dispose the script and related resources.
        /// </summary>
        /// <param name="A_0"></param>
        protected override void Dispose(bool A_0)
        {
            World.GetAllEntities().ToList().ForEach(x =>
            {
                if (x is Ped || x is Vehicle)
                { x.Delete(); }
            });

            // disable snow
            MemoryAccess.SetSnowEnabled(false);

            Function.Call((Hash)0xAEEDAD1420C65CC0, false);

            Function.Call((Hash)0x4CC7F0FEA5283FE0, false);

            base.Dispose(A_0);
        }
    }
}
