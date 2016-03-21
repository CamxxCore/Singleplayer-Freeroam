using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;

namespace SPFClient.Network
{
    public static class NativeHandler
    {
        static NativeHandler()
        {
        }

        public static NativeCallback ExecuteSerializedNativeWithArgs(Hash fHash, NativeArg[] args, DataType returnType, int callbackID)
        {
            var argsList = new List<InputArgument>();

            foreach (var arg in args)
            {
                switch (arg.Type)
                {
                    case DataType.Bool:
                        argsList.Add(new InputArgument(Convert.ToBoolean(arg.Value)));
                        break;
                    case DataType.Int:
                        argsList.Add(new InputArgument(Convert.ToInt32(arg.Value)));
                        break;
                    case DataType.String:
                        argsList.Add(new InputArgument(Convert.ToString(arg.Value)));
                        break;
                    case DataType.Float:
                        argsList.Add(new InputArgument(Convert.ToSingle(arg.Value)));
                        break;
                    case DataType.Double:
                        argsList.Add(new InputArgument(Convert.ToDouble(arg.Value)));
                        break;
                }
            }

            var fArgs = argsList.ToArray();

            object value = null;

            switch (returnType)
            {
                case DataType.Bool:
                    value = Function.Call<bool>(fHash, fArgs);
                    break;
                case DataType.Int:
                    value = Function.Call<int>(fHash, fArgs);
                    break;
                case DataType.String:
                    value = Function.Call<string>(fHash, fArgs);
                    break;
                case DataType.Float:
                    value = Function.Call<float>(fHash, fArgs);
                    break;
                case DataType.Double:
                    value = Function.Call<double>(fHash, fArgs);
                    break;
                default:
                case DataType.None:
                    Function.Call(fHash, fArgs);
                    break;
            }

            return new NativeCallback(value);
        }
    }
}
