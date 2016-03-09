using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class ServerHello
    {
        public int NetID { get; private set; }
        public string Message { get; set; }

        public ServerHello(int netID, string message)
        {
            NetID = netID;
            Message = message;
        }

        public ServerHello(byte[] data)
        {
            int seekIndex = 0;
            seekIndex += 1;
            NetID = BitConverter.ToInt32(data, seekIndex);
            seekIndex += 4;
            Message = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);
            seekIndex += 32;
        }

        public byte[] ToByteArray()
        {
            List<byte> data = new List<byte>();

            data.Add((byte)NetMessage.ServerHello);

            data.AddRange(BitConverter.GetBytes(NetID));

            if (Message != null && Message.Length <= 32)
            {
                var name = Encoding.UTF8.GetBytes(Message);
                data.AddRange(name);
                data.AddRange(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());
            }

            else
            {
                data.AddRange(Enumerable.Repeat((byte)0x00, 32).ToArray());
            }

            return data.ToArray();
        }
    }
}
