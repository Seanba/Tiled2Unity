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
using System.Collections.Generic;
using System.Text;


namespace Poly2Tri
{

    /// <summary>
    /// This is basically a light-weight version of the Polygon class, but with limited functionality and
    /// used for different purposes.   Nonetheless, for all intents and purposes, this should actually be
    /// a polygon (though not a Polygon..)
    /// </summary>
    public class Contour : Point2DList, ITriangulatable, IEnumerable<TriangulationPoint>, IList<TriangulationPoint>
    {
        private List<Contour> mHoles = new List<Contour>();
        private ITriangulatable mParent = null;
        private string mName = "";

        public new TriangulationPoint this[int index]
        {
            get { return mPoints[index] as TriangulationPoint; }
            set { mPoints[index] = value; }
        }
        public string Name { get { return mName; } set { mName = value; } }


        public IList<DelaunayTriangle> Triangles
        {
            get
            {
                throw new NotImplementedException("PolyHole.Triangles should never get called");
            }
            private set { }
        }
        public TriangulationMode TriangulationMode { get { return mParent.TriangulationMode; } }
        public string FileName { get { return mParent.FileName; } set { } }
        public bool DisplayFlipX { get { return mParent.DisplayFlipX; } set { } }
        public bool DisplayFlipY { get { return mParent.DisplayFlipY; } set { } }
        public float DisplayRotate { get { return mParent.DisplayRotate; } set { } }
        public double Precision { get { return mParent.Precision; } set { } }
        public double MinX { get { return mBoundingBox.MinX; } }
        public double MaxX { get { return mBoundingBox.MaxX; } }
        public double MinY { get { return mBoundingBox.MinY; } }
        public double MaxY { get { return mBoundingBox.MaxY; } }
        public Rect2D Bounds { get { return mBoundingBox; } }


        public Contour(ITriangulatable parent)
        {
            mParent = parent;
        }


        public Contour(ITriangulatable parent, IList<TriangulationPoint> points, Point2DList.WindingOrderType windingOrder)
        {
            // Currently assumes that input is pre-checked for validity
            mParent = parent;
            AddRange(points, windingOrder);
        }


        public override string ToString()
        {
            return mName + " : " + base.ToString();
        }


        IEnumerator<TriangulationPoint> IEnumerable<TriangulationPoint>.GetEnumerator()
        {
            return new TriangulationPointEnumerator(mPoints);
        }


        public int IndexOf(TriangulationPoint p)
        {
            return mPoints.IndexOf(p);
        }


        public void Add(TriangulationPoint p)
        {
            Add(p, -1, true);
        }


        protected override void Add(Point2D p, int idx, bool bCalcWindingOrderAndEpsilon)
        {
            TriangulationPoint pt = null;
            if (p is TriangulationPoint)
            {
                pt = p as TriangulationPoint;
            }
            else
            {
                pt = new TriangulationPoint(p.X, p.Y);
            }
            if (idx < 0)
            {
                mPoints.Add(pt);
            }
            else
            {
                mPoints.Insert(idx, pt);
            }
            mBoundingBox.AddPoint(pt);
            if (bCalcWindingOrderAndEpsilon)
            {
                if (mWindingOrder == WindingOrderType.Unknown)
                {
                    mWindingOrder = CalculateWindingOrder();
                }
                mEpsilon = CalculateEpsilon();
            }
        }


        public override void AddRange(IEnumerator<Point2D> iter, WindingOrderType windingOrder)
        {
            if (iter == null)
            {
                return;
            }

            if (mWindingOrder == WindingOrderType.Unknown && Count == 0)
            {
                mWindingOrder = windingOrder;
            }
            bool bReverseReadOrder = (WindingOrder != WindingOrderType.Unknown) && (windingOrder != WindingOrderType.Unknown) && (WindingOrder != windingOrder);
            bool bAddedFirst = true;
            int startCount = mPoints.Count;
            iter.Reset();
            while (iter.MoveNext())
            {
                TriangulationPoint pt = null;
                if (iter.Current is TriangulationPoint)
                {
                    pt = iter.Current as TriangulationPoint;
                }
                else
                {
                    pt = new TriangulationPoint(iter.Current.X, iter.Current.Y);
                }
                if (!bAddedFirst)
                {
                    bAddedFirst = true;
                    mPoints.Add(pt);
                }
                else if (bReverseReadOrder)
                {
                    mPoints.Insert(startCount, pt);
                }
                else
                {
                    mPoints.Add(pt);
                }
                mBoundingBox.AddPoint(iter.Current);
            }
            if (mWindingOrder == WindingOrderType.Unknown && windingOrder == WindingOrderType.Unknown)
            {
                mWindingOrder = CalculateWindingOrder();
            }
            mEpsilon = CalculateEpsilon();
        }


        public void AddRange(IList<TriangulationPoint> points, Point2DList.WindingOrderType windingOrder)
        {
            if (points == null || points.Count < 1)
            {
                return;
            }

            if (mWindingOrder == Point2DList.WindingOrderType.Unknown && Count == 0)
            {
                mWindingOrder = windingOrder;
            }

            int numPoints = points.Count;
            bool bReverseReadOrder = (WindingOrder != WindingOrderType.Unknown) && (windingOrder != WindingOrderType.Unknown) && (WindingOrder != windingOrder);
            for (int i = 0; i < numPoints; ++i)
            {
                int idx = i;
                if (bReverseReadOrder)
                {
                    idx = points.Count - i - 1;
                }
                Add(points[idx], -1, false);
            }
            if (mWindingOrder == WindingOrderType.Unknown)
            {
                mWindingOrder = CalculateWindingOrder();
            }
            mEpsilon = CalculateEpsilon();
        }


        public void Insert(int idx, TriangulationPoint p)
        {
            Add(p, idx, true);
        }


        public bool Remove(TriangulationPoint p)
        {
            return Remove(p as Point2D);
        }


        public bool Contains(TriangulationPoint p)
        {
            return mPoints.Contains(p);
        }


        public void CopyTo(TriangulationPoint[] array, int arrayIndex)
        {
            int numElementsToCopy = Math.Min(Count, array.Length - arrayIndex);
            for (int i = 0; i < numElementsToCopy; ++i)
            {
                array[arrayIndex + i] = mPoints[i] as TriangulationPoint;
            }
        }


        protected void AddHole(Contour c)
        {
            // no checking is done here as we rely on InitializeHoles for that
            c.mParent = this;
            mHoles.Add(c);
        }


        /// <summary>
        /// returns number of holes that are actually holes, including all children of children, etc.   Does NOT
        /// include holes that are not actually holes.   For example, if the parent is not a hole and this contour has
        /// a hole that contains a hole, then the number of holes returned would be 2 - one for the current hole (because
        /// the parent is NOT a hole and thus this hole IS a hole), and 1 for the child of the child.
        /// </summary>
        /// <param name="parentIsHole"></param>
        /// <returns></returns>
        public int GetNumHoles(bool parentIsHole)
        {
            int numHoles = parentIsHole ? 0 : 1;
            foreach (Contour c in mHoles)
            {
                numHoles += c.GetNumHoles(!parentIsHole);
            }

            return numHoles;
        }


        /// <summary>
        /// returns the basic number of child holes of THIS contour, not including any children of children, etc nor
        /// examining whether any children are actual holes.
        /// </summary>
        /// <returns></returns>
        public int GetNumHoles()
        {
            return mHoles.Count;
        }


        public Contour GetHole(int idx)
        {
            if (idx < 0 || idx >= mHoles.Count)
            {
                return null;
            }

            return mHoles[idx];
        }


        public void GetActualHoles(bool parentIsHole, ref List<Contour> holes)
        {
            if (parentIsHole)
            {
                holes.Add(this);
            }

            foreach (Contour c in mHoles)
            {
                c.GetActualHoles(!parentIsHole, ref holes);
            }
        }


        public List<Contour>.Enumerator GetHoleEnumerator()
        {
            return mHoles.GetEnumerator();
        }


        public void InitializeHoles(ConstrainedPointSet cps)
        {
            Contour.InitializeHoles(mHoles, this, cps);
            foreach (Contour c in mHoles)
            {
                c.InitializeHoles(cps);
            }
        }


        public static void InitializeHoles(List<Contour> holes, ITriangulatable parent, ConstrainedPointSet cps)
        {
            int numHoles = holes.Count;
            int holeIdx = 0;

            // pass 1 - remove duplicates
            while (holeIdx < numHoles)
            {
                int hole2Idx = holeIdx + 1;
                while (hole2Idx < numHoles)
                {
                    bool bSamePolygon = PolygonUtil.PolygonsAreSame2D(holes[holeIdx], holes[hole2Idx]);
                    if (bSamePolygon)
                    {
                        // remove one of them
                        holes.RemoveAt(hole2Idx);
                        --numHoles;
                    }
                    else
                    {
                        ++hole2Idx;
                    }
                }
                ++holeIdx;
            }

            // pass 2: Intersections and Containment
            holeIdx = 0;
            while (holeIdx < numHoles)
            {
                bool bIncrementHoleIdx = true;
                int hole2Idx = holeIdx + 1;
                while (hole2Idx < numHoles)
                {
                    if (PolygonUtil.PolygonContainsPolygon(holes[holeIdx], holes[holeIdx].Bounds, holes[hole2Idx], holes[hole2Idx].Bounds, false))
                    {
                        holes[holeIdx].AddHole(holes[hole2Idx]);
                        holes.RemoveAt(hole2Idx);
                        --numHoles;
                    }
                    else if (PolygonUtil.PolygonContainsPolygon(holes[hole2Idx], holes[hole2Idx].Bounds, holes[holeIdx], holes[holeIdx].Bounds, false))
                    {
                        holes[hole2Idx].AddHole(holes[holeIdx]);
                        holes.RemoveAt(holeIdx);
                        --numHoles;
                        bIncrementHoleIdx = false;
                        break;
                    }
                    else
                    {
                        bool bIntersect = PolygonUtil.PolygonsIntersect2D(holes[holeIdx], holes[holeIdx].Bounds, holes[hole2Idx], holes[hole2Idx].Bounds);
                        if (bIntersect)
                        {
                            // this is actually an error condition
                            // fix by merging hole1 and hole2 into hole1 (including the holes inside hole2!) and delete hole2
                            // Then, because hole1 is now changed, restart it's check.
                            PolygonOperationContext ctx = new PolygonOperationContext();
                            if (!ctx.Init(PolygonUtil.PolyOperation.Union | PolygonUtil.PolyOperation.Intersect, holes[holeIdx], holes[hole2Idx]))
                            {
                                if (ctx.mError == PolygonUtil.PolyUnionError.Poly1InsidePoly2)
                                {
                                    holes[hole2Idx].AddHole(holes[holeIdx]);
                                    holes.RemoveAt(holeIdx);
                                    --numHoles;
                                    bIncrementHoleIdx = false;
                                    break;
                                }
                                else
                                {
                                    throw new Exception("PolygonOperationContext.Init had an error during initialization");
                                }
                            }
                            PolygonUtil.PolyUnionError pue = PolygonUtil.PolygonOperation(ctx);
                            if (pue == PolygonUtil.PolyUnionError.None)
                            {
                                Point2DList union = ctx.Union;
                                Point2DList intersection = ctx.Intersect;

                                // create a new contour for the union
                                Contour c = new Contour(parent);
                                c.AddRange(union);
                                c.Name = "(" + holes[holeIdx].Name + " UNION " + holes[hole2Idx].Name + ")";
                                c.WindingOrder = Point2DList.WindingOrderType.Default;

                                // add children from both of the merged contours
                                int numChildHoles = holes[holeIdx].GetNumHoles();
                                for(int i = 0; i < numChildHoles; ++i)
                                {
                                    c.AddHole(holes[holeIdx].GetHole(i));
                                }
                                numChildHoles = holes[hole2Idx].GetNumHoles();
                                for (int i = 0; i < numChildHoles; ++i)
                                {
                                    c.AddHole(holes[hole2Idx].GetHole(i));
                                }

                                // make sure we preserve the contours of the intersection
                                Contour cInt = new Contour(c);
                                cInt.AddRange(intersection);
                                cInt.Name = "(" + holes[holeIdx].Name + " INTERSECT " + holes[hole2Idx].Name + ")";
                                cInt.WindingOrder = Point2DList.WindingOrderType.Default;
                                c.AddHole(cInt);

                                // replace the current contour with the merged contour
                                holes[holeIdx] = c;

                                // toss the second contour
                                holes.RemoveAt(hole2Idx);
                                --numHoles;

                                // current hole is "examined", so move to the next one
                                hole2Idx = holeIdx + 1;
                            }
                            else
                            {
                                throw new Exception("PolygonOperation had an error!");
                            }
                        }
                        else
                        {
                            ++hole2Idx;
                        }
                    }
                }
                if (bIncrementHoleIdx)
                {
                    ++holeIdx;
                }
            }

            numHoles = holes.Count;
            holeIdx = 0;
            while (holeIdx < numHoles)
            {
                int numPoints = holes[holeIdx].Count;
                for (int i = 0; i < numPoints; ++i)
                {
                    int j = holes[holeIdx].NextIndex(i);
                    uint constraintCode = TriangulationConstraint.CalculateContraintCode(holes[holeIdx][i], holes[holeIdx][j]);
                    TriangulationConstraint tc = null;
                    if (!cps.TryGetConstraint(constraintCode, out tc))
                    {
                        tc = new TriangulationConstraint(holes[holeIdx][i], holes[holeIdx][j]);
                        cps.AddConstraint(tc);
                    }

                    // replace the points in the holes with valid points
                    if (holes[holeIdx][i].VertexCode == tc.P.VertexCode)
                    {
                        holes[holeIdx][i] = tc.P;
                    }
                    else if (holes[holeIdx][j].VertexCode == tc.P.VertexCode)
                    {
                        holes[holeIdx][j] = tc.P;
                    }
                    if (holes[holeIdx][i].VertexCode == tc.Q.VertexCode)
                    {
                        holes[holeIdx][i] = tc.Q;
                    }
                    else if (holes[holeIdx][j].VertexCode == tc.Q.VertexCode)
                    {
                        holes[holeIdx][j] = tc.Q;
                    }
                }
                ++holeIdx;
            }
        }


        public void Prepare(TriangulationContext tcx)
        {
            throw new NotImplementedException("PolyHole.Prepare should never get called");
        }


        public void AddTriangle(DelaunayTriangle t)
        {
            throw new NotImplementedException("PolyHole.AddTriangle should never get called");
        }


        public void AddTriangles(IEnumerable<DelaunayTriangle> list)
        {
            throw new NotImplementedException("PolyHole.AddTriangles should never get called");
        }


        public void ClearTriangles()
        {
            throw new NotImplementedException("PolyHole.ClearTriangles should never get called");
        }


        public Point2D FindPointInContour()
        {
            if (Count < 3)
            {
                return null;
            }

            // first try the simple approach:
            Point2D p = GetCentroid();
            if (IsPointInsideContour(p))
            {
                return p;
            }

            // brute force it...
            Random random = new Random();
            while (true)
            {
                p.X = (random.NextDouble() * (MaxX - MinX)) + MinX;
                p.Y = (random.NextDouble() * (MaxY - MinY)) + MinY;
                if (IsPointInsideContour(p))
                {
                    return p;
                }
            }
        }


        public bool IsPointInsideContour(Point2D p)
        {
            if (PolygonUtil.PointInPolygon2D(this, p))
            {
                foreach (Contour c in mHoles)
                {
                    if (c.IsPointInsideContour(p))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

    }
}
