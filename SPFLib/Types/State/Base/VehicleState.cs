using SPFLib.Enums;

namespace SPFLib.Types
{
    public class VehicleState : IEntityState
    {
        #region custom entity properties

        public VehicleType Type { get; set; }
        public short Health { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Quaternion Rotation { get; set; }
        public byte PrimaryColor { get; set; }
        public byte SecondaryColor { get; set; }
        public byte RadioStation { get; set; }
        public short VehicleID { get; set; }
        public int ID { get; set; }
        public VehicleFlags Flags { get; set; }
        public ushort ExtraFlags { get; set; }

        #endregion

        public VehicleState() : this(-1, new Vector3(), new Vector3(), new Quaternion(), 0, 1000, 0, 0, 255, 0)
        { }

        public VehicleState(int id) : this(id, new Vector3(), new Vector3(), new Quaternion(), 0, 1000, 0, 0, 255, 0)
        { }

        public VehicleState(int id, Vector3 position, Vector3 velocity, Quaternion rotation, short flags,
            short health, byte primaryColor, byte secondaryColor, byte radioStation, short vehicleID)
        {
            ID = id;
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Flags = (VehicleFlags)flags;
            Health = health;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            RadioStation = radioStation;
            VehicleID = vehicleID;
        }
    }
}
