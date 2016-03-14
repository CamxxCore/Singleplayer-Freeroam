using System;
using System.Collections.Generic;
using SPFLib.Types;
using SPFLib;
using SPFClient.Types;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Entities
{
    public sealed class Extrapolator
    {
        /// <summary>
        /// Minimum amount of snapshots needed before interpolating.
        /// </summary>
        public const int SnapshotMin = 20;
        public const int InterpDelay = 200;

        private List<EntitySnapshot> unorderedPacketList = new List<EntitySnapshot>();

        public void QueueUnorderedPacket(IEntityState state, DateTime svTime, int pktID)
        {
            unorderedPacketList.Add(new EntitySnapshot(state.Position.Deserialize(),
                state.Velocity.Deserialize(),
                state.Rotation.Deserialize(),
                svTime,
                pktID));
        }

        public EntitySnapshot GetExtrapolatedPosition(Vector3 curPosition, Quaternion curRotation, EntitySnapshot[] extrpBuffer, int validSnapshots, float lerpFactor = 1.0f, bool forceExtrp = false)
        {
            if (validSnapshots < SnapshotMin) return null;

            var timeNow = PreciseDatetime.Now;
            var interpolationTime = timeNow - TimeSpan.FromMilliseconds(InterpDelay);

            List<EntitySnapshot> deletionQueue = new List<EntitySnapshot>();

            if (extrpBuffer[0].Timestamp > interpolationTime && !forceExtrp)
            {
                //UIManagement.UIManager.UISubtitleProxy("~g~interp");

                for (int i = 0; i < validSnapshots; i++)
                {
                    if (extrpBuffer[i].Timestamp <= interpolationTime || i == extrpBuffer.Length - 1)
                    {
                        EntitySnapshot newState = extrpBuffer[Math.Max(i - 1, 0)];
                        EntitySnapshot lastState = extrpBuffer[i];

                        /*if (unorderedPacketList.Count > 0)
                        {
                            if (unorderedPacketList.Count > 5) unorderedPacketList.Clear();

                            for (int p = unorderedPacketList.Count - 1; p > 0; p--)
                            {

                                // if the out of order packet is currentID - 1
                                if (unorderedPacketList[p].PacketID == newState.PacketID - 1)
                                {
                                    lastState = unorderedPacketList[p];
                                    deletionQueue.Add(unorderedPacketList[p]);
                                }

                                // if the out of order packet is currentID + 1
                                if (unorderedPacketList[p].PacketID == lastState.PacketID + 1)
                                {
                                    newState = unorderedPacketList[p];
                                    deletionQueue.Add(unorderedPacketList[p]);
                                }
                            }

                            deletionQueue.ForEach(x => unorderedPacketList.Remove(x));
                        }*/
                        
                        if (newState.Timestamp <= lastState.Timestamp) break;

                        float t = 0.0f;

                        var currentTime = interpolationTime - lastState.Timestamp;
                        var duration = newState.Timestamp - lastState.Timestamp;

                        if (duration.TotalMilliseconds > 1)
                        {
                          //  t = (float) (1.0f - ((newState.Timestamp - timeNow).TotalMilliseconds / InterpDelay));
                            //      UIManagement.UIManager.UINotifyProxy(string.Format("{0}, {1}", lastState.PacketID, newState.PacketID));
                            t = (float)((float)(currentTime.TotalMilliseconds / 1000f) / (float)(InterpDelay / 1000f));
                        }

                        var position = Vector3.Lerp(curPosition, Vector3.Lerp(lastState.Position, newState.Position, t), lerpFactor);

                        var quaternion = Helpers.Slerp(curRotation, Helpers.Slerp(lastState.Rotation, newState.Rotation, t), lerpFactor);

                        var velocity = Vector3.Lerp(lastState.Velocity, newState.Velocity, t);

                        var angles = Vector3.Lerp(lastState.Angles, newState.Angles, t);

                        return new EntitySnapshot(position, velocity, quaternion, angles, lastState.Timestamp + TimeSpan.FromMilliseconds(t * 1000), -1);  // Tuple<Vector3, Quaternion>(position, quaternion);
                    }
                }

                return extrpBuffer[0];
            }

            else
            {
            //    UIManagement.UIManager.UISubtitleProxy("~r~extrp");

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

                    return new EntitySnapshot(position, velocity, quaternion, angles);
                }

                else return null;
            }
        }
    }
}
