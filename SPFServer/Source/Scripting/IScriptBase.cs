using System;
using SPFServer.Types;

namespace SPFServer
{
    internal interface IScriptBase
    {
        void OnClientConnect(GameClient sender, DateTime time);
        void OnClientDisconnect(GameClient sender, DateTime time);
        void OnMessageReceived(GameClient sender, string message);
    }
}
