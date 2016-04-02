using System;

namespace SPFLib.Types
{
    public class SessionState
    {
        public uint Sequence { get; set; }
        public DateTime Timestamp { get; set; }
        public bool AIHost { get; set; }
        public ClientState[] Clients { get; set; }
        public AIState[] AI { get; set; }

        public SessionState()
        {
        }   
    }
}
