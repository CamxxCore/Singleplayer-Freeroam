namespace SPFLib.Enums
{
    /// <summary>
    /// Types of data that are recognized on the network.
    /// </summary>
    public enum NetMessage
    {
        ServerCommand = 1, //client
        ChatMessage = 2, //both
        ClientState = 3, //client
        AIState = 4, //client
        WeaponHit = 5, //client
        ClientInfo = 6, //client
        SessionUpdate = 7, //server
        UserEvent = 8, //server
        TimeSync = 9, //client
        NativeCall = 10, //server
        NativeCallback = 11, //client
        ServerHeartbeat = 12, //server
        ServerHello = 13, // server
        ServerNotification = 14,
        SimpleCallback = 15,
    }
}
