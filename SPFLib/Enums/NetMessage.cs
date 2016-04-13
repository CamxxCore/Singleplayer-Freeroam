
namespace SPFLib.Enums
{
    /// <summary>
    /// Types of data that are recognized on the network.
    /// </summary>
    public enum NetMessage
    {
        SessionCommand,
        SessionMessage,
        ClientState,
        VehicleState,
        Acknowledgement,
        WeaponData,
        ClientInfo,
        SessionUpdate,
        SessionEvent,
        Unk,
        SessionSync,
        NativeCall,
        NativeCallback,
        SessionHeartbeat,
        LoginRequest,
        SessionNotification,
        RankData = 16,

    }
}