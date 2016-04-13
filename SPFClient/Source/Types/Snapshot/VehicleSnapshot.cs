using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class VehicleSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float WheelRotation { get; private set; }
        public float Steering { get; private set; }
        public float RPM { get; private set; }


        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, float steering, float rpm, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            WheelRotation = wheelRotation;
            Steering = steering;
            RPM = rpm;
            Timestamp = timestamp;
        }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, float steering, float rpm) :
        this(position, velocity, rotation, wheelRotation, steering, rpm, new DateTime())
        { }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, 0f, 0f, new DateTime())
        { }

    }
}
