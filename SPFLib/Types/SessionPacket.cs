using System;
using System.IO;

namespace SPFLib.Types
{
    public class SessionPacket
    {
        MemoryStream stream;

        public SessionPacket()
        {
            stream = new MemoryStream(BitConverter.GetBytes(Helpers.GenerateUniqueID()));
        }

        public bool Write<T>(T value)
        {
            Type t = typeof(T);

            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                switch (t.Name)
                {
                    case "System.Byte":
                        writer.Write(Convert.ToByte(value));
                        return true;
                    case "System.Boolean":
                        writer.Write(Convert.ToBoolean(value));
                        return true;
                    case "System.String":
                        writer.Write(Convert.ToString(value));
                        return true;
                    case "System.Int16":
                        writer.Write(Convert.ToInt16(value));
                        return true;
                    case "System.UInt16":
                        writer.Write(Convert.ToUInt16(value));
                        return true;
                    case "System.Int32":
                        writer.Write(Convert.ToInt32(value));
                        return true;
                    case "System.UInt32":
                        writer.Write(Convert.ToUInt32(value));
                        return true;
                    case "System.Int64":
                        writer.Write(Convert.ToInt64(value));
                        return true;
                    case "System.UInt64":
                        writer.Write(Convert.ToUInt64(value));
                        return true;
                    case "System.Single":
                        writer.Write(Convert.ToSingle(value));
                        return true;
                    case "System.Double":
                        writer.Write(Convert.ToDouble(value));
                        return true;
                    default: return false;
                }
            }
        }

        public byte[] ToByteArray()
        {
            var bytes = stream.ToArray();
            var len = BitConverter.GetBytes((int)stream.Length);
            var buffer = new byte[bytes.Length + 4];
            Buffer.BlockCopy(len, 0, buffer, 0, 4);
            Buffer.BlockCopy(bytes, 0, buffer, 4, buffer.Length);
            return buffer;
        }
    }
}
