using SPFLib.Enums;
using GTA;
using GTA.Native;
using GTA.Math;
using SPFLib.Types;
using SPFClient.Types;
using System;
using Vector3 = GTA.Math.Vector3;

namespace SPFClient.Entities
{
    public sealed class NetworkHeli : NetworkVehicle
    {
        HeliState updatedState;

        private HeliSnapshot[] stateBuffer = new HeliSnapshot[20];

        private int snapshotCount = 0;

        public NetworkHeli(VehicleState state) : base(state)
        {
            OnUpdateReceived += UpdateReceived;
        }

        public NetworkHeli(Vehicle vehicle, int id) : base(vehicle, id)
        {
        }

        private void UpdateReceived(DateTime timeSent, VehicleState e)
        {
            updatedState = e as HeliState;

            var position = updatedState.Position.Deserialize();
            var vel = updatedState.Velocity.Deserialize();
            var rotation = updatedState.Rotation.Deserialize();

            for (int i = stateBuffer.Length - 1; i > 0; i--)
                stateBuffer[i] = stateBuffer[i - 1];

            stateBuffer[0] = new HeliSnapshot(position, vel, rotation, updatedState.RotorSpeed, 
                timeSent);

            snapshotCount = Math.Min(snapshotCount + 1, stateBuffer.Length);

            if (e.Flags.HasFlag(VehicleFlags.VehicleCannon))
            {
                FireWeapon((WeaponHash)0xE2822A29);
            }

            else if (e.Flags.HasFlag(VehicleFlags.VehicleRocket))
            {
                FireWeapon((WeaponHash)0xCF0896E0);
            }
        }

        public void FireWeapon(WeaponHash hash)
        {
            if (hash == WeaponHash.Unarmed) return;

            if (!Function.Call<bool>(Hash.HAS_WEAPON_ASSET_LOADED, (int)hash))
            {
                Function.Call(Hash.REQUEST_WEAPON_ASSET, (int)hash);
            }

            Vector3 vOffset1 = new Vector3(0.7126f, 1.6707f, 0.1192f);
            Vector3 vStartPos, vEndPos;

            vStartPos = GetOffsetInWorldCoords(vOffset1); //Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, _ped.Handle, vStartPos.X, vStartPos.Y, vStartPos.Z);
            vEndPos = Position + new Vector3(vOffset1.X, 0, vOffset1.Z) + ForwardVector * 20;
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, vStartPos.X, vStartPos.Y, vStartPos.Z, vEndPos.X, vEndPos.Y, vEndPos.Z,
         15, 1, (int)hash, Handle, 1, 1, 0xBF800000);

            vOffset1 = new Vector3(-vOffset1.X, vOffset1.Y, vOffset1.Z);

            vStartPos = GetOffsetInWorldCoords(vOffset1); //Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, _ped.Handle, vStartPos.X, vStartPos.Y, vStartPos.Z);
            vEndPos = Position + new Vector3(vOffset1.X, 0, vOffset1.Z) + ForwardVector * 20;
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, vStartPos.X, vStartPos.Y, vStartPos.Z, vEndPos.X, vEndPos.Y, vEndPos.Z,
                15, 1, (int)hash, Handle, 1, 1, 0xBF800000);
        }

        public HeliState GetHeliState()
        {
            var v = new Vehicle(Handle);

            var state = new HeliState(ID, Position.Serialize(), Velocity.Serialize(),
                 Quaternion.Serialize(), 0f, 0, Convert.ToInt16(Health),
                 (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                 GetRadioStation(), GetVehicleID());

            if (IsDead)
                state.Flags |= VehicleFlags.Exploded;

            return state;
        }

        public override void Update()
        {
            if (snapshotCount > EntityExtrapolator.SnapshotMin)
            {
                var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position, Quaternion, stateBuffer, snapshotCount, 0.1f, false);

                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
            }
            

            //Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, Handle);

            base.Update();
        }
    }
}
