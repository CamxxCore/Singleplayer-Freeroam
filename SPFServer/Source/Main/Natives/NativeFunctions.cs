using System;
using SPFLib.Enums;
using SPFLib.Types;
using SPFServer.Main;
using SPFServer.Types;
using SPFServer.Enums;

namespace SPFServer.Main
{
    public static class NativeFunctions
    {
        #region Set Position

        /// <summary>
        /// Teleport a client to another.
        /// </summary>
        public static void SetPosition(GameClient client, GameClient client1)
        {
            if (client1.State.InVehicle)
            {
                GameVehicle vehicle;

                if (Server.ActiveSession.GameManager.VehicleFromID(client1.State.VehicleID, out vehicle))
                {
                    SetPosition(client, vehicle.Position);
                }
            }

            else SetPosition(client, client1.State.Position);
        }

        /// <summary>
        /// Teleport a client to the specified position.
        /// </summary>
        public static void SetPosition(GameClient client, Vector3 destination)
        {           
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET",
                new NativeArg(DataType.LocalHandle),
                destination.X,
                destination.Y,
                destination.Z,
                1, 1, 1);
        }

        #endregion Set Position

        #region Set Rotation

        /// <summary>
        /// Set a players rotation.
        /// </summary>
        public static void SetRotation(GameClient client, Quaternion rotation)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_QUATERNION",
                new NativeArg(DataType.LocalHandle),
                rotation.X,
                rotation.Y,
                rotation.Z,
                rotation.W);
        }

        #endregion Set Rotation

        #region Play Animation

        /// <summary>
        /// Play client animation
        /// </summary>
        public static void PlayAnimation(GameClient client, string animDictionary, string animationName, int duration, int flags)
        {
            Server.ActiveSession?.InvokeClientNative(client, "REQUEST_ANIM_DICT",
                new NativeArg(DataType.String), animDictionary);

            Server.ActiveSession?.InvokeClientNative(client, "TASK_PLAY_ANIM", new NativeArg(DataType.LocalHandle), 
                animDictionary, 
                animationName, 
                8f, -8.0f, 
                duration, 
                flags, 1f, 0, 0, 0);
        }

        #endregion

        #region Set Health

        /// <summary>
        /// Set client health
        /// </summary>
        public static void SetHealth(GameClient client, int health)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_HEALTH",
                new NativeArg(DataType.LocalHandle), health);
        }

        #endregion

        #region Set Weather

        public static void SetWeatherTypeNow(GameClient client, WeatherType type)
        {
            Server.ActiveSession?.InvokeClientNative(client,
                "0xED712CA327900C8A", type.ToString());
        }

        public static void SetWeatherTypeOverTime(GameClient client, WeatherType type, float transitionTime)
        {
            Server.ActiveSession?.InvokeClientNative(client,
                "0xFB5045B7C42B75BF", type.ToString(), transitionTime);
        }

        #endregion

        #region Set Clock

        public static void SetClock(GameClient client, int hour, int minute, int second)
        {
            Server.ActiveSession?.InvokeClientNative(client, "0xC8CA9670B9D83B3B", hour, minute, second);
        //    Server.ActiveSession?.InvokeClientNative(client, "0xE679E3E06E363892", hour, minute, second);
        }

        #endregion
    }
}
