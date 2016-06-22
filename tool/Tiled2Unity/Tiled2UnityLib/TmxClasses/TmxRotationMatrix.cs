using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

// This is a working man's rotation matrix
// This keeps us from invoking the .NET GDI+ Matrix which causes issues on Mac builds
namespace Tiled2Unity
{
    class TmxRotationMatrix
    {
        private float[,] m = new float[2,2] { { 1, 0 },
                                              { 0, 1 } };

        public TmxRotationMatrix()
        {
        }

        public TmxRotationMatrix(float degrees)
        {
            double rads = degrees * Math.PI / 180.0f;
            float cos = (float)Math.Cos(rads);
            float sin = (float)Math.Sin(rads);

            m[0, 0] = cos;
            m[0, 1] = -sin;
            m[1, 0] = sin;
            m[1, 1] = cos;
        }

        public TmxRotationMatrix(float m00, float m01, float m10, float m11)
        {
            m[0, 0] = m00;
            m[0, 1] = m01;
            m[1, 0] = m10;
            m[1, 1] = m11;
        }

        public float this[int i, int j]
        {
            get { return m[i, j]; }
            set { m[i, j] = value; }
        }

        static public TmxRotationMatrix Multiply(TmxRotationMatrix M1, TmxRotationMatrix M2)
        {
            float m00 = M1[0, 0] * M2[0, 0] + M1[0, 1] * M2[1, 0];
            float m01 = M1[0, 0] * M2[0, 1] + M1[0, 1] * M2[1, 1];
            float m10 = M1[1, 0] * M2[0, 0] + M1[1, 1] * M2[1, 0];
            float m11 = M1[1, 0] * M2[0, 1] + M1[1, 1] * M2[1, 1];
            return new TmxRotationMatrix(m00, m01, m10, m11);
        }

        public void TransformPoint(ref PointF pt)
        {
            float x = pt.X * m[0, 0] + pt.Y * m[1, 0];
            float y = pt.X * m[0, 1] + pt.Y * m[1, 1];
            pt.X = x;
            pt.Y = y;
        }

        public void TransformPoints(PointF[] points)
        {
            for (int i = 0; i < points.Length; ++i)
            {
                TransformPoint(ref points[i]);
            }
        }

    }
}
