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

         Console.WriteLine("tick");
    }

    public override void OnClientConnect(GameClient sender, DateTime time)
    {
        foreach (var ai in Server.ActiveSession.ActiveAI)
        {
            Server.ActiveSession.InvokeClientNative(sender, "TASK_FOLLOW_TO_OFFSET_OF_ENTITY", new NativeArg(DataType.AIHandle, ai.ID),
                new NativeArg(DataType.LocalHandle),
                0f, 0f, 0f,
                2.0f, -1,
                0.0f, 1);

            Server.ActiveSession.InvokeClientNative(sender, "SET_PED_MOVEMENT_CLIPSET", new NativeArg(DataType.AIHandle, ai.ID), "move_m@drunk@verydrunk", 0x3e800000);
        }


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
                    foreach (var client in Server.ActiveSession.ActiveClients)
                    {
                        foreach (var ai in Server.ActiveSession.ActiveAI)
                        {
                            Server.ActiveSession.InvokeClientNative(client, "TASK_FOLLOW_TO_OFFSET_OF_ENTITY", new NativeArg(DataType.AIHandle, ai.ID), 
                                new NativeArg(DataType.LocalHandle), 
                                0f, 0f, 0f, 
                                2.0f, -1, 
                                0.0f, 1);
                        }
                    }
                    break;

            }
        }

        base.OnMessageReceived(sender, message);
    }
}