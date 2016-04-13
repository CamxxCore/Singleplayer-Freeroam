using SPFLib.Types;

namespace SPFClient.Types
{
    public class GameClient
    {
        public ClientInfo Info { get; private set; }
        public ClientState State { get; private set; }

        public GameClient(ClientInfo info, ClientState state)
        {
            Info = info;
            State = state;
        }

        public GameClient()
        { }
    }
}
