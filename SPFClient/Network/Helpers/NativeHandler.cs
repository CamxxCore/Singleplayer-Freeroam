using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;
using System.Globalization;

namespace SPFClient.Network
{
    public static class NativeHandler
    {
        private static bool GetNativeFunctionInfo(NativeCall native, out Hash hash, out InputArgument[] args)
        {
            var argsList = new List<InputArgument>();
            Hash hResult;
            long result;

            foreach (var arg in native.Args)
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
                    case DataType.LocalHandle:
                        argsList.Add(new InputArgument(Game.Player.Character.Handle));
                        break;
                }
            }

            if (Enum.TryParse(native.FunctionName, out hResult))
            {
                hash = (Hash)hResult;
                args = argsList.ToArray();
                return true;
            }

            if (long.TryParse(native.FunctionName, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result))
            {
                hash = (Hash)result;
                args = argsList.ToArray();
                return true;
            }        

            hash = 0;
            args = null;
            return false;
        }

        public static NativeCallback ExecuteLocalNativeWithArgs(NativeCall native)
        {
            Hash functionHash;
            InputArgument[] args;

            if (!GetNativeFunctionInfo(native, out functionHash, out args))
                return null;          

            object value = null;

            switch (native.ReturnType)
            {
                case DataType.Bool:
                    value = Function.Call<bool>(functionHash, args);
                    break;
                case DataType.Int:
                    value = Function.Call<int>(functionHash, args);
                    break;
                case DataType.String:
                    value = Function.Call<string>(functionHash, args);
                    break;
                case DataType.Float:
                    value = Function.Call<float>(functionHash, args);
                    break;
                case DataType.Double:
                    value = Function.Call<double>(functionHash, args);
                    break;
                default:
                case DataType.None:
                    Function.Call(functionHash, args);
                    break;
            }

            return new NativeCallback(value);
        }
    }
}
