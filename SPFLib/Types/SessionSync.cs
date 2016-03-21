using System;

namespace SPFLib.Types
{
    public class SessionSync
    {
        public DateTime ClientTime { get; set; }
        public DateTime ServerTime { get; set; }

        public SessionSync()
        {
            ClientTime = new DateTime();
            ServerTime = new DateTime();
        }  
    }
}
