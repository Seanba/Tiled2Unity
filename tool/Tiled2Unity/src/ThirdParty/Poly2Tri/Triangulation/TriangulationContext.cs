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
    public abstract class TriangulationContext
    {
        public TriangulationDebugContext DebugContext { get; protected set; }

        public readonly List<DelaunayTriangle> Triangles = new List<DelaunayTriangle>();
        public readonly List<TriangulationPoint> Points = new List<TriangulationPoint>(200);
        public TriangulationMode TriangulationMode { get; protected set; }
        public ITriangulatable Triangulatable { get; private set; }

        public int StepCount { get; private set; }

        public void Done()
        {
            StepCount++;
        }

        public abstract TriangulationAlgorithm Algorithm { get; }


        public virtual void PrepareTriangulation(ITriangulatable t)
        {
            Triangulatable = t;
            TriangulationMode = t.TriangulationMode;
            t.Prepare(this);

            //List<TriangulationConstraint> constraints = new List<TriangulationConstraint>();

            //Console.WriteLine("Points for " + t.FileName + ":");
            //Console.WriteLine("Idx,X,Y,VC,Edges");
            //int numPoints = Points.Count;
            //for (int i = 0; i < numPoints; ++i)
            //{
            //    StringBuilder sb = new StringBuilder(128);
            //    sb.Append(i.ToString());
            //    sb.Append(",");
            //    sb.Append(Points[i].X.ToString());
            //    sb.Append(",");
            //    sb.Append(Points[i].Y.ToString());
            //    sb.Append(",");
            //    sb.Append(Points[i].VertexCode.ToString());
            //    int numEdges = (Points[i].Edges != null) ? Points[i].Edges.Count : 0;
            //    for (int j = 0; j < numEdges; ++j)
            //    {
            //        TriangulationConstraint tc = Points[i].Edges[j];
            //        sb.Append(",");
            //        sb.Append(tc.ConstraintCode.ToString());
            //        constraints.Add(tc);
            //    }
            //    Console.WriteLine(sb.ToString());
            //}

            //int idx = 0;
            //Console.WriteLine("Constraints " + t.FileName + ":");
            //Console.WriteLine("EdgeIdx,Px,Py,PVC,Qx,Qy,QVC,ConstraintCode,Owner");
            //foreach (TriangulationConstraint tc in constraints)
            //{
            //    StringBuilder sb = new StringBuilder(128);

            //    sb.Append(idx.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.P.X.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.P.Y.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.P.VertexCode.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.Q.X.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.Q.Y.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.Q.VertexCode.ToString());
            //    sb.Append(",");
            //    sb.Append(tc.ConstraintCode.ToString());
            //    sb.Append(",");
            //    if (tc.Q.HasEdge(tc.P))
            //    {
            //        sb.Append("Q");
            //    }
            //    else
            //    {
            //        sb.Append("P");
            //    }
            //    Console.WriteLine(sb.ToString());

            //    ++idx;
            //}
        }


        public abstract TriangulationConstraint NewConstraint(TriangulationPoint a, TriangulationPoint b);


        public void Update(string message) { }


        public virtual void Clear()
        {
            Points.Clear();
            if (DebugContext != null)
            {
                DebugContext.Clear();
            }
            StepCount = 0;
        }


        public virtual bool IsDebugEnabled { get; protected set; }

        public DTSweepDebugContext DTDebugContext { get { return DebugContext as DTSweepDebugContext; } }
    }
}
