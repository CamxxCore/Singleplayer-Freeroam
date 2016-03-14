using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer.Scripting
{
    public interface IScriptBase
    {
        void OnClientConnect(GameClient client, DateTime time);
        void OnClientDisconnect(GameClient client, DateTime time);
        void OnMessageReceived(GameClient sender, string message);
    }
}
