using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer.Scripting
{
    public interface IScriptBase
    {
        void OnClientConnect(string username, DateTime time);
        void OnClientDisconnect(string username, DateTime time);
        void OnMessageReceived(string username, string message);
    }
}
