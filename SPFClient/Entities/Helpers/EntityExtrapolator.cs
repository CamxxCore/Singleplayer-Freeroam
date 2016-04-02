﻿using System;
using SPFLib;
using SPFClient.Types;
using GTA.Math;

namespace SPFClient.Entities
{
    public static class EntityExtrapolator
    {
        /// <summary>
        /// Minimum amount of snapshots needed before interpolating.
        /// </summary>
        public const int SnapshotMin = 10;
        public const int InterpDelay = 350;

        public static PlayerSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, PlayerSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            if (validSnapshots < SnapshotMin) return null;

            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        PlayerSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        PlayerSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var angles = Vector3.Lerp(lastState.Angles, newState.Angles, t);

                        return new PlayerSnapshot(position, velocity, quaternion, angles, newState.ActiveTask, newState.MovementFlags);  // Tuple<Vector3, Quaternion>(position, quaternion);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
                var lastState = extrpBuffer[0];

                float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                if (extrapolationLength < 0.70)
                {
                    var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[1].Rotation);

                    rot.Normalize();

                    float angle; Vector3 axis;

                    rot.ToAngleAxis(out angle, out axis);

                    var currentTime = interpolationTime - lastState.Timestamp;

                    var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                    if (angle > 180) angle -= 360;

                    angle = angle * (float)t % 360;

                    var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, lastState.Position + lastState.Velocity * extrapolationLength, t), lerpFactor);

                    var quaternion = Helpers.Slerp(lastState.Rotation, Helpers.RotationAxis(axis, angle) * extrpBuffer[1].Rotation, t);

                    var velocity = lastState.Velocity;

                    var angles = lastState.Angles;

                    return new PlayerSnapshot(position, velocity, quaternion, angles, lastState.ActiveTask, lastState.MovementFlags);
                }

                else return null;
            }
        }

        public static VehicleSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, VehicleSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            if (validSnapshots < SnapshotMin) return null;

            var timeNow = NetworkTime.Now;
            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        VehicleSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        VehicleSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) break;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (float)(newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var wRot = lastState.WheelRotation * (1.0f - t) + newState.WheelRotation * t;

                        return new VehicleSnapshot(position, velocity, quaternion, wRot);  // Tuple<Vector3, Quaternion>(position, quaternion);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
                var lastState = extrpBuffer[0];

                float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                //    if (extrapolationLength < 0.70)
                //    {
                var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[1].Rotation);

                rot.Normalize();

                float angle; Vector3 axis;

                rot.ToAngleAxis(out angle, out axis);

                var currentTime = interpolationTime - lastState.Timestamp;

                var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                if (angle > 180) angle -= 360;

                angle = angle * (float)t % 360;

                var position = lastState.Position + lastState.Velocity * extrapolationLength;

                var quaternion = Helpers.RotationAxis(axis, angle) * extrpBuffer[1].Rotation;

                var velocity = lastState.Velocity;


                return new VehicleSnapshot(position, velocity, quaternion);
                //  }

                //   else return null;
            }
        }

        public static AISnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, AISnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            if (validSnapshots < SnapshotMin) return null;

            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        AISnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        AISnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        return new AISnapshot(position, quaternion);  // Tuple<Vector3, Quaternion>(position, quaternion);
                    }
                }

                return extrpBuffer[0];
            }

            else return null;
        }
    }
}
