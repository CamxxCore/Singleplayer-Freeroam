using System;
using System.Collections.Generic;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class VehicleState : IEntityState
    {
        #region custom entity properties

        public short Health { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public Quaternion Rotation { get; }
        public byte PrimaryColor { get; }
        public byte SecondaryColor { get; }
        public byte RadioStation { get; }
        public short VehicleID { get; }
        public float CurrentRPM { get; }
        public float WheelRotation { get; }
        public int ID { get; }
        public VehicleFlags Flags { get; set; }
        public ushort ExtraFlags { get; set; }

        #endregion

        public VehicleState() : this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 0, 1000, 0, 0, 255, 0)
        { }

        public VehicleState(int id) : this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 0, 1000, 0, 0, 255, 0)
        { }

        public VehicleState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float currentRPM, float wheelRotation, short flags, 
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short vehicleID) : base()
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            CurrentRPM = currentRPM;
            WheelRotation = wheelRotation;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            RadioStation = radioStation;
            VehicleID = vehicleID;
        }

        //Converts the bytes into an object of type Data
        public VehicleState(byte[] data)
        {
            int seekIndex = 0;

            //position vector
            var floatArray2 = new float[3];
            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);

            Position = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);

            seekIndex += 12;

            floatArray2 = new float[3];

            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 12);

            Velocity = new Vector3(floatArray2[0], floatArray2[1], floatArray2[2]);

            seekIndex += 12;

            floatArray2 = new float[4];

            Buffer.BlockCopy(data, seekIndex, floatArray2, 0, 16);

            Rotation = new Quaternion(floatArray2[0], floatArray2[1], floatArray2[2], floatArray2[3]);

            seekIndex += 16;

            CurrentRPM = BitConverter.ToInt16(data, seekIndex).Deserialize();

            seekIndex += 2;

            WheelRotation = BitConverter.ToInt16(data, seekIndex).Deserialize();

            seekIndex += 2;

            //health
            Health = BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;
   
            VehicleID = BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;

            PrimaryColor = data[seekIndex];

            seekIndex += 1;

            SecondaryColor = data[seekIndex];

            seekIndex += 1;

            RadioStation = data[seekIndex];

            seekIndex += 1;

            Flags = (VehicleFlags)BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;

            ExtraFlags = BitConverter.ToUInt16(data, seekIndex);

            seekIndex += 2;

            ID = BitConverter.ToInt32(data, seekIndex);

            seekIndex += 4;
        }

        public byte[] ToByteArray()
        {
            List<byte> result = new List<byte>();

            var floatArray1 = new float[] { Position.X, Position.Y, Position.Z };

            var byteArray = new byte[12];
            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

            result.AddRange(byteArray);

            floatArray1 = new float[] { Velocity.X, Velocity.Y, Velocity.Z };

            Array.Clear(byteArray, 0, byteArray.Length);

            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 12);

            result.AddRange(byteArray);

            byteArray = new byte[16];

            floatArray1 = new float[] { Rotation.X, Rotation.Y, Rotation.Z, Rotation.W };

            Buffer.BlockCopy(floatArray1, 0, byteArray, 0, 16);

            result.AddRange(byteArray);

            result.AddRange(BitConverter.GetBytes(CurrentRPM.Serialize()));

            result.AddRange(BitConverter.GetBytes(WheelRotation.Serialize()));

            result.AddRange(BitConverter.GetBytes(Health));

            result.AddRange(BitConverter.GetBytes(VehicleID));

            result.Add(PrimaryColor);

            result.Add(SecondaryColor);

            result.Add(RadioStation);

            result.AddRange(BitConverter.GetBytes((short)Flags));

            result.AddRange(BitConverter.GetBytes(ExtraFlags));

            result.AddRange(BitConverter.GetBytes(ID));

            return result.ToArray();
        }
    }
}
