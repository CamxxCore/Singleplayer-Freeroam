using SPFLib.Enums;
using GTA;
using GTA.Native;
using GTA.Math;
namespace SPFClient.Entities
{
    public sealed class NetworkHeli : NetworkVehicle
    {
        public NetworkHeli(SPFLib.Types.VehicleState state) : base(state)
        {
            OnUpdateRecieved += UpdateRecieved;
        }

        private void UpdateRecieved(NetworkVehicle sender, SPFLib.Types.VehicleState e)
        {
            if (e.Flags.HasFlag(VehicleFlags.VehicleCannon))
            {
                FireWeapon((WeaponHash)0xE2822A29);
            }

            else if (e.Flags.HasFlag(VehicleFlags.VehicleRocket))
            {
                FireWeapon((WeaponHash)0xCF0896E0);
            }
        }

        public NetworkHeli(Vehicle vehicle, int id) : base(vehicle, id)
        {
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

        public override void Update()
        {
            //         Function.Call(Hash.SET_HELI_BLADES_FULL_SPEED, Handle);
            base.Update();
        }
    }
}
