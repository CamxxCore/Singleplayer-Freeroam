namespace SPFLib.Enums
{
    /// <summary>
    /// Types of data that are recognized on the network.
    /// </summary>
    public enum NetMessage
    {
        SessionCommand = 1, //client
        SessionMessage = 2, //both
        ClientState = 3, //client
        AIState = 4, //client
        WeaponData = 5, //client
        ClientInfo = 6, //client
        SessionUpdate = 7, //server
        SessionEvent = 8, //server
        SessionSync = 9, //client
        NativeCall = 10, //server
        NativeCallback = 11, //client
        SessionHeartbeat = 12, //server
        None = 13, // server
        SessionNotification = 14,
      //  SessionCallback = 15,
    }
}