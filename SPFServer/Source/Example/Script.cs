using SPFServer;
using SPFLib.Types;
using SPFLib.Enums;
using System;
using SPFServer.Main;
using System.Linq;
using SPFServer.Types;
using SPFServer.Enums;
using System.Collections.Generic;

public class Script : ScriptBase
{
    const string motd = "Default Server Message";

    private readonly Vector3 SpawnPosition = new Vector3(-2148.006f, 3239.402f, 32.8103f);

    public Script()
    {
        Tick += Script_Tick;
    }

    private void Script_Tick(object sender, EventArgs e)
    {


    }

    public override void OnClientConnect(GameClient sender, DateTime time)
    {
        //    sender.Position = SpawnPosition;


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
                        Server.ActiveSession.CurrentWeather = weather;
                    }
                    break;

                case "sp":
                    Server.ActiveSession.CreateVehicle(sender.Position, sender.Rotation, VehicleHash.Adder);
                    break;
            }
        }

        base.OnMessageReceived(sender, message);
    }

    public T GetRandomItem<T>(IEnumerable<T> items)
    {
        if (items.Count() < 1) return (T)(object)null;
        var random = new Random(Guid.NewGuid().GetHashCode());
        return (T)(object)items.ToArray()[random.Next(0, items.Count())];
    }
}