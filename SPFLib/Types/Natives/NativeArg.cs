using System.Collections.Generic;
using System.Linq;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeArg
    {
        public DataType Type { get; private set; }
        public object Value { get; private set; }

        public NativeArg(object value)
        {
            Type = Helpers.GetDataType(value);
            Value = value;
        }

        public static implicit operator NativeArg(double d)
        {
            return new NativeArg(d);
        }

        public static implicit operator NativeArg(int i)
        {
            return new NativeArg(i);
        }

        public static implicit operator NativeArg(bool b)
        {
            return new NativeArg(b);
        }

        public static implicit operator NativeArg(string s)
        {
            return new NativeArg(s);
        }


        public NativeArg(byte[] data)
        {
            int seekIndex = 0;

            Type = (DataType)data[0];

            seekIndex += 1;

            Value = Serializer.DeserializeObject<object>(data.Skip(1).ToArray());
        }

        public byte[] ToByteArray()
        {
            List<byte> data = new List<byte>();

            data.Add((byte)Type);

            data.AddRange(Serializer.SerializeObject(Value));

            return data.ToArray();
        }
    }
}
