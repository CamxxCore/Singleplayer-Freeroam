using SPFLib.Enums;

namespace SPFLib.Types
{
    public class PlaneState : VehicleState
    {
        #region custom entity properties

        public float Flaps { get; set; }
        public float Stabs { get; set; }
        public float Rudder { get; set; }

        #endregion

        public PlaneState() : 
            this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0, 255, 0)
        { }

        public PlaneState(int id) : 
            this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 0, 1000, 0, 0, 0, 0, 255, 0)
        { }

        public PlaneState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, byte primaryColor, byte secondaryColor, short modelID) : 
            this(id, position, velocity, rotation, 0, 0, 1000, 0, 0, primaryColor, secondaryColor, 255, modelID)
        { }

        public PlaneState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, float flaps, float stabs, float rudder, short flags,
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short modelID)
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Flaps = flaps;
            Stabs = stabs;
            Rudder = rudder;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            RadioStation = radioStation;
            ModelID = modelID;
        }
    }
}
