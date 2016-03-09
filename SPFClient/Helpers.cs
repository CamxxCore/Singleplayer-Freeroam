using System;
using GTA;
using GTA.Native;
using GTA.Math;
using SPFClient.Types;
using System.Drawing;

namespace SPFClient
{
    public static class Helpers
    {
        /// <summary>
        /// Returns true if this is a melee weapon or throwable.
        /// </summary>
        /// <param name="weap"></param>
        /// <returns></returns>
        public static bool IsMeleeOrThrowable(this Weapon weap)
        {
            return Function.Call<int>(Hash.GET_WEAPONTYPE_GROUP, (int)weap.Hash) ==
                Function.Call<int>(Hash.GET_HASH_KEY, "group_thrown") ||
                 Function.Call<int>(Hash.GET_WEAPONTYPE_GROUP, (int)weap.Hash) ==
                Function.Call<int>(Hash.GET_HASH_KEY, "group_melee");
        }

        public static void SetName(this Blip blip, string name)
        {
            Function.Call((Hash)0xF9113A30DE5C6670, "CUSPLNM");
            Function.Call(Hash._ADD_TEXT_COMPONENT_ITEM_STRING, name);
            Function.Call((Hash)0xBC38B49BCB83BC9B, blip.Handle);
            Function.Call(Hash.SET_BLIP_NAME_TO_PLAYER_NAME, blip.Handle, name);
        }

        /// <summary>
        /// Returns true if the animation is playing on a ped.
        /// </summary>
        /// <param name="animation"></param>
        /// <param name="ped"></param>
        /// <returns></returns>
        public static bool IsPlayingOn(this Animation animation, Ped ped)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM,
                ped.Handle,
                animation.Dictionary,
                animation.Name,
                3);
        }

        /// <summary>
        /// Get the closest vehicle to an entity.
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="radius">Search radius</param>
        /// <param name="type">Vehicle type (Default: 0)</param>
        /// <param name="flags">Flags (Default: 23)</param>
        /// <returns></returns>
        public static Vehicle GetClosestVehicle(this Entity ent, float radius, VehicleHash type = 0, int flags = 70)
        {
            var vHandle = Function.Call<int>(Hash.GET_CLOSEST_VEHICLE, 
                ent.Position.X, 
                ent.Position.Y, 
                ent.Position.Z, 
                radius, (int)type, flags);

            if (vHandle != 0) return new Vehicle(vHandle);
            else return null;
        }

        /// <summary>
        /// Gets the user ID of the local player.
        /// </summary>
        public static int GetUserID()
        {
            var outArg = new OutputArgument();
            Function.Call((Hash)0x4927FC39CD0869A0, Game.Player.Handle, outArg);
            return outArg.GetResult<int>();
        }


        /// <summary>
        /// Returns the 1080pixels-based screen resolution while mantaining current aspect ratio.
        /// </summary>
        /// <returns></returns>
        public static SizeF GetScreenResolutionMantainRatio()
        {
            int screenw = Game.ScreenResolution.Width;
            int screenh = Game.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            return new SizeF(width, height);
        }

        /// <summary>
        /// Gets a PedHash from its enum index / ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static PedHash PedIDToHash(short id)
        {
            try
            {
                var values = typeof(PedHash).GetEnumValues();

                if (id < 0 || id > values.Length)
                    throw new ArgumentOutOfRangeException("id: out of range");

                return (PedHash)values.GetValue(id);
            }

            catch
            {
                return PedHash.Michael;
            }
        }

        /// <summary>
        /// Gets an enum index/ ID from a PedHash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static short PedHashtoID(PedHash hash)
        {
            try
            {
                var values = typeof(PedHash).GetEnumValues();

                int i = 0;

                foreach (PedHash item in values)
                {
                    if ((PedHash)values.GetValue(i) == hash)
                        return (short)i;
                    i++;
                }

            }

            catch
            { }
            return 0;
        }

        /// <summary>
        /// Gets a WeaponHash from its enum index / ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static WeaponHash WeaponIDToHash(short id)
        {
            try
            {
                var values = typeof(WeaponHash).GetEnumValues();

                if (id < 0 || id > values.Length)
                    throw new ArgumentOutOfRangeException("id: out of range");

                return (WeaponHash)values.GetValue(id);
            }

            catch
            { }

            return WeaponHash.AssaultRifle;
        }

        /// <summary>
        /// Gets an enum index/ ID from a WeaponHash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static short WeaponHashtoID(WeaponHash hash)
        {
            try
            {
                var values = typeof(WeaponHash).GetEnumValues();

                for (int i = 0; i < values.Length; i++)
                {
                    if ((WeaponHash)values.GetValue(i) == hash)
                        return (short)i;
                }
            }

            catch
            { }

            return 0;
        }

        /// <summary>
        /// Gets a VehicleHash from its enum index / ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static VehicleHash VehicleIDToHash(short id)
        {
            try
            {
                var values = typeof(VehicleHash).GetEnumValues();

                if (id < 0 || id > values.Length)
                    throw new ArgumentOutOfRangeException("id: out of range");

                return (VehicleHash)values.GetValue(id);
            }

            catch
            {
                return VehicleHash.Ninef;
            }
        }

        /// <summary>
        /// Gets an enum index/ ID from a VehicleHash.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static short VehicleHashtoID(VehicleHash hash)
        {
            try
            {
                var values = typeof(VehicleHash).GetEnumValues();

                for (int i = 0; i < values.Length; i++)
                {
                    if ((VehicleHash)values.GetValue(i) == hash)
                        return (short)i;
                }
            }

            catch
            { }

            return 0;
        }

        public static Quaternion RotationAxis(Vector3 axis, float angle)
        {
            Quaternion result;

            axis.Normalize();

            float half = angle * 0.5f;
            float sin = (float)Math.Sin(half);
            float cos = (float)Math.Cos(half);

            result.X = axis.X * sin;
            result.Y = axis.Y * sin;
            result.Z = axis.Z * sin;
            result.W = cos;

            return result;
        }

        /// <summary>
        /// Convert a rotation vector to a directional one.
        /// </summary>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public static Vector3 RotationToDirection(Vector3 rotation)
        {
            double retZ = rotation.Z * 0.01745329f;
            double retX = rotation.X * 0.01745329f;
            double absX = Math.Abs(Math.Cos(retX));
            return new Vector3((float)-(Math.Sin(retZ) * absX), (float)(Math.Cos(retZ) * absX), (float)Math.Sin(retX));
        }

        public static double Magnitude(this Vector3 vec)
        {
            return Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        }

        public static Quaternion Slerp(Quaternion q1, Quaternion q2, float t)
        {
            Quaternion result;

            float opposite;
            float inverse;
            float dot = (q1.X * q2.X) + (q1.Y * q2.Y) + (q1.Z * q2.Z) + (q1.W * q2.W);
            bool flag = false;

            if (dot < 0.0f)
            {
                flag = true;
                dot = -dot;
            }

            if (dot > 0.999999f)
            {
                inverse = 1.0f - t;
                opposite = flag ? -t : t;
            }
            else
            {
                float acos = (float)Math.Acos(dot);
                float invSin = (float)(1.0f / Math.Sin(acos));

                inverse = (float)(Math.Sin((1.0f - t) * acos)) * invSin;
                opposite = flag ? (float)-Math.Sin(t * acos) * invSin : (float)Math.Sin(t * acos) * invSin;
            }

            result.X = (inverse * q1.X) + (opposite * q2.X);
            result.Y = (inverse * q1.Y) + (opposite * q2.Y);
            result.Z = (inverse * q1.Z) + (opposite * q2.Z);
            result.W = (inverse * q1.W) + (opposite * q2.W);

            return result;
        }

        public static Vector3 SmoothStep(Vector3 start, Vector3 end, float amount)
        {
            Vector3 vector;

            amount = (amount > 1.0f) ? 1.0f : ((amount < 0.0f) ? 0.0f : amount);
            amount = (amount * amount) * (3.0f - (2.0f * amount));

            vector.X = start.X + ((end.X - start.X) * amount);
            vector.Y = start.Y + ((end.Y - start.Y) * amount);
            vector.Z = start.Z + ((end.Z - start.Z) * amount);

            return vector;
        }

        public static void ToAngleAxis(this Quaternion q, out float angle, out Vector3 vec)
        {
            angle = (float)(2 * Math.Acos(q.W));
            var factor = Math.Sqrt(1 - q.W * q.W);
            vec = new Vector3(
                (float)(q.X * factor),
                (float)(q.Y * factor),
                (float)(q.Z * factor)
                );
        }

        public static SPFLib.Types.Quaternion Serialize(this Quaternion q)
        {
            var retQ = new SPFLib.Types.Quaternion();
            retQ.X = q.X;
            retQ.Y = q.Y;
            retQ.Z = q.Z;
            retQ.W = q.W;
            return retQ;
        }

        public static SPFLib.Types.Vector3 Serialize(this Vector3 vec)
        {
            var retVec = new SPFLib.Types.Vector3();
            retVec.X = vec.X;
            retVec.Y = vec.Y;
            retVec.Z = vec.Z;
            return retVec;
        }

        public static Quaternion Deserialize(this SPFLib.Types.Quaternion q)
        {
            var retQ = new Quaternion();
            retQ.X = q.X;
            retQ.Y = q.Y;
            retQ.Z = q.Z;
            retQ.W = q.W;
            return retQ;
        }

        public static Vector3 Deserialize(this SPFLib.Types.Vector3 vec)
        {
            var retVec = new Vector3();
            retVec.X = vec.X;
            retVec.Y = vec.Y;
            retVec.Z = vec.Z;
            return retVec;
        }   
    }
}
