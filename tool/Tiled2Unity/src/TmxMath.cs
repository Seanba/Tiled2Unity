using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;

// Helper utitlities for performing math within a Tiled context
namespace Tiled2Unity
{
    class TmxMath
    {
        static private readonly uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        static private readonly uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
        static private readonly uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;

        static public uint GetTileIdWithoutFlags(uint tileId)
        {
            return tileId & ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG);
        }

        static public bool IsTileFlippedDiagonally(uint tileId)
        {
            return (tileId & FLIPPED_DIAGONALLY_FLAG) != 0;
        }

        static public bool IsTileFlippedHorizontally(uint tileId)
        {
            return (tileId & FLIPPED_HORIZONTALLY_FLAG) != 0;
        }

        static public bool IsTileFlippedVertically(uint tileId)
        {
            return (tileId & FLIPPED_VERTICALLY_FLAG) != 0;
        }

        static public void TransformPoints(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            Matrix translate = new Matrix();
            Matrix rotate = new Matrix();

            // Put the points into origin/local space
            translate.Translate(-origin.X, -origin.Y);
            translate.TransformPoints(points);

            // Apply the flips/rotations (order matters)
            if (horizontal)
            {
                Matrix h = new Matrix(-1, 0, 0, 1, 0, 0);
                rotate.Multiply(h);
            }
            if (vertical)
            {
                Matrix v = new Matrix(1, 0, 0, -1, 0, 0);
                rotate.Multiply(v);
            }
            if (diagonal)
            {
                Matrix d = new Matrix(0, 1, 1, 0, 0, 0);
                rotate.Multiply(d);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            translate.Invert();
            translate.TransformPoints(points);
        }

        // Hack function to to diaonal flip first in transformations first
        static public void TransformPoints_DiagFirst(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            Matrix translate = new Matrix();
            Matrix rotate = new Matrix();

            // Put the points into origin/local space
            translate.Translate(-origin.X, -origin.Y);
            translate.TransformPoints(points);

            // Apply the flips/rotations
            if (diagonal)
            {
                Matrix d = new Matrix(0, 1, 1, 0, 0, 0);
                rotate.Multiply(d);
            }
            if (horizontal)
            {
                Matrix h = new Matrix(-1, 0, 0, 1, 0, 0);
                rotate.Multiply(h);
            }
            if (vertical)
            {
                Matrix v = new Matrix(1, 0, 0, -1, 0, 0);
                rotate.Multiply(v);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            translate.Invert();
            translate.TransformPoints(points);
        }

        static public void TranslatePoints(PointF[] points, PointF translate)
        {
            SizeF trans = new SizeF(translate.X, translate.Y);
            for (int p = 0; p < points.Length; ++p)
            {
                points[p] = PointF.Add(points[p], trans);
            }
        }

    }
}
