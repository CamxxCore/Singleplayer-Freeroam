namespace SPFLib.Types
{
    /// <summary>
    /// Messages sent by the server to all clients in the session.
    /// </summary>
    public enum EventType
    {
        PlayerLogon,
        PlayerLogout,
        PlayerTimeout,
        PlayerKicked,
        PlayerMessage,
        PlayerSynced
    }
}
