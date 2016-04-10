using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    [Serializable]
    public class ClientState : IEntityState
    {
        public string Name { get; set; }
        public int ClientID { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 Angles { get; set; }
        public Quaternion Rotation { get; set; }
        public ClientFlags MovementFlags { get; set; }
        public ActiveTask ActiveTask { get; set; }
        public PedHash PedHash { get; set; }
        public bool InVehicle { get; set; }
        public short WeaponID { get; set; }
        public short Health { get; set; }

        public int VehicleID { get; set; }
        public VehicleSeat VehicleSeat { get; set; }


        public ClientState() : this(0, null)
        {
        }

        public ClientState(int id, string name)
        {
            ClientID = id;
            Name = name;
            Position = new Vector3();
            Velocity = new Vector3();
            Angles = new Vector3();
            Rotation = new Quaternion();
            MovementFlags = 0;
            ActiveTask = 0;
            VehicleSeat = VehicleSeat.None;
            VehicleID = -1;
            PedHash = 0;
            WeaponID = -1;
            Health = 100;
        }    
    }
}
