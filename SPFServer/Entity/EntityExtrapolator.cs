using System;
using System.Collections.Generic;
using SPFLib.Enums;
using SPFLib.Types;
using SPFLib;

namespace SPFServer.Entity
{
    public sealed class EntityExtrapolator
    {
        /// <summary>
        /// Minimum amount of snapshots needed before interpolating.
        /// </summary>
        public const int SnapshotMin = 10;
        public const int InterpDelay = 200;

        private int validSnapshots = 0;

        private EntitySnapshot[] snapshots = new EntitySnapshot[20];

        public void QueueEntitySnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, Vector3 angles,
            ActiveTask task, ClientFlags flags, DateTime time)
        {
            for (int i = snapshots.Length - 1; i > 0; i--)
                snapshots[i] = snapshots[i - 1];
            snapshots[0] = new EntitySnapshot(position, velocity, rotation, angles, task, flags, time);
            validSnapshots = Math.Min(validSnapshots + 1, snapshots.Length);
        }

        public EntitySnapshot GetExtrapolatedPosition()
        {
            if (validSnapshots < SnapshotMin) return null;

            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            var lastState = snapshots[0];

            float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

            if (extrapolationLength < 0.70)
            {
                var rot = lastState.Rotation * Quaternion.Invert(snapshots[1].Rotation);

                rot.Normalize();

                float angle; Vector3 axis;

                rot.ToAngleAxis(out angle, out axis);

                var currentTime = interpolationTime - lastState.Timestamp;

                var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                if (angle > 180) angle -= 360;

                angle = angle * (float)t % 360;

                var position = Vector3.Lerp(lastState.Position, lastState.Position + lastState.Velocity * extrapolationLength, t);

                var quaternion = Quaternion.Slerp(lastState.Rotation, Quaternion.RotationAxis(axis, angle) * snapshots[1].Rotation, t);

                var velocity = lastState.Velocity;

                var angles = lastState.Angles;

                return new EntitySnapshot(position, velocity, quaternion, angles, lastState.ActiveTask, lastState.MovementFlags);
            }

            else return null;
        }
    }
}
