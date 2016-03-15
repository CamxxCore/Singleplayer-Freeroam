using System;
using SPFLib.Enums;
using System.Text;
using System.Linq;
using System.IO;

namespace SPFLib.Types
{
    [Serializable]
    public class ClientInfo
    {
        public int UID { get; }
        public string Name { get; }
        public int PedHash { get; }
        public int WeaponHash { get; }

        public ClientInfo(int uid, string name, int pedHash, int weaponHash)
        {
            UID = uid;
            Name = name;
            PedHash = pedHash;
            WeaponHash = weaponHash;
        }

        public ClientInfo(int uid, string name) : this(uid, name, -1, -1)
        {
        }

        public ClientInfo() : this(0, "", -1, -1)
        {
        }

        //Converts the bytes into an object of type Data
        public ClientInfo(byte[] data)
        {
            int seekIndex = 0;

            //data type
            seekIndex += 1;

            UID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            Name = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);

            seekIndex += 32;

            PedHash = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            WeaponHash = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {

                    writer.Write((byte)NetMessage.ClientInfo);

                    writer.Write(UID);

                    if (Name != null && Name.Length <= 32)
                    {
                        var name = Encoding.UTF8.GetBytes(Name);
                        writer.Write(name);
                        writer.Write(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());
                    }

                    else
                    {
                        writer.Write(Enumerable.Repeat((byte)0x00, 32).ToArray());
                    }

                    writer.Write(PedHash);
                    writer.Write(WeaponHash);
                }

                return stream.ToArray();
            }
        }
    }
}
