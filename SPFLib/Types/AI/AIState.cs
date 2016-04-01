using System;
using SPFLib.Enums;

namespace SPFLib.Types
{
    [Serializable]
    public class AIState : IEntityState
    {
        public string Name { get; set; }
        public int ClientID { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public PedType PedType { get; set; }

        public AIState() : this(null, PedType.Michael, new Vector3(), new Quaternion())
        {
        }

        public AIState(string name, PedType pedType, Vector3 position, Quaternion rotation)
        {
            ClientID = Helpers.GenerateUniqueID();
            Name = name;
            Position = position;
            Rotation = rotation;
            PedType = pedType;
        }
    }
}
