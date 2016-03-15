using System;
using System.Collections.Generic;
using SPFLib.Enums;
using System.Text;
using System.Linq;

namespace SPFLib.Types
{
    [Serializable]
    public class ClientState : IEntityState
    {
        public string Name;
        public int PktID { get; set; }
        public int ID { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 Angles { get; set; }
        public Quaternion Rotation { get; set; }
        public ClientFlags MovementFlags { get; set; }
        public ActiveTask ActiveTask { get; set; }
        public VehicleSeat Seat { get; set; }
        public bool InVehicle { get; set; }
        public short PedID { get; set; }
        public short WeaponID { get; set; }
        public short Health { get; set; }

        public VehicleState VehicleState;

        public ClientState() : this(0, null)
        {
        }

        public ClientState(int id, string name)
        {
            PktID = 0;
            ID = id;
            Name = name;
            Position = new Vector3();
            Velocity = new Vector3();
            Angles = new Vector3();
            Rotation = new Quaternion();
            MovementFlags = 0;
            ActiveTask = 0;
            Seat = VehicleSeat.None;
            PedID = -1;
            WeaponID = -1;
            Health = 100;
        }

        public ClientState(byte[] data)
        {
            int seekIndex = 0;
            seekIndex += 1;

            #region Packet ID
            PktID = BitConverter.ToInt32(data, seekIndex);
            seekIndex += 4;
            #endregion

            #region bInVehicle
            InVehicle = BitConverter.ToBoolean(data, seekIndex);
            seekIndex += 1;
            #endregion


            #region bSendName
            var sendName = BitConverter.ToBoolean(data, seekIndex);
            seekIndex += 1;
            #endregion

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

            #region Vehicle Seat
            Seat = (VehicleSeat)BitConverter.ToInt16(data, seekIndex);
            seekIndex += 2;
            #endregion
            //12
            if (!InVehicle)
            {

                #region Movement Flags
                MovementFlags = (ClientFlags)BitConverter.ToInt16(data, seekIndex);
                seekIndex += 2;
                #endregion

                #region Stance
                ActiveTask = (ActiveTask)BitConverter.ToInt16(data, seekIndex);
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

                #region Angles
                Array.Clear(floatArray2, 0, floatArray2.Length);
                Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);
                Angles = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);
                seekIndex += 12;
                #endregion

                #region Rotation
                floatArray2 = new float[4];

                Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 16);

                Rotation = new Quaternion(floatArray2[0], floatArray2[1], floatArray2[2], floatArray2[3]);

                seekIndex += 16;
                #endregion

            }

            #region User ID
            ID = BitConverter.ToInt32(data, seekIndex);
            seekIndex += 4;
            #endregion

            #region Name
            if (sendName)
            {
                Name = Encoding.UTF8.GetString(data, seekIndex, 32).Replace("\0", string.Empty);

                seekIndex += 32;
            }

            #endregion

            if (InVehicle && seekIndex != data.Length - 1)
            {
                var buffer = new byte[61];
                Buffer.BlockCopy(data, seekIndex, buffer, 0, buffer.Length);
                VehicleState = new VehicleState(buffer);
            }
        }

        public byte[] ToByteArray()
        {
            List<byte> result = new List<byte>();

            result.Add((byte)NetMessage.ClientState);

            result.AddRange(BitConverter.GetBytes(PktID));

            result.AddRange(BitConverter.GetBytes(InVehicle));

            result.AddRange(BitConverter.GetBytes(Name != null));

            result.AddRange(BitConverter.GetBytes((short)PedID));

            result.AddRange(BitConverter.GetBytes((short)WeaponID));

            result.AddRange(BitConverter.GetBytes((short)Health));

            result.AddRange(BitConverter.GetBytes((short)Seat));

            if (!InVehicle)
            {

                result.AddRange(BitConverter.GetBytes((short)MovementFlags));

                result.AddRange(BitConverter.GetBytes((short)ActiveTask));

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

                //angles

                floatArray1 = new float[] { Angles.X, Angles.Y, Angles.Z };

                Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

                result.AddRange(byteArray);

                //rotation

                byteArray = new byte[16];

                floatArray1 = new float[] { Rotation.X, Rotation.Y, Rotation.Z, Rotation.W };

                Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 16);

                result.AddRange(byteArray);

            }

            result.AddRange(BitConverter.GetBytes((int)ID));

            if (Name != null)
            {

                var name = Encoding.UTF8.GetBytes(Name);
                result.AddRange(name);
                result.AddRange(Enumerable.Repeat((byte)0x00, 32 - name.Length).ToArray());

            }

            if (InVehicle && VehicleState != null)
                result.AddRange(VehicleState.ToByteArray());

            return result.ToArray();
        }
    }
}
