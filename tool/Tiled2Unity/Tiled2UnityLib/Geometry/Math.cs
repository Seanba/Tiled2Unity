using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tiled2Unity.Geometry
{
    class Math
    {
        // Points are ordered CCW with B as the junction
        public static float Cross(PointF A, PointF B, PointF C)
        {
            PointF lhs = new PointF(B.X - A.X, B.Y - A.Y);
            PointF rhs = new PointF(C.X - B.X, C.Y - B.Y);
            return (lhs.X * rhs.Y) - (lhs.Y * rhs.X);
        }

    }
}
