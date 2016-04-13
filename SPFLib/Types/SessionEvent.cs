using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionEvent
    {
        public int ID { get; set; }
        public SessionEventType EventType { get; set; }
        public string SenderName { get; set; }

        public SessionEvent()
        {
            ID = 0;
            EventType = 0;
            SenderName = null;
        }
    }
}
