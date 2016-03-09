using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class NativeCall
    {
        public int NetID { get; private set; }
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

        public NativeCall(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            ReturnType = (DataType)data[seekIndex];

            seekIndex += 1;

            FunctionName = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);

            seekIndex += 32;

            int argsCount = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            byte[] buffer;
            var objList = new List<NativeArg>();

            for (int i = 0; i < argsCount; i++)
            {
                int sz = BitConverter.ToInt32(data, seekIndex);

                buffer = new byte[sz];

                seekIndex += 4;

                Buffer.BlockCopy(data, seekIndex, buffer, 0, sz);

                objList.Add(new NativeArg(buffer));

                seekIndex += sz;
            }

            Args = objList.ToArray();
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    writer.Write((byte)NetMessage.NativeCall);

                    writer.Write(NetID);

                    writer.Write((byte)ReturnType);

                    if (FunctionName != null && FunctionName.Length <= 32)
                    {
                        var name = Encoding.UTF8.GetBytes(FunctionName);
                        writer.Write(name);
                        writer.Write(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());
                    }

                    else
                    {
                        writer.Write(Enumerable.Repeat((byte)0x00, 32).ToArray());
                    }

                    writer.Write(Args.Length);

                    foreach (var arg in Args)
                    {
                        var bytes = arg.ToByteArray();
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                    }
                }

                return stream.ToArray();
            }
        }
    }
}
