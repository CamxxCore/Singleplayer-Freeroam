using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class EntitySnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 Angles { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int PacketID { get; private set; }


        public EntitySnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, Vector3 angles, DateTime timestamp, int pktId)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Angles = angles;
            Timestamp = timestamp;
            PacketID = pktId;
        }

        public EntitySnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, DateTime timestamp, int pktID) :
            this(position, velocity, rotation, new Vector3(), timestamp, pktID)
        { }

        public EntitySnapshot(Vector3 position, Vector3 velocity, Quaternion rotation ,Vector3 angles) : 
            this (position, velocity, rotation, angles, new DateTime(), -1)
        { }

        public EntitySnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) : 
            this(position, velocity, rotation, new Vector3(), new DateTime(), -1)
        { }
    }
}
