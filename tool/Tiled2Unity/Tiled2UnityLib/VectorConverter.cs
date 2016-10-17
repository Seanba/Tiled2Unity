using System.Numerics;
using System.Drawing;

namespace Tiled2Unity
{
    public static class VectorConverter
    {
        public static PointF ToPointF(this Vector2 v)
        {
            return new PointF(v.X, v.Y);
        }

        public static Vector3 ToVector3(this Vector2 v, float z = 0.0f)
        {
            return new Vector3(v.X, v.Y, z);
        }

        public static Vector2 ToVector2(this PointF p)
        {
            return new Vector2(p.X, p.Y);
        }

        public static Vector2 ToVector2(this Point p)
        {
            return new Vector2((float)p.X, (float)p.Y);
        }

        public static PointF FromVector2ToPointF(Vector2 input)
        {
            return input.ToPointF();
        }
    }
}
