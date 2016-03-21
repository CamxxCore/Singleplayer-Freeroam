namespace SPFLib.Types
{
    public class SessionEvent
    {
        public int SenderID { get; set; }
        public EventType EventType { get; set; }
        public string SenderName { get; set; }

        public SessionEvent()
        {
            SenderID = 0;
            EventType = 0;
            SenderName = null;
        }
    }
}
