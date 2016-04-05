using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SPFLib.Types
{
    public class WeaponData
    {
        public int TargetID { get; set; }
        public DateTime Timestamp { get; set; }
        public Vector3 HitCoords { get; set; }
        public float WeaponDamage { get; set; }

        public WeaponData(Vector3 hitCoords, int targetID, float weaponDamage)
        {
            TargetID = targetID;
            Timestamp = NetworkTime.Now;
            HitCoords = hitCoords;
            WeaponDamage = weaponDamage;
        }

        public WeaponData()
        {
            TargetID = 0;
            Timestamp = NetworkTime.Now;
            HitCoords = null;
            WeaponDamage = 0;
        }
    }
}
