using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    [Serializable]
    public class AIState : IEntityState
    {
        public string Name { get; set; }
        public int ClientID { get; set; }
        public short Health { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public PedType PedType { get; set; }

        public AIState() : this(-1, null, 100, PedType.Michael, new Vector3(), new Quaternion())
        {
        }

        public AIState(int id, string name, short health, PedType pedType, Vector3 position, Quaternion rotation)
        {
            ClientID = id;
            Name = name;
            Health = health;
            Position = position;
            Rotation = rotation;
            PedType = pedType;
        }
    }
}
