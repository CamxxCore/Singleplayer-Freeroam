using System;
using SPFLib.Enums;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class PlayerSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 AimCoords { get; private set; }
        public DateTime Timestamp { get; private set; }
        public ActiveTask ActiveTask { get; private set; }
        public ClientFlags MovementFlags { get; private set; }

        public PlayerSnapshot(Vector3 position, 
            Vector3 velocity, 
            Quaternion rotation, 
            Vector3 aimCoords, 
            ActiveTask activeTask, 
            ClientFlags movementFlags, 
            DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            AimCoords = aimCoords;
            ActiveTask = activeTask;
            MovementFlags = movementFlags;
            Timestamp = timestamp;
        }

        public PlayerSnapshot(Vector3 position,
              Vector3 velocity,
              Quaternion rotation,
              Vector3 aimCoords,
              ActiveTask activeTask,
              ClientFlags clientFlags) :
              this(position, velocity, rotation, aimCoords, activeTask, clientFlags, new DateTime())
        { }

        public PlayerSnapshot(Vector3 position, 
            Vector3 velocity,
            Quaternion rotation, 
            DateTime timestamp) :
            this(position, velocity, rotation, new Vector3(), 0, 0, timestamp)
        { }

        public PlayerSnapshot(Vector3 position, 
            Vector3 velocity, 
            Quaternion rotation ,
            Vector3 aimCoords) : 
            this (position, velocity, rotation, aimCoords, 0, 0, new DateTime())
        { }

        public PlayerSnapshot(Vector3 position, 
            Vector3 velocity, 
            Quaternion rotation) : 
            this(position, velocity, rotation, new Vector3(), 0, 0, new DateTime())
        { }
    }
}
