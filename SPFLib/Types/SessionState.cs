using System;

namespace SPFLib.Types
{
    public class SessionState
    {
        public uint Sequence { get; set; }
        public DateTime Timestamp { get; set; }
        public ClientState[] Clients { get; set; }
        public VehicleState[] Vehicles { get; set; }

        public SessionState()
        {
        }   
    }
}
