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
        public static void SetPosition(AIClient ai, Vector3 destination)
        {
            foreach (var client in Server.ActiveSession?.ActiveClients)
            {
                Server.ActiveSession.InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET",
                new NativeArg(DataType.AIHandle, ai.ID),
                destination.X,
                destination.Y,
                destination.Z,
                1, 1, 1);
            }
        }

        public static void SetPosition(AIClient ai, GameClient clientTP)
        {
            foreach (var client in Server.ActiveSession?.ActiveClients)
            {
                var dest = clientTP.State.InVehicle ?
                clientTP.State.VehicleState.Position : clientTP.State.Position;
                SetPosition(ai, dest);
            }
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
        public static void SetRotation(AIClient ai, Quaternion rotation)
        {
            foreach (var client in Server.ActiveSession?.ActiveClients)
            {
                Server.ActiveSession.InvokeClientNative(client, "SET_ENTITY_QUATERNION",
                new NativeArg(DataType.AIHandle, ai.ID),
                rotation.X,
                rotation.Y,
                rotation.Z,
                rotation.W);
            }
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


        /// <summary>
        /// Play AI animation
        /// </summary>
        public static void PlayAnimation(AIClient ai, string animDictionary, string animationName, int flags, int duration)
        {
            foreach (var client in Server.ActiveSession?.ActiveClients)
            {
                Server.ActiveSession.InvokeClientNative(client, "REQUEST_ANIM_DICT",
                    new NativeArg(DataType.String), animDictionary);

                Server.ActiveSession.InvokeClientNative(client, "TASK_PLAY_ANIM", new NativeArg(DataType.AIHandle, ai.ID),
                    animDictionary,
                    animationName,
                    8f, -8.0f,
                    duration,
                    flags, 1f, 0, 0, 0);
            }
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

        /// <summary>
        /// Set AI health
        /// </summary>
        public static void SetHealth(AIClient ai, int health)
        {
            foreach (var client in Server.ActiveSession?.ActiveClients)
            {
                Server.ActiveSession.InvokeClientNative(client, "SET_ENTITY_HEALTH",
                new NativeArg(DataType.AIHandle, ai.ID), health);
            }
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
            Server.ActiveSession?.InvokeClientNative(client, "0xE679E3E06E363892", hour, minute, second);
        }

        #endregion
    }
}
