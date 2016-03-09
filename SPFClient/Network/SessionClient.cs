using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using SPFLib.Types;
using SPFLib.Enums;
using SPFLib;

namespace SPFClient.Network
{
    public delegate void ChatEventHandler(EndPoint sender, MessageInfo e);

    public delegate void SessionEventHandler(EndPoint sender, UserEvent e);

    public delegate void SessionStateHandler(EndPoint sender, SessionState e);

    public delegate void ServerEchoHandler(EndPoint sender, TimeSync e);

    public delegate void ServerHelloHandler(EndPoint sender, ServerHello e);

    public delegate void ServerNotificationHandler(EndPoint sender, ServerNotification e);

    public delegate void NativeInvocationHandler(EndPoint sender, NativeCall e);

    public class SessionClient
    {
        private bool didInitCallback = false;

        private int port;
        private IPAddress remoteAddress;
        private Socket clientSocket;
        private EndPoint epServer;

        private List<ServerCommand> pendingCMDCallbacks;

        private Dictionary<int, byte[]> pendingCallbacks;

        /// fired when a user in the session sends a chat message
        public event ChatEventHandler ChatEvent;

        /// fired when a user in the session triggers a client event
        public event SessionEventHandler UserEvent;

        /// fired when the server sends a state update
        public event SessionStateHandler SessionStateEvent;

        /// fired when the server invokes a client native
        internal event NativeInvocationHandler NativeInvoked;

        /// fired when the server invokes a time sync
        internal event ServerEchoHandler TimeSyncEvent;

        /// fired when the server send a text notification
      //  public event ServerNotificationHandler ServerNotification;

        /// fired when the server send a hello
        internal event ServerHelloHandler ServerHelloEvent;

        private byte[] byteData = new byte[1024];

        public SessionClient(IPAddress remoteAddress, int port)
        {
            this.remoteAddress = remoteAddress;
            this.port = port;
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            pendingCallbacks = new Dictionary<int, byte[]>();
          
            epServer = new IPEndPoint(remoteAddress, port);
        }

        public void Login(int uid, string name)
        {
            try
            {
                ServerCommand req = new ServerCommand();
                req.UID = uid;
                req.Name = name;
                req.Command = CommandType.Login;
                var packedData = req.ToByteArray();
                clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                    new AsyncCallback(OnSend), packedData);

                var timer = new Timer(new TimerCallback(x =>
                {
                    // keep sending until we receive a callback
                    if (!didInitCallback)//   if (pendingCMDCallbacks.Contains(req)) 
                        clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                        new AsyncCallback(OnSend), packedData);
                }),
                null, 1000, 1000);

                pendingCMDCallbacks.Add(req);
            }

            catch (SocketException ex)
            {
                Console.WriteLine("Failed to login due to a connection error.\n" + ex.Message);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Failed to login.\n" + ex.Message);
            }
        }

        public void Say(string message)
        {
            try
            {
                MessageInfo msg = new MessageInfo();
                msg.Message = message;
                msg.Timestamp = DateTime.UtcNow;
                var packedData = msg.ToByteArray();
                clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                    new AsyncCallback(OnSend), packedData);

                var timer = new Timer(new TimerCallback(x => {
                    // keep sending until we receive a callback
                    if (pendingCallbacks.ContainsKey(msg.NetID))
                        clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                        new AsyncCallback(OnSend), packedData);
                }), null, 1000, 1000);

                pendingCallbacks.Add(msg.NetID, packedData);
            }

            catch (SocketException ex)
            {
                Console.WriteLine("Socket error.\n" + ex.Message);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Failed to send the message.\n" + ex.Message);
            }
        }

        public void UpdateUserData(ClientState clientState)
        {
            try
            {
                var packedData = clientState.ToByteArray();
                clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                    new AsyncCallback(OnSend), packedData);
            }

            catch (SocketException ex)
            {
                Console.WriteLine("Failed to send the data due to a connection error.\n" + ex.Message);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Failed to send the data.\n" + ex.Message);
            }
        }

        public void SendWeaponHit(short dmg, Vector3 hitCoords)
        {
            WeaponHit wh = new WeaponHit(hitCoords);
            wh.Timestamp = PreciseDatetime.Now;
            wh.WeaponDamage = dmg;
            var packedData = wh.ToByteArray();
            clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                new AsyncCallback(OnSend), packedData);

            var timer = new Timer(new TimerCallback(x => {
                // keep sending until we receive a callback
                if (pendingCallbacks.ContainsKey(wh.NetID))
                    clientSocket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, epServer,
                    new AsyncCallback(OnSend), packedData);
            }),
            null, 1000, 1000);

            pendingCallbacks.Add(wh.NetID, packedData);
        }

        public bool StartListening()
        {
            try
            {
                //Start listening to the data asynchronously
                clientSocket.BeginReceiveFrom(byteData, 0, byteData.Length, SocketFlags.None, ref epServer,
                    new AsyncCallback(OnReceive), byteData);
                return true;
            }

            catch (SocketException)
            {
                return false;
            }

            catch (Exception)
            {
                return false;
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                clientSocket.EndReceiveFrom(ar, ref sender);

                var msg = (NetMessage)byteData[0];

                switch (msg)
                {
                    case NetMessage.SessionUpdate:
                        OnSessionUpdate(sender, new SessionState(byteData));
                        break;
                    case NetMessage.ChatMessage:
                        OnChatEvent(sender, new MessageInfo(byteData));
                        break;
                    case NetMessage.UserEvent:
                        OnUserEvent(sender, new UserEvent(byteData));
                        break;
                    case NetMessage.TimeSync:
                        OnServerTimeSync(sender, new TimeSync(byteData));
                        break;
                    case NetMessage.NativeCall:
                        OnNativeInvoke(sender, new NativeCall(byteData));
                        break;
                  /*  case NetMessage.ServerNotification:
                        OnServerNotification(sender, new ServerNotification(byteData));
                        break;*/
                    case NetMessage.ServerHello:
                        OnServerHello(sender, new ServerHello(byteData));
                        break;
                    case NetMessage.SimpleCallback:
                        HandleSimpleCallback(sender, new GenericCallback(byteData));
                        break;
                }
            }

            catch (SocketException ex)
            {
                Console.WriteLine("Failed while recieving the data.\n" + ex.ToString());
            }

            catch (Exception ex)
            {
                Console.WriteLine("Failed while recieving the data.\n" + ex.ToString());
            }

            finally
            {
                try
                {
                    Array.Clear(byteData, 0, byteData.Length);
                    clientSocket.BeginReceiveFrom(byteData, 0, byteData.Length, SocketFlags.None, ref epServer,
                                               new AsyncCallback(OnReceive), byteData);
                }

                catch (Exception)
                {
                }
            }
        }

        protected virtual void OnChatEvent(EndPoint sender, MessageInfo msg)
        {
            ChatEvent?.Invoke(sender, msg);
        }

        protected virtual void OnSessionUpdate(EndPoint sender, SessionState msg)
        {
            SessionStateEvent?.Invoke(sender, msg);
        }

        protected virtual void OnUserEvent(EndPoint sender, UserEvent msg)
        {
            UserEvent?.Invoke(sender, msg);
        }

        protected virtual void OnServerTimeSync(EndPoint sender, TimeSync msg)
        {
            SendTimeSync(msg);
            TimeSyncEvent?.Invoke(sender, msg);
        }

        protected virtual void OnNativeInvoke(EndPoint sender, NativeCall msg)
        {
            NativeInvoked?.Invoke(sender, msg);
        }

       /* protected virtual void OnServerNotification(EndPoint sender, ServerNotification msg)
        {
            ServerNotification?.Invoke(sender, msg);
        }*/

        protected virtual void OnServerHello(EndPoint sender, ServerHello msg)
        {
            SendGenericCallback(new GenericCallback(msg.NetID));
            ServerHelloEvent?.Invoke(sender, msg);    
        }

        public void SendNativeCallback(NativeCallback cb)
        {
            var msgBytes = cb.ToByteArray();
            clientSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, epServer,
                new AsyncCallback(OnSend), msgBytes);
        }

        public void SendGenericCallback(GenericCallback cb)
        {
            var msgBytes = cb.ToByteArray();
            clientSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, epServer,
                new AsyncCallback(OnSend), msgBytes);
        }

        public void SendTimeSync(TimeSync req)
        {
            req.ClientTime = PreciseDatetime.Now;
            var msgBytes = req.ToByteArray();
            clientSocket.BeginSendTo(msgBytes, 0, msgBytes.Length, SocketFlags.None, epServer,
                new AsyncCallback(OnSend), msgBytes);
        }

        private void HandleSimpleCallback(EndPoint sender, GenericCallback callback)
        {
            if (pendingCallbacks.ContainsKey(callback.NetID))
                pendingCallbacks.Remove(callback.NetID);
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndSend(ar);
            }

            catch (ObjectDisposedException)
            { }

            catch (Exception)
            { }
        }

        public void StopListening()
        {
            //clientSocket.Disconnect(true);
            clientSocket.Close(50);
        }
    }
}
