using System;
using System.Linq;
using System.Text;
using System.IO;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class ServerNotification
    {
        public int MessageID { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }

        public ServerNotification()
        {
            MessageID = 1 << Environment.TickCount;
            Timestamp = default(DateTime);
            Message = null;
        }

        public ServerNotification(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            MessageID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            Timestamp = new DateTime(BitConverter.ToInt64(data, seekIndex));

            seekIndex += 8;

            Message = Encoding.UTF8.GetString(data, seekIndex, 100).Replace("\0", string.Empty);
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)NetMessage.ServerNotification);
                    writer.Write(MessageID);
                    writer.Write((long)Timestamp.Ticks);

                    if (Message != null && Message.Length <= 100)
                    {
                        var msg = Encoding.UTF8.GetBytes(Message);
                        writer.Write(msg);
                        writer.Write(Enumerable.Repeat((byte)0x00, 100 - msg.Length).ToArray());
                    }

                    else
                    {
                        writer.Write(Enumerable.Repeat((byte)0x00, 100).ToArray());
                    }
                }

                return stream.ToArray();
            }
        }
    }
}
