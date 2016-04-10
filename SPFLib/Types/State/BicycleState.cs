using SPFLib.Enums;

namespace SPFLib.Types
{
    public class BicycleState : VehicleState
    {
        #region custom entity properties

        public float WheelRotation { get; set; }
        public float Steering { get; set; }

        #endregion

        public BicycleState() : 
            this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0)
        { }

        public BicycleState(int id) : 
            this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0)
        { }


        public BicycleState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, byte primaryColor, byte secondaryColor, short modelID) : 
            this(id, position, velocity, rotation, 0, 0, 0, 1000, primaryColor, secondaryColor, modelID)
        { }

        public BicycleState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, float steering, short flags,
            short health, byte primaryColor, byte secondaryColor, short modelID)
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            WheelRotation = wheelRotation;
            Steering = steering;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            ModelID = modelID;
        }
    }
}
