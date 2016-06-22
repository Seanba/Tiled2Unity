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
    /*
     * Extends the PointSet by adding some Constraints on how it will be triangulated<br>
     * A constraint defines an edge between two points in the set, these edges can not
     * be crossed. They will be enforced triangle edges after a triangulation.
     * <p>
     * 
     * 
     * @author Thomas Åhlén, thahlen@gmail.com
     * @author Lee Wilson, lwilson@ea.com
     */
    public class ConstrainedPointSet : PointSet
    {
        protected Dictionary<uint, TriangulationConstraint> mConstraintMap = new Dictionary<uint, TriangulationConstraint>();
        protected List<Contour> mHoles = new List<Contour>();

        public override TriangulationMode TriangulationMode { get { return TriangulationMode.Constrained; } }


        public ConstrainedPointSet(List<TriangulationPoint> bounds)
            : base(bounds)
        {
            AddBoundaryConstraints();
        }

        public ConstrainedPointSet(List<TriangulationPoint> bounds, List<TriangulationConstraint> constraints)
            : base(bounds)
        {
            AddBoundaryConstraints();
            AddConstraints(constraints);
        }

        public ConstrainedPointSet(List<TriangulationPoint> bounds, int[] indices)
            : base(bounds)
        {
            AddBoundaryConstraints();
            List<TriangulationConstraint> l = new List<TriangulationConstraint>();
            for (int i = 0; i < indices.Length; i += 2)
            {
                TriangulationConstraint tc = new TriangulationConstraint(bounds[i], bounds[i + 1]);
                l.Add(tc);
            }
            AddConstraints(l);
        }


        protected void AddBoundaryConstraints()
        {
            TriangulationPoint ptLL = null;
            TriangulationPoint ptLR = null;
            TriangulationPoint ptUR = null;
            TriangulationPoint ptUL = null;
            if (!TryGetPoint(MinX, MinY, out ptLL))
            {
                ptLL = new TriangulationPoint(MinX, MinY);
                Add(ptLL);
            }
            if (!TryGetPoint(MaxX, MinY, out ptLR))
            {
                ptLR = new TriangulationPoint(MaxX, MinY);
                Add(ptLR);
            }
            if (!TryGetPoint(MaxX, MaxY, out ptUR))
            {
                ptUR = new TriangulationPoint(MaxX, MaxY);
                Add(ptUR);
            }
            if (!TryGetPoint(MinX, MaxY, out ptUL))
            {
                ptUL = new TriangulationPoint(MinX, MaxY);
                Add(ptUL);
            }
            TriangulationConstraint tcLLtoLR = new TriangulationConstraint(ptLL, ptLR);
            AddConstraint(tcLLtoLR);
            TriangulationConstraint tcLRtoUR = new TriangulationConstraint(ptLR, ptUR);
            AddConstraint(tcLRtoUR);
            TriangulationConstraint tcURtoUL = new TriangulationConstraint(ptUR, ptUL);
            AddConstraint(tcURtoUL);
            TriangulationConstraint tcULtoLL = new TriangulationConstraint(ptUL, ptLL);
            AddConstraint(tcULtoLL);
        }


        public override void Add(Point2D p)
        {
            Add(p as TriangulationPoint, -1, true);
        }


        public override void Add(TriangulationPoint p)
        {
            Add(p, -1, true);
        }


        public override bool AddRange(List<TriangulationPoint> points)
        {
            bool bOK = true;
            foreach (TriangulationPoint p in points)
            {
                bOK = Add(p, -1, true) && bOK;
            }

            return bOK;
        }


        // Assumes that points being passed in the list are connected and form a polygon.
        // Note that some error checking is done for robustness, but for the most part,
        // we have to rely on the user to feed us "correct" data
        public bool AddHole(List<TriangulationPoint> points, string name)
        {
            if (points == null)
            {
                return false;
            }

            //// split our self-intersection sections into their own lists
            List<Contour> pts = new List<Contour>();
            int listIdx = 0;
            {
                Contour c = new Contour(this, points, WindingOrderType.Unknown);
                pts.Add(c);

                // only constrain the points if we actually HAVE a bounding rect
                if (mPoints.Count > 1)
                {
                    // constrain the points to bounding rect
                    int numPoints = pts[listIdx].Count;
                    for (int i = 0; i < numPoints; ++i)
                    {
                        ConstrainPointToBounds(pts[listIdx][i]);
                    }
                }
            }

            while (listIdx < pts.Count)
            {
                // simple sanity checking - remove duplicate coincident points before
                // we check the polygon: fast, simple algorithm that eliminate lots of problems
                // that only more expensive checks will find
                pts[listIdx].RemoveDuplicateNeighborPoints();
                pts[listIdx].WindingOrder = Point2DList.WindingOrderType.Default;

                bool bListOK = true;
                Point2DList.PolygonError err = pts[listIdx].CheckPolygon();
                while (bListOK && err != PolygonError.None)
                {
                    if ((err & PolygonError.NotEnoughVertices) == PolygonError.NotEnoughVertices)
                    {
                        bListOK = false;
                        continue;
                    }
                    if ((err & PolygonError.NotSimple) == PolygonError.NotSimple)
                    {
                        // split the polygons, remove the current list and add the resulting list to the end
                        //List<Point2DList> l = TriangulationUtil.SplitSelfIntersectingPolygon(pts[listIdx], pts[listIdx].Epsilon);
                        List<Point2DList> l = PolygonUtil.SplitComplexPolygon(pts[listIdx], pts[listIdx].Epsilon);
                        pts.RemoveAt(listIdx);
                        foreach (Point2DList newList in l)
                        {
                            Contour c = new Contour(this);
                            c.AddRange(newList);
                            pts.Add(c);
                        }
                        err = pts[listIdx].CheckPolygon();
                        continue;
                    }
                    if ((err & PolygonError.Degenerate) == PolygonError.Degenerate)
                    {
                        pts[listIdx].Simplify(this.Epsilon);
                        err = pts[listIdx].CheckPolygon();
                        continue;
                        //err &= ~(PolygonError.Degenerate);
                        //if (pts[listIdx].Count < 3)
                        //{
                        //    err |= PolygonError.NotEnoughVertices;
                        //    bListOK = false;
                        //    continue;
                        //}
                    }
                    if ((err & PolygonError.AreaTooSmall) == PolygonError.AreaTooSmall ||
                        (err & PolygonError.SidesTooCloseToParallel) == PolygonError.SidesTooCloseToParallel ||
                        (err & PolygonError.TooThin) == PolygonError.TooThin ||
                        (err & PolygonError.Unknown) == PolygonError.Unknown)
                    {
                        bListOK = false;
                        continue;
                    }
                    // non-convex polygons are ok
                    //if ((err & PolygonError.NotConvex) == PolygonError.NotConvex)
                    //{
                    //}
                }
                if (!bListOK && pts[listIdx].Count != 2)
                {
                    pts.RemoveAt(listIdx);
                }
                else
                {
                    ++listIdx;
                }
            }

            bool bOK = true;
            listIdx = 0;
            while (listIdx < pts.Count)
            {
                int numPoints = pts[listIdx].Count;
                if (numPoints < 2)
                {
                    // should not be possible by this point...
                    ++listIdx;
                    bOK = false;
                    continue;
                }
                else if (numPoints == 2)
                {
                    uint constraintCode = TriangulationConstraint.CalculateContraintCode(pts[listIdx][0], pts[listIdx][1]);
                    TriangulationConstraint tc = null;
                    if (!mConstraintMap.TryGetValue(constraintCode, out tc))
                    {
                        tc = new TriangulationConstraint(pts[listIdx][0], pts[listIdx][1]);
                        AddConstraint(tc);
                    }
                }
                else
                {
                    Contour ph = new Contour(this, pts[listIdx], Point2DList.WindingOrderType.Unknown);
                    ph.WindingOrder = Point2DList.WindingOrderType.Default;
                    ph.Name = name + ":" + listIdx.ToString();
                    mHoles.Add(ph);
                }
                ++listIdx;
            }

            return bOK;
        }


        // this method adds constraints singly and does not assume that they form a contour
        // If you are trying to add a "series" or edges (or "contour"), use AddHole instead.
        public bool AddConstraints(List<TriangulationConstraint> constraints)
        {
            if (constraints == null || constraints.Count < 1)
            {
                return false;
            }

            bool bOK = true;
            foreach (TriangulationConstraint tc in constraints)
            {
                if (ConstrainPointToBounds(tc.P) || ConstrainPointToBounds(tc.Q))
                {
                    tc.CalculateContraintCode();
                }

                TriangulationConstraint tcTmp = null;
                if (!mConstraintMap.TryGetValue(tc.ConstraintCode, out tcTmp))
                {
                    tcTmp = tc;
                    bOK = AddConstraint(tcTmp) && bOK;
                }
            }

            return bOK;
        }


        public bool AddConstraint(TriangulationConstraint tc)
        {
            if (tc == null || tc.P == null || tc.Q == null)
            {
                return false;
            }

            // If we already have this constraint, then there's nothing to do.  Since we already have
            // a valid constraint in the map with the same ConstraintCode, then we're guaranteed that
            // the points are also valid (and have the same coordinates as the ones being passed in with
            // this constrain).  Return true to indicate that we successfully "added" the constraint
            if (mConstraintMap.ContainsKey(tc.ConstraintCode))
            {
                return true;
            }

            // Make sure the constraint is not using points that are duplicates of ones already stored
            // If it is, replace the Constraint Points with the points already stored.
            TriangulationPoint p;
            if (TryGetPoint(tc.P.X, tc.P.Y, out p))
            {
                tc.P = p;
            }
            else
            {
                Add(tc.P);
            }

            if (TryGetPoint(tc.Q.X, tc.Q.Y, out p))
            {
                tc.Q = p;
            }
            else
            {
                Add(tc.Q);
            }

            mConstraintMap.Add(tc.ConstraintCode, tc);

            return true;
        }


        public bool TryGetConstraint(uint constraintCode, out TriangulationConstraint tc)
        {
            return mConstraintMap.TryGetValue(constraintCode, out tc);
        }


        public int GetNumConstraints()
        {
            return mConstraintMap.Count;
        }


        public Dictionary<uint, TriangulationConstraint>.Enumerator GetConstraintEnumerator()
        {
            return mConstraintMap.GetEnumerator();
        }


        public int GetNumHoles()
        {
            int numHoles = 0;
            foreach (Contour c in mHoles)
            {
                numHoles += c.GetNumHoles(false);
            }

            return numHoles;
        }


        public Contour GetHole(int idx)
        {
            if (idx < 0 || idx >= mHoles.Count)
            {
                return null;
            }

            return mHoles[idx];
        }


        public int GetActualHoles(out List<Contour> holes)
        {
            holes = new List<Contour>();
            foreach (Contour c in mHoles)
            {
                c.GetActualHoles(false, ref holes);
            }

            return holes.Count;
        }


        protected void InitializeHoles()
        {
            Contour.InitializeHoles(mHoles, this, this);
            foreach (Contour c in mHoles)
            {
                c.InitializeHoles(this);
            }
        }


        public override bool Initialize()
        {
            InitializeHoles();
            return base.Initialize();
        }


        public override void Prepare(TriangulationContext tcx)
        {
            if (!Initialize())
            {
                return;
            }

            base.Prepare(tcx);

            Dictionary<uint, TriangulationConstraint>.Enumerator it = mConstraintMap.GetEnumerator();
            while (it.MoveNext())
            {
                TriangulationConstraint tc = it.Current.Value;
                tcx.NewConstraint(tc.P, tc.Q);
            }
        }


        public override void AddTriangle(DelaunayTriangle t)
        {
            Triangles.Add(t);
        }

    }
}
