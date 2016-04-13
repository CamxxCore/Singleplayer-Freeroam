using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using SPFLib.Types;
using SPFLib.Enums;
using SPFLib;
using System.Reflection;
using Lidgren.Network;

namespace SPFClient.Network
{
    public delegate void ChatEventHandler(EndPoint sender, SessionMessage e);

    public delegate void SessionEventHandler(EndPoint sender, SessionEvent e);

    public delegate void SessionStateHandler(EndPoint sender, SessionState e);

    public delegate void SessionSyncHandler(EndPoint sender, SessionSync e);

    public delegate void SessionAckHandler(EndPoint sender, SessionAck e);

    public delegate void NativeInvocationHandler(EndPoint sender, NativeCall e);

    public delegate void RankDataHandler(EndPoint sender, RankData e);

    public delegate void DisconnectEventHandler(EndPoint sender, string message);

    public class Client : IDisposable
    {

        public NetClient Connection
        {
            get
            {
                return client;
            }
        }

        private int localUID;
        private string localUsername;

        private IPEndPoint serverEP;
        private NetClient client;

        private readonly NetPeerConfiguration Config;

        /// fired when a user in the session sends a chat message
        public event ChatEventHandler ChatEvent;

        /// fired when the server triggers a session wide event
        public event SessionEventHandler SessionEvent;

        /// fired when the server sends a state update
        public event SessionStateHandler SessionStateEvent;

        /// fired when the server invokes a client native
        internal event NativeInvocationHandler NativeInvoked;

        /// fired when the server invokes a time sync
        internal event SessionSyncHandler SessionSyncEvent;

        /// fired when the server sends new rank data
        internal event RankDataHandler RankDataEvent;

        /// fired when the server has refused the local connection
        internal event DisconnectEventHandler OnDisconnect;

        NetIncomingMessage message;

        public Client(IPAddress remoteAddress, int port)
        {
            serverEP = new IPEndPoint(remoteAddress, port);
            Config = new NetPeerConfiguration("spfsession");       
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            client = new NetClient(Config);
        }

        public void Login()
        {          
            LoginRequest req = new LoginRequest();
            req.UID = localUID;
            req.Username = localUsername;
            req.Revision = Assembly.GetExecutingAssembly().GetName().Version.Build;
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.LoginRequest);
            msg.Write(req);
            client.Connect(serverEP, msg);
        }

        public void SendAck(AckType type, object value)
        {
            SessionAck ack = new SessionAck(type, value);
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.Acknowledgement);
            msg.Write(ack);
            client.SendMessage(msg, NetDeliveryMethod.ReliableUnordered);
        }

        public void Say(string message)
        {
            if (message.Length <= 0 || message.Length >= 50) return;
            SessionMessage sMessage = new SessionMessage();
            sMessage.SenderName = localUsername;
            sMessage.Timestamp = NetworkTime.Now;
            sMessage.Message = message;
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionMessage);
            msg.Write(sMessage);
            client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void UpdateUserData(ClientState state, VehicleState vehicle, uint sequence)
        {
            NetMessage type = vehicle == null ? NetMessage.ClientState : NetMessage.VehicleState;
            var msg = client.CreateMessage();
            msg.Write((byte)type);
            msg.Write(sequence);
            msg.Write(state, false);
            if (type == NetMessage.VehicleState)
            msg.Write(vehicle);
            client.SendMessage(msg, NetDeliveryMethod.Unreliable);
        }

        public void RequestNameSync()
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionCommand);
            msg.Write(new SessionCommand(CommandType.GetClientNames));
            client.SendMessage(msg, NetDeliveryMethod.ReliableSequenced);
        }

        public void SendImpactData(ImpactData data)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.WeaponData);
            msg.Write(data);
            client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public bool Inititialize(int uid, string username)
        {
            bool init = false;

            try
            {
                client.Start();
                client.RegisterReceivedCallback(new SendOrPostCallback(OnReceive), SynchronizationContext.Current);
                init = true;
            }

            catch (ArgumentNullException ex)
            {
                init = false;
                Logger.Log("Failed to receive the data due to an error.\n" + ex.Message);
            }

            catch (SocketException ex)
            {
                init = false;
                Logger.Log("Failed to receive the data due to a connection error.\n" + ex.Message);
            }

            catch (ArgumentOutOfRangeException ex)
            {
                init = false;
                Logger.Log("Failed to receive the data due to an error.\n" + ex.Message);
            }

            catch (ObjectDisposedException ex)
            {
                init = false;
                Logger.Log("Failed to receive the data due to an error.\n" + ex.Message);
            }

            catch (Exception ex)
            {
                init = false;
                Logger.Log("Failed to receive the data due to an error.\n" + ex.Message);
            }

            finally
            {
                if (init)
                {
                    localUID = uid;
                    localUsername = username;
                }
            }

            return init;
        }

        /// <summary>
        /// Process and handle any data received from the server.
        /// </summary>
        /// <param name="ar"></param>
        public void OnReceive(object state)
        {
            try
            {
                while ((message = client.ReadMessage()) != null)
                {
                    if (message.MessageType == NetIncomingMessageType.Data)
                        HandleIncomingDataMessage(message);

                    else if (message.MessageType == NetIncomingMessageType.StatusChanged)
                    {
                        var status = (NetConnectionStatus)message.ReadByte();


                        if (status == NetConnectionStatus.Disconnected)
                        {
                            string reason = message.ReadString();
                            if (string.IsNullOrEmpty(reason))
                                return;

                            OnDisconnect?.Invoke(message.SenderConnection.RemoteEndPoint, reason);
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                Logger.Log("Exception while receiving the message. " + ex.ToString());
            }
        }

        private void HandleIncomingDataMessage(NetIncomingMessage msg)
        {
            try
            {
                var dataType = (NetMessage)message.ReadByte();

                switch (dataType)
                {
                    case NetMessage.SessionUpdate:
                        OnSessionUpdate(msg.SenderEndPoint, msg.ReadSessionState());
                        return;
                    case NetMessage.SessionMessage:
                        OnSessionMessage(msg.SenderEndPoint, msg.ReadSessionMessage());
                        return;
                    case NetMessage.SessionEvent:
                        OnSessionEvent(msg.SenderEndPoint, msg.ReadSessionEvent());
                        return;
                    case NetMessage.SessionSync:
                        OnServerSessionSync(msg.SenderEndPoint, msg.ReadSessionSync());
                        return;
                    case NetMessage.NativeCall:
                        OnNativeInvoked(msg.SenderEndPoint, msg.ReadNativeCall());
                        return;
                    case NetMessage.RankData:
                        OnRankDataReceived(msg.SenderEndPoint, msg.ReadRankData());
                        return;       
                }
            }

            catch
            {
            }
        }

        public void SendNativeCallback(NativeCallback cb)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.NativeCallback);
            msg.Write(cb);
            client.SendMessage(msg, NetDeliveryMethod.ReliableSequenced);
        }

        internal void ReturnSessionSync(SessionSync req)
        {
            req.ClientTime = NetworkTime.Now;
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionSync);
            msg.Write(req);
            client.SendMessage(msg, NetDeliveryMethod.ReliableUnordered);
        }

        protected virtual void OnSessionUpdate(EndPoint sender, SessionState msg)
        {
            if (msg == null) return;
            SessionStateEvent?.Invoke(sender, msg);
        }

        protected virtual void OnSessionMessage(EndPoint sender, SessionMessage msg)
        {
            if (msg == null) return;
            ChatEvent?.Invoke(sender, msg);
        }

        protected virtual void OnServerSessionSync(EndPoint sender, SessionSync msg)
        {
            if (msg == null) return;
            ReturnSessionSync(msg);
            SessionSyncEvent?.Invoke(sender, msg);
        }

        protected virtual void OnNativeInvoked(EndPoint sender, NativeCall msg)
        {
            if (msg == null) return;
            NativeInvoked?.Invoke(sender, msg);
        }

        protected virtual void OnRankDataReceived(EndPoint sender, RankData msg)
        {
            if (msg == null) return;
            RankDataEvent?.Invoke(sender, msg);
        }

        protected virtual void OnSessionEvent(EndPoint sender, SessionEvent msg)
        {
            if (msg == null) return;
            SessionEvent?.Invoke(sender, msg);
        }

        public void Close()
        {
            client.UnregisterReceivedCallback(new SendOrPostCallback(OnReceive));
            client.Disconnect("NC_GRACEFUL_DISCONNECT");
        }

        public void Dispose()
        {
            client.UnregisterReceivedCallback(new SendOrPostCallback(OnReceive));
            client.Shutdown("NC_DISCONNECT");
        }
    }
}
