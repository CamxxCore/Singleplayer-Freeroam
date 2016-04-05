using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFClient.Types;
using SPFLib.Enums;
using System;
using System.Drawing;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Entities
{
    public delegate void NetworkVehicleEventHandler(DateTime timeSent, VehicleState e);

    public class NetworkVehicle : GameEntity
    {
        public readonly int ID;
        public readonly ulong Address;

        private int lastVehicleHash;
        private short lastVehicleID;
        private int currentRadioStation;

        private readonly ulong wheelsPtr;

        public event NetworkVehicleEventHandler OnUpdateReceived;

        public DateTime LastUpdateTime { get; private set; }

        private VehicleState lastReceivedState;

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkVehicle(VehicleState state) :
            base(SetupVehicle(state.Position.Deserialize(),
                state.Rotation.Deserialize(),
                state.VehicleID,
                state.Flags.HasFlag(VehicleFlags.DoorsLocked),
                state.PrimaryColor,
                state.SecondaryColor))
        {
            ID = state.ID;
            Address = MemoryAccess.GetEntityAddress(Handle);
            wheelsPtr = GetWheelPointer(Address);
            lastReceivedState = state;
        }

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkVehicle(Vehicle vehicle, int id) :
            base(vehicle)
        {
            ID = id;
            Address = MemoryAccess.GetEntityAddress(Handle);
            wheelsPtr = GetWheelPointer(Address);
            lastReceivedState = new VehicleState();
        }

        internal static Vehicle SetupVehicle(Vector3 position, Quaternion rotation, short vehID, bool doorsLocked, byte primaryColor, byte secondaryColor)
        {
            var model = new Model(Helpers.VehicleIDToHash(vehID));

            if (!model.IsLoaded)
                model.Request(1000);

            Vector3 vec; float angle;

            Helpers.ToAngleAxis(rotation, out angle, out vec);

            //Handle the vehicle
            var vehicle = World.CreateVehicle(model, position);

            vehicle.Quaternion = rotation;

            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, primaryColor, secondaryColor);

            var dt = DateTime.Now + TimeSpan.FromMilliseconds(300);

            while (DateTime.Now < dt) Script.Yield();

            vehicle.IsInvincible = true;

            vehicle.EngineRunning = true;

            Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, vehicle.Handle, true);

            Function.Call(Hash.SET_ENTITY_COLLISION, vehicle.Handle, 1, 0);

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, vehicle.Handle, true, true);

            Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, vehicle.Handle, 0.0f);

            return vehicle;
        }

        public virtual void HandleStateUpdate(DateTime timeSent, VehicleState state)
        {
            if ((state.Flags & VehicleFlags.Exploded) != 0 && IsAlive)
            {
                IsInvincible = false;
                Function.Call(Hash.EXPLODE_VEHICLE, Handle, true, false);
                return;
            }

            if ((state.Flags & VehicleFlags.DoorsLocked) != 
                (lastReceivedState?.Flags & VehicleFlags.DoorsLocked))
            {
                Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED_FOR_PLAYER, Handle, 
                    Game.Player.Handle, state.Flags.HasFlag(VehicleFlags.DoorsLocked));
            }

            if (state.RadioStation != currentRadioStation)
            {
                if (state.RadioStation == byte.MaxValue)
                    Function.Call(Hash.SET_VEH_RADIO_STATION, Handle, "OFF");
                else
                {
                    var stName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME, state.RadioStation);
                    Function.Call(Hash.SET_VEH_RADIO_STATION, Handle, stName);
                }

                currentRadioStation = state.RadioStation;
            }

            Function.Call(Hash.SET_VEHICLE_COLOURS, Handle, state.PrimaryColor, state.SecondaryColor);

            lastReceivedState = state;

            LastUpdateTime = SPFLib.NetworkTime.Now;

            OnUpdateReceived?.Invoke(timeSent, state);
        }

        internal byte GetRadioStation()
        {
            return Convert.ToByte(
                Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX));
        }

        public short GetVehicleID()
        {
            if (Model.Hash != lastVehicleHash)
            {
                lastVehicleHash = Model.Hash;
                lastVehicleID = Helpers.VehicleHashtoID((VehicleHash)lastVehicleHash);
            }
            return lastVehicleID;
        }

        internal Color GetVehicleColor()
        {
            OutputArgument outR, outG, outB;
            outR = outG = outB = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOR, Handle, outR, outG, outB);

            return Color.FromArgb(outR.GetResult<byte>(), 
                outG.GetResult<byte>(), 
                outB.GetResult<byte>());
        }

        public float GetWheelRotation()
        {
            if (wheelsPtr <= 0) return 0;
            return MemoryAccess.GetWheelRotation(wheelsPtr, 0);
        }

        public void SetWheelRotation(float value)
        {
            if (wheelsPtr <= 0) return;
            for (int i = 0; i < 4; i++)
                MemoryAccess.SetWheelRotation(wheelsPtr, i, value);
        }

        public float GetSteering()
        {
            if (Address <= 0) return 0;
            return MemoryAccess.ReadSingle(Address + Offsets.CVehicle.Steering);
        }

        public void SetSteering(float value)
        {
            if (Address <= 0) return;
            MemoryAccess.WriteSingle(Address + Offsets.CVehicle.Steering, value);
        }

        private static ulong GetWheelPointer(ulong baseAddress)
        {
            if (baseAddress <= 0) return 0;

            var wheelsAddr = MemoryAccess.ReadUInt64(baseAddress + Offsets.CVehicle.WheelsPtr);

            if (baseAddress > wheelsAddr)
            {
                wheelsAddr = ulong.Parse(baseAddress.ToString("X").Substring(0, 3) + 
                    wheelsAddr.ToString("X"), System.Globalization.NumberStyles.HexNumber);
            }

            return wheelsAddr;
        }

        public virtual VehicleState GetState()
        {
            var v = new Vehicle(Handle);

            var state = new VehicleState(ID,
                Position.Serialize(),
                Velocity.Serialize(),
                Quaternion.Serialize(),
                0, Convert.ToInt16(Health),
                (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                GetRadioStation(),
                GetVehicleID());

            return state;
        }

        public override void Update()
        {
            if (SPFLib.NetworkTime.Now - LastUpdateTime > TimeSpan.FromSeconds(1)) return;

            if (!Function.Call<bool>((Hash)0xAE31E7DF9B5B132E, Handle))
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Handle, true, true, 0);
            }

            base.Update();
        }
    }
}
