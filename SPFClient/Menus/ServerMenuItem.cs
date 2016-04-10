using ASUPService;
using NativeUI;

namespace SPFClient.Menus
{
    public class ServerMenuItem : UIMenuItem
    {
        public ActiveSession Session { get; private set; }

        public ServerMenuItem(ActiveSession session) :
            base(string.Format("~b~{0}~w~:  Players: ~y~{1}~w~/~y~{2}",
            session.Hostname,
            session.ClientCount,
            session.MaxClients))
        {
            Session = session;
            Enabled = false;
        }
    }
}
