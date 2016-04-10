using SPFLib.Enums;

namespace SPFLib.Types
{
    public class ClientEvent
    {
        public int ID { get; set; }
        public EventType EventType { get; set; }
        public string SenderName { get; set; }

        public ClientEvent()
        {
            ID = 0;
            EventType = 0;
            SenderName = null;
        }
    }
}
