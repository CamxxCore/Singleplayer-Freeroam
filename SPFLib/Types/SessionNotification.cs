using System;

namespace SPFLib.Types
{
    public class SessionNotification
    {
        public int MessageID { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }

        public SessionNotification()
        {
            MessageID = 1 << Environment.TickCount;
            Timestamp = default(DateTime);
            Message = null;
        }
   
    }
}
