using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

// Helper utitlities for performing math within a Tiled context
namespace Tiled2Unity
{
    public class TmxMath
    {
        static public readonly uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        static public readonly uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
        static public readonly uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;

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

        static public void RotatePoints(PointF[] points, TmxObject tmxObject)
        {
            TranslatePoints(points, -tmxObject.Position.X, -tmxObject.Position.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix(-tmxObject.Rotation);
            rotate.TransformPoints(points);

            TranslatePoints(points, tmxObject.Position.X, tmxObject.Position.Y);
        }

        static public void TransformPoints(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            // Put the points into origin/local space
            TranslatePoints(points, -origin.X, -origin.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix();

            // Apply the flips/rotations (order matters)
            if (horizontal)
            {
                TmxRotationMatrix h = new TmxRotationMatrix(-1, 0, 0, 1);
                rotate = TmxRotationMatrix.Multiply(h, rotate);
            }
            if (vertical)
            {
                TmxRotationMatrix v = new TmxRotationMatrix(1, 0, 0, -1);
                rotate = TmxRotationMatrix.Multiply(v, rotate);
            }
            if (diagonal)
            {
                TmxRotationMatrix d = new TmxRotationMatrix(0, 1, 1, 0);
                rotate = TmxRotationMatrix.Multiply(d, rotate);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            TranslatePoints(points, origin.X, origin.Y);
        }

        // Hack function to do diaonal flip first in transformations
        static public void TransformPoints_DiagFirst(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            // Put the points into origin/local space
            TranslatePoints(points, -origin.X, -origin.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix();

            // Apply the flips/rotations (order matters)
            if (diagonal)
            {
                TmxRotationMatrix d = new TmxRotationMatrix(0, 1, 1, 0);
                rotate = TmxRotationMatrix.Multiply(d, rotate);
            }
            if (horizontal)
            {
                TmxRotationMatrix h = new TmxRotationMatrix(-1, 0, 0, 1);
                rotate = TmxRotationMatrix.Multiply(h, rotate);
            }
            if (vertical)
            {
                TmxRotationMatrix v = new TmxRotationMatrix(1, 0, 0, -1);
                rotate = TmxRotationMatrix.Multiply(v, rotate);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            TranslatePoints(points, origin.X, origin.Y);
        }

        static public void TranslatePoints(PointF[] points, float tx, float ty)
        {
            TranslatePoints(points, new PointF(tx, ty));
        }

        static public void TranslatePoints(PointF[] points, PointF translate)
        {
            SizeF trans = new SizeF(translate.X, translate.Y);
            for (int p = 0; p < points.Length; ++p)
            {
                points[p] = PointF.Add(points[p], trans);
            }
        }

        static public bool DoStaggerX(TmxMap tmxMap, int x)
        {
            int staggerX = (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X) ? 1 : 0;
            int staggerEven = (tmxMap.StaggerIndex == TmxMap.MapStaggerIndex.Even) ? 1 : 0;

            return staggerX != 0 && ((x & 1) ^ staggerEven) != 0;
        }

        static public bool DoStaggerY(TmxMap tmxMap, int y)
        {
            int staggerX = (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X) ? 1 : 0;
            int staggerEven = (tmxMap.StaggerIndex == TmxMap.MapStaggerIndex.Even) ? 1 : 0;

            return staggerX == 0 && ((y & 1) ^ staggerEven) != 0;
        }

        static public Point TileCornerInGridCoordinates(TmxMap tmxMap, int x, int y)
        {
            // Support different map display types (orthographic, isometric, etc..)
            // Note: simulates "tileToScreenCoords" function from Tiled source
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Point point = Point.Empty;

                int origin_x = tmxMap.Height * tmxMap.TileWidth / 2;
                point.X = (x - y) * tmxMap.TileWidth / 2 + origin_x;
                point.Y = (x + y) * tmxMap.TileHeight / 2;

                return point;
            }
            else if (tmxMap.Orientation == TmxMap.MapOrientation.Staggered || tmxMap.Orientation == TmxMap.MapOrientation.Hexagonal)
            {
                Point point = Point.Empty;

                int tileWidth = tmxMap.TileWidth & ~1;
                int tileHeight = tmxMap.TileHeight & ~1;

                int sideLengthX = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X ? tmxMap.HexSideLength : 0;
                int sideLengthY = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y ? tmxMap.HexSideLength : 0;

                int sideOffsetX = (tileWidth - sideLengthX) / 2;
                int sideOffsetY = (tileHeight - sideLengthY) / 2;

                int columnWidth = sideOffsetX + sideLengthX;
                int rowHeight = sideOffsetY + sideLengthY;

                if (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X)
                {
                    point.Y = y * (tileHeight + sideLengthY);
                    if (TmxMath.DoStaggerX(tmxMap, x))
                    {
                        point.Y += rowHeight;
                    }

                    point.X = x * columnWidth;
                }
                else
                {
                    point.X = x * (tileWidth + sideLengthX);
                    if (TmxMath.DoStaggerY(tmxMap, y))
                    {
                        point.X += columnWidth;
                    }

                    point.Y = y * rowHeight;
                }

                point.Offset(tileWidth / 2, 0);
                return point;
            }

            // Default orthographic orientation
            return new Point(x * tmxMap.TileWidth, y * tmxMap.TileHeight);
        }

        static public Point TileCornerInScreenCoordinates(TmxMap tmxMap, int x, int y)
        {
            Point point = TileCornerInGridCoordinates(tmxMap, x, y);

            if (tmxMap.Orientation != TmxMap.MapOrientation.Orthogonal)
            {
                point.Offset(-tmxMap.TileWidth / 2, 0);
            }

            return point;
        }

        static public PointF ObjectPointFToMapSpace(TmxMap tmxMap, float x, float y)
        {
            return ObjectPointFToMapSpace(tmxMap, new PointF(x, y));
        }

        static public PointF ObjectPointFToMapSpace(TmxMap tmxMap, PointF pt)
        {
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                PointF xf = PointF.Empty;

                float origin_x = tmxMap.Height * tmxMap.TileWidth * 0.5f;
                float tile_y = pt.Y / tmxMap.TileHeight;
                float tile_x = pt.X / tmxMap.TileHeight;

                xf.X = (tile_x - tile_y) * tmxMap.TileWidth * 0.5f + origin_x;
                xf.Y = (tile_x + tile_y) * tmxMap.TileHeight * 0.5f;
                return xf;
            }

            // Other maps types don't transform object points
            return pt;
        }


        public static Point AddPoints(Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        public static PointF AddPoints(PointF a, PointF b)
        {
            return new PointF(a.X + b.X, a.Y + b.Y);
        }

        public static PointF MidPoint(PointF a, PointF b)
        {
            float mx = (a.X + b.X) * 0.5f;
            float my = (a.Y + b.Y) * 0.5f;
            return new PointF(mx, my);
        }

        public static PointF ScalePoint(PointF p, float s)
        {
            return new PointF(p.X * s, p.Y * s);
        }

        public static PointF ScalePoint(float x, float y, float s)
        {
            return new PointF(x * s, y * s);
        }

        public static PointF AddPointsScale(PointF a, PointF b, float scale)
        {
            return new PointF(a.X + b.X * scale, a.Y + b.Y * scale);
        }

        public static List<PointF> GetPointsInMapSpace(TmxMap tmxMap, TmxHasPoints objectWithPoints)
        {
            PointF local = TmxMath.ObjectPointFToMapSpace(tmxMap, 0, 0);
            local.X = -local.X;
            local.Y = -local.Y;

            List<PointF> xfPoints = objectWithPoints.Points.Select(pt => TmxMath.ObjectPointFToMapSpace(tmxMap, pt)).ToList();
            xfPoints = xfPoints.Select(pt => TmxMath.AddPoints(pt, local)).ToList();
            return xfPoints;
        }

        // We don't want ugly floating point issues. Take for granted that sanitized values can be rounded to nearest 1/256th of value
        public static float Sanitize(float v)
        {
            return (float)Math.Round(v * 256) / 256.0f;
        }

        public static PointF Sanitize(PointF pt)
        {
            return new PointF(Sanitize(pt.X), Sanitize(pt.Y));
        }
    }
}
