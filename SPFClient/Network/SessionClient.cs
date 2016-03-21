using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
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

    public delegate void SessionNotificationHandler(EndPoint sender, SessionNotification e);

    public delegate void NativeInvocationHandler(EndPoint sender, NativeCall e);

    public class SessionClient : IDisposable
    {
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

        NetIncomingMessage message;

        public SessionClient(IPAddress remoteAddress, int port)
        {
            serverEP = new IPEndPoint(remoteAddress, port);
            Config = new NetPeerConfiguration("spfsession");
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            client = new NetClient(Config);
        }

        public void Login(int uid, string name)
        {
            SessionCommand req = new SessionCommand();
            req.UID = uid;
            req.Name = name;
            req.Command = CommandType.Login;
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionCommand);
            msg.Write(req);
            client.Connect(serverEP, msg);
        }

        public void Say(string message)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.SessionMessage);
            msg.WriteTime(false);
            msg.Write(message);
            client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void UpdateUserData(ClientState state)
        {
            var msg = client.CreateMessage();
            msg.Write((byte)NetMessage.ClientState);
            msg.Write(state);
            client.SendMessage(msg, NetDeliveryMethod.ReliableSequenced);
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

        public bool StartListening()
        {
            try
            {
                client.Start();
                return true;
            }

            catch (ArgumentNullException)
            {
            }

            catch (SocketException ex)
            {
                Console.WriteLine("Failed to receive the data due to a connection error.\n" + ex.Message);
            }

            catch (ArgumentOutOfRangeException)
            {
            }

            catch (ObjectDisposedException)
            {
            }

            catch (Exception)
            {
            }

            return false;
        }

        /// <summary>
        /// Process and handle any data received from the client.
        /// </summary>
        /// <param name="ar"></param>
        public void OnReceive()
        {
            while ((message = client.ReadMessage()) != null)
            {
                switch (message.MessageType)
                {
                    case NetIncomingMessageType.Data:
                        HandleIncomingDataMessage(message);
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        SPFClient.UI.UIManager.UISubtitleProxy("status " + message.SenderConnection.Status.ToString());
                        break;
                }
            }
        }

        private void HandleIncomingDataMessage(NetIncomingMessage msg)
        {
            var dataType = (NetMessage)message.ReadByte();

            switch (dataType)
            {
                case NetMessage.SessionUpdate:
                    OnSessionUpdate(msg.SenderEndPoint, msg.ReadSessionState());
                    break;
                case NetMessage.SessionMessage:
                    OnSessionMessage(msg.SenderEndPoint, msg.ReadSessionMessage());
                    break;
                case NetMessage.SessionEvent:
                    OnSessionEvent(msg.SenderEndPoint, msg.ReadSessionEvent());
                    break;
                case NetMessage.SessionSync:
                    OnServerSessionSync(msg.SenderEndPoint, msg.ReadSessionSync());
                    break;
                case NetMessage.NativeCall:
                    OnNativeInvoked(msg.SenderEndPoint, msg.ReadNativeCall());
                    break;
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
            SessionStateEvent?.Invoke(sender, msg);
        }

        protected virtual void OnSessionMessage(EndPoint sender, SessionMessage msg)
        {
            ChatEvent?.Invoke(sender, msg);
        }

        protected virtual void OnServerSessionSync(EndPoint sender, SessionSync msg)
        {
            ReturnSessionSync(msg);
            SessionSyncEvent?.Invoke(sender, msg);
        }

        protected virtual void OnNativeInvoked(EndPoint sender, NativeCall msg)
        {
            NativeInvoked?.Invoke(sender, msg);
        }

        protected virtual void OnSessionEvent(EndPoint sender, SessionEvent msg)
        {
            SessionEvent?.Invoke(sender, msg);
        }
     
        public void Close()
        {
            client.Disconnect("NC_GRACEFUL_DISCONNECT");
        }

        public void Dispose()
        {
            client.Shutdown("NC_DISCONNECT");
        }
    }
}
