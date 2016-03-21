using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer
{
    internal interface IScriptBase
    {
        void OnClientConnect(GameClient sender, DateTime time);
        void OnClientDisconnect(GameClient sender, DateTime time);
        void OnMessageReceived(GameClient sender, string message);
    }
}
