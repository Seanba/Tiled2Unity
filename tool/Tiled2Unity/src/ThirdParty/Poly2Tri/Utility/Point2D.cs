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
using System.Collections;
using System.Collections.Generic;


namespace Poly2Tri
{
    public class Point2D : IComparable<Point2D>
    {
        protected double mX = 0.0;
        public virtual double X { get { return mX; } set { mX = value; } }
        protected double mY = 0.0;
        public virtual double Y { get { return mY; } set { mY = value; } }

        public float Xf { get { return (float)X; } }
        public float Yf { get { return (float)Y; } }


        public Point2D()
        {
            mX = 0.0;
            mY = 0.0;
        }


        public Point2D(double x, double y)
        {
            mX = x;
            mY = y;
        }


        public Point2D(Point2D p)
        {
            mX = p.X;
            mY = p.Y;
        }


        public override string ToString()
        {
            return "[" + X.ToString() + "," + Y.ToString() + "]";
        }


        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        public override bool Equals(Object obj)
        {
            Point2D p = obj as Point2D;
            if (p != null)
            {
                return Equals(p);
            }

            return base.Equals(obj);
        }


        public bool Equals(Point2D p)
        {
            return Equals(p, 0.0);
        }


        public bool Equals(Point2D p, double epsilon)
        {
            if ((object)p == null || !MathUtil.AreValuesEqual(X, p.X, epsilon) || !MathUtil.AreValuesEqual(Y, p.Y, epsilon))
            {
                return false;
            }

            return true;
        }


        public int CompareTo(Point2D other)
        {
            if (Y < other.Y)
            {
                return -1;
            }
            else if (Y > other.Y)
            {
                return 1;
            }
            else
            {
                if (X < other.X)
                {
                    return -1;
                }
                else if (X > other.X)
                {
                    return 1;
                }
            }

            return 0;
        }


        public virtual void Set(double x, double y) { X = x; Y = y; }
        public virtual void Set(Point2D p) { X = p.X; Y = p.Y; }

        public void Add(Point2D p) { X += p.X; Y += p.Y; }
        public void Add(double scalar) { X += scalar; Y += scalar; }
        public void Subtract(Point2D p) { X -= p.X; Y -= p.Y; }
        public void Subtract(double scalar) { X -= scalar; Y -= scalar; }
        public void Multiply(Point2D p) { X *= p.X; Y *= p.Y; }
        public void Multiply(double scalar) { X *= scalar; Y *= scalar; }
        public void Divide(Point2D p) { X /= p.X; Y /= p.Y; }
        public void Divide(double scalar) { X /= scalar; Y /= scalar; }
        public void Negate() { X = -X; Y = -Y; }
        public double Magnitude() { return Math.Sqrt((X * X) + (Y * Y)); }
        public double MagnitudeSquared() { return (X * X) + (Y * Y); }
        public double MagnitudeReciprocal() { return 1.0 / Magnitude(); }
        public void Normalize() { Multiply(MagnitudeReciprocal()); }
        public double Dot(Point2D p) { return (X * p.X) + (Y * p.Y); }
        public double Cross(Point2D p) { return (X * p.Y) - (Y * p.X); }
        public void Clamp(Point2D low, Point2D high) { X = Math.Max(low.X, Math.Min(X, high.X)); Y = Math.Max(low.Y, Math.Min(Y, high.Y)); }
        public void Abs() { X = Math.Abs(X); Y = Math.Abs(Y); }
        public void Reciprocal() { if (X != 0.0 && Y != 0.0) { X = 1.0 / X; Y = 1.0 / Y; } }

        public void Translate(Point2D vector) { Add(vector); }
        public void Translate(double x, double y) { X += x; Y += y; }
        public void Scale(Point2D vector) { Multiply(vector); }
        public void Scale(double scalar) { Multiply(scalar); }
        public void Scale(double x, double y) { X *= x; Y *= y; }
        public void Rotate(double radians)
        {
            double cosr = Math.Cos(radians);
            double sinr = Math.Sin(radians);
            double xold = X;
            double yold = Y;
            X = (xold * cosr) - (yold * sinr);
            Y = (xold * sinr) + (yold * cosr);
        }
        public void RotateDegrees(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            Rotate(radians);
        }

        public static double Dot(Point2D lhs, Point2D rhs) { return (lhs.X * rhs.X) + (lhs.Y * rhs.Y); }
        public static double Cross(Point2D lhs, Point2D rhs) { return (lhs.X * rhs.Y) - (lhs.Y * rhs.X); }
        public static Point2D Clamp(Point2D a, Point2D low, Point2D high) { Point2D p = new Point2D(a); p.Clamp(low, high); return p; }
        public static Point2D Min(Point2D a, Point2D b) { Point2D p = new Point2D(); p.X = Math.Min(a.X, b.X); p.Y = Math.Min(a.Y, b.Y); return p; }
        public static Point2D Max(Point2D a, Point2D b) { Point2D p = new Point2D(); p.X = Math.Max(a.X, b.X); p.Y = Math.Max(a.Y, b.Y); return p; }
        public static Point2D Abs(Point2D a) { Point2D p = new Point2D(Math.Abs(a.X), Math.Abs(a.Y)); return p; }
        public static Point2D Reciprocal(Point2D a) { Point2D p = new Point2D(1.0 / a.X, 1.0 / a.Y); return p; }

        // returns a scaled perpendicular vector.  Which direction it goes depends on the order in which the arguments are passed
        public static Point2D Perpendicular(Point2D lhs, double scalar) { Point2D p = new Point2D(lhs.Y * scalar, lhs.X * -scalar); return p; }
        public static Point2D Perpendicular(double scalar, Point2D rhs) { Point2D p = new Point2D(-scalar * rhs.Y, scalar * rhs.X); return p; }

        
        //
        // operator overloading
        //

        // Binary Operators
        // Note that in C#, when a binary operator is overloaded, its corresponding compound assignment operator is also automatically
        // overloaded.  So, for example, overloading operator + implicitly overloads += as well
        public static Point2D operator +(Point2D lhs, Point2D rhs) { Point2D result = new Point2D(lhs);  result.Add(rhs);  return result; }
        public static Point2D operator +(Point2D lhs, double scalar) { Point2D result = new Point2D(lhs); result.Add(scalar); return result; }
        public static Point2D operator -(Point2D lhs, Point2D rhs) { Point2D result = new Point2D(lhs); result.Subtract(rhs); return result; }
        public static Point2D operator -(Point2D lhs, double scalar) { Point2D result = new Point2D(lhs); result.Subtract(scalar); return result; }
        public static Point2D operator *(Point2D lhs, Point2D rhs) { Point2D result = new Point2D(lhs); result.Multiply(rhs); return result; }
        public static Point2D operator *(Point2D lhs, double scalar) { Point2D result = new Point2D(lhs); result.Multiply(scalar); return result; }
        public static Point2D operator *(double scalar, Point2D lhs) { Point2D result = new Point2D(lhs); result.Multiply(scalar); return result; }
        public static Point2D operator /(Point2D lhs, Point2D rhs) { Point2D result = new Point2D(lhs); result.Divide(rhs); return result; }
        public static Point2D operator /(Point2D lhs, double scalar) { Point2D result = new Point2D(lhs); result.Divide(scalar); return result; }

        // Unary Operators
        public static Point2D operator -(Point2D p) { Point2D tmp = new Point2D(p); tmp.Negate(); return tmp; }

        // Relational Operators
        //public static bool operator ==(Point2D lhs, Point2D rhs) { if ((object)lhs != null) { return lhs.Equals(rhs, 0.0); } if ((object)rhs == null) { return true; } else { return false; } }
        //public static bool operator !=(Point2D lhs, Point2D rhs) { if ((object)lhs != null) { return !lhs.Equals(rhs, 0.0); } if ((object)rhs == null) { return false; } else { return true; } }
        public static bool operator <(Point2D lhs, Point2D rhs) { return (lhs.CompareTo(rhs) == -1) ? true : false; }
        public static bool operator >(Point2D lhs, Point2D rhs) { return (lhs.CompareTo(rhs) == 1) ? true : false; }
        public static bool operator <=(Point2D lhs, Point2D rhs) { return (lhs.CompareTo(rhs) <= 0) ? true : false; }
        public static bool operator >=(Point2D lhs, Point2D rhs) { return (lhs.CompareTo(rhs) >= 0) ? true : false; }
    }


    public class Point2DEnumerator : IEnumerator<Point2D>
    {
        protected IList<Point2D> mPoints;
        protected int position = -1;  // Enumerators are positioned before the first element until the first MoveNext() call.


        public Point2DEnumerator(IList<Point2D> points)
        {
            mPoints = points;
        }

        public bool MoveNext()
        {
            position++;
            return (position < mPoints.Count);
        }

        public void Reset()
        {
            position = -1;
        }

        void IDisposable.Dispose() { }

        Object IEnumerator.Current { get { return Current; } }

        public Point2D Current
        {
            get
            {
                if (position < 0 || position >= mPoints.Count)
                {
                    return null;
                }
                return mPoints[position];
            }
        }
    }

}
