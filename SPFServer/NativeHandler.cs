using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using SPFLib.Types;
using System.Threading;

namespace SPFServer
{
    public class NativeHandler
    {
        Socket _socket;

        private List<NativeCall> pendingCallbacks = new List<NativeCall>();

        public NativeHandler(Socket serverSocket)
        {
            _socket = serverSocket;
        }

        /// <summary>
        /// Invoke a native on the client with return type void.
        /// </summary>
        /// <param name="client"></param>
        internal void InvokeClientNative(EndPoint client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo(func, args);
            var packedData = native.ToByteArray();

            _socket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);

            var timer = new Timer(new TimerCallback(x => {
                // keep sending until we receive a callback
                if (pendingCallbacks.Contains(native))
                    _socket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                    new AsyncCallback(OnSend), packedData);
            }),
                    null, 1000, 1000);

            pendingCallbacks.Add(native);
        }

        /// <summary>
        /// Invoke a native on the client with a return type.
        /// </summary>
        /// <param name="client"></param>
        internal void InvokeClientNative<T>(EndPoint client, string func, params NativeArg[] args)
        {
            var native = new NativeCall();
            native.SetFunctionInfo<T>(func, args);
            var packedData = native.ToByteArray();
            _socket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                 new AsyncCallback(OnSend), packedData);

            var timer = new Timer(new TimerCallback(x => {
                // keep sending until we receive a callback
                if (pendingCallbacks.Contains(native))
                    _socket.BeginSendTo(packedData, 0, packedData.Length, SocketFlags.None, client,
                    new AsyncCallback(OnSend), packedData);
            }), null, 1000, 1000);

            pendingCallbacks.Add(native);
        }

        /// <summary>
        /// Callback method for sent data.
        /// </summary>
        /// <param name="ar"></param>
        private void OnSend(IAsyncResult ar)
        {
            try
            {
                _socket.EndSend(ar);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Send error. \n" + ex.ToString());
            }
        }
    }
}
