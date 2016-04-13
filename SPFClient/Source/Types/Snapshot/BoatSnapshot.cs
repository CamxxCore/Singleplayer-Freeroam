using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class BoatSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float Steering { get; private set; }
        public float RPM { get; private set; }

        public BoatSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float steering, float rpm, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Steering = steering;
            RPM = rpm;
            Timestamp = timestamp;
        }

        public BoatSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float steering, float rpm) :
        this(position, velocity, rotation, steering, rpm, new DateTime())
        { }

        public BoatSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, 0f, new DateTime())
        { }

    }
}
