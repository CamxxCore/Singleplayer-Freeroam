using System;

namespace SPFLib.Types
{
    public class SessionMessage
    {
        public DateTime Timestamp { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }

        public SessionMessage()
        {
            Timestamp = new DateTime();
            SenderName = null;
            Message = null;
        }
    }
}