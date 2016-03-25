using System;
using SPFLib.Types;

namespace SPFServer.Vehicle
{
    public class VehicleSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float WheelRotation { get; private set; }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            WheelRotation = wheelRotation;
            Timestamp = timestamp;
        }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation) :
        this(position, velocity, rotation, wheelRotation, new DateTime())
        { }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, new DateTime())
        { }

    }
}
