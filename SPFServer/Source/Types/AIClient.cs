using System;
using SPFLib.Types;

namespace SPFServer.Types
{
    public class AIClient : IGameEntity
    {
        public int ID { get; set; }

        public string Name { get; }

        public bool NeedsUpdate { get; set; }

        public Vector3 Position
        {
            get { return State.Position; }
            set
            {
                State.Position = value;
                NeedsUpdate = true;
            }
        }

        public Quaternion Rotation
        {
            get { return State.Rotation; }
            set
            {
                State.Rotation = value;
                NeedsUpdate = true;
            }
        }

        internal AIState State { get; private set; }

        internal DateTime LastUpd;

        public AIClient(string name, AIState initialState)
        {
            ID = SPFLib.Helpers.GenerateUniqueID();
            Name = name;
            State = initialState;
            State.ClientID = ID;
            NeedsUpdate = true;
        }

        internal void UpdateState(AIState state, DateTime currentTime)
        {
            State = state;
            State.ClientID = ID;
            State.Name = Name;
            LastUpd = currentTime;
            NeedsUpdate = true;
        }
    }
}
