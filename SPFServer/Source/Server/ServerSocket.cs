using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;

namespace SPFServer.Main
{
    public delegate void NetMessageReceivedHandler(NetConnection sender, NetIncomingMessage message);

    public class ServerSocket
    {
        private NetServer server;

        private NetIncomingMessage message;

        public NetPeerConfiguration NetConfig { get; private set; }

        public event NetMessageReceivedHandler OnMessageReceived;

        public ServerSocket() : this(27852)
        { }

        public ServerSocket(int port)
        {
            NetConfig = new NetPeerConfiguration("spfsession");
            NetConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            NetConfig.Port = port;
            server = new NetServer(NetConfig);
            server.RegisterReceivedCallback(new SendOrPostCallback(OnReceive), SynchronizationContext.Current);
        }

        public void Start()
        {
            server.Start();
        }

        public NetOutgoingMessage CreateMessage()
        {
            return server.CreateMessage();
        }

        public bool SendReliableMessage(NetOutgoingMessage message, NetConnection recipient)
        {
            var sendResult = server.SendMessage(message, recipient, NetDeliveryMethod.ReliableOrdered);
            return sendResult == NetSendResult.FailedNotConnected ?
                true : sendResult == NetSendResult.Dropped ? true : false;
        }

        public bool SendUnorderedMessage(NetOutgoingMessage message, NetConnection recipient)
        {
            var sendResult = server.SendMessage(message, recipient, NetDeliveryMethod.ReliableUnordered);
            return sendResult == NetSendResult.FailedNotConnected ?
                true : sendResult == NetSendResult.Dropped ? true : false;
        }

        public virtual void OnReceive(object state)
        {
            var message = server.ReadMessage();
            OnMessageReceived?.Invoke(message.SenderConnection, message);
        }
    }
}
