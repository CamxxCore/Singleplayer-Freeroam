using System.Collections.Generic;
using System.Linq;
using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeCallback
    {
        public int NetID { get; private set; }
        public DataType Type { get; private set; }
        public object Value { get; private set; }

        public NativeCallback(int netID, object value)
        {
            NetID = netID;
            Value = value;
            Type = value == null ? DataType.None : Helpers.GetDataType(value);
        }

        public NativeCallback(byte[] data)
        {
            int index = 0;
            index += 1;
            NetID = BitConverter.ToInt32(data, index);
            index += 4;
            Type = (DataType)data[index];
            index += 1;
            if (Type != DataType.None)
            Value = Serializer.DeserializeObject<object>(data.Skip(index).ToArray());
        }

        public byte[] ToByteArray()
        {
            List<byte> data = new List<byte>();

            data.Add((byte)NetMessage.NativeCallback);

            data.AddRange(BitConverter.GetBytes(NetID));

            data.Add((byte)Type);

            if (Type != DataType.None && Value != null)
            data.AddRange(Serializer.SerializeObject(Value));

            return data.ToArray();
        }
    }
}
