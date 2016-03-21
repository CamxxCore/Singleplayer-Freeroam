using System.Collections.Generic;
using System.Linq;
using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionCallback
    {
        public int NetID { get; private set; }

        public SessionCallback()
        {
            NetID = Helpers.GenerateUniqueID();
        }

        public SessionCallback(int netID)
        {
            NetID = netID;
        }

        public SessionCallback(byte[] data)
        {
            int index = 0;
            index += 1;
            NetID = BitConverter.ToInt32(data, index);
            index += 4;
        }

        public byte[] ToByteArray()
        {
            List<byte> data = new List<byte>();

            data.Add((byte)Enums.NetMessage.SessionCallback);

            data.AddRange(BitConverter.GetBytes(NetID));

            return data.ToArray();
        }
    }
}
