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
        ClientStateAI = 4,
        AckWorldSync = 5,
        WeaponData = 6,
        ClientInfo = 7,
        SessionUpdate = 8,
        SessionEvent = 9,
        SessionSync = 10,
        NativeCall = 11,
        NativeCallback = 12,
        SessionHeartbeat = 13,
        LoginRequest = 14,
        SessionNotification = 15,
        RankData = 16,
    }
}