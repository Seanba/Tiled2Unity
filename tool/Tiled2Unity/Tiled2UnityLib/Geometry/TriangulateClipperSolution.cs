using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;


namespace Tiled2Unity.Geometry
{
    // Input is a ClipperLib solution and output is a collection of triangles
    public class TriangulateClipperSolution
    {
        public List<PointF[]> Triangulate(ClipperLib.PolyTree solution)
        {
            List<PointF[]> triangles = new List<PointF[]>();

            var tess = new LibTessDotNet.Tess();
            tess.NoEmptyPolygons = true;

            // Transformation function from ClipperLip Point to LibTess contour vertex
            Func<ClipperLib.IntPoint, LibTessDotNet.ContourVertex> xfToContourVertex = (p) => new LibTessDotNet.ContourVertex() { Position = new LibTessDotNet.Vec3 { X = p.X, Y = p.Y, Z = 0 } };

            // Add a contour for each part of the solution tree
            ClipperLib.PolyNode node = solution.GetFirst();
            while (node != null)
            {
                // Only interested in closed paths
                if (!node.IsOpen)
                {
                    // Add a new countor. Holes are automatically generated.
                    var vertices = node.Contour.Select(xfToContourVertex).ToArray();
                    tess.AddContour(vertices);
                }
                node = node.GetNext();
            }

            // Do the tessellation
            tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3);

            // Extract the triangles
            int numTriangles = tess.ElementCount;
            for (int i = 0; i < numTriangles; i++)
            {
                var v0 = tess.Vertices[tess.Elements[i * 3 + 0]].Position;
                var v1 = tess.Vertices[tess.Elements[i * 3 + 1]].Position;
                var v2 = tess.Vertices[tess.Elements[i * 3 + 2]].Position;

                List<PointF> triangle = new List<PointF>()
                {
                    new PointF(v0.X, v0.Y),
                    new PointF(v1.X, v1.Y),
                    new PointF(v2.X, v2.Y),
                };

                // Assume each triangle needs to be CCW
                float cross = Geometry.Math.Cross(triangle[0], triangle[1], triangle[2]);
                if (cross > 0)
                {
                    triangle.Reverse();
                }

                triangles.Add(triangle.ToArray());
            }

            return triangles;
        }

    }
}
