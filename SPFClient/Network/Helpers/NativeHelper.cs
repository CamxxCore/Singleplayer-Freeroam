using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFLib.Enums;
using System.Globalization;

namespace SPFClient.Network
{
    public static class NativeHelper
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
                    case DataType.AIHandle:
                        var ai = EntityManager.AIFromID(Convert.ToInt32(arg.Value));
                        if (ai == null || ai.Handle <= 0)
                            goto endfunc;
                        argsList.Add(ai.Handle);
                        break;
                }
            }

            if (Enum.TryParse(native.FunctionName, out hResult))
            {
                hash = (Hash)hResult;
                args = argsList.ToArray();
                return true;
            }

            if (long.TryParse(native.FunctionName.StartsWith("0x") ? native.FunctionName.Substring(2) : 
                native.FunctionName, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result))
            {
                hash = (Hash)result;
                args = argsList.ToArray();
                return true;
            }

            endfunc:
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

            if (functionHash == (Hash)0xED712CA327900C8A ||
                functionHash == (Hash)0x29B487C359E19889 ||
                functionHash == (Hash)0x704983DF373B198F ||
                functionHash == (Hash)0xFB5045B7C42B75BF ||
                functionHash == (Hash)0xA43D5C6FE51ADBEF)
            {
                if (native.Args[0].Type == DataType.String && 
                    (Convert.ToString(native.Args[0].Value).Equals("xmas", StringComparison.InvariantCultureIgnoreCase) ||
                     Convert.ToString(native.Args[0].Value).Equals("blizzard", StringComparison.InvariantCultureIgnoreCase) ||
                     Convert.ToString(native.Args[0].Value).Equals("snow", StringComparison.InvariantCultureIgnoreCase)))
                {
         
                    if (!MemoryAccess.SnowEnabled)
                    {
                        // invoke functions for snow weather.
                        MemoryAccess.SetSnowEnabled(true);
                        Function.Call((Hash)0xAEEDAD1420C65CC0, true);
                        Function.Call((Hash)0x4CC7F0FEA5283FE0, true);
                    }
                }

                else
                {
                    if (MemoryAccess.SnowEnabled)
                    {
                        MemoryAccess.SetSnowEnabled(false);
                        Function.Call((Hash)0xAEEDAD1420C65CC0, true);
                        Function.Call((Hash)0x4CC7F0FEA5283FE0, true);
                    }
                }
            }

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
