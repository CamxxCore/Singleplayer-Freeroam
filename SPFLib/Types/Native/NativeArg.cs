using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeArg
    {
        public DataType Type { get; set; }
        public object Value { get; set; }

        public NativeArg(object value)
        {
            Type = Helpers.GetDataType(value);
            Value = value;
        }
 
        public NativeArg(DataType type, object value)
        {
            Type = type;
            Value = value;
        }

        public NativeArg(DataType type) : this(type, null)
        { }

        public NativeArg()
        {
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
    }
}
