
namespace SPFLib.Enums
{
    /// <summary>
    /// Messages sent by the server to all clients in the session.
    /// </summary>
    public enum EventType
    {
        PlayerSynced,
        PlayerLogout,
        PlayerTimeout,
        PlayerKicked,
        PlayerMessage,
        VehicleRemoved
    }
}
