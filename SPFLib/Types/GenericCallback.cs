using System.Collections.Generic;
using System.Linq;
using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class GenericCallback
    {
        public int NetID { get; private set; }

        public GenericCallback()
        {
            NetID = Helpers.GenerateUniqueID();
        }

        public GenericCallback(int netID)
        {
            NetID = netID;
        }

        public GenericCallback(byte[] data)
        {
            int index = 0;
            index += 1;
            NetID = BitConverter.ToInt32(data, index);
            index += 4;
        }

        public byte[] ToByteArray()
        {
            List<byte> data = new List<byte>();

            data.Add((byte)NetMessage.SimpleCallback);

            data.AddRange(BitConverter.GetBytes(NetID));

            return data.ToArray();
        }
    }
}
