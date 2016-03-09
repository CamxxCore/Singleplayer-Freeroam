using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class ServerCommand
    {
        public int NetID { get; private set; }
        public int UID { get; set; }
        public string Name { get; set; }
        public CommandType Command { get; set; }

        public ServerCommand()
        {
            NetID = Helpers.GenerateUniqueID();
            UID = 0;
            Name = null;
            Command = 0;
        }

        public ServerCommand(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            UID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            Command = (CommandType)BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;

            Name = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);
        }

        public byte[] ToByteArray()
        {
            List<byte> result = new List<byte>();

            result.Add((byte)NetMessage.ServerCommand);

            result.AddRange(BitConverter.GetBytes((int)NetID));

            result.AddRange(BitConverter.GetBytes((int)UID));

            result.AddRange(BitConverter.GetBytes((short)Command));

            if (Name != null && Name.Length <= 32)
            {
                var msg = Encoding.UTF8.GetBytes(Name);
                result.AddRange(msg);
                result.AddRange(Enumerable.Repeat((byte)0x00, 32 - msg.Length).ToArray());
            }

            else
            {
                result.AddRange(Enumerable.Repeat((byte)0x00, 32).ToArray());
            }

            return result.ToArray();
        }
    }
}
