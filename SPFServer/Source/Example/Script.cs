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
    const string motd = "Default Server Message";

    public Script()
    {
        Tick += Script_Tick;
    }

    private void Script_Tick(object sender, EventArgs e)
    {
        foreach (AIClient ai in Server.ActiveSession.ActiveAI)
        {
            foreach (GameClient client in Server.ActiveSession.ActiveClients)
            {
                if (ai.Position.DistanceTo(client.Position) < 1f && client.Health > 0)
                {
                    client.Health -= 10;
                    Console.WriteLine("damage");
                }
            }
        }
    }

    public override void OnClientConnect(GameClient sender, DateTime time)
    {
        foreach (var ai in Server.ActiveSession.ActiveAI)
        {
            Server.ActiveSession.InvokeClientNative(sender, "TASK_GOTO_ENTITY_OFFSET_XY",
                          new NativeArg(DataType.AIHandle, ai.ID), new NativeArg(DataType.LocalHandle), -1, 0, 0, 0, 1.0f, true);

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
                case "motd":
                    Server.ActiveSession.Say(motd);
                    break;
                case "tpto":
                    int clientNum = 0;
                    if (int.TryParse(command[1], out clientNum))
                    {
                        var tpTarget = Server.ActiveSession.ActiveClients.ElementAt(clientNum);

                        if (tpTarget != null)
                            NativeFunctions.SetPosition(sender, tpTarget);
                    }
                    break;
                case "setweather":
                    WeatherType weather;
                    if (Enum.TryParse(command[1], true, out weather))
                    {
                        Server.ActiveSession.SetWeather(weather);
                    }
                    break;
                case "spawnai":
                    for (int i = 0; i < 10; i++)
                    {
                        var ai = Server.ActiveSession.CreateAI("test ai", PedType.Zombie01, sender.Position.Around(15f), new Quaternion());

                        foreach (var client in Server.ActiveSession.ActiveClients)
                        {
                            Server.ActiveSession.InvokeClientNative(client, "TASK_GOTO_ENTITY_OFFSET_XY",
                                new NativeArg(DataType.AIHandle, ai.ID), new NativeArg(DataType.LocalHandle), -1, 0, 0, 0, 1.0f, true);
                            //     Server.ActiveSession.InvokeClientNative(client, "TASK_FOLLOW_TO_OFFSET_OF_ENTITY", new NativeArg(DataType.AIHandle, ai.ID),
                            //            new NativeArg(DataType.LocalHandle), 0f, 0f, 0f, 2.0f, -1, 0.0f, 1);

                            Server.ActiveSession.InvokeClientNative(client, "SET_PED_MOVEMENT_CLIPSET", new NativeArg(DataType.AIHandle, ai.ID), "move_m@drunk@verydrunk", 0x3e800000);
                        }
                    }
                    break;
            }
        }

        base.OnMessageReceived(sender, message);
    }
}