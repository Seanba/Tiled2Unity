using System.Drawing;

namespace Tiled2Unity
{
    // Working man's vertex
    public struct Vertex3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public static Vertex3 FromPointF(PointF point, float depth)
        {
            return new Vertex3 { X = point.X, Y = point.Y, Z = depth };
        }
    }
}
