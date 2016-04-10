using System;
using SPFLib.Types;

namespace SPFServer.Types
{
    public class GameVehicle : IGameEntity
    {
        public int ID { get; set; }

        public Vector3 Position
        {
            get { return State.Position; }
            set
            {
                State.Position = value;
            }
        }

        public Quaternion Rotation
        {
            get { return State.Rotation; }
            set
            {
                State.Rotation = value;
            }
        }

        internal VehicleState State { get; private set; }

        internal DateTime LastUpd;

        public GameVehicle(VehicleState state)
        {
            ID = state.ID;
            State = state;
        }

        internal void UpdateState(VehicleState state, DateTime currentTime)
        {
            State = state;
            LastUpd = currentTime;
        }
    }
}
