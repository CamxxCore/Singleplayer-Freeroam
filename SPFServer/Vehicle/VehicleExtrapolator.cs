using System;
using System.Collections.Generic;
using SPFLib.Enums;
using SPFLib.Types;
using SPFLib;

namespace SPFServer.Vehicle
{
    public sealed class VehicleExtrapolator
    {
        /// <summary>
        /// Minimum amount of snapshots needed before interpolating.
        /// </summary>
        public const int SnapshotMin = 10;
        public const int InterpDelay = 200;

        private int validSnapshots = 0;

        private VehicleSnapshot[] snapshots = new VehicleSnapshot[20];

        public void QueueVehicleSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float wheelRotation, DateTime time)
        {
            for (int i = snapshots.Length - 1; i > 0; i--)
                snapshots[i] = snapshots[i - 1];
            snapshots[0] = new VehicleSnapshot(position, velocity, rotation, wheelRotation, time);
            validSnapshots = Math.Min(validSnapshots + 1, snapshots.Length);
        }

        public VehicleSnapshot GetExtrapolatedPosition()
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

                var position = lastState.Position + lastState.Velocity * extrapolationLength;

                var quaternion = Quaternion.RotationAxis(axis, angle) * snapshots[1].Rotation;

                var velocity = lastState.Velocity;

                return new VehicleSnapshot(position, velocity, quaternion);
            }

            else return null;
        }
    }
}
