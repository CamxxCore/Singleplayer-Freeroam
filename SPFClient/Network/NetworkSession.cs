﻿#define DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using GTA;
using GTA.Native;
using ASUPService;
using SPFLib.Types;
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

        private static int lastSync = 0;

        public static SessionClient Current { get { return current; } }
        private static SessionClient current;

        private static Queue<NativeCall> queuedNativeCalls = new Queue<NativeCall>();

        private static Queue<RankData> queuedRankData = new Queue<RankData>();

        private static DateTime disconnectTimeout = new DateTime();

        private static Queue<SessionMessage> msgQueue =
            new Queue<SessionMessage>();

        private static uint lastSequence;

        public static uint PacketsSent { get; private set; }

        public NetworkSession()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            UIChat.MessageSent += MessageSent;
            EntityManager.WorldSynced += WorldSynced;
            Tick += OnTick;
        }

        private void WorldSynced(object sender, EventArgs e)
        {
            current?.SendSynchronizationAck();
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

                EntityManager.LocalPlayer.Setup();
                // wait for server callback and handle initialization there.
            }

            else
            {
                GTA.UI.Notify(string.Format("~r~Connection error. The server is offline or UDP port ~y~{0} ~r~is not properly forwarded.", Port));
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

            EntityManager.DeleteAllEntities();

            Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);

            Function.Call(Hash._DISABLE_AUTOMATIC_RESPAWN, true);

            disconnectTimeout = new DateTime();

            lastSequence = 0;
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
                    EntityManager.QueueClientUpdate(client, e.Timestamp, client.ClientID == UID);
                }

                foreach (var ai in e.AI)
                {
                    EntityManager.QueueAIUpdate(ai, e.Timestamp);
                }

                lastSequence = e.Sequence;
                EntityManager.HostingAI = e.AIHost;
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
            var client = EntityManager.PlayerFromID(e.SenderID);

            switch (e.EventType)
            {
                case EventType.PlayerLogon:
                    if (e.SenderID == UID)
                    {
                        Initialized = true;
                        EntityManager.Enabled = true;
                        UIManager.UINotifyProxy("Successfully Connected.");
                    }
                    else
                        UIManager.UINotifyProxy(e.SenderName + " joined. " + e.SenderID.ToString());
                    break;

                case EventType.PlayerLogout:
                    if (e.SenderID == UID) return;
                    if (client != null)
                    {
                        EntityManager.DeleteClient(client);
                        if (client.ActiveVehicle != null)
                            EntityManager.DeleteVehicle(client.ActiveVehicle);
                    }
                    UIManager.UINotifyProxy(e.SenderName + " left. " + e.SenderID.ToString());
                    break;

                case EventType.PlayerKicked:
                    if (e.SenderID == UID) return;
                    if (client != null) EntityManager.DeleteClient(client);
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

            try
            {
                if (Game.GameTime - lastSync >= ClUpdateRate)
                {
                    PacketsSent++;
                    PacketsSent %= uint.MaxValue;

                    var localPlayer = EntityManager.LocalPlayer;

                    var state = localPlayer.GetClientState();

                    if (EntityManager.HostingAI)
                    {
                        var localAI = EntityManager.GetAIForUpdate().ToArray();

                        if (localAI.Length > 0)
                        {
                            current.UpdateUserData(state, localAI, PacketsSent);
                        }

                        else
                            current.UpdateUserData(state, PacketsSent);
                    }
                    else
                        current.UpdateUserData(state, PacketsSent);

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
                    UIManager.RankBar.ShowRankBar(rData.RankIndex, rData.RankXP, rData.NewXP, 123, 3000, 2000);
                }

                #endregion

                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0.0f);

                Function.Call(Hash.SET_WANTED_LEVEL_MULTIPLIER, 0f);
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);

                Game.Player.WantedLevel = 0;

            }

            catch (Exception ex)
            {
                UIManager.UINotifyProxy("Exception Details:\n" + ex.ToString());
            }
        }

        private static void OnDisconnected(EndPoint sender, string message)
        {
            switch (message)
            {
                case "NC_TOOFREQUENT":
                    UIManager.UINotifyProxy("Server refused the connection\nYou are connecting too frequently.");
                    break;
                case "NC_REVMISMATCH":
                    UIManager.UINotifyProxy("Server refused the connection.\nClient/ server version mismatch.");
                    break;
                case "NC_INVALID":
                    UIManager.UINotifyProxy("Server refused the connection. Failed to login the user.");
                    break;
                case "NC_LOBBYFULL":
                    UIManager.UINotifyProxy("The lobby is full.");
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
                if ((x is Ped || x is Vehicle) &&
                x.Handle != Game.Player.Character.CurrentVehicle?.Handle)
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
