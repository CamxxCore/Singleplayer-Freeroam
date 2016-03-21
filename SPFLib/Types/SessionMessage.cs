using System;

namespace SPFLib.Types
{
    public class SessionMessage
    {
        public int NetID { get; set; }
        public DateTime Timestamp { get; set; }
        public int SenderUID { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }

        public SessionMessage()
        {
            NetID = Helpers.GenerateUniqueID();
            Timestamp = default(DateTime);
            SenderUID = 0;
            SenderName = null;
            Message = null;
        }
    }
}