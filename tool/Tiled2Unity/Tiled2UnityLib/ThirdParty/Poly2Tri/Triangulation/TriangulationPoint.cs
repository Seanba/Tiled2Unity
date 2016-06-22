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

    public class TriangulationPoint : Point2D
    {
        public static readonly double kVertexCodeDefaultPrecision = 3.0;

        public override double X
        {
            get { return mX; }
            set
            {
                if (value != mX)
                {
                    mX = value;
                    mVertexCode = TriangulationPoint.CreateVertexCode(mX, mY, kVertexCodeDefaultPrecision);

                    // Technically, we should change the ConstraintCodes of any edges that contain this point.
                    // We don't for 2 reasons:
                    // 1) Currently the only time we care about Vertex/Constraint Codes is when entering data in the point-set.
                    //    Once the data is being used by the algorithm, the point locations are (currently) not modified.
                    // 2) Since this Point's Edge list will only contain SOME of the edges that this point is a part of, 
                    //    there currently isn't a way to (easily) get any edges that contain this point but are not in this
                    //    point's edge list.
                }
            }
        }
        public override double Y
        {
            get { return mY; }
            set
            {
                if (value != mY)
                {
                    mY = value;
                    mVertexCode = TriangulationPoint.CreateVertexCode(mX, mY, kVertexCodeDefaultPrecision);

                    // Technically, we should change the ConstraintCodes of any edges that contain this point.
                    // We don't for 2 reasons:
                    // 1) Currently the only time we care about Vertex/Constraint Codes is when entering data in the point-set.
                    //    Once the data is being used by the algorithm, the point locations are (currently) not modified.
                    // 2) Since this Point's Edge list will only contain SOME of the edges that this point is a part of, 
                    //    there currently isn't a way to (easily) get any edges that contain this point but are not in this
                    //    point's edge list.
                }
            }
        }

        protected uint mVertexCode = 0;
        public uint VertexCode { get { return mVertexCode; } }

        // List of edges this point constitutes an upper ending point (CDT)
        public List<DTSweepConstraint> Edges { get; private set; }
        public bool HasEdges { get { return Edges != null; } }


        public TriangulationPoint(double x, double y)
            : this(x, y, kVertexCodeDefaultPrecision)
        {
        }


        public TriangulationPoint(double x, double y, double precision)
            : base(x,y)
        {
            mVertexCode = TriangulationPoint.CreateVertexCode(x, y, precision);
        }


        public override string ToString()
        {
            return base.ToString() + ":{" + mVertexCode.ToString() + "}";
        }


        public override int GetHashCode()
        {
            return (int)mVertexCode;
        }


        public override bool Equals(object obj)
        {
            TriangulationPoint p2 = obj as TriangulationPoint;
            if (p2 != null)
            {
                return mVertexCode == p2.VertexCode;
            }
            else
            {
                return base.Equals(obj);
            }
        }


        public override void Set(double x, double y)
        {
            if (x != mX || y != mY)
            {
                mX = x;
                mY = y;
                mVertexCode = TriangulationPoint.CreateVertexCode(mX, mY, kVertexCodeDefaultPrecision);
            }
        }

        
        public static uint CreateVertexCode(double x, double y, double precision)
        {
            float fx = (float)MathUtil.RoundWithPrecision(x, precision);
            float fy = (float)MathUtil.RoundWithPrecision(y, precision);
            uint vc = MathUtil.Jenkins32Hash(BitConverter.GetBytes(fx), 0);
            vc = MathUtil.Jenkins32Hash(BitConverter.GetBytes(fy), vc);

            return vc;
        }


        public void AddEdge(DTSweepConstraint e)
        {
            if (Edges == null)
            {
                Edges = new List<DTSweepConstraint>();
            }
            Edges.Add(e);
        }

        
        public bool HasEdge(TriangulationPoint p)
        {
            DTSweepConstraint tmp = null;
            return GetEdge(p, out tmp);
        }


        public bool GetEdge(TriangulationPoint p, out DTSweepConstraint edge)
        {
            edge = null;
            if (Edges == null || Edges.Count < 1 || p == null || p.Equals(this))
            {
                return false;
            }

            foreach (DTSweepConstraint sc in Edges)
            {
                if ((sc.P.Equals(this) && sc.Q.Equals(p)) || (sc.P.Equals(p) && sc.Q.Equals(this)))
                {
                    edge = sc;
                    return true;
                }
            }

            return false;
        }


        public static Point2D ToPoint2D(TriangulationPoint p)
        {
            return p as Point2D;
        }
    }


    public class TriangulationPointEnumerator : IEnumerator<TriangulationPoint>
    {
        protected IList<Point2D> mPoints;
        protected int position = -1;  // Enumerators are positioned before the first element until the first MoveNext() call.


        public TriangulationPointEnumerator(IList<Point2D> points)
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

        public TriangulationPoint Current
        {
            get
            {
                if (position < 0 || position >= mPoints.Count)
                {
                    return null;
                }
                return mPoints[position] as TriangulationPoint;
            }
        }
    }


    public class TriangulationPointList : Point2DList
    {

    }

}