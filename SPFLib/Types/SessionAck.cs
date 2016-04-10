using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionAck
    {
        public AckType Type { get; set; }
        public object Value { get; set; }

        public SessionAck(AckType type, object value)
        {
            Type = type;
            Value = value;
        }

        public SessionAck()
        {
        }
    }
}
