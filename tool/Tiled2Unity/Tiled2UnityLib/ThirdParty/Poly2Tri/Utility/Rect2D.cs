/* Poly2Tri
 * Copyright (c) 2009-2010, Poly2Tri Contributors
 * http://code.google.com/p/poly2tri/
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * * Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 * * Neither the name of Poly2Tri nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without specific
 *   prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;


namespace Poly2Tri
{
    public class Rect2D
    {
        private double mMinX;   // left
        private double mMaxX;   // right
        private double mMinY;   // bottom // top
        private double mMaxY;   // top    // bottom

        public double MinX { get { return mMinX; } set { mMinX = value; } }
        public double MaxX { get { return mMaxX; } set { mMaxX = value; } }
        public double MinY { get { return mMinY; } set { mMinY = value; } }
        public double MaxY { get { return mMaxY; } set { mMaxY = value; } }
        public double Left { get { return mMinX; } set { mMinX = value; } }
        public double Right { get { return mMaxX; } set { mMaxX = value; } }
        public double Top { get { return mMaxY; } set { mMaxY = value; } }
        public double Bottom { get { return mMinY; } set { mMinY = value; } }

        public double Width { get { return (Right - Left); } }
        public double Height { get { return (Top - Bottom); } }
        public bool Empty { get { return (Left == Right) || (Top == Bottom); } }


        public Rect2D()
        {
            Clear();
        }


        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        public override bool Equals(Object obj)
        {
            Rect2D r = obj as Rect2D;
            if( r != null)
            {
                return Equals(r);
            }

            return base.Equals(obj);
        }


        public bool Equals(Rect2D r)
        {
            return Equals(r, MathUtil.EPSILON);
        }


        public bool Equals(Rect2D r, double epsilon)
        {
            if (!MathUtil.AreValuesEqual(MinX, r.MinX, epsilon))
            {
                return false;
            }
            if (!MathUtil.AreValuesEqual(MaxX, r.MaxX))
            {
                return false;
            }
            if (!MathUtil.AreValuesEqual(MinY, r.MinY, epsilon))
            {
                return false;
            }
            if (!MathUtil.AreValuesEqual(MaxY, r.MaxY, epsilon))
            {
                return false;
            }

            return true;
        }


        public void Clear()
        {
            MinX = Double.MaxValue;
            MaxX = Double.MinValue;
            MinY = Double.MaxValue;
            MaxY = Double.MinValue;
        }


        public void Set(double xmin, double xmax, double ymin, double ymax)
        {
            MinX = xmin;
            MaxX = xmax;
            MinY = ymin;
            MaxY = ymax;
            Normalize();
        }


        public void Set(Rect2D b)
        {
            MinX = b.MinX;
            MaxX = b.MaxX;
            MinY = b.MinY;
            MaxY = b.MaxY;
        }


        public void SetSize(double w, double h)
        {
            Right = Left + w;
            Top = Bottom + h;
        }


        /// <summary>
        /// Returns whether the coordinate is inside the bounding box.  Note that this will return
        /// false if the point is ON the edge of the bounding box.  If you want to test for whether
        /// the point is inside OR on the rect, use ContainsInclusive
        /// </summary>
        public bool Contains(double x, double y)
        {
            return (x > Left) && (y > Bottom) && (x < Right) && (y < Top);
        }
        public bool Contains(Point2D p) { return Contains(p.X, p.Y); }
        public bool Contains(Rect2D r)
        {
            return (Left < r.Left) && (Right > r.Right) && (Top < r.Top) && (Bottom > r.Bottom);
        }


        /// <summary>
        /// Returns whether the coordinate is inside the bounding box.  Note that this will return
        /// false if the point is ON the edge of the bounding box.  If you want to test for whether
        /// the point is inside OR on the rect, use ContainsInclusive
        /// </summary>
        public bool ContainsInclusive(double x, double y)
        {
            return (x >= Left) && (y >= Top) && (x <= Right) && (y <= Bottom);
        }
        public bool ContainsInclusive(double x, double y, double epsilon)
        {
            return ((x + epsilon) >= Left) && ((y + epsilon) >= Top) && ((x - epsilon) <= Right) && ((y - epsilon) <= Bottom);
        }
        public bool ContainsInclusive(Point2D p) { return ContainsInclusive(p.X, p.Y); }
        public bool ContainsInclusive(Point2D p, double epsilon) { return ContainsInclusive(p.X, p.Y, epsilon); }
        public bool ContainsInclusive(Rect2D r)
        {
            return (Left <= r.Left) && (Right >= r.Right) && (Top <= r.Top) && (Bottom >= r.Bottom);
        }
        public bool ContainsInclusive(Rect2D r, double epsilon)
        {
            return ((Left - epsilon) <= r.Left) && ((Right + epsilon) >= r.Right) && ((Top - epsilon) <= r.Top) && ((Bottom + epsilon) >= r.Bottom);
        }


        public bool Intersects(Rect2D r)
        {
            return  (Right > r.Left) &&
                    (Left < r.Right) &&
                    (Bottom < r.Top) &&
                    (Top > r.Bottom);
        }


        public Point2D GetCenter()
        {
            Point2D p = new Point2D((Left + Right ) / 2, (Bottom + Top) / 2);
            return p;
        }

 
        public bool IsNormalized()
        {
            return (Right >= Left) && (Bottom <= Top);
        }


        public void Normalize()
        {
            if (Left > Right)
            {
                MathUtil.Swap<double>(ref mMinX, ref mMaxX);
            }

            if (Bottom < Top)
            {
                MathUtil.Swap<double>(ref mMinY, ref mMaxY);
            }
        }


        public void AddPoint(Point2D p)
        {
            MinX = Math.Min(MinX, p.X);
            MaxX = Math.Max(MaxX, p.X);
            MinY = Math.Min(MinY, p.Y);
            MaxY = Math.Max(MaxY, p.Y);
        }


        public void Inflate(double w, double h)
        { 
            Left   -= w; 
            Top    += h; 
            Right  += w; 
            Bottom -= h; 
        }


        public void Inflate(double left, double top, double right, double bottom)
        { 
            Left   -= left; 
            Top    += top; 
            Right  += right; 
            Bottom -= bottom; 
        }


        public void Offset(double w, double h)
        {
            Left   += w; 
            Top    += h; 
            Right  += w; 
            Bottom += h;
        }


        public void SetPosition(double x, double y)
        {
            double w = Right  - Left;
            double h = Bottom - Top;
            Left   = x; 
            Bottom = y; 
            Right  = x + w; 
            Top    = y + h;
        }


        /// Intersection
        ///
        /// Sets the rectangle to the intersection of two rectangles. 
        /// Returns true if there is any intersection between the two rectangles.
        /// If there is no intersection, the rectangle is set to 0, 0, 0, 0.
        /// Either of the input rectangles may be the same as destination rectangle.
        ///
        public bool Intersection(Rect2D r1, Rect2D r2)
        {
            if (!TriangulationUtil.RectsIntersect(r1, r2))
            {
                Left = Right = Top = Bottom = 0.0;
                return false;
            }

            Left   = (r1.Left   > r2.Left)   ? r1.Left   : r2.Left;
            Top    = (r1.Top    < r2.Top )   ? r1.Top    : r2.Top;
            Right  = (r1.Right  < r2.Right)  ? r1.Right  : r2.Right;
            Bottom = (r1.Bottom > r2.Bottom) ? r1.Bottom : r2.Bottom;

            return true;
        }


        /// Union
        ///
        /// Sets the rectangle to the union of two rectangles r1 and r2. 
        /// If either rect is empty, it is ignored. If both are empty, the rectangle
        /// is set to r1.
        /// Either of the input rectangle references may refer to the destination rectangle.
        ///
        public void Union(Rect2D r1, Rect2D r2)
        {
            if ((r2.Right == r2.Left) || (r2.Bottom == r2.Top))
            {
                Set(r1);
            }
            else if ((r1.Right == r1.Left) || (r1.Bottom == r1.Top))
            {
                Set(r2);
            }
            else
            {
                Left = (r1.Left < r2.Left) ? r1.Left : r2.Left;
                Top = (r1.Top > r2.Top) ? r1.Top : r2.Top;
                Right = (r1.Right > r2.Right) ? r1.Right : r2.Right;
                Bottom = (r1.Bottom < r2.Bottom) ? r1.Bottom : r2.Bottom;
            }
        }

    }
}
