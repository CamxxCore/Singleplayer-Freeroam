using System;
using System.Collections.Generic;
using SPFLib.Enums;
using System.Text;
using System.Linq;

namespace SPFLib.Types
{
    public class AIState : IEntityState
    {
        public int ID { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Quaternion Rotation { get; set; }
        public ClientFlags MovementFlags { get; set; }
        public NetworkAnimation Animation { get; set; }
        public short PedID { get; set; }
        public short WeaponID { get; set; }
        public short Health { get; set; }

        public AIState()
        {
            ID = Helpers.GenerateUniqueID();
            Position = new Vector3();
            Velocity = new Vector3();
            Rotation = new Quaternion();
            MovementFlags = 0;
            Animation = 0;
            PedID = -1;
            WeaponID = -1;
            Health = 100;
        }

        public AIState(byte[] data)
        {
            int seekIndex = 0;
          //  seekIndex += 1;

            #region Ped ID
            PedID = BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion

            #region Weapon ID
            WeaponID = BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion

            #region Health
            Health = BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion


            #region Movement Flags
            MovementFlags = (ClientFlags)BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion

            #region Animation
            Animation = (NetworkAnimation)BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion

            #region Position
            var floatArray2 = new float[3];
            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);

            Position = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);

            seekIndex += 12;
            #endregion

            #region Velocity
            floatArray2 = new float[3];
            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);

            Velocity = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);

            seekIndex += 12;

            #endregion

            #region Rotation
            floatArray2 = new float[4];

            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 16);

            Rotation = new Quaternion(floatArray2[0], floatArray2[1], floatArray2[2], floatArray2[3]);

            seekIndex += 16;
            #endregion

            #region ID
            ID = BitConverter.ToInt32(data, seekIndex);
            seekIndex += 4;
            #endregion
        }

        public byte[] ToByteArray()
        {
            List<byte> result = new List<byte>();

            result.AddRange(BitConverter.GetBytes((short)PedID));

            result.AddRange(BitConverter.GetBytes((short)WeaponID));

            result.AddRange(BitConverter.GetBytes((short)Health));

            result.AddRange(BitConverter.GetBytes((short)MovementFlags));

            result.AddRange(BitConverter.GetBytes((short)Animation));

            //position
            var floatArray1 = new float[] { Position.X, Position.Y, Position.Z };

            var byteArray = new byte[12];
            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

            result.AddRange(byteArray);

            Array.Clear(byteArray, 0, byteArray.Length);

            //velocity
            floatArray1 = new float[] { Velocity.X, Velocity.Y, Velocity.Z };

            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

            result.AddRange(byteArray);

            Array.Clear(byteArray, 0, byteArray.Length);

            //rotation

            byteArray = new byte[16];

            floatArray1 = new float[] { Rotation.X, Rotation.Y, Rotation.Z, Rotation.W };

            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 16);

            result.AddRange(byteArray);

            result.AddRange(BitConverter.GetBytes((int)ID));

            return result.ToArray();
        }
    }
}
