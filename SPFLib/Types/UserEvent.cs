using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class UserEvent
    {
        public int NetID { get; set; }
        public int SenderID { get; set; }
        public EventType EventType { get; set; }
        public string SenderName { get; set; }

        public UserEvent()
        {
            NetID = Helpers.GenerateUniqueID();
            SenderID = 0;
            EventType = 0;
            SenderName = null;
        }

        public UserEvent(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            SenderID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            EventType = (EventType)BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;

            SenderName = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);
        }

        public byte[] ToByteArray()
        {
            List<byte> result = new List<byte>();

            result.Add((byte)NetMessage.UserEvent);

            result.AddRange(BitConverter.GetBytes(NetID));

            result.AddRange(BitConverter.GetBytes((int)SenderID));

            result.AddRange(BitConverter.GetBytes((short)EventType));

            if (SenderName != null && SenderName.Length <= 32)
            {
                var name = Encoding.UTF8.GetBytes(SenderName);
                result.AddRange(name);
                result.AddRange(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());
            }

            return result.ToArray();
        }
    }
}
