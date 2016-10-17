using System.Drawing;

namespace Tiled2Unity
{
    public struct FaceVertices
    {
        public PointF[] Vertices { get; set; }
        public float Depth_z { get; set; }

        public Vertex3 V0
        {
            get { return Vertex3.FromPointF(Vertices[0], this.Depth_z); }
        }

        public Vertex3 V1
        {
            get { return Vertex3.FromPointF(Vertices[1], this.Depth_z); }
        }

        public Vertex3 V2
        {
            get { return Vertex3.FromPointF(Vertices[2], this.Depth_z); }
        }

        public Vertex3 V3
        {
            get { return Vertex3.FromPointF(Vertices[3], this.Depth_z); }
        }
    }
}
