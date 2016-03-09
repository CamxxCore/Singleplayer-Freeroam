using System;
using System.Linq;
using System.Text;
using System.IO;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class MessageInfo
    {
        public int NetID { get; set; }
        public DateTime Timestamp { get; set; }
        public int SenderUID { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }

        public MessageInfo()
        {
            NetID = Helpers.GenerateUniqueID();
            Timestamp = default(DateTime);
            SenderUID = 0;
            SenderName = null;
            Message = null;
        }

        public MessageInfo(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            Timestamp = new DateTime(BitConverter.ToInt64(data, seekIndex));

            seekIndex += 8;

            SenderUID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            SenderName = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);

            seekIndex += 32;

            Message = Encoding.UTF8.GetString(data, seekIndex, 100).Replace("\0", string.Empty);
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)NetMessage.ChatMessage);
                    writer.Write(NetID);
                    writer.Write(Timestamp.Ticks);
                    writer.Write(SenderUID);

                    if (SenderName != null && SenderName.Length <= 32)
                    {
                        var name = Encoding.UTF8.GetBytes(SenderName);
                        writer.Write(name);
                        writer.Write(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());
                    }

                    else
                    {
                        writer.Write(Enumerable.Repeat((byte)0x00, 32).ToArray());
                    }


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
