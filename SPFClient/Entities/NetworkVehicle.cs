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
    public delegate void NetworkVehicleEventHandler(NetworkVehicle sender, VehicleState e);

    public class NetworkVehicle : GameEntity
    {
        public readonly int ID;
        private int snapshotCount;
        private int currentRadioStation;
        private readonly ulong vAddress, wheelsPtr;

        public event NetworkVehicleEventHandler OnUpdateRecieved;

        public event NetworkVehicleEventHandler OnSendUpdate;

        private VehicleSnapshot[] moveBuffer = new VehicleSnapshot[20];

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
            vAddress = MemoryAccess.GetEntityAddress(Handle);
            wheelsPtr = GetWheelPointer(vAddress);
            lastReceivedState = state;
        }

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkVehicle(Vehicle vehicle, int id) :
            base(vehicle)
        {
            ID = id;
            vAddress = MemoryAccess.GetEntityAddress(Handle);
            wheelsPtr = GetWheelPointer(vAddress);
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

            while (DateTime.Now < dt)
                Script.Yield();

            vehicle.IsInvincible = true;

            vehicle.EngineRunning = true;

            Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, vehicle.Handle, true);

            Function.Call(Hash.SET_ENTITY_COLLISION, vehicle.Handle, 1, 0);

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, vehicle.Handle, true, true);

            Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, vehicle.Handle, 0.0f);

            return vehicle;
        }

        internal virtual void UpdateSent(VehicleState e)
        {
            OnSendUpdate?.Invoke(this, e);
        }

        internal void HandleStateUpdate(VehicleState state, DateTime svTime)
        {
            var position = state.Position.Deserialize();
            var vel = state.Velocity.Deserialize();
            var rotation = state.Rotation.Deserialize();

            for (int i = moveBuffer.Length - 1; i > 0; i--)
                moveBuffer[i] = moveBuffer[i - 1];

            moveBuffer[0] = new VehicleSnapshot(position, vel, rotation, state.WheelRotation, svTime);
            snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

            if ((state.Flags & VehicleFlags.DoorsLocked) != (lastReceivedState?.Flags & VehicleFlags.DoorsLocked))
            {
                Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED_FOR_PLAYER, Handle, Game.Player.Handle, state.Flags.HasFlag(VehicleFlags.DoorsLocked));
            }

            if ((state.Flags & VehicleFlags.Exploded) != 0 && IsAlive)
            {
                IsInvincible = false;
                Function.Call(Hash.EXPLODE_VEHICLE, Handle, true, false);
            }

            Function.Call(Hash.SET_VEHICLE_COLOURS, Handle, state.PrimaryColor, state.SecondaryColor);

            if (state.RadioStation != currentRadioStation)
            {
                if (state.RadioStation == byte.MaxValue)
                    Function.Call(Hash.SET_VEH_RADIO_STATION, Handle, "OFF");
                else
                {
                    var stName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME, (int)state.RadioStation);
                    Function.Call(Hash.SET_VEH_RADIO_STATION, Handle, stName);
                }

                currentRadioStation = state.RadioStation;
            }

            lastReceivedState = state;

            LastUpdateTime = SPFLib.NetworkTime.Now;

            OnUpdateRecieved?.Invoke(this, state);
        }

         private static ulong GetWheelPointer(ulong baseAddress)
          {
            if (baseAddress <= 0) return 0;

              var wheelsAddr = MemoryAccess.ReadUInt64(baseAddress + Offsets.CVehicle.WheelsPtr);

              if (baseAddress > wheelsAddr)
              {
                  wheelsAddr = ulong.Parse(baseAddress.ToString("X").Substring(0, 3) + wheelsAddr.ToString("X"), System.Globalization.NumberStyles.HexNumber);
              }

              return wheelsAddr;
          }

        internal Color GetVehicleColor()
        {
            OutputArgument outR, outG, outB;
            outR = outG = outB = new OutputArgument();
            Function.Call(Hash.GET_VEHICLE_COLOR, Handle, outR, outG, outB);

            return Color.FromArgb(outR.GetResult<byte>(), outG.GetResult<byte>(), outB.GetResult<byte>());
        }

        public float GetWheelRotation()
        {
            if (wheelsPtr <= 0) return 0;
            return MemoryAccess.GetWheelRotation(wheelsPtr, 0);
        }

        public void SetWheelRotation(float value)
        {
            if (!Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, Model.Hash) || wheelsPtr <= 0) return;
            for (int i = 0; i < 4; i++)
                MemoryAccess.SetWheelRotation(wheelsPtr, i, value);
        }

        public byte GetCurrentGear()
        {
            if (vAddress <= 0) return 0;
            return (byte)MemoryAccess.ReadInt16(vAddress + Offsets.CVehicle.CurrentGear);
        }

        public void SetCurrentGear(byte gear)
        {
            if (vAddress <= 0) return;
            MemoryAccess.WriteInt16(vAddress + Offsets.CVehicle.CurrentGear, (short)gear);
        }

        public void SetSteering(float value)
        {
            Function.Call(Hash.SET_VEHICLE_STEER_BIAS, Handle, value);
            //if (!Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, Model.Hash) || vAddress <= 0) return;
             //   MemoryAccess.WriteSingle(vAddress + Offsets.CVehicle.Steering, value);
        }

        /// <summary>
        /// Attempts to parse the current vehicle for active flags and populates the output parameters
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="flags"></param>
        /// <param name="extraFlags"></param>
        /// <returns></returns>
        public bool GetActiveVehicleFlags(Ped owner, out VehicleFlags flags, out ushort extraFlags)
        {
            Type t = GetType();
            flags = 0;
            extraFlags = 0;

            if (owner.CurrentVehicleSeat() == GTA.VehicleSeat.Driver)
            flags |= VehicleFlags.Driver;

            if (Function.Call<bool>(Hash.IS_HORN_ACTIVE, Handle))
                flags |= VehicleFlags.HornPressed;

            if (Health <= 0)
                flags |= VehicleFlags.Exploded;

            if (t == typeof(NetworkCar))
            {
                if (Function.Call<bool>((Hash)0x5EF77C9ADD3B11A3, Handle)) //Left Headlight?
                    extraFlags |= (ushort)DamageFlags.LHeadlight;

                if (Function.Call<bool>((Hash)0xA7ECB73355EB2F20, Handle)) //Right Headlight?
                    extraFlags |= (ushort)DamageFlags.RHeadlight;

                if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int) VehicleDoor.FrontLeftDoor))
                    extraFlags |= (ushort)DamageFlags.LDoor;

                if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int) VehicleDoor.FrontRightDoor))
                    extraFlags |= (ushort)DamageFlags.RDoor;

                if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int) VehicleDoor.BackLeftDoor))
                    extraFlags |= (ushort)DamageFlags.BLDoor;

                if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int) VehicleDoor.BackRightDoor))
                    extraFlags |= (ushort)DamageFlags.BRDoor;

                if (Function.Call<bool>(Hash.IS_VEHICLE_DOOR_DAMAGED, Handle, (int) VehicleDoor.Hood))
                    extraFlags |= (ushort)DamageFlags.Hood;

                return true;
            }

            else if (t == typeof(NetworkHeli))
            {
                return true;
            }

            else if (t == typeof(NetworkPlane))
            {
                if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehicleFlyUnderCarriage))
                {
                    var lgState = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, Handle);
                    extraFlags = (ushort)lgState;
                }

                if (Game.IsControlPressed(0, Control.VehicleFlyAttack) || Game.IsControlPressed(0, Control.VehicleFlyAttack2))
                {
                    var outArg = new OutputArgument();
                    if (Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, owner.Handle, outArg))
                    {
                        unchecked
                        {
                            switch ((WeaponHash)outArg.GetResult<int>())
                            {
                                case (WeaponHash)0xCF0896E0:
                                    flags |= VehicleFlags.VehicleRocket;
                                    break;

                                case (WeaponHash)0xE2822A29:
                                    flags |= VehicleFlags.VehicleCannon;
                                    break;
                            }
                        }
                    }
                }

                return true;
            }

            else if (t == typeof(NetworkBicycle))
            {
                if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehiclePushbikePedal))
                {
                    if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                        extraFlags = (ushort)BicycleState.TuckPedaling;
                    else extraFlags = (ushort)BicycleState.Pedaling;
                }

                else
                {
                    if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 2, (int)Control.ReplayPreviewAudio))
                        extraFlags = (ushort)BicycleState.TuckCruising;
                    else extraFlags = (ushort)BicycleState.Cruising;
                }

                return true;
            }

            else return false;
        } 


        public void SetCurrentRPM(float value)
        {
            if (vAddress <= 0) return;
            MemoryAccess.WriteSingle(vAddress + Offsets.CVehicle.RPM, value);
        }

        public override void Update()
        {
            if (SPFLib.NetworkTime.Now - LastUpdateTime > TimeSpan.FromSeconds(1)) return;

            var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position, Quaternion, moveBuffer, snapshotCount, this.GetType() == typeof(NetworkHeli) ? 0.1f : 0.89f, false);

            if (snapshot != null)
            {
                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
                SetWheelRotation(snapshot.WheelRotation);    
            }

            SetCurrentRPM(lastReceivedState.CurrentRPM);

            if (lastReceivedState.Flags.HasFlag(VehicleFlags.HornPressed))
            Function.Call(Hash.OVERRIDE_VEH_HORN, Handle, 1, 0x2445ad61);

            //IS_VEHICLE_ENGINE_ON
            if (!Function.Call<bool>((Hash)0xAE31E7DF9B5B132E, Handle))
            {
                Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Handle, true, true, 0);
            }

            base.Update();
        }
    }
}
