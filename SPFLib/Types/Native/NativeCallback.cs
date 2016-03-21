using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeCallback
    {
        public DataType Type { get; set; }
        public object Value { get; set; }

        public NativeCallback(object value)
        {
            Value = value;
            Type = value == null ? DataType.None : Helpers.GetDataType(value);
        }

        public NativeCallback()
        {
        }
    }
}
