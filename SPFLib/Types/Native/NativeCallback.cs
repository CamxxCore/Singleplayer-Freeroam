using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeCallback
    {
        public int NetID { get; set; }
        public DataType Type { get; set; }
        public object Value { get; set; }

        public NativeCallback(int nativeID, object value)
        {
            NetID = nativeID;
            Value = value;
            Type = value == null ? DataType.None : Helpers.GetDataType(value);
        }

        public NativeCallback()
        {
        }
    }
}
