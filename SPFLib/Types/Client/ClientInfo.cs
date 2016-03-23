using System;
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
    }
}
