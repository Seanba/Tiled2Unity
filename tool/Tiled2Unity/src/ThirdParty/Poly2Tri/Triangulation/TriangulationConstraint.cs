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
/**
 * Forces a triangle edge between two points p and q
 * when triangulating. For example used to enforce
 * Polygon Edges during a polygon triangulation.
 * 
 * @author Thomas Åhlén, thahlen@gmail.com
 */

using System;


namespace Poly2Tri
{
    public class Edge
    {
        protected Point2D mP = null;
        protected Point2D mQ = null;

        public Point2D EdgeStart { get { return mP; } set { mP= value;} }
        public Point2D EdgeEnd { get { return mQ; } set { mQ = value; } }

        public Edge() { mP = null; mQ = null; }
        public Edge(Point2D edgeStart, Point2D edgeEnd)
        {
            mP = edgeStart;
            mQ = edgeEnd;
        }
    }

 
    public class TriangulationConstraint : Edge
    {
        private uint mContraintCode = 0;

        public TriangulationPoint P
        {
            get { return mP as TriangulationPoint; } 
            set 
            {
                // Note:  intentionally use != instead of !Equals() because we
                // WANT to compare pointer values here rather than VertexCode values
                if (value != null && mP != value)
                {
                    mP = value;
                    CalculateContraintCode();
                }
            }
        }
        public TriangulationPoint Q
        {
            get { return mQ as TriangulationPoint; }
            set
            {
                // Note:  intentionally use != instead of !Equals() because we
                // WANT to compare pointer values here rather than VertexCode values
                if (value != null && mQ != value)
                {
                    mQ = value;
                    CalculateContraintCode();
                }
            }
        }
        public uint ConstraintCode { get { return mContraintCode; } }


        /// <summary>
        /// Give two points in any order. Will always be ordered so
        /// that q.y > p.y and q.x > p.x if same y value 
        /// </summary>
        public TriangulationConstraint(TriangulationPoint p1, TriangulationPoint p2)
        {
            mP = p1;
            mQ = p2;
            if (p1.Y > p2.Y)
            {
                mQ = p1;
                mP = p2;
            }
            else if (p1.Y == p2.Y)
            {
                if (p1.X > p2.X)
                {
                    mQ = p1;
                    mP = p2;
                }
                else if (p1.X == p2.X)
                {
                    //                logger.info( "Failed to create constraint {}={}", p1, p2 );
                    //                throw new DuplicatePointException( p1 + "=" + p2 );
                    //                return;
                }
            }
            CalculateContraintCode();
        }


        public override string ToString()
        {
            return "[P=" + P.ToString() + ", Q=" + Q.ToString() + " : {" + mContraintCode.ToString() + "}]";
        }

        
        public void CalculateContraintCode()
        {
            mContraintCode = TriangulationConstraint.CalculateContraintCode(P, Q);
        }


        public static uint CalculateContraintCode(TriangulationPoint p, TriangulationPoint q)
        {
            if (p == null || p == null)
            {
                throw new ArgumentNullException();
            }

            uint constraintCode = MathUtil.Jenkins32Hash(BitConverter.GetBytes(p.VertexCode), 0);
            constraintCode = MathUtil.Jenkins32Hash(BitConverter.GetBytes(q.VertexCode), constraintCode);

            return constraintCode;
        }

    }
}
