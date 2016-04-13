using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class BicycleSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float WheelRotation { get; private set; }
        public float Steering { get; private set; }

        public BicycleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, float steering, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            WheelRotation = wheelRotation;
            Steering = steering;
            Timestamp = timestamp;
        }

        public BicycleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, float steering) :
        this(position, velocity, rotation, wheelRotation, steering, new DateTime())
        { }

        public BicycleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, 0f, new DateTime())
        { }

    }
}
