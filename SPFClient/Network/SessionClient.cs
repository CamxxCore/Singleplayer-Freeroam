using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using SPFLib.Types;
using SPFLib.Enums;
using SPFLib;
using Lidgren.Network;

namespace SPFClient.Network
{
    public delegate void ChatEventHandler(EndPoint sender, SessionMessage e);

    public delegate void SessionEventHandler(EndPoint sender, SessionEvent e);

    public delegate void SessionStateHandler(EndPoint sender, SessionState e);

    public delegate void SessionSyncHandler(EndPoint sender, SessionSync e);

    public delegate void NativeInvocationHandler(EndPoint sender, NativeCall e);

    public delegate void RankDataHandler(EndPoint sender, RankData e);

    public class SessionClient : IDisposable
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

        /// fired when a user in the session triggers a client event
        public event SessionEventHandler SessionEvent;

        /// fired when the server sends a state update
        public event SessionStateHandler SessionStateEvent;

        /// fired when the server invokes a client native
        internal event NativeInvocationHandler NativeInvoked;

        /// fired when the server invokes a time sync
        internal event SessionSyncHandler SessionSyncEvent;

        /// fired when the server sends new rank data
        internal event RankDataHandler RankDataEvent;

        NetIncomingMessage message;

        public SessionClient(IPAddress remoteAddress, int port)
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
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.LoginRequest);
            msg.Write(req);
            client.Connect(serverEP, msg);
        }

        public void SendSynchronizationAck()
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.AckWorldSync);
            client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
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

        public void UpdateUserData(ClientState state, uint sequence)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.ClientState);
            msg.Write(sequence);
            msg.Write(state, false);
            client.SendMessage(msg, NetDeliveryMethod.Unreliable);
        }

        public void UpdateUserData(ClientState state, AIState[] ai, uint sequence)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.ClientStateAI);
            msg.Write(sequence);
            msg.Write(state, false);
            msg.Write(ai.Length);
            foreach (var aiPlayer in ai)
                msg.Write(aiPlayer, false);
            client.SendMessage(msg, NetDeliveryMethod.Unreliable);
        }

        public void SendWeaponData(short dmg, Vector3 hitCoords)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.WeaponData);
            msg.Write(NetworkTime.Now.Ticks);
            msg.Write(hitCoords.X);
            msg.Write(hitCoords.Y);
            msg.Write(hitCoords.Z);
            msg.Write(dmg);
            client.SendMessage(msg, NetDeliveryMethod.ReliableSequenced);
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
            while ((message = client.ReadMessage()) != null)
            {
                if (message.MessageType == NetIncomingMessageType.Data)
                    HandleIncomingDataMessage(message);
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
            msg.Write((short)cb.Type);
            if (cb.Type != DataType.None && cb.Value != null)
                msg.Write(Serializer.SerializeObject(cb.Value));
            client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        internal void ReturnSessionSync(SessionSync req)
        {
            req.ClientTime = NetworkTime.Now;
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionSync);
            msg.Write(req);
            client.SendMessage(msg, NetDeliveryMethod.Unreliable);
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
