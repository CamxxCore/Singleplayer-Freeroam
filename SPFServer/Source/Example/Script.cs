using SPFServer;
using SPFLib.Types;
using SPFLib.Enums;
using System;
using SPFServer.Main;
using System.Linq;
using SPFServer.Types;
using SPFServer.Natives;

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
                            NativeFunctions.SetPosition(sender, tpTarget);
                    }
                    break;
                case "spawnai":
                    Server.ActiveSession.CreateAI("test ai", PedType.Abner, sender.Position, new Quaternion());
                    break;

                case "ainative":
                    NativeFunctions.SetPosition(sender, Server.ActiveSession.ActiveAI[0], sender);
                    break;

            }
        }

        base.OnMessageReceived(sender, message);
    }
}