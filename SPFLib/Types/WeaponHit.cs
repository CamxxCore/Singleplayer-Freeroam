using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class WeaponHit
    {
        public int NetID { get; private set; }
        public DateTime Timestamp { get; set; }
        public Vector3 HitCoords { get; set; }
        public short WeaponDamage { get; set; }

        public WeaponHit(Vector3 hitCoords)
        {
            NetID = Helpers.GenerateUniqueID();
            Timestamp = default(DateTime);
            HitCoords = hitCoords;
            WeaponDamage = 0;
        }

        public WeaponHit(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            NetID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;

            Timestamp = new DateTime(BitConverter.ToInt64(data, seekIndex));

            seekIndex += 8;

            #region Coords
            var floatArray2 = new float[3];
            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);

            HitCoords = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);

            seekIndex += 12;

            WeaponDamage = BitConverter.ToInt16(data, seekIndex);

            seekIndex += 4;

            #endregion
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    writer.Write((byte)NetMessage.WeaponHit);

                    writer.Write(NetID);

                    writer.Write((long)Timestamp.Ticks);

                    var floatArray1 = new float[] { HitCoords.X, HitCoords.Y, HitCoords.Z };

                    var byteArray = new byte[12];
                    Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

                    writer.Write(byteArray);

                    writer.Write(WeaponDamage);
                }

                return stream.ToArray();
            }
        }
    }
}
