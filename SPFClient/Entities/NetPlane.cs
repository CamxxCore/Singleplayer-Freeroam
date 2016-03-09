using VehicleState = SPFLib.Types.VehicleState;
using SPFLib.Enums;
using GTA;
using GTA.Native;
using GTA.Math;

namespace SPFClient.Entities
{
    public sealed class NetPlane : NetworkVehicle
    {
        public NetPlane(VehicleState state) : base(state)
        {
            OnUpdateRecieved += UpdateReceived;
        }

        private void UpdateReceived(NetworkVehicle sender, VehicleState e)
        {
          //  new Vehicle(Handle).EnginePowerMultiplier = new Vehicle(Handle).Speed / 1000;

            UpdateVehicleLandingGear((LGearState)e.ExtraFlags);

            if (e.Flags.HasFlag(VehicleFlags.PlaneGun))
            {
                FireWeapon((WeaponHash)0xE2822A29);
            }

            else if (e.Flags.HasFlag(VehicleFlags.PlaneShoot))
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

        public void UpdateVehicleLandingGear(LGearState state)
        {
            var vehicle = new Vehicle(Handle);

            switch (state)
            {
                case LGearState.Closing:
                case LGearState.Retracted:
                    LandingGearState = LGearState.Closing;
                    break;
                case LGearState.Opening:
                case LGearState.Deployed:
                    LandingGearState = LGearState.Deployed;
                    break;
            }
        }

        /// <summary>
        /// State of the vehicle landing gear.
        /// </summary>
        public LGearState LandingGearState
        {
            get { return (LGearState)Function.Call<int>(Hash._GET_VEHICLE_LANDING_GEAR, Handle); }
            set { Function.Call(Hash._SET_VEHICLE_LANDING_GEAR, Handle, (int)value); }
        }

        public override void Update()
        {
       
            base.Update();
        }
    }
}
