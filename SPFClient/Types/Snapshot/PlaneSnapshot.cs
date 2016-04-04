using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class PlaneSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float Flaps { get; private set; }
        public float Stabs { get; private set; }
        public float Rudder { get; private set; }

        public PlaneSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float flaps, float stabs, float rudder, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Flaps = flaps;
            Stabs = stabs;
            Rudder = rudder;
            Timestamp = timestamp;
        }

        public PlaneSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float flaps, float stabs, float rudder) :
        this(position, velocity, rotation, flaps, stabs, rudder, new DateTime())
        { }

        public PlaneSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, 0f, 0f, new DateTime())
        { }

    }
}
