using SPFLib.Enums;

namespace SPFLib.Types
{
    public class HeliState : VehicleState
    {
        #region custom entity properties

        public float RotorSpeed { get; set; }

        #endregion

        public HeliState() : 
            this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 255, 0)
        { }

        public HeliState(int id) : 
            this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 255, 0)
        { }

        public HeliState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, byte primaryColor, byte secondaryColor, short vehicleID) : 
            this(id, position, velocity, rotation, 0, 0, 1000, primaryColor, secondaryColor, 255, vehicleID)
        { }


        public HeliState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float rotorSpeed, short flags,
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short modelID)
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            RotorSpeed = rotorSpeed;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            RadioStation = radioStation;
            ModelID = modelID;
        }
    }
}
