using Vector3 = GTA.Math.Vector3;
using SPFLib.Types;
using SPFLib.Enums;
using SPFClient.Types;
using GTA;
using GTA.Native;
using GTA.Math;
using System;

namespace SPFClient.Entities
{
    public sealed class NetworkPlane : NetworkVehicle
    {
        PlaneState updatedState;

        private PlaneSnapshot[] stateBuffer = new PlaneSnapshot[20];

        private int snapshotCount = 0;

        public NetworkPlane(VehicleState state) : base(state)
        {
            OnUpdateReceived += UpdateReceived;         
        }

        public NetworkPlane(Vehicle vehicle, int id) : base(vehicle, id)
        {
            OnUpdateReceived += UpdateReceived;
        }

        private void UpdateReceived(DateTime timeSent, VehicleState e)
        {
            updatedState = e as PlaneState;

            var position = updatedState.Position.Deserialize();
            var vel = updatedState.Velocity.Deserialize();
            var rotation = updatedState.Rotation.Deserialize();

            for (int i = stateBuffer.Length - 1; i > 0; i--)
                stateBuffer[i] = stateBuffer[i - 1];

            stateBuffer[0] = new PlaneSnapshot(position, vel, 
                rotation, 
                updatedState.Flaps, 
                updatedState.Stabs, 
                updatedState.Rudder, 
                timeSent);

            snapshotCount = Math.Min(snapshotCount + 1, stateBuffer.Length);

            SetLandingGear((LGearState)e.ExtraFlags);

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

            vStartPos = GetOffsetInWorldCoords(vOffset1);
            vEndPos = Position + new Vector3(vOffset1.X, 0, vOffset1.Z) + ForwardVector * 20;
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, vStartPos.X, vStartPos.Y, vStartPos.Z, vEndPos.X, vEndPos.Y, vEndPos.Z,
                15, 1, (int)hash, Handle, 1, 1, 0xBF800000);

            vOffset1 = new Vector3(-vOffset1.X, vOffset1.Y, vOffset1.Z);

            vStartPos = GetOffsetInWorldCoords(vOffset1);
            vEndPos = Position + new Vector3(vOffset1.X, 0, vOffset1.Z) + ForwardVector * 20;
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, vStartPos.X, vStartPos.Y, vStartPos.Z, vEndPos.X, vEndPos.Y, vEndPos.Z,
                15, 1, (int)hash, Handle, 1, 1, 0xBF800000);
        }

        public float GetFlaps()
        {
            if (Address <= 0) return 0;
            return MemoryAccess.ReadSingle(Address + Offsets.CPlane.Flaps);
        }

        public void SetFlaps(float value)
        {
            if (Address <= 0) return;
            MemoryAccess.WriteSingle(Address + Offsets.CPlane.Flaps, value);
        }

        public float GetStabs()
        {
            if (Address <= 0) return 0;
            return MemoryAccess.ReadSingle(Address + Offsets.CPlane.Stabs);
        }

        public void SetStabs(float value)
        {
            if (Address <= 0) return;
            MemoryAccess.WriteSingle(Address + Offsets.CPlane.Stabs, value);
        }

        public float GetRudder()
        {
            if (Address <= 0) return 0;
            return MemoryAccess.ReadSingle(Address + Offsets.CPlane.Rudder);
        }

        public void SetRudder(float value)
        {
            if (Address <= 0) return;
            MemoryAccess.WriteSingle(Address + Offsets.CPlane.Rudder, value);
        }

        public void SetLandingGear(LGearState state)
        {
            var vehicle = new Vehicle(Handle);

            switch (state)
            {
                case LGearState.Closing:
                    Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, Handle, (int)LGearState.Closing);
                    break;
                case LGearState.Opening:
                    Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, Handle, (int)LGearState.Deployed);
                    break;
            }
        }

        public override void Update()
        {
            var snapshot = EntityExtrapolator.GetExtrapolatedPosition(Position, Quaternion, 
                stateBuffer, snapshotCount, 0.88f);

            if (snapshot != null)
            {
                PositionNoOffset = snapshot.Position;
                Quaternion = snapshot.Rotation;
                Velocity = snapshot.Velocity;
                SetFlaps(snapshot.Flaps);
                SetStabs(snapshot.Stabs);
                SetRudder(snapshot.Rudder);
            }

            Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, Handle);

            base.Update();
        }

        public PlaneState GetPlaneState()
        {
            var v = new Vehicle(Handle);

            var state = new PlaneState(ID,
                Position.Serialize(),
                Velocity.Serialize(),
                Quaternion.Serialize(),
                GetFlaps(), GetStabs(), GetRudder(),
                0, Convert.ToInt16(Health),
                (byte)v.PrimaryColor, (byte)v.SecondaryColor,
                GetRadioStation(),
                GetVehicleID());

            if (Function.Call<bool>(Hash.IS_CONTROL_PRESSED, 2, (int)Control.VehicleFlyUnderCarriage))
            {
                var lgState = Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, Handle);
                state.ExtraFlags = (ushort)lgState;
            }

            if (Game.IsControlPressed(0, Control.VehicleFlyAttack) || 
                Game.IsControlPressed(0, Control.VehicleFlyAttack2))
            {
                var outArg = new OutputArgument();
                if (Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, 
                    v.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle, outArg))
                {
                    unchecked
                    {
                        switch ((WeaponHash)outArg.GetResult<int>())
                        {
                            case (WeaponHash)0xCF0896E0:
                                state.Flags |= VehicleFlags.VehicleRocket;
                                break;

                            case (WeaponHash)0xE2822A29:
                                state.Flags |= VehicleFlags.VehicleCannon;
                                break;
                        }
                    }
                }
            }

            if (IsDead)
                state.Flags |= VehicleFlags.Exploded;

            return state;
        }
    }
}
