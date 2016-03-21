using System;

namespace SPFLib.Types
{
    public class SessionState
    {
        public DateTime Timestamp { get; set; }
        public ClientState[] Clients { get; set; }

        public SessionState()
        {
            Timestamp = default(DateTime);
        }   
    }
}
