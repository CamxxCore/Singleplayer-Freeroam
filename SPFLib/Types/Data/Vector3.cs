using System;

namespace SPFLib.Types
{
    [Serializable]
    public class Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3(float X, float Y, float Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public Vector3()
        { }

        public static Vector3 operator +(Vector3 v1, Vector3 v2)
        {
            return new Vector3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }

        public static Vector3 operator -(Vector3 v1, Vector3 v2)
        {
            return new Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }

        public static Vector3 operator * (Vector3 value, float scale)
        {
            return new Vector3(value.X * scale, value.Y * scale, value.Z * scale);
        }

        public static Vector3 operator * (float scale, Vector3 vec)
        {
            return vec * scale;
        }

        public float Length()
        {
            return (float)(Math.Sqrt((X * X) + (Y * Y) + (Z * Z)));
        }

        public float DistanceTo(Vector3 position)
        {
            return (position - this).Length();
        }

        public Vector3 Around(float distance)
        {
            return this + RandomXY() * distance;
        }

        Vector3 RandomXY()
        {
            Vector3 v = new Vector3();
            double radian = new Random().NextDouble() * 2 * Math.PI;
            v.X = (float)(Math.Cos(radian));
            v.Y = (float)(Math.Sin(radian));
            v.Normalize();
            return v;
        }

        public void Normalize()
        {
            float length = Length();
            if (length == 0)
                return;
            float num = 1 / length;
            X *= num;
            Y *= num;
            Z *= num;
        }

        public static Vector3 Lerp(Vector3 start, Vector3 end, float factor)
        {
            Vector3 vector = new Vector3();

            vector.X = start.X + ((end.X - start.X) * factor);
            vector.Y = start.Y + ((end.Y - start.Y) * factor);
            vector.Z = start.Z + ((end.Z - start.Z) * factor);

            return vector;
        }
    }
}
