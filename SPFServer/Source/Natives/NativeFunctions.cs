using System;
using SPFLib.Enums;
using SPFLib.Types;
using SPFServer.Main;
using SPFServer.Types;

namespace SPFServer.Natives
{
    public static class NativeFunctions
    {
        #region Set Position

        /// <summary>
        /// Teleport a client to another.
        /// </summary>
        public static void SetPosition(GameClient client, GameClient clientTP)
        {
            var dest = clientTP.State.InVehicle ? 
                clientTP.State.VehicleState.Position : clientTP.State.Position;
            SetPosition(client, dest);
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

        /// <summary>
        /// Teleport a client to another.
        /// </summary>
        public static void SetPosition(GameClient client, AIClient ai, Vector3 destination)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET",
                new NativeArg(DataType.AIHandle, ai.ID),
                destination.X,
                destination.Y,
                destination.Z,
                1, 1, 1);
        }

        public static void SetPosition(GameClient client, AIClient ai, GameClient clientTP)
        {
            var dest = clientTP.State.InVehicle ? 
                clientTP.State.VehicleState.Position : clientTP.State.Position;
            SetPosition(client, ai, dest);
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

        /// <summary>
        /// Set an AI clients rotation.
        /// </summary>
        public static void SetRotation(GameClient client, AIClient ai, Quaternion rotation)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_QUATERNION",
                new NativeArg(DataType.AIHandle, ai.ID),
                rotation.X,
                rotation.Y,
                rotation.Z,
                rotation.W);
        }

        #endregion Set Rotation

        #region Set Health

        /// <summary>
        /// Set client health
        /// </summary>
        public static void SetHealth(GameClient client, int health)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_HEALTH",
                new NativeArg(DataType.LocalHandle), health);
        }

        /// <summary>
        /// Set AI health
        /// </summary>
        public static void SetHealth(GameClient client, AIClient ai, int health)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_HEALTH",
                new NativeArg(DataType.AIHandle, ai.ID), health);
        }

        #endregion

        #region Set Weather

        public static void SetWeatherTypeNow(GameClient client, WeatherType type)
        {
            Server.ActiveSession?.InvokeClientNative(client,
                "0xED712CA327900C8A", type.ToString());
        }

        #endregion

        #region Set Clock

        public static void SetClock(GameClient client, int hour, int minute, int second)
        {
            Server.ActiveSession?.InvokeClientNative(client, "0x47C3B5848C3E45D8", hour, minute, second);
        }

        #endregion
    }
}
