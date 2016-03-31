using SPFServer;
using SPFLib.Types;
using System;
using SPFServer.Session;
using System.Linq;

public class Script : ScriptBase
{
    public Script()
    {
        Tick += Script_Tick;
    }

    private void Script_Tick(object sender, EventArgs e)
    {

        //  Console.WriteLine("tick");
    }

    public override void OnClientConnect(GameClient sender, DateTime time)
    {
        base.OnClientConnect(sender, time);
    }

    public override void OnMessageReceived(GameClient sender, string message)
    {
        if (message.StartsWith("/"))
        {
            var command = message.TrimStart('/').Split(' ');

            switch (command[0])
            {
                case "tpto":
                    int clientNum = 0;
                    if (int.TryParse(command[1], out clientNum))
                    {
                        var tpTarget = Server.ActiveSession.ActiveClients.ElementAt(clientNum);

                        if (tpTarget != null)
                        Server.ActiveSession.TeleportClient(sender, tpTarget);
                    }
                    break;
            }
        }

        base.OnMessageReceived(sender, message);
    }
}