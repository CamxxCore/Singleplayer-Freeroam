using SPFLib.Enums;
using SPFLib.Types;
using SPFServer.Session;

namespace SPFServer.Scripting
{
    public static class Natives
    {
        /// <summary>
        /// Teleport a client to another.
        /// </summary>
        public static void SetPosition(GameClient client, GameClient clientTP)
        {
            var dest = clientTP.State.Position;
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_COORDS_NO_OFFSET", 
                new NativeArg(DataType.LocalHandle), 
                dest.X, 
                dest.Y, 
                dest.Z, 
                1, 1, 1);
        }

        /// <summary>
        /// Teleport a client to another.
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
        /// Set client health
        /// </summary>
        public static void SetHealth(GameClient client, int health)
        {
            Server.ActiveSession?.InvokeClientNative(client, "SET_ENTITY_HEALTH",
                new NativeArg(DataType.LocalHandle), health);
        }
    }
}
