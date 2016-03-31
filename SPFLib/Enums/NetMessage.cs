namespace SPFLib.Enums
{
    /// <summary>
    /// Types of data that are recognized on the network.
    /// </summary>
    public enum NetMessage
    {
        SessionCommand = 1,
        SessionMessage = 2,
        ClientState = 3,
        AIState = 4,
        WeaponData = 5,
        ClientInfo = 6,
        SessionUpdate = 7,
        SessionEvent = 8,
        SessionSync = 9,
        NativeCall = 10,
        NativeCallback = 11,
        SessionHeartbeat = 12,
        LoginRequest = 13,
        SessionNotification = 14,
        RankData = 15,
    }
}