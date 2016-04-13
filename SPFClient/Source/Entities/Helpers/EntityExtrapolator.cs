using System;
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
        public const int SnapshotMin = 15;
        public const int InterpDelay = 300;

        public static PlayerSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, PlayerSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
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

                        var aimCoords = Vector3.Lerp(lastState.AimCoords, newState.AimCoords, t);

                        return new PlayerSnapshot(position, velocity, quaternion, aimCoords, newState.ActiveTask, newState.MovementFlags);  // Tuple<Vector3, Quaternion>(position, quaternion);
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

                    var aimCoords = lastState.AimCoords;

                    return new PlayerSnapshot(position, velocity, quaternion, aimCoords, lastState.ActiveTask, lastState.MovementFlags);
                }

                else return extrpBuffer[0];
            }
        }

        public static VehicleSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, VehicleSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
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

                        var wRot = lastState.WheelRotation * (1.0f - t) + newState.WheelRotation * t;

                        var steering = lastState.Steering * (1.0f - t) + newState.Steering * t;

                        var rpm = lastState.RPM * (1.0f - t) + newState.RPM * t;

                        return new VehicleSnapshot(position, new Vector3(), quaternion, wRot, steering, rpm);
                    }
                }
            }

            return extrpBuffer[0];

            //  }

            /*  else
              {
                  var lastState = extrpBuffer[0];

                  float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                  var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[0].Rotation);

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
              }*/
        }

        public static HeliSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, HeliSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        HeliSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        HeliSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        return new HeliSnapshot(position, velocity, quaternion, 1f);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
                var lastState = extrpBuffer[0];

                float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[1].Rotation);

                rot.Normalize();

                float angle; Vector3 axis;

                rot.ToAngleAxis(out angle, out axis);

                var currentTime = interpolationTime - lastState.Timestamp;

                var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                if (angle > 180) angle -= 360;

                angle = angle * t % 360;

                var position = lastState.Position + lastState.Velocity * extrapolationLength;

                var quaternion = Helpers.RotationAxis(axis, angle) * extrpBuffer[1].Rotation;

                var velocity = lastState.Velocity;

                return new HeliSnapshot(position, velocity, quaternion, 1f);
            }
        }

        public static PlaneSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, PlaneSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        PlaneSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        PlaneSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        var flaps = Helpers.Lerp(lastState.Flaps, newState.Flaps, t);

                        var stabs = Helpers.Lerp(lastState.Stabs, newState.Stabs, t);

                        var rudder = Helpers.Lerp(lastState.Rudder, newState.Rudder, t);

                        return new PlaneSnapshot(position, velocity, quaternion, flaps, stabs, rudder);
                    }
                }

                return extrpBuffer[0];
            }

            return extrpBuffer[0];
        }

        public static BicycleSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, BicycleSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        BicycleSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        BicycleSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        var wheelRot = Helpers.Lerp(lastState.WheelRotation, newState.WheelRotation, t);

                        var steering = Helpers.Lerp(lastState.Steering, newState.Steering, t);

                        return new BicycleSnapshot(position, velocity, quaternion, wheelRot, steering);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
                var lastState = extrpBuffer[0];

                float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[1].Rotation);

                rot.Normalize();

                float angle; Vector3 axis;

                rot.ToAngleAxis(out angle, out axis);

                var currentTime = interpolationTime - lastState.Timestamp;

                var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                if (angle > 180) angle -= 360;

                angle = angle * t % 360;

                var position = lastState.Position + lastState.Velocity * extrapolationLength;

                var quaternion = Helpers.RotationAxis(axis, angle) * extrpBuffer[1].Rotation;

                var velocity = lastState.Velocity;

                return new BicycleSnapshot(position, velocity, quaternion, lastState.WheelRotation, lastState.Steering);
            }
        }

        public static BoatSnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, BoatSnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            var timeNow = NetworkTime.Now;

            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        BoatSnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        BoatSnapshot lastState = extrpBuffer[i];

                        if (newState.Timestamp <= lastState.Timestamp) continue;

                        float t = 0.0f;

                        var currentTime = (interpolationTime - lastState.Timestamp).TotalMilliseconds / 1000f;
                        var duration = (newState.Timestamp - lastState.Timestamp).TotalMilliseconds / 1000f;

                        if (duration > 0.001f)
                        {
                            t = (float)(currentTime / duration);
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);
       
                        var steering = lastState.Steering * (1.0f - t) + newState.Steering * t;

                        var rpm = lastState.RPM * (1.0f - t) + newState.RPM * t;

                        return new BoatSnapshot(position, velocity, quaternion, steering, rpm);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
                var lastState = extrpBuffer[0];

                float extrapolationLength = ((float)(interpolationTime - lastState.Timestamp).TotalMilliseconds) / 1000.0f;

                var rot = lastState.Rotation * Quaternion.Invert(extrpBuffer[1].Rotation);

                rot.Normalize();

                float angle; Vector3 axis;

                rot.ToAngleAxis(out angle, out axis);

                var currentTime = interpolationTime - lastState.Timestamp;

                var t = (float)(currentTime.TotalMilliseconds / 1000) / extrapolationLength;

                if (angle > 180) angle -= 360;

                angle = angle * t % 360;

                var position = lastState.Position + lastState.Velocity * extrapolationLength;

                var quaternion = Helpers.RotationAxis(axis, angle) * extrpBuffer[1].Rotation;
          

                return new BoatSnapshot(position, lastState.Velocity, quaternion, lastState.Steering, lastState.RPM);
            }
        }
    }
}
