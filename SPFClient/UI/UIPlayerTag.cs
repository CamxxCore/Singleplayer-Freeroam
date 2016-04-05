using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;

namespace SPFClient.UI
{
    public class UIPlayerTag
    {
        Ped ped;
        Scaleform scaleform;
        string name;

        public UIPlayerTag(Ped ped, string name)
        {
            this.ped = ped;
            this.name = name;
            scaleform = new Scaleform(Function.Call<int>(Hash.REQUEST_SCALEFORM_MOVIE, "player_name_01"));
            if (name != null) SetPlayerName(name);
        }

        public void SetPlayerName(string name)
        {
            this.name = name;
            scaleform.CallFunction("SET_PLAYER_NAME", 
                string.Format("<font color=\'#F50000\'>{0}</font>", name));
        }

        public void Update()
        {
            if (name == null) return;

            if (Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, ped.Handle))
            {
                int bone = Function.Call<int>(Hash._GET_ENTITY_BONE_INDEX, ped.CurrentVehicle.Handle, "chassis_dummy");
                var pos = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, ped.Handle, bone) + -ped.ForwardVector + new Vector3(0, 0, 1.25f);

                Function.Call((Hash)0x1CE592FDC749D6F5, scaleform.Handle, pos.X, pos.Y, pos.Z, GameplayCamera.Rotation.X, GameplayCamera.Rotation.Y, GameplayCamera.Rotation.Z,
                    1.8f, 0.92f, 2.4f, 1.8f, 0.92f, 2.4f, 2);
            }

            else
            {

                var offset = new Vector3(0, 0.05f, 0.76f);
                var boneCoords = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, ped.Handle, 24818, 0.0, 0.0, 0.0);
                var offsetPos = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, ped.Handle, offset.X, offset.Y, offset.Z) - ped.Position;
                var pos = boneCoords + offsetPos;// + offsetPos;

                Function.Call((Hash)0x1CE592FDC749D6F5, scaleform.Handle, pos.X, pos.Y, pos.Z, GameplayCamera.Rotation.X, 0, GameplayCamera.Rotation.Z,
                    1.8f, 0.92f, 2.4f, 1.8f, 0.92f, 2.4f, 2);

            }
         
        }
    }
}
