using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;
using System.Drawing;

namespace SPFClient.UI
{
    public class UIPlayerTag
    {
        Scaleform scaleform;
        string name;

        public string Name { get { return name; } }

        public UIPlayerTag(int index)
        {
            scaleform = new Scaleform(Function.Call<int>(Hash.REQUEST_SCALEFORM_MOVIE, 
                string.Format("player_name_{0}", index.ToString("D2"))));
        }

   

        public void SetPlayerName(string name)
        {
            this.name = name;
            //     var random = new Random();
            //      var color = string.Format("#{0:X6}", random.Next(0x1000000));
            scaleform.CallFunction("SET_PLAYER_NAME", name);
          //      string.Format("<font color=\'{0}\' style=\'opacity: .2\'>{1}</font>", color, name));
        }

        private int LerpColor(int a, int b, float lerpAmount)
        {
            var mask1 = 0xff00ff;
            var mask2 = 0x00ff00;

            var f2 = (int)(256 * lerpAmount);
            var f1 = 256 - f2;

            return (((((a & mask1) * f1) + ((b & mask1) * f2)) >> 8) & mask1)
                   | (((((a & mask2) * f1) + ((b & mask2) * f2)) >> 8) & mask2);
        }

        public void Update(int pedHandle)
        {
            if (name == null) return;

            if (Function.Call<bool>(Hash.IS_PED_IN_ANY_VEHICLE, pedHandle))
            {
                var vehicle = new Vehicle(Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, pedHandle, false));

                int bone = Function.Call<int>(Hash._GET_ENTITY_BONE_INDEX, vehicle.Handle, "windscreen");
                var pos = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, pedHandle, bone) + (-vehicle.ForwardVector * 0.06f) + new Vector3(0, 0, 1f);

                float heading = 180.0f - Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, GameplayCamera.Position.X - pos.X, GameplayCamera.Position.Y - pos.Y);

                Function.Call((Hash)0x1CE592FDC749D6F5, scaleform.Handle, pos.X, pos.Y, pos.Z, 0, 0, heading,
                    1.8f, 0.92f, 2.4f, 1.8f, 0.92f, 2.4f, 2);
            }

            else
            {
                var coords = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, pedHandle, false);

                var offset = new Vector3(0, 0.05f, 0.747f);

                var boneCoords = Function.Call<Vector3>(Hash.GET_PED_BONE_COORDS, pedHandle, 24818, 0.0, 0.0, 0.0);
                var offsetPos = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS, pedHandle, offset.X, offset.Y, offset.Z) - coords;

                var pos = boneCoords + offsetPos;

                float heading = 180.0f - Function.Call<float>(Hash.GET_HEADING_FROM_VECTOR_2D, GameplayCamera.Position.X - pos.X, GameplayCamera.Position.Y - pos.Y);

                Function.Call((Hash)0x1CE592FDC749D6F5, scaleform.Handle, pos.X, pos.Y, pos.Z, 0, 0, heading,
                    1.8f, 0.92f, 2.4f, 1.8f, 0.92f, 2.4f, 2);
            }
        }
    }
}
