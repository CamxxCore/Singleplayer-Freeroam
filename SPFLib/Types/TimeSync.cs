using System;
using System.IO;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class TimeSync
    {
        public int NetID { get; private set; }
        public DateTime ClientTime { get; set; }
        public DateTime ServerTime { get; set; }

        public TimeSync()
        {
            NetID = Helpers.GenerateUniqueID();
            ClientTime = new DateTime();
            ServerTime = new DateTime();
        }

        public TimeSync(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            ClientTime = new DateTime(BitConverter.ToInt64(data, seekIndex));

            seekIndex += 8;

            ServerTime = new DateTime(BitConverter.ToInt64(data, seekIndex));
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)NetMessage.TimeSync);
                    writer.Write(NetID);
                    writer.Write(ClientTime.Ticks);
                    writer.Write(ServerTime.Ticks);
                }

                return stream.ToArray();
            }
        }
    }
}
