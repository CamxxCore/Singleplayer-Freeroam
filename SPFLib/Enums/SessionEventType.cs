
namespace SPFLib.Enums
{
    /// <summary>
    /// Messages sent by the server to all clients in the session.
    /// </summary>
    public enum SessionEventType
    {
        PlayerSynced,
        PlayerLogout,
        PlayerTimeout,
        PlayerKicked,
        VehicleDeleted
    }
}
