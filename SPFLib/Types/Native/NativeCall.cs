using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeCall
    {
        public int NetID { get; set; }
        public string FunctionName { get; set; }
        public NativeArg[] Args { get; set; }
        public DataType ReturnType { get; set; }

        public NativeCall()
        {
            NetID = Helpers.GenerateUniqueID();
            FunctionName = null;
            Args = null;
            ReturnType = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="functionName">Function name</param>
        /// <param name="args">Function args</param>
        /// <returns></returns>
        public NativeCall SetFunctionInfo<T>(string functionName, params NativeArg[] args)
        {
            ReturnType = Helpers.GetDataType(typeof(T));
            FunctionName = functionName;
            Args = args;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="functionName">Function name</param>
        /// <param name="args">Function args</param>
        /// <returns></returns>
        public NativeCall SetFunctionInfo(string functionName, params NativeArg[] args)
        {
            ReturnType = DataType.None;
            FunctionName = functionName;
            Args = args;
            return this;
        }    
    }
}
