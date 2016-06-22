using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // Build a list of convex polygons from a list of triangles
    // This uses the Hertel-Mehlhorn alogorithm to (quickly) generate the convex polygons with no worse than 4x the minimum number.
    // This is generally good enough. Generating a guaranteed minimum set uses dynamic programming with high alogorithmic complexity.
    class ConvexPolygonSet
    {
        // We potentially combine polygons along these edges
        public class SharedPolygonEdge
        {
            public Poly2Tri.Point2D EdgePoint1 { get; set; }
            public Poly2Tri.Point2D EdgePoint2 { get; set; }

            public Poly2Tri.Point2DList PolygonA { get; set; }
            public Poly2Tri.Point2DList PolygonB { get; set; }
        }

        public List<SharedPolygonEdge> PolygonEdges { get; private set; }
        public List<Poly2Tri.Point2DList> Polygons { get; private set; }


        public ConvexPolygonSet()
        {
            this.PolygonEdges = new List<SharedPolygonEdge>();
            this.Polygons = new List<Poly2Tri.Point2DList>();
        }

        public void MakeConvextSetFromClipperSolution(ClipperLib.PolyTree solution)
        {
            var triangles = GetTriangleListFromClipperSolution(solution);
            MakeConvexSetFromTriangles(triangles);
        }

        public void MakeConvexSetFromTriangles(IEnumerable<Poly2Tri.DelaunayTriangle> triangles)
        {
            PreparePolygons(triangles);
            CombinePolygons();
        }

        private List<Poly2Tri.DelaunayTriangle> GetTriangleListFromClipperSolution(ClipperLib.PolyTree solution)
        {
            Func<ClipperLib.IntPoint, Poly2Tri.PolygonPoint> xfToPolygonPoint = (p) => new Poly2Tri.PolygonPoint(p.X, p.Y);
            Poly2Tri.PolygonSet polygonSet = new Poly2Tri.PolygonSet();

            ClipperLib.PolyNode node = solution.GetFirst();
            while (node != null)
            {
                // Only interested in closed paths
                if (!node.IsOpen)
                {
                    if (node.IsHole)
                    {
                        if (polygonSet.Polygons.Count() > 0)
                        {
                            // Add hole to last polygon entered
                            var polyPoints = node.Contour.Select(xfToPolygonPoint).ToArray();
                            Poly2Tri.Polygon hole = new Poly2Tri.Polygon(polyPoints);

                            Poly2Tri.Polygon polygon = polygonSet.Polygons.Last();
                            polygon.AddHole(hole);
                        }
                    }
                    else
                    {
                        // Add a new polygon to the set
                        var polyPoints = node.Contour.Select(xfToPolygonPoint).ToList();
                        Poly2Tri.Polygon polygon = new Poly2Tri.Polygon(polyPoints);
                        polygonSet.Add(polygon);
                    }
                }
                node = node.GetNext();
            }

            // Now triangulate the whole set
            Poly2Tri.P2T.Triangulate(polygonSet);

            // Combine all the triangles into one list
            List<Poly2Tri.DelaunayTriangle> triangles = new List<Poly2Tri.DelaunayTriangle>();
            foreach (var polygon in polygonSet.Polygons)
            {
                triangles.AddRange(polygon.Triangles);
            }

            return triangles;
        }

        private void CombinePolygons()
        {
            // Test each edge between polygons and see if it can be removed
            for (int i = 0; i < this.PolygonEdges.Count; ++i)
            {
                if (CanRemoveEdge(i))
                {
                    CombinePolygonsAlongEdge(i);
                }
            }
        }

        private void CombinePolygonsAlongEdge(int index)
        {
            // We are going to combine all the points from polygon B into polygon A
            var edge = this.PolygonEdges[index];

            // Gather a list of all the points from polygon B to be added
            // This is all the points from polygon B except for the shared edge
            List<Poly2Tri.Point2D> pointsToAdd = new List<Poly2Tri.Point2D>();
            {
                int edgeStartOnBIndex = edge.PolygonB.IndexOf(edge.EdgePoint1);
                int edgeStopOnBIndex = edge.PolygonB.IndexOf(edge.EdgePoint2);

                int indexToAdd = edge.PolygonB.NextIndex(edgeStartOnBIndex);
                while (indexToAdd != edgeStopOnBIndex)
                {
                    Poly2Tri.Point2D point = edge.PolygonB[indexToAdd];
                    pointsToAdd.Add(point);

                    indexToAdd = edge.PolygonB.NextIndex(indexToAdd);
                }
            }

            // Insert the points to add between the edge points on polygon A
            List<Poly2Tri.Point2D> newPolygonAPoints = edge.PolygonA.ToList();
            {
                int edgeStartOnAIndex = edge.PolygonA.IndexOf(edge.EdgePoint1);
                int nextIndexA = edge.PolygonA.NextIndex(edgeStartOnAIndex);
                newPolygonAPoints.InsertRange(nextIndexA, pointsToAdd);
            }

            // Create a new polygon A
            Poly2Tri.Point2DList newPolygonA = new Poly2Tri.Point2DList(newPolygonAPoints);
            this.Polygons.Add(newPolygonA);

            // Any furter edges that had PolygonA or PolygonB in it must be updated
            for (int i = index + 1; i < this.PolygonEdges.Count; ++i)
            {
                var furtherEdge = this.PolygonEdges[i];

                if (furtherEdge.PolygonA == edge.PolygonA)
                {
                    furtherEdge.PolygonA = newPolygonA;
                }
                else if (furtherEdge.PolygonA == edge.PolygonB)
                {
                    furtherEdge.PolygonA = newPolygonA;
                }

                if (furtherEdge.PolygonB == edge.PolygonA)
                {
                    furtherEdge.PolygonB = newPolygonA;
                }
                else if (furtherEdge.PolygonB == edge.PolygonB)
                {
                    furtherEdge.PolygonB = newPolygonA;
                }
            }

            // Old PolygonA and PolygonB are removed from the list of polygons
            this.Polygons.Remove(edge.PolygonA);
            this.Polygons.Remove(edge.PolygonB);
        }

        private bool CanRemoveEdge(int index)
        {
            var edge = this.PolygonEdges[index];

            // (Assumes CCW list in polygons)
            // (Assumes the edge is CCW along polygon A)
            // In order to be able to remove an edge and (eventually) combine the polygons together two corners where the polygons match be less than 180 degress

            // CornerA: (P1 - A.prev) x (B.next - P1)
            {
                int indexA = edge.PolygonA.IndexOf(edge.EdgePoint1);
                int indexPrevA = edge.PolygonA.PreviousIndex(indexA);

                int indexB = edge.PolygonB.IndexOf(edge.EdgePoint1);
                int indexNextB = edge.PolygonB.NextIndex(indexB);

                Poly2Tri.Point2D prevA = edge.PolygonA[indexPrevA];
                Poly2Tri.Point2D nextB = edge.PolygonB[indexNextB];
                Poly2Tri.Point2D line1 = edge.EdgePoint1 - prevA;
                Poly2Tri.Point2D line2 = nextB - edge.EdgePoint1;

                double cross = Poly2Tri.Point2D.Cross(line1, line2);
                if (cross < 0)
                    return false;
            }

            // CornerB: (A.next - P2) x (P2 - B.prev)
            {
                int indexA = edge.PolygonA.IndexOf(edge.EdgePoint2);
                int indexNextA = edge.PolygonA.NextIndex(indexA);

                int indexB = edge.PolygonB.IndexOf(edge.EdgePoint2);
                int indexPrevB = edge.PolygonB.PreviousIndex(indexB);

                Poly2Tri.Point2D nextA = edge.PolygonA[indexNextA];
                Poly2Tri.Point2D prevB = edge.PolygonB[indexPrevB];
                Poly2Tri.Point2D line1 = nextA - edge.EdgePoint2;
                Poly2Tri.Point2D line2 = edge.EdgePoint2 - prevB;

                double cross = Poly2Tri.Point2D.Cross(line1, line2);
                if (cross > 0)
                    return false;
            }
            return true;
        }

        private void CombingPolygonsAlongEdge(int index)
        {
            var edge = this.PolygonEdges[index];
        }

        private void PreparePolygons(IEnumerable<Poly2Tri.DelaunayTriangle> triangles)
        {
            this.PolygonEdges.Clear();
            this.Polygons.Clear();

            // For building edges we need a mapping of the original triangles to polygons
            Dictionary<Poly2Tri.DelaunayTriangle, Poly2Tri.Point2DList> triangleToPolygon = new Dictionary<Poly2Tri.DelaunayTriangle, Poly2Tri.Point2DList>();
            Dictionary<Poly2Tri.Point2DList, Poly2Tri.DelaunayTriangle> polygonToTriangle = new Dictionary<Poly2Tri.Point2DList, Poly2Tri.DelaunayTriangle>();

            // Initially, our polygons are simply the triangles in polygon form
            foreach (var triangle in triangles)
            {
                var polygonPoints = triangle.Points.Select(p => new Poly2Tri.Point2D(p.X, p.Y)).ToList();
                Poly2Tri.Point2DList polygon = new Poly2Tri.Point2DList(polygonPoints);

                triangleToPolygon[triangle] = polygon;
                polygonToTriangle[polygon] = triangle;

                this.Polygons.Add(polygon);
            }

            // Build up the edge list
            foreach (var polygon in this.Polygons)
            {
                // Does this polygon have any neighbors? If so, that's how we make our edges.
                var triangle = polygonToTriangle[polygon];

                // We can have up to 3 neighbors on each triangle
                for (int n = 0; n < 3; ++n)
                {
                    var neighborTriangle = triangle.Neighbors[n];
                    if (neighborTriangle == null)
                        continue;

                    if (!triangleToPolygon.ContainsKey(neighborTriangle))
                        continue;

                    var neighborPolygon = triangleToPolygon[neighborTriangle];

                    // If the neighbor polygon still has a triangle associated with it then we haven't added its edges yet
                    // Otherwise, we have added her edges and we don't want to do so again
                    if (!polygonToTriangle.ContainsKey(neighborPolygon))
                        continue;

                    // Gather the points needed for the edge
                    Poly2Tri.TriangulationPoint triPoint1 = triangle.Points[(n + 1) % 3];
                    Poly2Tri.TriangulationPoint triPoint2 = triangle.Points[(n + 2) % 3];

                    // Create an polygon edge
                    SharedPolygonEdge edge = new SharedPolygonEdge();
                    edge.PolygonA = polygon;
                    edge.PolygonB = neighborPolygon;
                    edge.EdgePoint1 = new Poly2Tri.PolygonPoint(triPoint1.X, triPoint1.Y);
                    edge.EdgePoint2 = new Poly2Tri.PolygonPoint(triPoint2.X, triPoint2.Y);
                    this.PolygonEdges.Add(edge);
                }

                // Remove the polygon from the triangle mapping. We are done with it.
                polygonToTriangle.Remove(polygon);
            }
        }

    }
}
