using System;
using SPFLib.Enums;
using System.Security.Cryptography;
using SPFLib.Types;
using Lidgren.Network;
using System.Collections.Generic;
using System.Linq;

namespace SPFLib
{
    public static class Helpers
    {
        public static int GenerateUniqueID()
        {
            char[] chars = new char[62];
            string a = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            chars = a.ToCharArray();
            byte[] data = new byte[1];
            RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
            crypto.GetNonZeroBytes(data);
            var size = 8;
            data = new byte[size];
            crypto.GetNonZeroBytes(data);
            return BitConverter.ToInt32(data, 0);
        }

        public static DataType GetDataType(object obj)
        {
            return GetDataType(obj.GetType());
        }

        public static DataType GetDataType(Type t)
        {
            if (t == typeof(int))
            {
                return DataType.Int;
            }

            else if (t == typeof(float))
            {
                return DataType.Float;
            }

            else if (t == typeof(double))
            {
                return DataType.Double;
            }

            else if (t == typeof(bool))
            {
                return DataType.Bool;
            }

            else if (t == typeof(string))
            {
                return DataType.String;
            }

            else if (t == typeof(object))
            {
                return DataType.Object;
            }

            else throw new ArgumentException("t: Not a known type.");
        }

        public static LoginRequest ReadLoginRequest(this NetIncomingMessage message)
        {
            var req = new LoginRequest();
            req.UID = message.ReadInt32();
            req.Username = message.ReadString();
            return req;
        }

        public static void Write(this NetOutgoingMessage message, LoginRequest req)
        {
            message.Write(req.UID);
            message.Write(req.Username);
        }

        public static SessionState ReadSessionState(this NetIncomingMessage message)
        {
            var state = new SessionState();
            state.Sequence = message.ReadUInt32();
            state.Timestamp = new DateTime(message.ReadInt64());
            var clientCount = message.ReadInt32();
            state.Clients = GetClientStates(message, clientCount).ToArray();
            return state;
        }

        public static void Write(this NetOutgoingMessage message, SessionState state)
        {
            message.Write(state.Sequence);
            message.Write(state.Timestamp.Ticks);
            message.Write(state.Clients.Length);
            foreach (var client in state.Clients)
                message.Write(client);
        }

        private static IEnumerable<ClientState> GetClientStates(this NetIncomingMessage message, int clientCount)
        {
            ClientState[] clients = new ClientState[clientCount];
            for (int i = 0; i < clientCount; i++)
                yield return message.ReadClientState();
        }

        public static WeaponData ReadWeaponData(this NetIncomingMessage message)
        {
            var wd = new WeaponData();
            wd.Timestamp = new DateTime(message.ReadInt64());
            wd.HitCoords = message.ReadVector3();
            wd.WeaponDamage = message.ReadInt16();
            return wd;
        }

        public static void Write(this NetOutgoingMessage message, WeaponData wd)
        {
            message.Write(wd.Timestamp.Ticks);
            message.Write(wd.HitCoords);
            message.Write(wd.WeaponDamage);
        }

        public static SessionCommand ReadSessionCommand(this NetIncomingMessage message)
        {
            var cmd = new SessionCommand();
            cmd.UID = message.ReadInt32();
            cmd.Name = message.ReadString();
            cmd.Command = (CommandType)message.ReadInt16();
            return cmd;
        }

        public static void Write(this NetOutgoingMessage message, SessionCommand cmd)
        {
            message.Write(cmd.UID);
            message.Write(cmd.Name);
            message.Write((short)cmd.Command);
        }

        public static SessionSync ReadSessionSync(this NetIncomingMessage message)
        {
            var sync = new SessionSync();
            sync.ServerTime = new DateTime(message.ReadInt64());
            sync.ClientTime = new DateTime(message.ReadInt64());
            return sync;
        }

        public static void Write(this NetOutgoingMessage message, SessionSync sync)
        {
            message.Write(sync.ServerTime.Ticks);
            message.Write(sync.ClientTime.Ticks);
        }

        public static NativeCall ReadNativeCall(this NetIncomingMessage message)
        {
            var nc = new NativeCall();
            nc.FunctionName = message.ReadString();
            nc.ReturnType = (DataType)message.ReadByte();
            var argsCount = message.ReadInt16();
            nc.Args = new NativeArg[argsCount];
            for (int i = 0; i < argsCount; i++)
            {
               nc.Args[i] = ReadNativeArg(message);
            }
            return nc;
        }

        public static void Write(this NetOutgoingMessage message, NativeCall nc)
        {
            if (nc.Args.Length > short.MaxValue)
                throw new ArgumentOutOfRangeException("nc.Args: Array length exeeds maximum range of serializable items.");
            message.Write(nc.FunctionName);
            message.Write((byte)nc.ReturnType);
            message.Write((short)nc.Args.Length);
            foreach (var arg in nc.Args)
                message.Write(arg);
        }

        public static NativeArg ReadNativeArg(this NetIncomingMessage message)
        {
            var na = new NativeArg();
            na.Type = (DataType)message.ReadByte();
            var valueLen = message.ReadInt32();
            if (valueLen > 0)
            {
                byte[] bytes = message.ReadBytes(valueLen);
                na.Value = Serializer.DeserializeObject<object>(bytes);
            }
            return na;
        }

        public static void Write(this NetOutgoingMessage message, NativeArg na)
        {
            message.Write((byte)na.Type);
            if (na.Value != null)
            {
                var bytes = Serializer.SerializeObject(na.Value);
                message.Write(bytes.Length);
                message.Write(bytes);
            }
            else
                message.Write(0);
        }

        public static NativeCallback ReadNativeCallback(this NetIncomingMessage message)
        {
            var nc = new NativeCallback();
            nc.Type = (DataType)message.ReadByte();
            var valueLen = message.ReadInt32();
            if (valueLen > 0)
            {
                byte[] bytes = message.ReadBytes(valueLen);
                nc.Value = Serializer.DeserializeObject<object>(bytes);
            }
            return nc;
        }

        public static void Write(this NetOutgoingMessage message, NativeCallback nc)
        {
            message.Write((byte)nc.Type);
            if (nc.Value != null)
            {
                var bytes = Serializer.SerializeObject(nc.Value);
                message.Write(bytes.Length);
                message.Write(bytes);
            }

            else message.Write(0);
        }

        public static SessionEvent ReadSessionEvent(this NetIncomingMessage message)
        {
            var sEvent = new SessionEvent();
            sEvent.SenderID = message.ReadInt32();
            sEvent.SenderName = message.ReadString();
            sEvent.EventType = (EventType)message.ReadInt16();
            return sEvent;
        }

        public static void Write(this NetOutgoingMessage message, SessionEvent sEvent)
        {
            message.Write(sEvent.SenderID);
            message.Write(sEvent.SenderName);
            message.Write((short)sEvent.EventType);
        }

        public static SessionMessage ReadSessionMessage(this NetIncomingMessage message)
        {
            var msg = new SessionMessage();
            msg.Timestamp = new DateTime(message.ReadInt64());
            msg.SenderName = message.ReadString();
            int messageLength = message.ReadInt32();
            msg.Message = System.Text.Encoding.UTF8.GetString(message.ReadBytes(messageLength));
            return msg;
        }

        public static void Write(this NetOutgoingMessage message, SessionMessage sMessage)
        {
            message.Write(sMessage.Timestamp.Ticks);
            message.Write(sMessage.SenderName);
            message.Write(sMessage.Message.Length);
            message.Write(System.Text.Encoding.UTF8.GetBytes(sMessage.Message));
        }

        public static ClientState ReadClientState(this NetIncomingMessage message)
        {
            try
            {
                var state = new ClientState();
                state.ClientID = message.ReadInt32();
                state.InVehicle = message.ReadBoolean();
                state.PedID = message.ReadInt16();
                state.WeaponID = message.ReadInt16();
                state.Health = message.ReadInt16();

                if (!state.InVehicle)
                {
                    state.MovementFlags = (ClientFlags)message.ReadInt16();
                    state.ActiveTask = (ActiveTask)message.ReadInt16();
                    state.Position = message.ReadVector3();
                    state.Velocity = message.ReadVector3();
                    state.Angles = message.ReadVector3();
                    state.Rotation = message.ReadVector3().ToQuaternion();
                }

                else
                {
                    state.VehicleSeat = (VehicleSeat)message.ReadInt16();

                    state.VehicleState = new VehicleState();

                    state.VehicleState.Position = message.ReadVector3();

                    state.VehicleState.Velocity = message.ReadVector3();

                    state.VehicleState.Rotation = message.ReadQuaternion();

                    state.VehicleState.CurrentRPM = message.ReadInt16().Deserialize();

                    state.VehicleState.WheelRotation = message.ReadInt16().Deserialize();

                    state.VehicleState.Health = message.ReadInt16();

                    state.VehicleState.VehicleID = message.ReadInt16();

                    state.VehicleState.PrimaryColor = message.ReadByte();

                    state.VehicleState.SecondaryColor = message.ReadByte();

                    state.VehicleState.RadioStation = message.ReadByte();

                    state.VehicleState.Flags = (VehicleFlags)message.ReadByte();

                    state.VehicleState.ExtraFlags = message.ReadUInt16();

                    state.VehicleState.ID = message.ReadInt32();
                }

                return state;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, ClientState state)
        {
            message.Write(state.ClientID);
            message.Write(state.InVehicle);
            message.Write(state.PedID);
            message.Write(state.WeaponID);
            message.Write(state.Health);

            if (!state.InVehicle)
            {
                message.Write((short)state.MovementFlags);
                message.Write((short)state.ActiveTask);
                message.Write(state.Position);
                message.Write(state.Velocity);
                message.Write(state.Angles);
                message.Write(state.Rotation.ToVector3());
            }

            else
            {
                message.Write((short)state.VehicleSeat);

                message.Write(state.VehicleState.Position);

                message.Write(state.VehicleState.Velocity);

                message.Write(state.VehicleState.Rotation);

                message.Write(state.VehicleState.CurrentRPM.Serialize());

                message.Write(state.VehicleState.WheelRotation.Serialize());

                message.Write(state.VehicleState.Health);

                message.Write(state.VehicleState.VehicleID);

                message.Write(state.VehicleState.PrimaryColor);

                message.Write(state.VehicleState.SecondaryColor);

                message.Write(state.VehicleState.RadioStation);

                message.Write((byte)state.VehicleState.Flags);

                message.Write(state.VehicleState.ExtraFlags);

                message.Write(state.VehicleState.ID);
            }
        }

        public static Quaternion ReadQuaternion(this NetIncomingMessage message)
        {
            Quaternion q = new Quaternion();
            q.X = message.ReadFloat();
            q.Y = message.ReadFloat();
            q.Z = message.ReadFloat();
            q.W = message.ReadFloat();
            return q;
        }

        public static void Write(this NetOutgoingMessage message, Quaternion q)
        {
            message.Write(q.X);
            message.Write(q.Y);
            message.Write(q.Z);
            message.Write(q.W);

        //    return new Quaternion(vec.X, vec.Y, vec.Z, (float)Math.Sqrt(Math.Pow(1 - vec.X, 2) - Math.Pow(vec.Y, 2) - Math.Pow(vec.Z, 2)));
        }

        public static Vector3 ReadVector3(this NetIncomingMessage message)
        {
            Vector3 vec = new Vector3();
            vec.X = message.ReadFloat();
            vec.Y = message.ReadFloat();
            vec.Z = message.ReadFloat();
            return vec;
        }

        public static void Write(this NetOutgoingMessage message, Vector3 vec)
        {
            message.Write(vec.X);
            message.Write(vec.Y);
            message.Write(vec.Z);
        }

        public static RankData ReadRankData(this NetIncomingMessage message)
        {
            RankData rData = new RankData();
            rData.RankIndex = message.ReadInt32();
            rData.RankXP = message.ReadInt32();
            rData.NewXP = message.ReadInt32();
            return rData;
        }

        public static void Write(this NetOutgoingMessage message, RankData rData)
        {
            message.Write(rData.RankIndex);
            message.Write(rData.RankXP);
            message.Write(rData.NewXP);
        }

        public static short Serialize(this float fl)
        {
            return (short)(fl * 256);
        }

        public static float Deserialize(this short us)
        {
            return (us / 256f);
        }

        public static bool ValidateSequence(uint s1, uint s2, uint max)
        {
            return (s1 > s2) && (s1 - s2 <= max / 2) || (s2 > s1) && (s2 - s1 > max / 2);
        }

        public static Vector3 ToVector3(this Quaternion q)
        {
            q.Normalize();
            if (q.W < 0)
                return (new Vector3(-q.X, -q.Y, -q.Z));
            else
            return new Vector3(q.X, q.Y, q.Z);
        }

        public static Quaternion ToQuaternion(this Vector3 vec)
        {
            return new Quaternion(vec.X, vec.Y, vec.Z, (float)Math.Sqrt(Math.Pow(1 - vec.X, 2) - Math.Pow(vec.Y, 2) - Math.Pow(vec.Z, 2)));
        }
    }
}
