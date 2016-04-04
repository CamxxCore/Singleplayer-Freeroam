using SPFLib.Enums;

namespace SPFLib.Types
{
    public class AutomobileState : VehicleState
    {
        #region custom entity properties

        public float CurrentRPM { get; set; }
        public float WheelRotation { get; set; }
        public float Steering { get; set; }

        #endregion

        public AutomobileState() : this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0, 255, 0)
        { }

        public AutomobileState(int id) : this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0, 255, 0)
        { }

        public AutomobileState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float currentRPM, float wheelRotation, float steering, short flags,
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short vehicleID)
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
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
