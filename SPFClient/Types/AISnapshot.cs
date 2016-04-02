using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class AISnapshot
    {
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }

        public AISnapshot(Vector3 position,
            Quaternion rotation,
            DateTime timestamp)
        {
            Position = position;
            Rotation = rotation;
            Timestamp = timestamp;
        }

        public AISnapshot(Vector3 position,
              Quaternion rotation) :
              this(position, rotation, new DateTime())
        { }
    }
}
