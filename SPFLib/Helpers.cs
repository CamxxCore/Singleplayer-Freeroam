using System;
using SPFLib.Enums;
using System.Security.Cryptography;
using System.Text;

namespace SPFLib
{
    public static class Helpers
    {
        public static int GenerateUniqueID()
        {
            char[] chars = new char[62];
            string a = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            chars = a.ToCharArray();
            byte[] data = new byte[1];
            RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
            crypto.GetNonZeroBytes(data);
            var size = 8;
            data = new byte[size];
            crypto.GetNonZeroBytes(data);
            return BitConverter.ToInt32(data, 0);
        }

        public static DataType GetDataType(object obj)
        {
            return GetDataType(obj.GetType());
        }

        public static DataType GetDataType(Type t)
        {
            if (t == typeof(int))
            {
                return DataType.Int;
            }

            else if (t == typeof(float))
            {
                return DataType.Float;
            }

            else if (t == typeof(double))
            {
                return DataType.Double;
            }

            else if (t == typeof(bool))
            {
                return DataType.Bool;
            }

            else if (t == typeof(string))
            {
                return DataType.String;
            }

            else if (t == typeof(object))
            {
                return DataType.Object;
            }

            else throw new ArgumentException("t: Not a known type.");
        }

        public static short Serialize(this float fl)
        {
            return (short)(fl * 256);
        }

        public static float Deserialize(this short us)
        {
            return (us / 256f);
        }

    }
}
