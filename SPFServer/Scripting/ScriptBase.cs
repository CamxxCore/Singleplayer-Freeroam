using System;
using SPFServer.Session;

namespace SPFServer
{
    public abstract class ScriptBase : IScriptBase
    {
        internal string Name { get { return GetType().FullName; } }

        internal bool running;

        public event EventHandler Tick;

        internal virtual void OnTick()
        {
            Tick?.Invoke(this, new EventArgs());
        }


        public virtual void OnClientConnect(GameClient sender, DateTime time)
        {
           
        }

        public virtual void OnClientDisconnect(GameClient sender, DateTime time)
        {

        }

        public virtual void OnMessageReceived(GameClient sender, string message)
        {
        }
    }
}
