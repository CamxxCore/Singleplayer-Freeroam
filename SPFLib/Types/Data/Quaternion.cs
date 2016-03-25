using System;

namespace SPFLib.Types
{
    [Serializable]
    public class Quaternion
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public void Normalize()
        {
            float length = 1.0f / Length();
            X *= length;
            Y *= length;
            Z *= length;
            W *= length;
        }

        public float Length()
        {
            return (float)(Math.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W)));
        }

        public void ToAngleAxis(out float angle, out Vector3 vec)
        {
            angle = (float)(2 * Math.Acos(W));
            var factor = Math.Sqrt(1 - W * W);
            vec = new Vector3(
                (float)(X * factor),
                (float)(Y * factor),
                (float)(Z * factor)
                );
        }

        public static Quaternion operator * (Quaternion left, Quaternion right)
        {
            Quaternion quaternion = new Quaternion();

            float lx = left.X;
            float ly = left.Y;
            float lz = left.Z;
            float lw = left.W;
            float rx = right.X;
            float ry = right.Y;
            float rz = right.Z;
            float rw = right.W;

            quaternion.X = (lx * rw + rx * lw) + (ly * rz) - (lz * ry);
            quaternion.Y = (ly * rw + ry * lw) + (lz * rx) - (lx * rz);
            quaternion.Z = (lz * rw + rz * lw) + (lx * ry) - (ly * rx);
            quaternion.W = (lw * rw) - (lx * rx + ly * ry + lz * rz);

            return quaternion;
        }

        public static Quaternion operator * (Quaternion quaternion, float scale)
        {
            Quaternion result = new Quaternion();

            result.X = quaternion.X * scale;
            result.Y = quaternion.Y * scale;
            result.Z = quaternion.Z * scale;
            result.W = quaternion.W * scale;
            return result;
        }

        public static Quaternion operator * (float scale, Quaternion quaternion)
        {
            Quaternion result = new Quaternion();

            result.X = quaternion.X * scale;
            result.Y = quaternion.Y * scale;
            result.Z = quaternion.Z * scale;
            result.W = quaternion.W * scale;
            return result;
        }

        public static Quaternion Invert(Quaternion q)
        {
            float lengthSq = 1.0f / ((q.X * q.X) + 
                (q.Y * q.Y) + 
                (q.Z * q.Z) + 
                (q.W * q.W));

            q.X = -q.X * lengthSq;
            q.Y = -q.Y * lengthSq;
            q.Z = -q.Z * lengthSq;
            q.W = q.W * lengthSq;

            return q;
        }

        public static Quaternion Slerp(Quaternion q1, Quaternion q2, float t)
        {
            Quaternion result = new Quaternion();

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

        public static Quaternion RotationAxis(Vector3 axis, float angle)
        {
            Quaternion result = new Quaternion();

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

        public Quaternion(float X, float Y, float Z, float W)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = W;
        }

        public Quaternion() : this(0, 0, 0, 0)
        { }
    }
}
