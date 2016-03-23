using System;
using System.Collections.Generic;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class VehicleState : IEntityState
    {
        #region custom entity properties

        public short Health { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Quaternion Rotation { get; set; }
        public byte PrimaryColor { get; set; }
        public byte SecondaryColor { get; set; }
        public byte RadioStation { get; set; }
        public short VehicleID { get; set; }
        public byte CurrentGear { get; set; }
        public float CurrentRPM { get; set; }
        public float WheelRotation { get; set; }
        public float Steering { get; set; }
        public int ID { get; set; }
        public VehicleFlags Flags { get; set; }
        public ushort ExtraFlags { get; set; }

        #endregion

        public VehicleState() : this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 0, 0, 1000, 0, 0, 0, 255, 0)
        { }

        public VehicleState(int id) : this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 0, 0, 1000, 0, 0, 0, 255, 0)
        { }

        public VehicleState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float currentRPM, byte currentGear, float wheelRotation, float steering, short flags,
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short vehicleID) : base()
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            CurrentGear = currentGear;
            CurrentRPM = currentRPM;
            WheelRotation = wheelRotation;
            Steering = steering;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            RadioStation = radioStation;
            VehicleID = vehicleID;
        }
    }
}
