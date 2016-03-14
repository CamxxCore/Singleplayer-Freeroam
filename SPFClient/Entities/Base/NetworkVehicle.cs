using GTA;
using GTA.Native;
using SPFLib.Types;
using SPFClient.Types;
using SPFLib.Enums;
using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Entities
{
    public delegate void NetworkVehicleEventHandler(NetworkVehicle sender, VehicleState e);

    public class NetworkVehicle : GameEntity
    {
        public readonly int ID;

        private int snapshotCount;

        private int lastPacketID;

        private int currentRadioStation;

        private readonly ulong vAddress, wheelsPtr;

        public event NetworkVehicleEventHandler OnUpdateRecieved;

        public event NetworkVehicleEventHandler OnSendUpdate;

        private EntitySnapshot[] moveBuffer = new EntitySnapshot[20];

        private static Extrapolator extrapolator = new Extrapolator();

        public int LastUpdateTime { get; private set; }

        private VehicleState lastReceivedState;

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkVehicle(VehicleState state) :
            base(SetupVehicle(state.Position.Deserialize(),
                state.Rotation.Deserialize(),
                state.VehicleID,
                state.Flags.HasFlag(VehicleFlags.DoorsLocked)))
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

        internal static Vehicle SetupVehicle(Vector3 position, Quaternion rotation, short vehID, bool doorsLocked)
        {
            var model = new Model(Helpers.VehicleIDToHash(vehID));

            if (!model.IsLoaded)
                model.Request(1000);

            Vector3 vec; float angle;

            Helpers.ToAngleAxis(rotation, out angle, out vec);

            //Handle the vehicle
            var vehicle = World.CreateVehicle(model, position);

            vehicle.Quaternion = rotation;

            var dt = DateTime.Now + TimeSpan.FromMilliseconds(100);

            while (DateTime.Now < dt)
                Script.Yield();

            vehicle.IsInvincible = true;

            vehicle.EngineRunning = true;

          //  Function.Call(Hash.SET_ENTITY_MOTION_BLUR, vehicle.Handle, true);

          //  Function.Call(Hash.NETWORK_SET_ENTITY_CAN_BLEND, vehicle.Handle, true);

            Function.Call(Hash.SET_ENTITY_COLLISION, vehicle.Handle, 1, 0);

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, vehicle.Handle, true, true);

            Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, vehicle.Handle, 0.0f);

          //  Function.Call(Hash.SET_ENTITY_PROOFS, vehicle.Handle, true, true, false, true, false, false, false, false);

       //     Function.Call(Hash._0x1CF38D529D7441D9, vehicle.Handle, 1);

        //    Function.Call(Hash.NETWORK_FADE_IN_ENTITY, vehicle.Handle);

            return vehicle;
        }

        internal virtual void UpdateSent(VehicleState e)
        {
            OnSendUpdate?.Invoke(this, e);
        }

        internal void HandleUpdatePacket(VehicleState state, int packetID, DateTime svTime)
        {
           /* if ((state.Flags & VehicleFlags.Exploded) != 0)
            {
                IsInvincible = false;
                new Vehicle(Handle).Explode();
                return;
            }*/

            if (packetID > 0 && packetID > lastPacketID)
            {
                var position = state.Position.Deserialize();
                var vel = state.Velocity.Deserialize();
                var rotation = state.Rotation.Deserialize();

                for (int i = moveBuffer.Length - 1; i > 0; i--)
                    moveBuffer[i] = moveBuffer[i - 1];

                moveBuffer[0] = new EntitySnapshot(position, vel, rotation, svTime, packetID);
                snapshotCount = Math.Min(snapshotCount + 1, moveBuffer.Length);

                if ((state.Flags & VehicleFlags.DoorsLocked) != (lastReceivedState?.Flags & VehicleFlags.DoorsLocked))
                {
                    Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED_FOR_PLAYER, Handle, Game.Player.Handle, state.Flags.HasFlag(VehicleFlags.DoorsLocked));
                }

                if ((state.Flags & VehicleFlags.HornPressed) != 0)
                {
                    UI.ShowSubtitle("horn");
                    Function.Call(Hash.START_VEHICLE_HORN, Handle, 100, 0, false);
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

                        Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, Handle, true);
                    }

                    currentRadioStation = state.RadioStation;
                }

                lastReceivedState = state;
                lastPacketID = packetID;
            }

            else
            {
               // QueueUnorderedPacket(state, svTime, packetID);
            }

            if (packetID - lastPacketID > 5)
            {
                lastReceivedState = state;
                lastPacketID = packetID;
            }

            OnUpdateRecieved?.Invoke(this, state);

            LastUpdateTime = Game.GameTime;
        }

         private static ulong GetWheelPointer(ulong baseAddress)
          {
              var wheelsAddr = MemoryAccess.ReadUInt64(baseAddress + Offsets.CVehicle.WheelsPtr);

              if (baseAddress > wheelsAddr)
              {
                  wheelsAddr = ulong.Parse(baseAddress.ToString("X").Substring(0, 3) + wheelsAddr.ToString("X"), System.Globalization.NumberStyles.HexNumber);
              }

              return wheelsAddr;
          }

        public override void Update()
        {
            var entityPosition = extrapolator.GetExtrapolatedPosition(Position, Quaternion, moveBuffer, snapshotCount, 0.3f);

            if (entityPosition != null)
            {
                PositionNoOffset = entityPosition.Position;
                Quaternion = entityPosition.Rotation;
            //    Velocity = entityPosition.Velocity;
            }

            SetCurrentRPM(lastReceivedState.CurrentRPM);

            SetWheelRotation(lastReceivedState.WheelRotation);

            //SetSteering(lastReceivedState.Steering);

            base.Update();
        }

        protected EntitySnapshot GetEntitySnapshot(int index)
        {
            if (index > moveBuffer.Length - 1) throw new ArgumentOutOfRangeException("index: out of range.");
            return moveBuffer[index];
        }

        public void QueueUnorderedPacket(VehicleState state, DateTime svTime, int pktID)
        {
            extrapolator.QueueUnorderedPacket(state, svTime, pktID);
        }

        public float GetWheelRotation()
        {
            return MemoryAccess.GetWheelRotation(wheelsPtr, 0);
        }

        public void SetWheelRotation(float value)
        {
            if (!Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, Model.Hash)) return;
            for (int i = 0; i < 4; i++)
                MemoryAccess.SetWheelRotation(wheelsPtr, i, value);
        }

        public void SetSteering(float value)
        {
            if (!Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, Model.Hash)) return;
                MemoryAccess.WriteSingle(vAddress + Offsets.CVehicle.Steering, value);
        }

        public void SetCurrentRPM(float value)
        {
            var offset = (ushort)((int)Game.Version > 3 ? 0x7D4 : 0x7C4);

            MemoryAccess.WriteSingle(vAddress + offset, value);
        }

        /// <summary>
        /// Removes the ped and vehicle from the world.
        /// </summary>
        public void Remove()
        {
            CurrentBlip?.Remove();
            Delete();
        }
    }
}
