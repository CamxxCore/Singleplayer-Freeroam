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
