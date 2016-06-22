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
    /**
     * @author Thomas Åhlén, thahlen@gmail.com
     */
    public class TriangulationUtil
    {
        /// <summary>
        ///   Requirements:
        /// 1. a,b and c form a triangle.
        /// 2. a and d is know to be on opposite side of bc
        /// <code>
        ///                a
        ///                +
        ///               / \
        ///              /   \
        ///            b/     \c
        ///            +-------+ 
        ///           /    B    \  
        ///          /           \ 
        /// </code>
        ///    Facts:
        ///  d has to be in area B to have a chance to be inside the circle formed by a,b and c
        ///  d is outside B if orient2d(a,b,d) or orient2d(c,a,d) is CW
        ///  This preknowledge gives us a way to optimize the incircle test
        /// </summary>
        /// <param name="pa">triangle point, opposite d</param>
        /// <param name="pb">triangle point</param>
        /// <param name="pc">triangle point</param>
        /// <param name="pd">point opposite a</param>
        /// <returns>true if d is inside circle, false if on circle edge</returns>
        public static bool SmartIncircle(Point2D pa, Point2D pb, Point2D pc, Point2D pd)
        {
            double pdx = pd.X;
            double pdy = pd.Y;
            double adx = pa.X - pdx;
            double ady = pa.Y - pdy;
            double bdx = pb.X - pdx;
            double bdy = pb.Y - pdy;

            double adxbdy = adx * bdy;
            double bdxady = bdx * ady;
            double oabd = adxbdy - bdxady;
            //        oabd = orient2d(pa,pb,pd);
            if (oabd <= 0)
            {
                return false;
            }

            double cdx = pc.X - pdx;
            double cdy = pc.Y - pdy;

            double cdxady = cdx * ady;
            double adxcdy = adx * cdy;
            double ocad = cdxady - adxcdy;
            //      ocad = orient2d(pc,pa,pd);
            if (ocad <= 0)
            {
                return false;
            }

            double bdxcdy = bdx * cdy;
            double cdxbdy = cdx * bdy;

            double alift = adx * adx + ady * ady;
            double blift = bdx * bdx + bdy * bdy;
            double clift = cdx * cdx + cdy * cdy;

            double det = alift * (bdxcdy - cdxbdy) + blift * ocad + clift * oabd;

            return det > 0;
        }


        public static bool InScanArea(Point2D pa, Point2D pb, Point2D pc, Point2D pd)
        {
            double pdx = pd.X;
            double pdy = pd.Y;
            double adx = pa.X - pdx;
            double ady = pa.Y - pdy;
            double bdx = pb.X - pdx;
            double bdy = pb.Y - pdy;

            double adxbdy = adx * bdy;
            double bdxady = bdx * ady;
            double oabd = adxbdy - bdxady;
            //        oabd = orient2d(pa,pb,pd);
            if (oabd <= 0)
            {
                return false;
            }

            double cdx = pc.X - pdx;
            double cdy = pc.Y - pdy;

            double cdxady = cdx * ady;
            double adxcdy = adx * cdy;
            double ocad = cdxady - adxcdy;
            //      ocad = orient2d(pc,pa,pd);
            if (ocad <= 0)
            {
                return false;
            }
            return true;
        }


        /// Forumla to calculate signed area
        /// Positive if CCW
        /// Negative if CW
        /// 0 if collinear
        /// A[P1,P2,P3]  =  (x1*y2 - y1*x2) + (x2*y3 - y2*x3) + (x3*y1 - y3*x1)
        ///              =  (x1-x3)*(y2-y3) - (y1-y3)*(x2-x3)
        public static Orientation Orient2d(Point2D pa, Point2D pb, Point2D pc)
        {
            double detleft = (pa.X - pc.X) * (pb.Y - pc.Y);
            double detright = (pa.Y - pc.Y) * (pb.X - pc.X);
            double val = detleft - detright;
            if (val > -MathUtil.EPSILON && val < MathUtil.EPSILON)
            {
                return Orientation.Collinear;
            }
            else if (val > 0)
            {
                return Orientation.CCW;
            }
            return Orientation.CW;
        }


        ///////////////////////////////////////////////////////////////////////////////
        // PointRelativeToLine2D
        //
        // Returns -1 if point is on left of line, 0 if point is on line, and 1 if 
        // the point is to the right of the line. This assumes a coordinate system
        // whereby the y axis goes upward when the x axis goes rightward. This is how
        // 3D systems (both right and left-handed) and PostScript works, but is not 
        // how the Win32 GUI works. If you are using a 'y goes downward' coordinate 
        // system, simply negate the return value from this function.
        //
        // Given a point (a,b) and a line from (x1,y1) to (x2,y2), we calculate the 
        // following equation:
        //    (y2-y1)*(a-x1)-(x2-x1)*(b-y1)                        (left)
        // If the result is > 0, the point is on             1 --------------> 2
        // the right, else left.                                   (right)
        //
        // For example, with a point at (1,1) and a 
        // line going from (0,0) to (2,0), we get:
        //    (0-0)*(1-0)-(2-0)*(1-0)
        // which equals:
        //    -2
        // Which indicates the point is (correctly)
        // on the left of the directed line.
        //
        // This function has been checked to a good degree.
        // 
        /////////////////////////////////////////////////////////////////////////////
        //public static double PointRelativeToLine2D(Point2D ptPoint, Point2D ptLineBegin, Point2D ptLineEnd)
        //{
        //    return (ptLineEnd.Y - ptLineBegin.Y) * (ptPoint.X - ptLineBegin.X) - (ptLineEnd.X - ptLineBegin.X) * (ptPoint.Y - ptLineBegin.Y);
        //}


        ///////////////////////////////////////////////////////////////////////////
        // PointInBoundingBox - checks if a point is completely inside an 
        // axis-aligned bounding box defined by xmin, xmax, ymin, and ymax.
        // Note that the point must be fully inside for this method to return
        // true - it cannot lie on the border of the bounding box.
        ///////////////////////////////////////////////////////////////////////////
        public static bool PointInBoundingBox(double xmin, double xmax, double ymin, double ymax, Point2D p)
        {
            return (p.X > xmin && p.X < xmax && p.Y > ymin && p.Y < ymax);
        }


        public static bool PointOnLineSegment2D(Point2D lineStart, Point2D lineEnd, Point2D p, double epsilon)
        {
            return TriangulationUtil.PointOnLineSegment2D(lineStart.X, lineStart.Y, lineEnd.X, lineEnd.Y, p.X, p.Y, epsilon);
        }


        public static bool PointOnLineSegment2D(double x1, double y1, double x2, double y2, double x, double y, double epsilon)
        {
            // First checking if (x, z) is in the range of the line segment's end points.
            if (MathUtil.IsValueBetween(x, x1, x2, epsilon) && MathUtil.IsValueBetween(y, y1, y2, epsilon))
            {
                if (MathUtil.AreValuesEqual(x2 - x1, 0.0f, epsilon))
                {
                    // Vertical line.
                    return true;
                }

                double slope = (y2 - y1) / (x2 - x1);
                double yIntercept = -(slope * x1) + y1;

                // Checking if (x, y) is on the line passing through the end points.
                double t = y - ((slope * x) + yIntercept);

                return MathUtil.AreValuesEqual(t, 0.0f, epsilon);
            }

            return false;
        }

        
        public static bool RectsIntersect(Rect2D r1, Rect2D r2)
        {
            return  (r1.Right > r2.Left) &&
                    (r1.Left < r2.Right) &&
                    (r1.Bottom > r2.Top) &&
                    (r1.Top < r2.Bottom);
        }


        /// <summary>
        /// This method detects if two line segments (or lines) intersect,
        /// and, if so, the point of intersection. Use the <paramref name="firstIsSegment"/> and
        /// <paramref name="secondIsSegment"/> parameters to set whether the intersection point
        /// must be on the first and second line segments. Setting these
        /// both to true means you are doing a line-segment to line-segment
        /// intersection. Setting one of them to true means you are doing a
        /// line to line-segment intersection test, and so on.
        /// Note: If two line segments are coincident, then 
        /// no intersection is detected (there are actually
        /// infinite intersection points).
        /// </summary>
        /// <param name="ptStart0">The first point of the first line segment.</param>
        /// <param name="ptEnd0">The second point of the first line segment.</param>
        /// <param name="ptStart1">The first point of the second line segment.</param>
        /// <param name="ptEnd1">The second point of the second line segment.</param>
        /// <param name="firstIsSegment">Set this to true to require that the 
        /// intersection point be on the first line segment.</param>
        /// <param name="secondIsSegment">Set this to true to require that the
        /// intersection point be on the second line segment.</param>
        /// <param name="coincidentEndPointCollisions">Set this to true to enable collisions if the line segments share
        /// an endpoint</param>
        /// <param name="pIntersectionPt">This is set to the intersection
        /// point if an intersection is detected.</param>
        /// <returns>True if an intersection is detected, false otherwise.</returns>
        public static bool LinesIntersect2D(    Point2D ptStart0, Point2D ptEnd0,
                                                Point2D ptStart1, Point2D ptEnd1,
                                                bool firstIsSegment, bool secondIsSegment, bool coincidentEndPointCollisions,
                                                ref Point2D pIntersectionPt,
                                                double epsilon)
        {
            double d = (ptEnd0.X - ptStart0.X) * (ptStart1.Y - ptEnd1.Y) - (ptStart1.X - ptEnd1.X) * (ptEnd0.Y - ptStart0.Y);
            if (Math.Abs(d) < epsilon)
            {
                //The lines are parallel.
                return false;
            }

            double d0 = (ptStart1.X - ptStart0.X) * (ptStart1.Y - ptEnd1.Y) - (ptStart1.X - ptEnd1.X) * (ptStart1.Y - ptStart0.Y);
            double d1 = (ptEnd0.X - ptStart0.X) * (ptStart1.Y - ptStart0.Y) - (ptStart1.X - ptStart0.X) * (ptEnd0.Y - ptStart0.Y);
            double kOneOverD = 1 / d;
            double t0 = d0 * kOneOverD;
            double t1 = d1 * kOneOverD;

            if ((!firstIsSegment  || ((t0 >= 0.0) && (t0 <= 1.0))) &&
                (!secondIsSegment || ((t1 >= 0.0) && (t1 <= 1.0))) &&
                (coincidentEndPointCollisions || (!MathUtil.AreValuesEqual(0.0, t0, epsilon) && !MathUtil.AreValuesEqual(0.0, t1, epsilon))))
            {
                if (pIntersectionPt != null)
                {
                    pIntersectionPt.X = ptStart0.X + t0 * (ptEnd0.X - ptStart0.X);
                    pIntersectionPt.Y = ptStart0.Y + t0 * (ptEnd0.Y - ptStart0.Y);
                }

                return true;
            }

            return false;
        }


        public static bool LinesIntersect2D(    Point2D ptStart0, Point2D ptEnd0,
                                                Point2D ptStart1, Point2D ptEnd1,
                                                ref Point2D pIntersectionPt,
                                                double epsilon)
        {
            return TriangulationUtil.LinesIntersect2D(ptStart0, ptEnd0, ptStart1, ptEnd1, true, true, false, ref pIntersectionPt, epsilon);
        }

        
        ///////////////////////////////////////////////////////////////////////////
        // RaysIntersect2D
        //
        // Given two lines defined by (sorry about the lame notation):
        //    x0 = x00 + vector_x0*s;
        //    y0 = y00 + vector_y0*s;
        //
        //    x1 = x10 + vector_x1*t;
        //    y1 = y10 + vector_y1*t;
        //
        // This function determines the intersection between them, if there is any.
        //
        // This function assumes the lines to have no endpoints and will intersect
        // them anywhere in 2D space.
        //
        // This algorithm taken from "Realtime-Rendering" section 10.12.
        // 
        // This function has been checked to a good degree.
        // 
        ///////////////////////////////////////////////////////////////////////////
        public static double LI2DDotProduct(Point2D v0, Point2D v1)
        {
            return ((v0.X * v1.X) + (v0.Y * v1.Y));
        }


        public static bool RaysIntersect2D( Point2D ptRayOrigin0, Point2D ptRayVector0,
                                            Point2D ptRayOrigin1, Point2D ptRayVector1,
                                            ref Point2D ptIntersection)
        {
            double kEpsilon = 0.01;

            if (ptIntersection != null)
            {
                //If the user wants an actual intersection result...

                //This is a vector from pLineOrigin0 to ptLineOrigin1.
                Point2D ptTemp1 = new Point2D(ptRayOrigin1.X - ptRayOrigin0.X, ptRayOrigin1.Y - ptRayOrigin0.Y);

                //This is a vector perpendicular to ptVector1.
                Point2D ptTemp2 = new Point2D(-ptRayVector1.Y, ptRayVector1.X);

                double fDot1 = TriangulationUtil.LI2DDotProduct(ptRayVector0, ptTemp2);

                if (Math.Abs(fDot1) < kEpsilon)
                {
                    return false; //The lines are essentially parallel.
                }

                double fDot2 = TriangulationUtil.LI2DDotProduct(ptTemp1, ptTemp2);
                double s = fDot2 / fDot1;
                ptIntersection.X = ptRayOrigin0.X + ptRayVector0.X * s;
                ptIntersection.Y = ptRayOrigin0.Y + ptRayVector0.Y * s;
                return true;
            }

            //Else the user just wants to know if there is an intersection...
            //In this case we need only compare the slopes of the lines.
            double delta = ptRayVector1.X - ptRayVector0.X;
            if (Math.Abs(delta) > kEpsilon)
            {
                delta = ptRayVector1.Y - ptRayVector0.Y;
                if (Math.Abs(delta) > kEpsilon)
                {
                    return true; //The lines are not parallel.
                }
            }

            return false;
        }

    }


}
