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
            try
            {
                var req = new LoginRequest();
                req.Revision = message.ReadInt32();
                req.UID = message.ReadInt32();
                req.Username = message.ReadString();
                return req;
            }
            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, LoginRequest req)
        {
            message.Write(req.Revision);
            message.Write(req.UID);
            message.Write(req.Username);
        }

        public static SessionState ReadSessionState(this NetIncomingMessage message)
        {
            try
            {
                var state = new SessionState();
                state.Sequence = message.ReadUInt32();
                state.Timestamp = new DateTime(message.ReadInt64());
                var count = message.ReadInt32();
                state.Vehicles = GetVehicleStates(message, count).ToArray();
                count = message.ReadInt32();
                state.Clients = GetClientStates(message, count).ToArray();
                return state;
            }
            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, SessionState state, bool sendNames)
        {
            message.Write(state.Sequence);
            message.Write(state.Timestamp.Ticks);
            message.Write(state.Vehicles.Length);
            foreach (var vehicle in state.Vehicles)
                message.Write(vehicle);
            message.Write(state.Clients.Length);
            foreach (var client in state.Clients)
                message.Write(client, sendNames);
        }

        private static IEnumerable<ClientState> GetClientStates(this NetIncomingMessage message, int count)
        {
            ClientState[] clients = new ClientState[count];
            for (int i = 0; i < count; i++)
                yield return message.ReadClientState();
        }

        private static IEnumerable<VehicleState> GetVehicleStates(this NetIncomingMessage message, int count)
        {
            VehicleState[] vehicles = new VehicleState[count];
            for (int i = 0; i < count; i++)
                yield return message.ReadVehicleState();
        }

        public static ImpactData ReadImpactData(this NetIncomingMessage message)
        {
            var wd = new ImpactData();
            wd.TargetID = message.ReadInt32();
            wd.HitCoords = message.ReadVector3();
            wd.WeaponDamage = message.ReadInt16();
            wd.Timestamp = new DateTime(message.ReadInt64());
            return wd;
        }

        public static void Write(this NetOutgoingMessage message, ImpactData wd)
        {
            message.Write(wd.TargetID);
            message.Write(wd.HitCoords);
            message.Write((short)wd.WeaponDamage);
            message.Write(wd.Timestamp.Ticks);
        }

        public static SessionCommand ReadSessionCommand(this NetIncomingMessage message)
        {
            var cmd = new SessionCommand();
            cmd.Command = (CommandType)message.ReadInt16();
            return cmd;
        }

        public static void Write(this NetOutgoingMessage message, SessionCommand cmd)
        {
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
            try
            {
                var nc = new NativeCall();
                nc.NetID = message.ReadInt32();
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

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, NativeCall nc)
        {
            if (nc.Args.Length > short.MaxValue)
                throw new ArgumentOutOfRangeException("nc.Args: Array length exeeds maximum range of serializable items.");
            message.Write(nc.NetID);
            message.Write(nc.FunctionName);
            message.Write((byte)nc.ReturnType);
            message.Write((short)nc.Args.Length);
            foreach (var arg in nc.Args)
                message.Write(arg);
        }

        public static NativeArg ReadNativeArg(this NetIncomingMessage message)
        {
            try
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

            catch
            {
                return null;
            }
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
            try
            {
                var nc = new NativeCallback();
                nc.NetID = message.ReadInt32();
                nc.Type = (DataType)message.ReadByte();
                var valueLen = message.ReadInt32();
                if (valueLen > 0)
                {
                    byte[] bytes = message.ReadBytes(valueLen);
                    nc.Value = Serializer.DeserializeObject<object>(bytes);
                }
                return nc;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, NativeCallback nc)
        {
            message.Write(nc.NetID);
            message.Write((byte)nc.Type);
            if (nc.Type != DataType.None && nc.Value != null)
            {
                var bytes = Serializer.SerializeObject(nc.Value);
                message.Write(bytes.Length);
                message.Write(bytes);
            }

            else message.Write(0);
        }

        public static ClientEvent ReadSessionEvent(this NetIncomingMessage message)
        {
            try
            {
                var sEvent = new ClientEvent();
                sEvent.ID = message.ReadInt32();
                sEvent.SenderName = message.ReadString();
                sEvent.EventType = (EventType)message.ReadInt16();
                return sEvent;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, ClientEvent sEvent)
        {
            message.Write(sEvent.ID);
            message.Write(sEvent.SenderName);
            message.Write((short)sEvent.EventType);
        }

        public static SessionMessage ReadSessionMessage(this NetIncomingMessage message)
        {
            try
            {
                var msg = new SessionMessage();
                msg.Timestamp = new DateTime(message.ReadInt64());
                msg.SenderName = message.ReadString();
                int messageLength = message.ReadInt32();
                msg.Message = System.Text.Encoding.UTF8.GetString(message.ReadBytes(messageLength));
                return msg;
            }

            catch
            {
                return null;
            }
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
                state.PedHash = PedIDToHash(message.ReadInt16());
                state.WeaponID = message.ReadInt16();
                state.Health = message.ReadInt16();

                if (!state.InVehicle)
                {
                    state.MovementFlags = (ClientFlags)message.ReadInt16();
                    state.ActiveTask = (ActiveTask)message.ReadInt16();
                    state.Position = message.ReadVector3();
                    state.Velocity = message.ReadVector3();
                    state.Angles = message.ReadVector3();
                    state.Rotation = message.ReadQuaternion();//ReadVector3().ToQuaternion();
                }

                else
                {
                    state.VehicleID = message.ReadInt32();
                    state.VehicleSeat = (VehicleSeat)message.ReadInt16();
                }

                int nameLen = message.ReadInt32();

                if (nameLen > 0)
                    state.Name = System.Text.Encoding.UTF8.GetString(message.ReadBytes(nameLen));

                return state;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, ClientState state, bool sendName)
        {
            message.Write(state.ClientID);
            message.Write(state.InVehicle);
            message.Write(PedHashtoID(state.PedHash));
            message.Write(state.WeaponID);
            message.Write(state.Health);

            if (!state.InVehicle)
            {
                message.Write((short)state.MovementFlags);
                message.Write((short)state.ActiveTask);
                message.Write(state.Position);
                message.Write(state.Velocity);
                message.Write(state.Angles);
                message.Write(state.Rotation);
            }

            else
            {
                message.Write(state.VehicleID);
                message.Write((short)state.VehicleSeat);
            }

            if (sendName && state.Name != null)
            {
                message.Write(state.Name.Length);
                message.Write(System.Text.Encoding.UTF8.GetBytes(state.Name));
            }
            else message.Write(0);
        }

        public static VehicleState ReadVehicleState(this NetIncomingMessage message)
        {
            try
            {
                var type = (VehicleType)message.ReadByte();

                if (type == VehicleType.Automobile)
                {
                    var state = new AutomobileState();

                    state.CurrentRPM = message.ReadInt16().Deserialize();

                    state.WheelRotation = message.ReadInt16().Deserialize();

                    state.Steering = message.ReadInt16().Deserialize();

                    #region global vehicle attributes

                    state.Position = message.ReadVector3();

                    state.Velocity = message.ReadVector3();

                    state.Rotation = message.ReadQuaternion();

                    state.Health = message.ReadInt16();

                    state.ModelID = message.ReadInt16();

                    state.PrimaryColor = message.ReadByte();

                    state.SecondaryColor = message.ReadByte();

                    state.RadioStation = message.ReadByte();

                    state.Flags = (VehicleFlags)message.ReadByte();

                    state.ExtraFlags = message.ReadUInt16();

                    state.ID = message.ReadInt32();

                    #endregion

                    return state;
                }

                else if (type == VehicleType.Plane)
                {
                    var state = new PlaneState();

                    state.Flaps = message.ReadInt16().Deserialize();

                    state.Stabs = message.ReadInt16().Deserialize();

                    state.Rudder = message.ReadInt16().Deserialize();

                    #region global vehicle attributes

                    state.Position = message.ReadVector3();

                    state.Velocity = message.ReadVector3();

                    state.Rotation = message.ReadQuaternion();

                    state.Health = message.ReadInt16();

                    state.ModelID = message.ReadInt16();

                    state.PrimaryColor = message.ReadByte();

                    state.SecondaryColor = message.ReadByte();

                    state.RadioStation = message.ReadByte();

                    state.Flags = (VehicleFlags)message.ReadByte();

                    state.ExtraFlags = message.ReadUInt16();

                    state.ID = message.ReadInt32();

                    #endregion

                    return state;
                }

                else if (type == VehicleType.Heli)
                {
                    var state = new HeliState();

                    state.RotorSpeed = message.ReadInt16().Deserialize();

                    #region global vehicle attributes

                    state.Position = message.ReadVector3();

                    state.Velocity = message.ReadVector3();

                    state.Rotation = message.ReadQuaternion();

                    state.Health = message.ReadInt16();

                    state.ModelID = message.ReadInt16();

                    state.PrimaryColor = message.ReadByte();

                    state.SecondaryColor = message.ReadByte();

                    state.RadioStation = message.ReadByte();

                    state.Flags = (VehicleFlags)message.ReadByte();

                    state.ExtraFlags = message.ReadUInt16();

                    state.ID = message.ReadInt32();

                    #endregion

                    return state;
                }

                else if (type == VehicleType.Bike)
                {
                    var state = new BicycleState();

                    state.WheelRotation = message.ReadInt16().Deserialize();

                    state.Steering = message.ReadInt16().Deserialize();

                    state.Velocity = message.ReadVector3();

                    state.Rotation = message.ReadQuaternion();

                    state.Health = message.ReadInt16();

                    state.ModelID = message.ReadInt16();

                    state.PrimaryColor = message.ReadByte();

                    state.SecondaryColor = message.ReadByte();

                    state.RadioStation = message.ReadByte();

                    state.Flags = (VehicleFlags)message.ReadByte();

                    state.ExtraFlags = message.ReadUInt16();

                    state.ID = message.ReadInt32();

                    return state;
                }

                else
                {
                    var state = new VehicleState();

                    state.Position = message.ReadVector3();

                    state.Velocity = message.ReadVector3();

                    state.Rotation = message.ReadQuaternion();

                    state.Health = message.ReadInt16();

                    state.ModelID = message.ReadInt16();

                    state.PrimaryColor = message.ReadByte();

                    state.SecondaryColor = message.ReadByte();

                    state.RadioStation = message.ReadByte();

                    state.Flags = (VehicleFlags)message.ReadByte();

                    state.ExtraFlags = message.ReadUInt16();

                    state.ID = message.ReadInt32();

                    return state;
                }
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, VehicleState state)
        {
            if (state is AutomobileState)
            {
                var activeState = state as AutomobileState;

                message.Write((byte)VehicleType.Automobile);

                message.Write(activeState.CurrentRPM.Serialize());

                message.Write(activeState.WheelRotation.Serialize());

                message.Write(activeState.Steering.Serialize());
            }

            else if (state is PlaneState)
            {
                var activeState = state as PlaneState;

                message.Write((byte)VehicleType.Plane);

                message.Write(activeState.Flaps.Serialize());

                message.Write(activeState.Stabs.Serialize());

                message.Write(activeState.Rudder.Serialize());
            }

            else if (state is HeliState)
            {
                message.Write((byte)VehicleType.Heli);

                message.Write((state as HeliState).RotorSpeed.Serialize());
            }

            else if (state is BicycleState)
            {
                message.Write((byte)VehicleType.Bike);

                message.Write((state as BicycleState).WheelRotation.Serialize());

                message.Write((state as BicycleState).Steering.Serialize());
            }

            else message.Write((byte)VehicleType.Any);

            message.Write(state.Position);

            message.Write(state.Velocity);

            message.Write(state.Rotation);

            message.Write(state.Health);

            message.Write(state.ModelID);

            message.Write(state.PrimaryColor);

            message.Write(state.SecondaryColor);

            message.Write(state.RadioStation);

            message.Write((byte)state.Flags);

            message.Write(state.ExtraFlags);

            message.Write(state.ID);
        }

        public static Quaternion ReadQuaternion(this NetIncomingMessage message)
        {
            try
            {
                Quaternion q = new Quaternion();
                q.X = message.ReadFloat();
                q.Y = message.ReadFloat();
                q.Z = message.ReadFloat();
                q.W = message.ReadFloat();
                return q;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, Quaternion q)
        {
            message.Write(q.X);
            message.Write(q.Y);
            message.Write(q.Z);
            message.Write(q.W);
        }

        public static void Write(this NetOutgoingMessage message, SessionAck ack)
        {
            message.Write((byte)ack.Type);

            switch (ack.Type)
            {
                case AckType.VehicleSync:
                case AckType.PedSync:
                    message.Write((int)ack.Value);
                    break;
            }
        }

        public static SessionAck ReadSessionAck(this NetIncomingMessage message)
        {
            SessionAck ack = new SessionAck();
            ack.Type = (AckType) message.ReadByte();

            switch (ack.Type)
            {
                case AckType.VehicleSync:
                case AckType.PedSync:
                    ack.Value = message.ReadInt32();
                    break;
            }

            return ack;
        }

        public static Vector3 ReadVector3(this NetIncomingMessage message)
        {
            try
            {
                Vector3 vec = new Vector3();
                vec.X = message.ReadFloat();
                vec.Y = message.ReadFloat();
                vec.Z = message.ReadFloat();
                return vec;
            }

            catch
            {
                return null;
            }
        }

        public static void Write(this NetOutgoingMessage message, Vector3 vec)
        {
            message.Write(vec.X);
            message.Write(vec.Y);
            message.Write(vec.Z);
        }

        public static RankData ReadRankData(this NetIncomingMessage message)
        {
            try
            {
                RankData rData = new RankData();
                rData.RankIndex = message.ReadInt32();
                rData.RankXP = message.ReadInt32();
                rData.NewXP = message.ReadInt32();
                return rData;
            }

            catch
            {
                return null;
            }
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


        /// <summary>
        /// Gets a VehicleHash from its enum index / ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static VehicleHash VehicleIDToHash(short id)
        {
            try
            {
                var values = typeof(VehicleHash).GetEnumValues();

                if (id < 0 || id > values.Length)
                    throw new ArgumentOutOfRangeException("id: out of range");

                return (VehicleHash)values.GetValue(id);
            }

            catch
            {
                return VehicleHash.Ninef;
            }
        }

        /// <summary>
        /// Gets an enum index/ ID from a VehicleHash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static short VehicleHashtoID(VehicleHash hash)
        {
            try
            {
                var values = typeof(VehicleHash).GetEnumValues();

                for (int i = 0; i < values.Length; i++)
                {
                    if ((VehicleHash)values.GetValue(i) == hash)
                        return (short)i;
                }
            }

            catch
            { }

            return 0;
        }

        /// <summary>
        /// Gets a VehicleHash from its enum index / ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PedHash PedIDToHash(short id)
        {
            try
            {
                var values = typeof(PedHash).GetEnumValues();

                if (id < 0 || id > values.Length)
                    throw new ArgumentOutOfRangeException("id: out of range");

                return (PedHash)values.GetValue(id);
            }

            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets an enum index/ ID from a PedHash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static short PedHashtoID(PedHash hash)
        {
            try
            {
                var values = typeof(PedHash).GetEnumValues();

                for (int i = 0; i < values.Length; i++)
                {
                    if ((PedHash)values.GetValue(i) == hash)
                        return (short)i;
                }
            }

            catch
            { }

            return 0;
        }


        public static VehicleState VehicleStateFromArgs(VehicleHash hash, Vector3 position,
            Quaternion rotation, byte primaryColor, byte secondaryColor)
        {
            return VehicleStateFromArgs(GenerateUniqueID(), hash, position, rotation, primaryColor, secondaryColor);
        }

        public static VehicleState VehicleStateFromArgs(int id, VehicleHash hash,
            Vector3 position, Quaternion rotation, byte primaryColor, byte secondaryColor)
        {
            var modelID = VehicleHashtoID(hash);

            switch (hash)
            {
                case VehicleHash.Hotknife:
                case VehicleHash.TipTruck:
                case VehicleHash.Youga:
                case VehicleHash.Glendale:
                case VehicleHash.Dominator:
                case VehicleHash.Kalahari:
                case VehicleHash.Coquette:
                case VehicleHash.BType:
                case VehicleHash.Boxville3:
                case VehicleHash.Baller2:
                case VehicleHash.FreightCar:
                case VehicleHash.Buffalo3:
                case VehicleHash.FreightCont2:
                case VehicleHash.GBurrito2:
                case VehicleHash.Crusader:
                case VehicleHash.CogCabrio:
                case VehicleHash.Vacca:
                case VehicleHash.Gauntlet2:
                case VehicleHash.Chino:
                case VehicleHash.PropTrailer:
                case VehicleHash.Surano:
                case VehicleHash.RakeTrailer:
                case VehicleHash.Turismor:
                case VehicleHash.Kuruma2:
                case VehicleHash.Infernus:
                case VehicleHash.Boxville4:
                case VehicleHash.Handler:
                case VehicleHash.PoliceT:
                case VehicleHash.Tornado:
                case VehicleHash.Lguard:
                case VehicleHash.Mixer2:
                case VehicleHash.Huntley:
                case VehicleHash.Fusilade:
                case VehicleHash.UtilityTruck:
                case VehicleHash.Voodoo2:
                case VehicleHash.BoatTrailer:
                case VehicleHash.Dune2:
                case VehicleHash.Khamelion:
                case VehicleHash.Packer:
                case VehicleHash.TankerCar:
                case VehicleHash.Romero:
                case VehicleHash.Barracks3:
                case VehicleHash.FreightGrain:
                case VehicleHash.Surfer:
                case VehicleHash.TrailerSmall:
                case VehicleHash.Dukes:
                case VehicleHash.Speedo2:
                case VehicleHash.SlamVan:
                case VehicleHash.Sadler2:
                case VehicleHash.Buffalo2:
                case VehicleHash.Pranger:
                case VehicleHash.Ztype:
                case VehicleHash.Alpha:
                case VehicleHash.Rhino:
                case VehicleHash.Coquette3:
                case VehicleHash.SlamVan2:
                case VehicleHash.Rhapsody:
                case VehicleHash.BJXL:
                case VehicleHash.Biff:
                case VehicleHash.Sentinel2:
                case VehicleHash.Habanero:
                case VehicleHash.Intruder:
                case VehicleHash.UtilityTruck2:
                case VehicleHash.Burrito4:
                case VehicleHash.Mule:
                case VehicleHash.Mesa:
                case VehicleHash.FreightCont1:
                case VehicleHash.Casco:
                case VehicleHash.Pony2:
                case VehicleHash.Sultan:
                case VehicleHash.Sandking2:
                case VehicleHash.Coquette2:
                case VehicleHash.GrainTrailer:
                case VehicleHash.Freight:
                case VehicleHash.Ninef:
                case VehicleHash.Blista2:
                case VehicleHash.JB700:
                case VehicleHash.BobcatXL:
                case VehicleHash.Barracks2:
                case VehicleHash.Pigalle:
                case VehicleHash.Superd:
                case VehicleHash.BfInjection:
                case VehicleHash.FBI:
                case VehicleHash.Burrito5:
                case VehicleHash.Caddy:
                case VehicleHash.Rumpo:
                case VehicleHash.Ambulance:
                case VehicleHash.Dubsta:
                case VehicleHash.Seminole:
                case VehicleHash.Marshall:
                case VehicleHash.Landstalker:
                case VehicleHash.Airbus:
                case VehicleHash.Serrano:
                case VehicleHash.Vestra:
                case VehicleHash.Oracle:
                case VehicleHash.Sentinel:
                case VehicleHash.Flatbed:
                case VehicleHash.Warrener:
                case VehicleHash.Tractor3:
                case VehicleHash.Paradise:
                case VehicleHash.Forklift:
                case VehicleHash.Picador:
                case VehicleHash.Hauler:
                case VehicleHash.Tornado2:
                case VehicleHash.Stinger:
                case VehicleHash.Airtug:
                case VehicleHash.Windsor:
                case VehicleHash.Tractor:
                case VehicleHash.RancherXL:
                case VehicleHash.T20:
                case VehicleHash.Dilettante2:
                case VehicleHash.Stratum:
                case VehicleHash.RapidGT2:
                case VehicleHash.Bison3:
                case VehicleHash.Stockade:
                case VehicleHash.Tornado3:
                case VehicleHash.DLoader:
                case VehicleHash.Washington:
                case VehicleHash.Mower:
                case VehicleHash.TR3:
                case VehicleHash.Besra:
                case VehicleHash.Peyote:
                case VehicleHash.Camper:
                case VehicleHash.Bulldozer:
                case VehicleHash.Fugitive:
                case VehicleHash.Police3:
                case VehicleHash.Trash:
                case VehicleHash.Sheriff2:
                case VehicleHash.Stalion:
                case VehicleHash.RancherXL2:
                case VehicleHash.FireTruck:
                case VehicleHash.Tourbus:
                case VehicleHash.Taco:
                case VehicleHash.Tanker2:
                case VehicleHash.Osiris:
                case VehicleHash.Cavalcade:
                case VehicleHash.TrailerLogs:
                case VehicleHash.Futo:
                case VehicleHash.Police:
                case VehicleHash.Benson:
                case VehicleHash.Insurgent2:
                case VehicleHash.Bison2:
                case VehicleHash.Carbonizzare:
                case VehicleHash.TR2:
                case VehicleHash.TR4:
                case VehicleHash.Pounder:
                case VehicleHash.UtilityTruck3:
                case VehicleHash.Rocoto:
                case VehicleHash.DockTrailer:
                case VehicleHash.Phantom:
                case VehicleHash.Dump:
                case VehicleHash.Blazer:
                case VehicleHash.Manana:
                case VehicleHash.Stunt:
                case VehicleHash.Guardian:
                case VehicleHash.StingerGT:
                case VehicleHash.Technical:
                case VehicleHash.Phoenix:
                case VehicleHash.Tractor2:
                case VehicleHash.Coach:
                case VehicleHash.Mesa3:
                case VehicleHash.Trailers3:
                case VehicleHash.Mule3:
                case VehicleHash.Rebel2:
                case VehicleHash.Tornado4:
                case VehicleHash.PBus:
                case VehicleHash.Feltzer2:
                case VehicleHash.Boxville:
                case VehicleHash.Police4:
                case VehicleHash.Stretch:
                case VehicleHash.RapidGT:
                case VehicleHash.Asterope:
                case VehicleHash.Surge:
                case VehicleHash.Premier:
                case VehicleHash.Emperor2:
                case VehicleHash.Insurgent:
                case VehicleHash.Asea:
                case VehicleHash.Asea2:
                case VehicleHash.Gauntlet:
                case VehicleHash.PoliceOld2:
                case VehicleHash.Rumpo2:
                case VehicleHash.Granger:
                case VehicleHash.TVTrailer:
                case VehicleHash.GBurrito:
                case VehicleHash.Burrito3:
                case VehicleHash.Rubble:
                case VehicleHash.Scrap:
                case VehicleHash.Bullet:
                case VehicleHash.SabreGT:
                case VehicleHash.Sheriff:
                case VehicleHash.Velum:
                case VehicleHash.Double:
                case VehicleHash.Dune:
                case VehicleHash.Radi:
                case VehicleHash.FBI2:
                case VehicleHash.ArmyTrailer2:
                case VehicleHash.Police2:
                case VehicleHash.Voltic:
                case VehicleHash.Trailers2:
                case VehicleHash.Feltzer3:
                case VehicleHash.Gresley:
                case VehicleHash.PoliceOld1:
                case VehicleHash.Brawler:
                case VehicleHash.Stanier:
                case VehicleHash.ArmyTrailer:
                case VehicleHash.Ninef2:
                case VehicleHash.Sanchez2:
                case VehicleHash.Prairie:
                case VehicleHash.Bodhi2:
                case VehicleHash.Zentorno:
                case VehicleHash.Kuruma:
                case VehicleHash.TRFlat:
                case VehicleHash.Burrito:
                case VehicleHash.TowTruck:
                case VehicleHash.Surfer2:
                case VehicleHash.Cheetah:
                case VehicleHash.Jester:
                case VehicleHash.EntityXF:
                case VehicleHash.Ingot:
                case VehicleHash.Blazer3:
                case VehicleHash.Trash2:
                case VehicleHash.Schafter2:
                case VehicleHash.Emperor3:
                case VehicleHash.Dubsta3:
                case VehicleHash.Adder:
                case VehicleHash.Rebel:
                case VehicleHash.ArmyTanker:
                case VehicleHash.Blade:
                case VehicleHash.Riot:
                case VehicleHash.Zion2:
                case VehicleHash.Sandking:
                case VehicleHash.Issi2:
                case VehicleHash.Primo:
                case VehicleHash.Fq2:
                case VehicleHash.Dilettante:
                case VehicleHash.Zion:
                case VehicleHash.Jester2:
                case VehicleHash.RentalBus:
                case VehicleHash.Furoregt:
                case VehicleHash.Submersible2:
                case VehicleHash.Mule2:
                case VehicleHash.Comet2:
                case VehicleHash.Banshee:
                case VehicleHash.Tailgater:
                case VehicleHash.Cutter:
                case VehicleHash.CableCar:
                case VehicleHash.Taxi:
                case VehicleHash.TipTruck2:
                case VehicleHash.Dominator2:
                case VehicleHash.Burrito2:
                case VehicleHash.Docktug:
                case VehicleHash.Trailers:
                case VehicleHash.Ripley:
                case VehicleHash.Monster:
                case VehicleHash.Vigero:
                case VehicleHash.Barracks:
                case VehicleHash.Baller:
                case VehicleHash.Patriot:
                case VehicleHash.Cavalcade2:
                case VehicleHash.Mixer:
                case VehicleHash.FreightTrailer:
                case VehicleHash.Mesa2:
                case VehicleHash.Schwarzer:
                case VehicleHash.Tanker:
                case VehicleHash.Bus:
                case VehicleHash.Emperor:
                case VehicleHash.Buccaneer:
                case VehicleHash.RatLoader:
                case VehicleHash.Massacro2:
                case VehicleHash.Jackal:
                case VehicleHash.Seashark2:
                case VehicleHash.Sadler:
                case VehicleHash.Blista3:
                case VehicleHash.F620:
                case VehicleHash.RatLoader2:
                case VehicleHash.Elegy2:
                case VehicleHash.Caddy2:
                case VehicleHash.Oracle2:
                case VehicleHash.Virgo:
                case VehicleHash.Predator:
                case VehicleHash.TowTruck2:
                case VehicleHash.Monroe:
                case VehicleHash.Panto:
                case VehicleHash.Stalion2:
                case VehicleHash.BaleTrailer:
                case VehicleHash.Dubsta2:
                case VehicleHash.Felon:
                case VehicleHash.Penumbra:
                case VehicleHash.Bifta:
                case VehicleHash.Blista:
                case VehicleHash.Dukes2:
                case VehicleHash.Minivan:
                case VehicleHash.Buffalo:
                case VehicleHash.Boxville2:
                case VehicleHash.Ruiner:
                case VehicleHash.Stockade3:
                case VehicleHash.Massacro:
                case VehicleHash.Journey:
                case VehicleHash.Pony:
                case VehicleHash.Felon2:
                case VehicleHash.Blazer2:
                case VehicleHash.Policeb:
                case VehicleHash.Bison:
                case VehicleHash.Regina:
                case VehicleHash.Exemplar:
                    return new AutomobileState(id, position, new Vector3(), rotation, primaryColor, secondaryColor, modelID);
                //heli
                case VehicleHash.Polmav:
                case VehicleHash.Valkyrie:
                case VehicleHash.Swift2:
                case VehicleHash.Swift:
                case VehicleHash.Savage:
                case VehicleHash.Maverick:
                case VehicleHash.Frogger2:
                case VehicleHash.Frogger:
                case VehicleHash.Cargobob:
                case VehicleHash.Cargobob2:
                case VehicleHash.Cargobob3:
                case VehicleHash.Skylift:
                case VehicleHash.Buzzard:
                case VehicleHash.Annihilator:
                case VehicleHash.Buzzard2:
                    return new HeliState(id, position, new Vector3(), rotation, primaryColor, secondaryColor, modelID);
                //plane
                case VehicleHash.Velum2:
                case VehicleHash.Dodo:
                case VehicleHash.Mammatus:
                case VehicleHash.Duster:
                case VehicleHash.Hydra:
                case VehicleHash.Cuban800:
                case VehicleHash.Titan:
                case VehicleHash.Lazer:
                case VehicleHash.CargoPlane:
                case VehicleHash.Jet:
                case VehicleHash.Miljet:
                case VehicleHash.Blimp2:
                case VehicleHash.Shamal:
                case VehicleHash.Luxor2:
                case VehicleHash.Luxor:
                case VehicleHash.Blimp:
                    return new PlaneState(id, position, new Vector3(), rotation, primaryColor, secondaryColor, modelID);
                /*    //boat
                    case VehicleHash.Dinghy3:
                    case VehicleHash.Tropic:
                    case VehicleHash.Toro:
                    case VehicleHash.Suntrap:
                    case VehicleHash.Submersible:
                    case VehicleHash.Squalo:
                    case VehicleHash.Speeder:
                    case VehicleHash.Seashark:
                    case VehicleHash.Marquis:
                    case VehicleHash.Dinghy:
                    case VehicleHash.Jetmax:
                    case VehicleHash.Dinghy2:
                    case VehicleHash.Speedo:

                    //bike
                    case VehicleHash.Vader:
                    case VehicleHash.Sovereign:
                    case VehicleHash.Ruffian:
                    case VehicleHash.PCJ:
                    case VehicleHash.Nemesis:
                    case VehicleHash.Lectro:
                    case VehicleHash.Innovation:
                    case VehicleHash.Hexer:
                    case VehicleHash.Hakuchou:
                    case VehicleHash.Enduro:
                    case VehicleHash.Daemon:
                    case VehicleHash.CarbonRS:
                    case VehicleHash.Bagger:
                    case VehicleHash.Akuma:
                    case VehicleHash.Sanchez:
                    case VehicleHash.Bati2:
                    case VehicleHash.Bati:
                    case VehicleHash.Faggio2:
                    case VehicleHash.Thrust:
                    case VehicleHash.Vindicator:*/
                // cycle
                case VehicleHash.TriBike3:
                case VehicleHash.Bmx:
                case VehicleHash.TriBike:
                case VehicleHash.TriBike2:
                case VehicleHash.Fixter:
                case VehicleHash.Cruiser:
                case VehicleHash.Scorcher:
                    return new BicycleState(id, position, new Vector3(), rotation, primaryColor, secondaryColor, modelID);

                default: return new VehicleState(id, position, new Vector3(), rotation, primaryColor, secondaryColor, modelID);
            }
        }
    }
}
