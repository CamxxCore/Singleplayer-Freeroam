using System;
using SPFLib.Enums;
using SPFLib.Types;

namespace SPFServer.Entity
{
    public class EntitySnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 Angles { get; private set; }
        public DateTime Timestamp { get; private set; }
        public ActiveTask ActiveTask { get; private set; }
        public ClientFlags MovementFlags { get; private set; }

        public EntitySnapshot(Vector3 position,
            Vector3 velocity,
            Quaternion rotation,
            Vector3 angles,
            ActiveTask activeTask,
            ClientFlags movementFlags,
            DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Angles = angles;
            ActiveTask = activeTask;
            MovementFlags = movementFlags;
            Timestamp = timestamp;
        }

        public EntitySnapshot(Vector3 position,
              Vector3 velocity,
              Quaternion rotation,
              Vector3 angles,
              ActiveTask activeTask,
              ClientFlags clientFlags) :
              this(position, velocity, rotation, angles, activeTask, clientFlags, new DateTime())
        { }

        public EntitySnapshot(Vector3 position,
            Vector3 velocity,
            Quaternion rotation,
            DateTime timestamp) :
            this(position, velocity, rotation, new Vector3(), 0, 0, timestamp)
        { }

        public EntitySnapshot(Vector3 position,
            Vector3 velocity,
            Quaternion rotation,
            Vector3 angles) :
            this(position, velocity, rotation, angles, 0, 0, new DateTime())
        { }

        public EntitySnapshot(Vector3 position,
            Vector3 velocity,
            Quaternion rotation) :
            this(position, velocity, rotation, new Vector3(), 0, 0, new DateTime())
        { }
    }
}
