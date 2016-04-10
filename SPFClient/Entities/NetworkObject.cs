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
    public class NetworkObject : GameEntity
    {
        public readonly int ID;
        public readonly ulong Address;

        public DateTime LastUpdateTime { get; private set; }

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkObject(VehicleState state) :
            base(SetupObject(state.Position.Deserialize(),
                state.Rotation.Deserialize(),
                state.ModelID,
                state.Flags.HasFlag(VehicleFlags.DoorsLocked),
                state.PrimaryColor,
                state.SecondaryColor))
        {
            ID = state.ID;
            Address = MemoryAccess.GetEntityAddress(Handle);
        }

        /// <summary>
        /// Setup entity for the network vehicle.
        /// </summary>
        public NetworkObject(Vehicle vehicle, int id) :
            base(vehicle)
        {
            ID = id;
            Address = MemoryAccess.GetEntityAddress(Handle);
        }

        internal static Vehicle SetupObject(Vector3 position, Quaternion rotation, short vehID, bool doorsLocked, byte primaryColor, byte secondaryColor)
        {
            var model = new Model((int)SPFLib.Helpers.VehicleIDToHash(vehID));

            if (!model.IsLoaded)
                model.Request(1000);

            Vector3 vec; float angle;

            Helpers.ToAngleAxis(rotation, out angle, out vec);

            //Handle the vehicle
            var vehicle = World.CreateVehicle(model, position);

            vehicle.Quaternion = rotation;

            vehicle.PositionNoOffset = position;

            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicle.Handle, primaryColor, secondaryColor);

            //   var dt = DateTime.Now + TimeSpan.FromMilliseconds(300);

            //   while (DateTime.Now < dt) Script.Yield();

            vehicle.IsInvincible = true;

            vehicle.EngineRunning = true;

            Function.Call(Hash.SET_VEHICLE_RADIO_LOUD, vehicle.Handle, true);

            //   Function.Call(Hash.SET_ENTITY_COLLISION, vehicle.Handle, 1, 0);

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, vehicle.Handle, true, true);

            Function.Call(Hash.SET_VEHICLE_FRICTION_OVERRIDE, vehicle.Handle, 0.0f);

            return vehicle;
        }

        public virtual void HandleStateUpdate(VehicleState state, DateTime timeSent)
        {

            LastUpdateTime = SPFLib.NetworkTime.Now;
        }

        public override void Update()
        {
            if (SPFLib.NetworkTime.Now - LastUpdateTime > TimeSpan.FromSeconds(1)) return;

            /*  if (!Function.Call<bool>((Hash)0xAE31E7DF9B5B132E, Handle))
              {
                  Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Handle, true, true, 0);
              }*/

            base.Update();
        }
    }
}
