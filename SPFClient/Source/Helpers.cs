using System;
using SPFClient.Types;
using System.Drawing;
using SPFClient.Entities;
using System.Collections.Generic;
using System.IO;
using Vector3 = SPFLib.Types.Vector3;
using Quaternion = SPFLib.Types.Quaternion;
using GTA;
using GTA.Native;
using System.Linq;

namespace SPFClient
{
    public static class Helpers
    {
        public static NetworkVehicle GameVehicleToNetworkVehicle(Vehicle vehicle)
        {
            var uid = SPFLib.Helpers.GenerateUniqueID();
            var hash = vehicle.Model.Hash;

            if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_CAR, hash))
            {
                return new NetworkCar(vehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_HELI, hash))
            {
                return new NetworkHeli(vehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_PLANE, hash))
            {
                return new NetworkPlane(vehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BICYCLE, hash))
            {
                return new NetworkBicycle(vehicle, uid);
            }

            else if (Function.Call<bool>(Hash.IS_THIS_MODEL_A_BOAT, hash))
            {
                return new NetworkBoat(vehicle, uid);
            }

            else return new NetworkVehicle(vehicle, uid);
        }

        /// <summary>
        /// Get random position near given entity
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="multiplier"></param>
        /// <returns></returns>
        public static GTA.Math.Vector3 GetRandomPositionNearEntity(Entity entity, float multiplier)
        {
            float randX, randY;
            randX = randY = 0.0f;

            var rand = Function.Call<int>(Hash.GET_RANDOM_INT_IN_RANGE, 0, 3999) / 1000;

            if (rand == 0)
            {
                randX = -Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 50.0f, 200.0f) * multiplier;
                randY = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, -50.0f, 50.0f) * multiplier;
            }
            else if (rand == 1)
            {
                randX = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 50.0f, 200.0f) * multiplier;
                randY = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 50.0f, 50.0f) * multiplier;
            }
            else if (rand == 2)
            {
                randX = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 50.0f, 200.0f) * multiplier;
                randY = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, -50.0f, 50.0f) * multiplier;
            }
            else
            {
                randX = -Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 50.0f, 200.0f) * multiplier;
                randY = Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, -50.0f, 50.0f) * multiplier;
            }

            return entity.GetOffsetInWorldCoords(new GTA.Math.Vector3(randX, randY, 0.0f));
        }

        public static Tuple<GTA.Math.Vector3, float> GetVehicleNodeForRespawn(GTA.Math.Vector3 position)
        {
            GTA.Math.Vector3 result;
            OutputArgument arg, arg1, arg2;
            arg = arg1 = arg2 = new OutputArgument();

            for (int index = 50; index < 100; ++index)
            {
                Function.Call<bool>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING, position.X, position.Y, position.Z, index, arg, arg1, arg2, 9, 3.0, 2.5);
                result = arg.GetResult<GTA.Math.Vector3>();
                var heading = arg1.GetResult<float>();
                if (!Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, result.X, result.Y, result.Z, 5f, 5f, 5f, 0))
                {
                    return new Tuple<GTA.Math.Vector3, float>(result, heading);
                }
            }

            for (int index = 50; index < 100; ++index)
            {
                Function.Call(Hash.GET_NTH_CLOSEST_VEHICLE_NODE, position.X, position.Y, position.Z, index, arg, 1, 1077936128, 0);
                result = arg.GetResult<GTA.Math.Vector3>();
                if (!Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, result.X, result.Y, result.Z, 5f, 5f, 5f, 0))
                {
                    return new Tuple<GTA.Math.Vector3, float>(result, 0f);
                }
            }

            return new Tuple<GTA.Math.Vector3, float>(position, 0f);
        }


        public static int GetClosestVehicleDoorIndex(this Ped ped, NetworkVehicle vehicle)
        {
            if (vehicle is NetworkCar)
            {
                var bones = new int[]
                {
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_dside_f"), //-1 front left
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_pside_f"), //0 front right
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_dside_r"), //1 back left                     
                    Function.Call<int>((Hash)0xFB71170B7E76ACBA , vehicle.Handle, "handle_pside_r") //2 back right                     
                };

                var closestBone = bones.OrderBy(x =>
                Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, vehicle.Handle, x)
                .DistanceTo(ped.Position)).First();

                return (Array.IndexOf(bones, closestBone) - 1);
            }

            else
            {
                var bones = new int[]
                    {
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_dside_f"), //-1 front left\
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_pside_f"), //0 front right
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_dside_r"), //1 back left                     
                        Function.Call<int>((Hash)0xFB71170B7E76ACBA, vehicle.Handle, "door_pside_r") //2 back right                     
                    };

                var closestBone = bones.OrderBy(x =>
                Function.Call<GTA.Math.Vector3>((Hash)0x44A8FCB8ED227738, vehicle.Handle, x)
                .DistanceTo(ped.Position)).First();

                return (Array.IndexOf(bones, closestBone) - 1);
            }
        }

        public static string GetSessionEventString(SPFLib.Enums.SessionEventType type)
        {
            switch (type)
            {
                case SPFLib.Enums.SessionEventType.PlayerSynced:
                    return "Successfully Connected.";
                case SPFLib.Enums.SessionEventType.PlayerKicked:
                    return " was kicked.";
                case SPFLib.Enums.SessionEventType.PlayerLogout:
                    return " left.";
                case SPFLib.Enums.SessionEventType.PlayerTimeout:
                    return " timed out.";
                default: return null;
            }
        }

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
            Function.Call((Hash)0xF9113A30DE5C6670, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, name);
            Function.Call((Hash)0xBC38B49BCB83BC9B, blip.Handle);
        }

        /// <summary>
        /// Returns true if the animation is playing on a ped.
        /// </summary>
        /// <param name="animation"></param>
        /// <param name="ped"></param>
        /// <returns></returns>
        public static bool IsPlayingOn(this Animation animation, Ped ped)
        {
            if (!ped.Exists()) return false;

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

        public static VehicleSeat CurrentVehicleSeat(this Ped ped)
        {
            Vehicle vehicle;

            if (ped.IsGettingIntoAVehicle)
            {
                vehicle = new Vehicle(Function.Call<int>((Hash)0x814FA8BE5449445D, ped.Handle));
                return (GTA.VehicleSeat)Function.Call<int>((Hash)0x6F4C85ACD641BCD2, ped.Handle);
            }

            else if (ped.IsInVehicle())
            {
                vehicle = ped.CurrentVehicle;

                foreach (GTA.VehicleSeat seat in Enum.GetValues(typeof(GTA.VehicleSeat)))
                    if (vehicle.GetPedOnSeat(seat) == ped)
                        return seat;
            }

            return GTA.VehicleSeat.None;

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

        public static float Hermite(float start, float end, float value)
        {
            return Lerp(start, end, value * value * (3.0f - 2.0f * value));
        }

        public static float Lerp(float a, float b, float f)
        {
            return (a * (1.0f - f)) + (b * f);
        }

        public static GTA.Math.Quaternion RotationAxis(GTA.Math.Vector3 axis, float angle)
        {
            GTA.Math.Quaternion result;

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
        public static GTA.Math.Vector3 RotationToDirection(GTA.Math.Vector3 rotation)
        {
            double retZ = rotation.Z * 0.01745329f;
            double retX = rotation.X * 0.01745329f;
            double absX = Math.Abs(Math.Cos(retX));
            return new GTA.Math.Vector3((float)-(Math.Sin(retZ) * absX), (float)(Math.Cos(retZ) * absX), (float)Math.Sin(retX));
        }

        public static double Magnitude(this Vector3 vec)
        {
            return Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        }

        public static GTA.Math.Vector3 Hermite(GTA.Math.Vector3 value1, GTA.Math.Vector3 tangent1, GTA.Math.Vector3 value2, GTA.Math.Vector3 tangent2, float amount)
        {
            GTA.Math.Vector3 vector;
            float squared = amount * amount;
            float cubed = amount * squared;
            float part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
            float part2 = (-2.0f * cubed) + (3.0f * squared);
            float part3 = (cubed - (2.0f * squared)) + amount;
            float part4 = cubed - squared;

            vector.X = (((value1.X * part1) + (value2.X * part2)) + (tangent1.X * part3)) + (tangent2.X * part4);
            vector.Y = (((value1.Y * part1) + (value2.Y * part2)) + (tangent1.Y * part3)) + (tangent2.Y * part4);
            vector.Z = (((value1.Z * part1) + (value2.Z * part2)) + (tangent1.Z * part3)) + (tangent2.Z * part4);

            return vector;
        }

        public static GTA.Math.Vector3 CatmullRom(GTA.Math.Vector3 value1, GTA.Math.Vector3 value2, GTA.Math.Vector3 value3, GTA.Math.Vector3 value4, float amount)
        {
            GTA.Math.Vector3 vector;
            float squared = amount * amount;
            float cubed = amount * squared;

            vector.X = 0.5f * ((((2.0f * value2.X) + ((-value1.X + value3.X) * amount)) +
                (((((2.0f * value1.X) - (5.0f * value2.X)) + (4.0f * value3.X)) - value4.X) * squared)) +
                ((((-value1.X + (3.0f * value2.X)) - (3.0f * value3.X)) + value4.X) * cubed));

            vector.Y = 0.5f * ((((2.0f * value2.Y) + ((-value1.Y + value3.Y) * amount)) +
                (((((2.0f * value1.Y) - (5.0f * value2.Y)) + (4.0f * value3.Y)) - value4.Y) * squared)) +
                ((((-value1.Y + (3.0f * value2.Y)) - (3.0f * value3.Y)) + value4.Y) * cubed));

            vector.Z = 0.5f * ((((2.0f * value2.Z) + ((-value1.Z + value3.Z) * amount)) +
                (((((2.0f * value1.Z) - (5.0f * value2.Z)) + (4.0f * value3.Z)) - value4.Z) * squared)) +
                ((((-value1.Z + (3.0f * value2.Z)) - (3.0f * value3.Z)) + value4.Z) * cubed));

            return vector;
        }

        public static GTA.Math.Quaternion Slerp(GTA.Math.Quaternion q1, GTA.Math.Quaternion q2, float t)
        {
            GTA.Math.Quaternion result;

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

        public static GTA.Math.Vector3 SmoothStep(GTA.Math.Vector3 start, GTA.Math.Vector3 end, float amount)
        {
            GTA.Math.Vector3 vector;

            amount = (amount > 1.0f) ? 1.0f : ((amount < 0.0f) ? 0.0f : amount);
            amount = (amount * amount) * (3.0f - (2.0f * amount));

            vector.X = start.X + ((end.X - start.X) * amount);
            vector.Y = start.Y + ((end.Y - start.Y) * amount);
            vector.Z = start.Z + ((end.Z - start.Z) * amount);

            return vector;
        }

        public static double SmoothStep(double value1, double value2, double amount)
        {
            double result = Clamp(amount, 0f, 1f);
            result = Hermite(value1, 0f, value2, 0f, result);
            return result;
        }

        public static double Hermite(double value1, double tangent1, double value2, double tangent2, double amount)
        {
            // All transformed to double not to lose precission
            // Otherwise, for high numbers of param:amount the result is NaN instead of Infinity
            double v1 = value1, v2 = value2, t1 = tangent1, t2 = tangent2, s = amount, result;
            double sCubed = s * s * s;
            double sSquared = s * s;

            if (amount == 0f)
                result = value1;
            else if (amount == 1f)
                result = value2;
            else
                result = (2 * v1 - 2 * v2 + t2 + t1) * sCubed +
                    (3 * v2 - 3 * v1 - 2 * t1 - t2) * sSquared +
                    t1 * s +
                    v1;
            return (double)result;
        }

        public static double Clamp(double value, double min, double max)
        {
            // First we check to see if we're greater than the max
            value = (value > max) ? max : value;

            // Then we check to see if we're less than the min.
            value = (value < min) ? min : value;

            // There's no check to see if min > max.
            return value;
        }


        public static Vector3 DirectionToRotation(GTA.Math.Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, Math.Sqrt(direction.Y * direction.Y + direction.X * direction.X));
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double RadToDeg(double rad)
        {
            return rad * 180.0 / Math.PI;
        }


        public static void ToAngleAxis(this GTA.Math.Quaternion q, out float angle, out GTA.Math.Vector3 vec)
        {
            angle = (float)(2 * Math.Acos(q.W));
            var factor = Math.Sqrt(1 - q.W * q.W);
            vec = new GTA.Math.Vector3(
                (float)(q.X * factor),
                (float)(q.Y * factor),
                (float)(q.Z * factor)
                );
        }

        public static Quaternion Serialize(this GTA.Math.Quaternion q)
        {
            var retQ = new Quaternion();
            retQ.X = q.X;
            retQ.Y = q.Y;
            retQ.Z = q.Z;
            retQ.W = q.W;
            return retQ;
        }

        public static Vector3 Serialize(this GTA.Math.Vector3 vec)
        {
            var retVec = new  Vector3();
            retVec.X = vec.X;
            retVec.Y = vec.Y;
            retVec.Z = vec.Z;
            return retVec;
        }

        public static GTA.Math.Quaternion Deserialize(this Quaternion q)
        {
            var retQ = new GTA.Math.Quaternion();
            retQ.X = q.X;
            retQ.Y = q.Y;
            retQ.Z = q.Z;
            retQ.W = q.W;
            return retQ;
        }

        public static GTA.Math.Vector3 Deserialize(this  Vector3 vec)
        {
            var retVec = new GTA.Math.Vector3();
            retVec.X = vec.X;
            retVec.Y = vec.Y;
            retVec.Z = vec.Z;
            return retVec;
        }

        /// <summary>
        /// Concatenates an array of strings with each member on a new line.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string[] GetLines(this string s)
        {
            return s.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        }

        /// <summary>
        /// Populates a list of strings from an embedded string resource.
        /// </summary>
        /// <param name="resource">The string resource (Properties.Resources.ProjectName...)</param>
        /// <returns></returns>
        public static IList<string> ReadEmbeddedResource(string resource)
        {
            string[] text = resource.GetLines();
            return new List<string>(text);
        }

        /// <summary>
        /// Writes a list of strings to a file at the specified path.
        /// </summary>
        /// <param name="list">The list to write</param>
        /// <param name="filepath">The specified path</param>
        public static void WriteListToFile(IList<string> list, string filepath)
        {
            if (File.Exists(filepath)) File.Delete(filepath);
            using (StreamWriter stream = new StreamWriter(filepath))
            {
                foreach (string line in list)
                {
                    stream.WriteLine(line);
                }
            }
        }

    }
}
