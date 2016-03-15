using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer.Scripting
{
    public abstract class ScriptBase : IScriptBase
    {
        internal string Name { get { return GetType().FullName; } }

        public int Interval { get; private set; }

        internal bool running;

        public event EventHandler Tick;

        internal virtual void OnTick()
        {
            Console.WriteLine("tick");
            Tick?.Invoke(this, new EventArgs());
            System.Threading.Thread.Sleep(Interval);
        }


        public virtual void OnClientConnect(string username, DateTime time)
        {
           
        }

        public virtual void OnClientDisconnect(string client, DateTime time)
        {

        }

        public virtual void OnMessageReceived(string username, string message)
        {
        }
    }
}
