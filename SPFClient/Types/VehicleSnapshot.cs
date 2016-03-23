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
        public byte CurrentGear { get;  private set;}

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, byte currentGear, float wheelRotation, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            CurrentGear = currentGear;
            WheelRotation = wheelRotation;
            Timestamp = timestamp;
        }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, byte currentGear, float wheelRotation) :
        this(position, velocity, rotation, currentGear, wheelRotation, new DateTime())
        { }

        public VehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0x0, 0f, new DateTime())
        { }

    }
}
