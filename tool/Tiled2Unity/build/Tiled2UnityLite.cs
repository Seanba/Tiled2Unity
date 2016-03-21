// Tiled2UnityLite is automatically generated. Do not modify by hand.
// version 1.0.4.4

//css_reference System;
//css_reference System.Core;
//css_reference System.Xml.Linq;
//css_reference System.Data.DataSetExtensions;
//css_reference System.Data;
//css_reference System.Drawing;
//css_reference System.Xml;

#define TILED_2_UNITY_LITE
#define use_lines

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;


namespace Tiled2Unity
{
    static partial class Program
    {
        public static string GetVersion()
        {
            return "1.0.4.4";
        }
    }
}


// ----------------------------------------------------------------------
// ChDir.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public class ChDir : IDisposable
    {
        private string directoryOld;
        private string directoryNow;

        public ChDir(string path)
        {
            this.directoryOld = Directory.GetCurrentDirectory();
            if (Directory.Exists(path))
                this.directoryNow = path;
            else if (File.Exists(path))
                this.directoryNow = Path.GetDirectoryName(path);
            else
                throw new DirectoryNotFoundException(String.Format("Cannot set current directory. Does not exist: {0}", path));

            Directory.SetCurrentDirectory(this.directoryNow);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(this.directoryOld);
        }
    }
}

// ----------------------------------------------------------------------
// ConvexPolygonSet.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

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

// ----------------------------------------------------------------------
// HashIndexOf.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // Generic collection class that gives us O(1) insertion with distinct values and O(1) IndexOf
    public class HashIndexOf<T>
    {
        private Dictionary<T, int> dictionary = new Dictionary<T, int>();

        public List<T> List { get; private set; }

        public HashIndexOf()
        {
            this.List = new List<T>();
        }

        public int Add(T value)
        {
            if (this.dictionary.ContainsKey(value))
            {
                return this.dictionary[value];
            }
            else
            {
                int index = this.dictionary.Count;
                this.List.Add(value);
                this.dictionary[value] = index;
                return index;
            }
        }

        public int IndexOf(T value)
        {
            return this.dictionary[value];
        }
    }
}

// ----------------------------------------------------------------------
// LayerClipper.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

// Given a TmxMap and TmxLayer, crank out a Clipper polytree solution
namespace Tiled2Unity
{
    using ClipperPolygon = List<ClipperLib.IntPoint>;
    using ClipperPolygons = List<List<ClipperLib.IntPoint>>;

    class LayerClipper
    {
        // Break the map into smaller pieces to feed to Clipper
        private static readonly int GroupBySize = 10;

        // Note: Will need to work with this. We need Even Odd fill rules right now because winding order on polygons is not deterministic
        private static ClipperLib.PolyFillType SubjectFillRule = ClipperLib.PolyFillType.pftNonZero;
        private static ClipperLib.PolyFillType ClipFillRule = ClipperLib.PolyFillType.pftEvenOdd;

        // Need a method to transform points into our coordinate system (different between Windows and Unity)
        public delegate ClipperLib.IntPoint TransformPointFunc(float x, float y);
        public delegate void ProgressFunc(string progress);

        public static ClipperLib.PolyTree ExecuteClipper(TmxMap tmxMap, TmxLayer tmxLayer, TransformPointFunc xfFunc, ProgressFunc progFunc)
        {
            // The "fullClipper" combines the clipper results from the smaller pieces
            ClipperLib.Clipper fullClipper = new ClipperLib.Clipper();

            // From the perspective of Clipper lines are polygons too
            // Closed paths == polygons
            // Open paths == lines
            var polygonGroups = from y in Enumerable.Range(0, tmxLayer.Height)
                                from x in Enumerable.Range(0, tmxLayer.Width)
                                let rawTileId = tmxLayer.GetRawTileIdAt(x, y)
                                let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                where tileId != 0
                                let tile = tmxMap.Tiles[tileId]
                                from polygon in tile.ObjectGroup.Objects
                                where (polygon as TmxHasPoints) != null
                                let groupX = x / LayerClipper.GroupBySize
                                let groupY = y / LayerClipper.GroupBySize
                                group new
                                {
                                    PositionOnMap = tmxMap.GetMapPositionAt(x, y, tile),
                                    HasPointsInterface = polygon as TmxHasPoints,
                                    TmxObjectInterface = polygon,
                                    IsFlippedDiagnoally = TmxMath.IsTileFlippedDiagonally(rawTileId),
                                    IsFlippedHorizontally = TmxMath.IsTileFlippedHorizontally(rawTileId),
                                    IsFlippedVertically = TmxMath.IsTileFlippedVertically(rawTileId),
                                    TileCenter = new PointF(tile.TileSize.Width * 0.5f, tile.TileSize.Height * 0.5f),
                                }
                                by Tuple.Create(groupX, groupY);

            int groupIndex = 0;
            int groupCount = polygonGroups.Count();

            foreach (var polyGroup in polygonGroups)
            {
                if (groupIndex % 5 == 0)
                {
                    progFunc(String.Format("Clipping '{0}' polygons: {1}%", tmxLayer.Name, (groupIndex / (float)groupCount) * 100));
                }
                groupIndex++;

                // The "groupClipper" clips the polygons in a smaller part of the world
                ClipperLib.Clipper groupClipper = new ClipperLib.Clipper();

                // Add all our polygons to the Clipper library so it can reduce all the polygons to a (hopefully small) number of paths
                foreach (var poly in polyGroup)
                {
                    // Create a clipper library polygon out of each and add it to our collection
                    ClipperPolygon clipperPolygon = new ClipperPolygon();

                    // Our points may be transformed due to tile flipping/rotation
                    // Before we transform them we put all the points into local space relative to the tile
                    SizeF offset = new SizeF(poly.TmxObjectInterface.Position);
                    PointF[] transformedPoints = poly.HasPointsInterface.Points.Select(pt => PointF.Add(pt, offset)).ToArray();

                    // Now transform the points relative to the tile
                    TmxMath.TransformPoints(transformedPoints, poly.TileCenter, poly.IsFlippedDiagnoally, poly.IsFlippedHorizontally, poly.IsFlippedVertically);

                    foreach (var pt in transformedPoints)
                    {
                        float x = poly.PositionOnMap.X + pt.X;
                        float y = poly.PositionOnMap.Y + pt.Y;

                        ClipperLib.IntPoint point = xfFunc(x, y);
                        clipperPolygon.Add(point);
                    }

                    // Because of Unity's cooridnate system, the winding order of the polygons must be reversed
                    clipperPolygon.Reverse();

                    // Add the "subject"
                    groupClipper.AddPath(clipperPolygon, ClipperLib.PolyType.ptSubject, poly.HasPointsInterface.ArePointsClosed());
                }

                // Get a solution for this group
                ClipperLib.PolyTree solution = new ClipperLib.PolyTree();
                groupClipper.Execute(ClipperLib.ClipType.ctUnion, solution, LayerClipper.SubjectFillRule, LayerClipper.ClipFillRule);

                // Combine the solutions into the full clipper
                fullClipper.AddPaths(ClipperLib.Clipper.ClosedPathsFromPolyTree(solution), ClipperLib.PolyType.ptSubject, true);
                fullClipper.AddPaths(ClipperLib.Clipper.OpenPathsFromPolyTree(solution), ClipperLib.PolyType.ptSubject, false);
            }
            progFunc(String.Format("Clipping '{0}' polygons: 100%", tmxLayer.Name));

            ClipperLib.PolyTree fullSolution = new ClipperLib.PolyTree();
            fullClipper.Execute(ClipperLib.ClipType.ctUnion, fullSolution, LayerClipper.SubjectFillRule, LayerClipper.ClipFillRule);

            return fullSolution;
        }

        // Put the closed path polygons into an enumerable collection of an array of points.
        // Each array of points in a path in a "complex" polygon that supports convace edges and holes
        public static IEnumerable<PointF[]> SolutionPolygons_Complex(ClipperLib.PolyTree solution)
        {
            foreach (var points in ClipperLib.Clipper.ClosedPathsFromPolyTree(solution))
            {
                var pointfs = points.Select(pt => new PointF(pt.X, pt.Y));
                yield return pointfs.ToArray();
            }
        }

        // Put the closed path polygons into an enumerable collection of an array of points.
        // Each array of points in a separate convex polygon
        public static IEnumerable<PointF[]> SolutionPolygons_Simple(ClipperLib.PolyTree solution)
        {
            ConvexPolygonSet convexPolygonSet = new ConvexPolygonSet();
            convexPolygonSet.MakeConvextSetFromClipperSolution(solution);

            foreach (var polygon in convexPolygonSet.Polygons)
            {
                var pointfs = polygon.Select(pt => new PointF(pt.Xf, pt.Yf));
                yield return pointfs.ToArray();
            }
        }

    }
}

// ----------------------------------------------------------------------
// PolylineReduction.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;


namespace Tiled2Unity
{
    // Join line segments into polylines
    class PolylineReduction
    {
        private static int CurrentPolylineId = 0;

        // Cheap internal class for grouping similar polyines (that differ only in direction) by an assinged Id
        public class InternalPolyline
        {
            public int Id;
            public List<ClipperLib.IntPoint> Points = new List<ClipperLib.IntPoint>();
        }

        // Hash polylines by their endpoints so we can combine them
        SD.Tools.Algorithmia.GeneralDataStructures.MultiValueDictionary<ClipperLib.IntPoint, InternalPolyline> tablePolyline = new SD.Tools.Algorithmia.GeneralDataStructures.MultiValueDictionary<ClipperLib.IntPoint, InternalPolyline>();

        public void AddLine(List<ClipperLib.IntPoint> points)
        {
            PolylineReduction.CurrentPolylineId++;

            // Get rid of mid-points along the line that are not needed
            points = RemovePointsOnLine(points);

            // Always add the polyline forward
            InternalPolyline forwards = new InternalPolyline();
            forwards.Id = PolylineReduction.CurrentPolylineId;
            forwards.Points.AddRange(points);

            this.tablePolyline.Add(forwards.Points.Last(), forwards);

            // Add the polyline backwards too if the end-points are different
            // Make sure the Id is the same though
            if (points.First() != points.Last())
            {
                InternalPolyline backwards = new InternalPolyline();
                backwards.Id = PolylineReduction.CurrentPolylineId;
                backwards.Points.AddRange(points);
                backwards.Points.Reverse();

                this.tablePolyline.Add(backwards.Points.Last(), backwards);
            }
        }

        private bool AreNormalsEquivalent(ClipperLib.DoublePoint n0, ClipperLib.DoublePoint n1)
        {
            const double epsilon = 1.0f / 1024.0f;
            double ax = Math.Abs(n0.X - n1.X);
            double ay = Math.Abs(n0.Y - n1.Y);
            return (ax < epsilon) && (ay < epsilon);
        }

        private List<ClipperLib.IntPoint> RemovePointsOnLine(List<ClipperLib.IntPoint> points)
        {
            int index = 0;
            while (index < points.Count - 2)
            {
                ClipperLib.DoublePoint normal0 = ClipperLib.ClipperOffset.GetUnitNormal(points[index], points[index + 1]);
                ClipperLib.DoublePoint normal1 = ClipperLib.ClipperOffset.GetUnitNormal(points[index], points[index + 2]);

                if (AreNormalsEquivalent(normal0, normal1))
                {
                    points.RemoveAt(index + 1);
                }
                else
                {
                    index++;
                }
            }

            return points;
        }

        private void CombinePolyline(InternalPolyline line0, InternalPolyline line1)
        {
            // Assumes Line0 and Line1 have the same end-points
            // We reverse Line1 and remove its first end-point
            List<ClipperLib.IntPoint> combined = new List<ClipperLib.IntPoint>();
            combined.AddRange(line0.Points);

            line1.Points.Reverse();
            line1.Points.RemoveAt(0);
            combined.AddRange(line1.Points);

            AddLine(combined);
        }

        private void RemovePolyline(InternalPolyline polyline)
        {
            var removes = from pairs in this.tablePolyline
                          from line in pairs.Value
                          where line.Id == polyline.Id
                          select line;

            var removeList = removes.ToList();
            foreach (var rem in removeList)
            {
                this.tablePolyline.Remove(rem.Points.Last(), rem);
            }
        }

        // Returns a list of polylines (each polyine is itself a list of points)
        public List<List<ClipperLib.IntPoint>> Reduce()
        {
            // Combine all the polylines together
            // We should end up with a table of polylines where each key has only one entry
            var set = this.tablePolyline.FirstOrDefault(kvp => kvp.Value.Count > 1);
            while (set.Value != null)
            {
                // The set is guaranteed to have at least two polylines in it
                // Combine the first and reverse-second polylines into a bigger polyline
                // Remove both polylines from the table
                // Add the combined polyline
                var polylines = set.Value.ToList();
                InternalPolyline line0 = polylines[0];
                InternalPolyline line1 = polylines[1];

                RemovePolyline(line0);
                RemovePolyline(line1);
                CombinePolyline(line0, line1);

                // Look for the next group of polylines that share an endpoint
                set = this.tablePolyline.FirstOrDefault(kvp => kvp.Value.Count > 1);
            }

            // The resulting lines will be in the table twice so make the list unique on Polyline Id
            var unique = from pairs in this.tablePolyline
                        from line in pairs.Value
                        select line;
            unique = unique.GroupBy(ln => ln.Id).Select(grp => grp.First());

            var lines = from l in unique
                        select l.Points;

            return lines.ToList();
        }

    }
}

// ----------------------------------------------------------------------
// Program.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Linq;
// using System.Reflection;
// using System.Windows.Forms;


namespace Tiled2Unity
{
    static partial class Program
    {
        public delegate void WriteLineDelegate(string line);
        public static event WriteLineDelegate OnWriteLine;

        public delegate void WriteWarningDelegate(string line);
        public static event WriteWarningDelegate OnWriteWarning;

        public delegate void WriteErrorDelegate(string line);
        public static event WriteErrorDelegate OnWriteError;

        public delegate void WriteSuccessDelegate(string line);
        public static event WriteSuccessDelegate OnWriteSuccess;

        public delegate void WriteVerboseDelegate(string line);
        public static event WriteVerboseDelegate OnWriteVerbose;

        static private readonly float DefaultTexelBias = 8192.0f;

#if !TILED_2_UNITY_LITE
        // AutoExport is redundant in Tiled2UnityLite
        static public bool AutoExport { get; private set; }
#endif
        static public float Scale { get; set; }
        static public bool PreferConvexPolygons { get; set; }
        static public float TexelBias { get; private set; }
        static public bool Verbose { get; private set; }
        static public bool Help { get; private set; }

        static public string TmxPath { get; private set; }
        static public string ExportUnityProjectDir { get; private set; }

        static public string LogFilePath { get; private set; }

        static private NDesk.Options.OptionSet Options = new NDesk.Options.OptionSet()
            {
#if !TILED_2_UNITY_LITE
                { "a|auto-export", "Automatically export to UNITYDIR and close.", ae => Program.AutoExport = true },
#endif
                { "s|scale=", "Scale the output vertices by a value.\nA value of 0.01 is popular for many Unity projects that use 'Pixels Per Unit' of 100 for sprites.\nDefault is 1 (no scaling).", s => Program.Scale = ParseFloatDefault(s, 1.0f) },
                { "c|convex", "Limit polygon colliders to be convex with no holes. Increases the number of polygon colliders in export. Can be overriden on map or layer basis with unity:convex property.", c => Program.PreferConvexPolygons = true },
                { "t|texel-bias=", "Bias for texel sampling.\nTexels are offset by 1 / value.\nDefault value is 8192.\n A value of 0 means no bias.", t => Program.TexelBias = ParseFloatDefault(t, DefaultTexelBias) },
                { "v|verbose", "Print verbose messages.", v => Program.Verbose = true },
                { "h|help", "Display this help message.", h => Program.Help = true },
            };

#if TILED_2_UNITY_LITE

        // Scripting main
        static void Main(string[] args)
        {
            SetCulture();

            // Listen to any success, warning, and error messages. Give a report when finished.
            List<string> errors = new List<string>();
            Action<string> funcError = delegate(string line)
            {
                errors.Add(line);
            };

            List<string> warnings = new List<string>();
            Action<string> funcWaring = delegate(string line)
            {
                warnings.Add(line);
            };

            List<string> successes = new List<string>();
            Action<string> funcSuccess = delegate(string line)
            {
                successes.Add(line);
            };

            // Temporarily capture output while exporting
            Program.OnWriteError += new Program.WriteErrorDelegate(funcError);
            Program.OnWriteWarning += new Program.WriteWarningDelegate(funcWaring);
            Program.OnWriteSuccess += new Program.WriteSuccessDelegate(funcSuccess);

            // Default options
            Program.Scale = 1.0f;
            Program.PreferConvexPolygons = false;
            Program.TexelBias = DefaultTexelBias;
            Program.Verbose = false;
            Program.Help = false;
            Program.TmxPath = "";
            Program.ExportUnityProjectDir = "";

            bool success = ParseOptions(args);

            if (success && !Program.Help)
            {
                if (String.IsNullOrEmpty(Program.ExportUnityProjectDir))
                {
                    Console.Error.WriteLine("UNITYDIR is missing!");
                    PrintHelp();
                    return;
                }

                // We should have everyting we need to export a TMX file to a Unity project
                TmxMap tmxMap = TmxMap.LoadFromFile(Program.TmxPath);
                TiledMapExporter tiledMapExporter = new TiledMapExporter(tmxMap);
                tiledMapExporter.Export(Program.ExportUnityProjectDir);

                // Write a summary that repeats warnings and errors
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("Export completed");
                foreach (string msg in successes)
                {
                    Console.WriteLine(msg);
                }

                Console.WriteLine("Warnings: {0}", warnings.Count);
                foreach (string warning in warnings)
                {
                    Console.WriteLine(warning);
                }
            
                Console.Error.WriteLine("Errors: {0}\n", errors.Count);
                foreach (string error in errors)
                {
                    Console.WriteLine(error);
                }
                Console.WriteLine("----------------------------------------");
            }
        }
#else
        // Windows exe main
        [STAThread]
        static void Main(string[] args)
        {
            SetCulture();

            // Default options
            Program.AutoExport = false;
            Program.Scale = -1.0f;
            Program.PreferConvexPolygons = false;
            Program.TexelBias = DefaultTexelBias;
            Program.Verbose = false;
            Program.Help = false;
            Program.TmxPath = "";
            Program.ExportUnityProjectDir = "";

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (Tiled2UnityForm form = new Tiled2UnityForm(args))
            {
                StartLogging(args);
                Application.Run(form);
            }
        }
#endif

        public static bool ParseOptions(string[] args)
        {
            // Parse the options
            List<string> extra = Program.Options.Parse(args);

            // Did we ask for help?
            if (Program.Help)
            {
                Program.PrintHelp();
                return true;
            }

            // If we didn''t overide scale then use the old value
#if !TILED_2_UNITY_LITE
            if (Program.Scale <= 0.0f)
            {
                if (Properties.Settings.Default.LastVertexScale > 0)
                {
                    Program.Scale = Properties.Settings.Default.LastVertexScale;
                }
                else
                {
                    Program.Scale = 1.0f;
                }
            }
            else
            {
                // Save our new value
                Properties.Settings.Default.LastVertexScale = Program.Scale;
                Properties.Settings.Default.Save();
            }
#endif
           // If we didn't set convex polygons as default then use old value
#if !TILED_2_UNITY_LITE
            if (Program.PreferConvexPolygons == false)
            {
                Program.PreferConvexPolygons = Properties.Settings.Default.LastPreferConvexPolygons;
            }
            Properties.Settings.Default.LastPreferConvexPolygons = Program.PreferConvexPolygons;
            Properties.Settings.Default.Save();
#endif

            // First left over option is the TMX file we are exporting
            if (extra.Count() == 0)
            {
                Program.WriteLine("Missing TMXPATH argument.");
                Program.WriteLine("  If using the GUI, try opening a TMX file now");
                Program.WriteLine("  If using the command line, provide a path to a TMX file");
                Program.WriteLine("  If using from Tiled Map Editor, try adding %mapfile to the command");
                PrintHelp();
                return false;
            }
            else
            {
                Program.TmxPath = Path.GetFullPath(extra[0]);

                if (!File.Exists(Program.TmxPath))
                {
                    Program.WriteError("TMXPATH file '{0}' does not exist.", Program.TmxPath);
                    Program.TmxPath = null;
                    PrintHelp();
                    return false;
                }

                extra.RemoveAt(0);
            }

            // The next 'left over' option is the Tiled2Unity folder of the Unity project that we are exporting to
            if (extra.Count() > 0)
            {
                Program.ExportUnityProjectDir = Path.GetFullPath(extra[0]);

                if (!Directory.Exists(Program.ExportUnityProjectDir))
                {
                    Program.WriteError("UNITYDIR Unity Tiled2Unity Project Directory '{0}' does not exist", Program.ExportUnityProjectDir);
                    Program.ExportUnityProjectDir = null;
                    PrintHelp();
                    return false;
                }
                if (!File.Exists(Path.Combine(Program.ExportUnityProjectDir, "Tiled2Unity.export.txt")))
                {
                    Program.WriteError("UNITYDIR '{0}' is not a Tiled2Unity Unity Project folder", Program.ExportUnityProjectDir);
                    Program.ExportUnityProjectDir = null;
                    PrintHelp();
                    return false;
                }

                extra.RemoveAt(0);
            }
#if !TILED_2_UNITY_LITE
            else if (Program.AutoExport)
            {
                // If we are auto-exporting then this arugment *must* be present (and it isn't so bail)
                Program.WriteError("Auto-exporting is enabled but UNITYDIR is missing");
                PrintHelp();
                return false;
            }
#endif

            // Do we have any other options left over? We shouldn't.
            if (extra.Count() > 0)
            {
                Program.WriteError("Too many arguments. Can't parse '{0}'", extra[0]);
                PrintHelp();
                return false;
            }

            // Success
            return true;
        }

        public static void PrintHelp()
        {
            Program.WriteLine("{0} Utility, Version: {1}", GetProgramName(), GetVersion());
            Program.WriteLine("Usage: {0} [OPTIONS]+ TMXPATH [UNITYDIR]", GetProgramName());
            Program.WriteLine("Example: {0} --verbose -s=0.01 MyTiledMap.tmx ../../MyUnityProjectFolder/Assets/Tiled2Unity", GetProgramName());
            Program.WriteLine("");
            Program.WriteLine("Options:");

            TextWriter writer = new StringWriter();
            Program.Options.WriteOptionDescriptions(writer);
            Program.WriteLine(writer.ToString());

            Program.WriteLine("Prefab object properties (set in TMX file for each layer/object)");
            Program.WriteLine("  unity:sortingLayerName");
            Program.WriteLine("  unity:sortingOrder");
            Program.WriteLine("  unity:layer");
            Program.WriteLine("  unity:tag");
            Program.WriteLine("  unity:scale");
            Program.WriteLine("  unity:isTrigger");
            Program.WriteLine("  unity:convex");
            Program.WriteLine("  unity:ignore (value = [false|true|collision|visual])");
            Program.WriteLine("  unity:resource (value = [false|true])");
            Program.WriteLine("  unity:resourcePath");
            Program.WriteLine("  (Other properties are exported for custom scripting in your Unity project)");
            Program.WriteLine("Support Tiled Map Editor on Patreon: https://www.patreon.com/bjorn");
            Program.WriteLine("Make a donation for Tiled2Unity: http://www.seanba.com/donate");
        }

        public static void WriteLine()
        {
            WriteLine("");
        }

        public static void WriteLine(string line)
        {
            line += "\n";
            if (OnWriteLine != null)
                OnWriteLine(line);
            Console.Write(line);
            Log(line);
        }

        public static void WriteLine(string fmt, params object[] args)
        {
            WriteLine(String.Format(fmt, args));
        }

        public static void WriteWarning(string warning)
        {
            warning += "\n";
            if (OnWriteWarning != null)
                OnWriteWarning(warning);
            Console.Write(warning);
            Log(warning);
        }

        public static void WriteWarning(string fmt, params object[] args)
        {
            WriteWarning(String.Format(fmt, args));
        }

        public static void WriteError(string error)
        {
            error += "\n";
            if (OnWriteError != null)
                OnWriteError(error);
            Console.Write(error);
            Log(error);
        }

        public static void WriteError(string fmt, params object[] args)
        {
            WriteError(String.Format(fmt, args));
        }

        public static void WriteSuccess(string success)
        {
            success += "\n";
            if (OnWriteSuccess != null)
                OnWriteSuccess(success);
            Console.Write(success);
            Log(success);
        }

        public static void WriteSuccess(string fmt, params object[] args)
        {
            WriteSuccess(String.Format(fmt, args));
        }

        public static void WriteVerbose(string line)
        {
            if (!Program.Verbose)
                return;

            line += "\n";
            if (OnWriteVerbose != null)
                OnWriteVerbose(line);
            Console.Write(line);
            Log(line);
        }

        public static void WriteVerbose(string fmt, params object[] args)
        {
            WriteVerbose(String.Format(fmt, args));
        }

        public static string GetExportedFilename(TmxMap tmxMap)
        {
            return String.Format("{0}.tiled2unity.xml", tmxMap.Name);
        }

#if !TILED_2_UNITY_LITE
        // GetVersion() is automatically generated with Tiled2UnityLite
        public static string GetVersion()
        {
            var thisApp = Assembly.GetExecutingAssembly();
            AssemblyName name = new AssemblyName(thisApp.FullName);
            return name.Version.ToString();
        }
#endif

#if TILED_2_UNITY_LITE
        public static string GetProgramName()
        {
            return "Tiled2UnityLite";
        }
#else
        public static string GetProgramName()
        {
            return "Tiled2Unity";
        }
#endif

        static private void StartLogging(string[] args)
        {
            // Create the directory if need be
            Program.LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tiled2Unity");
            if (!Directory.Exists(Program.LogFilePath))
            {
                Directory.CreateDirectory(Program.LogFilePath);
            }

            // Start off the log empty
            Program.LogFilePath = Path.Combine(Program.LogFilePath, "tiled2unity.log");
            File.WriteAllText(Program.LogFilePath, String.Empty);

            // Write our very first entries into the log
            Program.WriteLine(DateTime.Now.ToString());
            Program.WriteLine("Tiled2Unity {0}", String.Join(" ", args));
            Program.WriteLine("Log path: {0}", Program.LogFilePath);
        }

        static private void Log(string line)
        {
#if !TILED_2_UNITY_LITE
            // No logging in Tiled2UnityLite
            using (StreamWriter writer = File.AppendText(Program.LogFilePath))
            {
                writer.Write(line);
            }
#endif
        }

        static private void SetCulture()
        {
            // Force decimal numbers to use '.' as the decimal separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
        }

        static private float ParseFloatDefault(string str, float defaultValue)
        {
            float resultValue = 0;
            if (float.TryParse(str, out resultValue))
            {
                return resultValue;
            }
            return defaultValue;
        }

    } // end class
} // end namespace

// ----------------------------------------------------------------------
// TiledMapExporter.AssignMaterials.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        private List<XElement> CreateAssignMaterialsElements()
        {
            // Each mesh in each viewable layer needs to have its material assigned to it
            List<XElement> elements = new List<XElement>();
            foreach (var layer in this.tmxMap.Layers)
            {
                if (layer.Visible == false)
                    continue;
                if (layer.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                foreach (TmxMesh mesh in layer.Meshes)
                {
                   XElement assignment =
                        new XElement("AssignMaterial",
                            new XAttribute("mesh", mesh.UniqueMeshName),
                            new XAttribute("material", Path.GetFileNameWithoutExtension(mesh.TmxImage.AbsolutePath)));

                    // Is there a transparent color key?
                    if (!String.IsNullOrEmpty(mesh.TmxImage.TransparentColor))
                    {
                        assignment.SetAttributeValue("alphaColorKey", mesh.TmxImage.TransparentColor);
                    }

                    elements.Add(assignment);
                }
            }

            // Each mesh for each TileObject needs its material assigned
            foreach (var tmxMesh in this.tmxMap.GetUniqueListOfVisibleObjectTileMeshes())
            {
                XElement assignment =
                     new XElement("AssignMaterial",
                         new XAttribute("mesh", tmxMesh.UniqueMeshName),
                         new XAttribute("material", Path.GetFileNameWithoutExtension(tmxMesh.TmxImage.AbsolutePath)));

                    elements.Add(assignment);
            }

            return elements;
        }
    }
}

// ----------------------------------------------------------------------
// TiledMapExporter.Clipper.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    using ClipperPolygon = List<ClipperLib.IntPoint>;
    using ClipperPolygons = List<List<ClipperLib.IntPoint>>;

    partial class TiledMapExporter
    {
        // After a certain number of paths in a polygon collider Unity will start to slow down considerably
        private static readonly int MaxNumberOfSafePaths = 16 * 16;

        private XElement CreateCollisionElementForLayer(TmxLayer layer)
        {
            // Collision elements look like this
            // (Can also have EdgeCollider2Ds)
            //      <GameOject name="Collision">
            //        <PolygonCollider2D>
            //          <Path>list of points</Path>
            //          <Path>another list of points</Path>
            //        </PolygonCollider2D>
            //      </GameOject>

            LayerClipper.TransformPointFunc xfFunc =
                delegate(float x, float y)
                {
                    // Transform point to Unity space
                    PointF pointUnity3d = PointFToUnityVector_NoScale(new PointF(x, y));
                    ClipperLib.IntPoint point = new ClipperLib.IntPoint(pointUnity3d.X, pointUnity3d.Y);
                    return point;
                };

            LayerClipper.ProgressFunc progFunc =
                delegate(string prog)
                {
                    Program.WriteLine(prog);
                };

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(this.tmxMap, layer, xfFunc, progFunc);

            var paths = ClipperLib.Clipper.ClosedPathsFromPolyTree(solution);
            if (paths.Count >= MaxNumberOfSafePaths)
            {
                StringBuilder warning = new StringBuilder();
                warning.AppendFormat("Layer '{0}' has a large number of polygon paths ({1}).", layer.Name, paths.Count);
                warning.AppendLine("  Importing this layer may be slow in Unity.");
                warning.AppendLine("  Check polygon/rectangle objects in Tile Collision Editor in Tiled and use 'Snap to Grid' or 'Snap to Fine Grid'.");
                warning.AppendLine("  You want colliders to be set up so they can be merged with colliders on neighboring tiles, reducing path count considerably.");
                warning.AppendLine("  In some cases, the size of the map may need to be reduced.");
                Program.WriteWarning(warning.ToString());
            }

            // Add our polygon and edge colliders
            List<XElement> polyColliderElements = new List<XElement>();

            if (layer.IsExportingConvexPolygons())
            {
                AddPolygonCollider2DElements_Convex(solution, polyColliderElements);
            }
            else
            {
                AddPolygonCollider2DElements_Complex(solution, polyColliderElements);
            }

            AddEdgeCollider2DElements(ClipperLib.Clipper.OpenPathsFromPolyTree(solution), polyColliderElements);

            if (polyColliderElements.Count() == 0)
            {
                // No collisions on this layer
                return null;
            }

            XElement gameObjectCollision =
                new XElement("GameObject",
                    new XAttribute("name", "Collision"),
                    polyColliderElements);

            return gameObjectCollision;
        }

        private void AddPolygonCollider2DElements_Convex(ClipperLib.PolyTree solution, List<XElement> xmlList)
        {
            // This may generate many convex polygons as opposed to one "complicated" one
            var polygons = LayerClipper.SolutionPolygons_Simple(solution);

            // Each PointF array is a polygon with a single path
            foreach (var pointfArray in polygons)
            {
                string data = String.Join(" ", pointfArray.Select(pt => String.Format("{0},{1}", pt.X * Program.Scale, pt.Y * Program.Scale)));
                XElement pathElement = new XElement("Path", data);

                XElement polyColliderElement = new XElement("PolygonCollider2D", pathElement);
                xmlList.Add(polyColliderElement);
            }
        }

        private void AddPolygonCollider2DElements_Complex(ClipperLib.PolyTree solution, List<XElement> xmlList)
        {
            // This should generate one "complicated" polygon which may contain holes and concave edges
            var polygons = ClipperLib.Clipper.ClosedPathsFromPolyTree(solution);
            if (polygons.Count == 0)
                return;

            // Add just one polygon collider that has all paths in it.
            List<XElement> pathElements = new List<XElement>();
            foreach (var path in polygons)
            {
                string data = String.Join(" ", path.Select(pt => String.Format("{0},{1}", pt.X * Program.Scale, pt.Y * Program.Scale)));
                XElement pathElement = new XElement("Path", data);
                pathElements.Add(pathElement);
            }

            XElement polyColliderElement = new XElement("PolygonCollider2D", pathElements);
            xmlList.Add(polyColliderElement);
        }

        private void AddEdgeCollider2DElements(ClipperPolygons lines, List<XElement> xmlList)
        {
            if (lines.Count == 0)
                return;

            // Add one edge collider for every polyline
            // Clipper does not combine line segments for us
            var combined = CombineLineSegments(lines);
            foreach (var points in combined)
            {
                string data = String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X * Program.Scale, pt.Y * Program.Scale)));
                XElement edgeCollider =
                    new XElement("EdgeCollider2D",
                        new XElement("Points", data));

                xmlList.Add(edgeCollider);
            }
        }

        private ClipperPolygons CombineLineSegments(ClipperPolygons lines)
        {
            PolylineReduction reduction = new PolylineReduction();

            foreach (var points in lines)
            {
                reduction.AddLine(points);
            }

            return reduction.Reduce();
        }



    } // end class
} // end namespace

// ----------------------------------------------------------------------
// TiledMapExporter.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.IO;
// using System.IO.Compression;
// using System.Linq;
// using System.Text;
// using System.Text.RegularExpressions;
// using System.Reflection;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        private TmxMap tmxMap = null;

        public TiledMapExporter(TmxMap tmxMap)
        {
            this.tmxMap = tmxMap;
        }

        public void Export(string exportToTiled2UnityPath)
        {
            // Create an Xml file to be imported by a Unity project
            // The unity project will have code that turns the Xml into Unity objects and prefabs
            string fileToSave = Program.GetExportedFilename(this.tmxMap);
            Program.WriteLine("Compiling tiled2unity file: {0}", fileToSave);

            // Need an element for embedded file data that will be imported into Unity
            // These are models and textures
            List<XElement> importFiles = CreateImportFilesElements(exportToTiled2UnityPath);
            List<XElement> assignMaterials = CreateAssignMaterialsElements();

            Program.WriteLine("Gathering prefab data ...");
            XElement prefab = CreatePrefabElement();

            // Create the Xml root and populate it
            Program.WriteLine("Writing as Xml ...");

            string version = Program.GetVersion();
            XElement root = new XElement("Tiled2Unity", new XAttribute("version", version));
            root.Add(assignMaterials);
            root.Add(prefab);
            root.Add(importFiles);

            // Create the XDocument to save
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XComment("Tiled2Unity generated xml data"),
                new XComment("Do not modify by hand"),
                new XComment(String.Format("Last exported: {0}", DateTime.Now)),
                root);

            // Build the export directory
            string exportDir = Path.Combine(exportToTiled2UnityPath, "Imported");

            if (!Directory.Exists(exportDir))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Could not export '{0}'\n", fileToSave);
                builder.AppendFormat("Tiled2Unity.unitypackage is not installed in unity project: {0}\n", exportToTiled2UnityPath);
                builder.AppendFormat("Select \"Help -> Import Unity Package to Project\" and re-export");
                Program.WriteError(builder.ToString());
                return;
            }

            // Detect which version of Tiled2Unity is in our project
            // ...\Tiled2Unity\Tiled2Unity.export.txt
            string unityProjectVersionTXT = Path.Combine(exportToTiled2UnityPath, "Tiled2Unity.export.txt");
            if (!File.Exists(unityProjectVersionTXT))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Could not export '{0}'\n", fileToSave);
                builder.AppendFormat("Tiled2Unity.unitypackage is not properly installed in unity project: {0}\n", exportToTiled2UnityPath);
                builder.AppendFormat("Missing file: {0}\n", unityProjectVersionTXT);
                builder.AppendFormat("Select \"Help -> Import Unity Package to Project\" and re-export");
                Program.WriteError(builder.ToString());
                return;
            }

            // Open the unity-side script file and check its version number
            string text = File.ReadAllText(unityProjectVersionTXT);
            if (!String.IsNullOrEmpty(text))
            {
                string pattern = @"^\[Tiled2Unity Version (?<version>.*)?\]";
                Regex regex = new Regex(pattern);
                Match match = regex.Match(text);
                Group group = match.Groups["version"];
                if (group.Success)
                {
                    if (Program.GetVersion() != group.ToString())
                    {
                        StringBuilder builder = new StringBuilder();
                        builder.AppendFormat("Export/Import Version mismatch\n");
                        builder.AppendFormat("  Tiled2Unity version   : {0}\n", Program.GetVersion());
                        builder.AppendFormat("  Unity Project version : {0}\n", group.ToString());
                        Program.WriteWarning(builder.ToString());
                    }
                }
            }

            // Save the file (which is importing it into Unity)
            string pathToSave = Path.Combine(exportDir, fileToSave);
            Program.WriteLine("Exporting to: {0}", pathToSave);
            doc.Save(pathToSave);
            Program.WriteSuccess("Succesfully exported: {0}\n  vertex scale = {1}", pathToSave, Program.Scale);
        }

        public static PointF PointFToUnityVector_NoScale(PointF pt)
        {
            // Unity's coordinate sytem has y-up positive, y-down negative
            // Have to watch for negative zero, ffs
            return new PointF(pt.X, pt.Y == 0 ? 0 : -pt.Y);
        }

        public static PointF PointFToUnityVector(float x, float y)
        {
            return PointFToUnityVector(new PointF(x, y));
        }

        public static PointF PointFToUnityVector(PointF pt)
        {
            // Unity's coordinate sytem has y-up positive, y-down negative
            // Apply scaling
            PointF scaled = pt;
            scaled.X *= Program.Scale;
            scaled.Y *= Program.Scale;

            // Have to watch for negative zero, ffs
            return new PointF(scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointFToObjVertex(PointF pt)
        {
            // Note, we negate the x and y due to Wavefront's coordinate system
            // Applying scaling
            PointF scaled = pt;
            scaled.X *= Program.Scale;
            scaled.Y *= Program.Scale;

            // Watch for negative zero, ffs
            return new PointF(scaled.X == 0 ? 0 : -scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointToTextureCoordinate(PointF pt, Size imageSize)
        {
            float tx = pt.X / (float)imageSize.Width;
            float ty = pt.Y / (float)imageSize.Height;
            return new PointF(tx, 1.0f - ty);
        }

        private string StringToBase64String(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        private string FileToBase64String(string path)
        {
            return Convert.ToBase64String(File.ReadAllBytes(path));
        }

        private string FileToCompressedBase64String(string path)
        {
            using (FileStream originalStream = File.OpenRead(path))
            using (MemoryStream byteStream = new MemoryStream())
            using (GZipStream gzipStream = new GZipStream(byteStream, CompressionMode.Compress))
            {
                originalStream.CopyTo(gzipStream);
                byte[] compressedBytes = byteStream.ToArray();
                return Convert.ToBase64String(compressedBytes);
            }

            // Without compression (testing shows it ~300% larger)
            //return Convert.ToBase64String(File.ReadAllBytes(path));
        }

    } // end class
} // end namepsace

// ----------------------------------------------------------------------
// TiledMapExporter.ImportFiles.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;


namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        private List<XElement> CreateImportFilesElements(string exportToUnityProjectPath)
        {
            List<XElement> elements = new List<XElement>();

            // Add the mesh file as raw text
            {
                StringWriter objBuilder = BuildObjString();

                XElement mesh =
                    new XElement("ImportMesh",
                        new XAttribute("filename", this.tmxMap.Name + ".obj"),
                        StringToBase64String(objBuilder.ToString()));

                elements.Add(mesh);
            }

            {
                // Add all image files as compressed base64 strings
                var layerImagePaths = from layer in this.tmxMap.Layers
                                      where layer.Visible == true
                                      from rawTileId in layer.TileIds
                                      where rawTileId != 0
                                      let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                      let tile = this.tmxMap.Tiles[tileId]
                                      select tile.TmxImage.AbsolutePath;
                layerImagePaths = layerImagePaths.Distinct();

                // Tile Objects may have images not yet references by a layer
                var objectImagePaths = from objectGroup in this.tmxMap.ObjectGroups
                                       where objectGroup.Visible == true
                                       from tmxObject in objectGroup.Objects
                                       where tmxObject.Visible == true
                                       where tmxObject is TmxObjectTile
                                       let tmxTileObject = tmxObject as TmxObjectTile
                                       from mesh in tmxTileObject.Tile.Meshes
                                       select mesh.TmxImage.AbsolutePath;
                objectImagePaths = objectImagePaths.Distinct();

                List<string> imagePaths = new List<string>();
                imagePaths.AddRange(layerImagePaths);
                imagePaths.AddRange(objectImagePaths);
                imagePaths = imagePaths.Distinct().ToList();

                // Do not import files if they are already in the project (in the /Assets/ directory of where we're exporting too)
                string unityAssetsDir = Path.Combine(exportToUnityProjectPath, "Assets");

                foreach (string path in imagePaths)
                {
                    // If the copy from location comes from within the project we want to copy to, then don't do it.
                    // This allows us to have tileset images that are alreday in use by the Unity project
                    string saveToAssetsDir = unityAssetsDir.ToLower();
                    string copyFromDir = path.ToLower();
                    if (copyFromDir.StartsWith(saveToAssetsDir))
                    {
                        // The path to the texture will be WRT to the Unity project root
                        string assetPath = path.Remove(0, exportToUnityProjectPath.Length);
                        assetPath = assetPath.TrimStart('\\');
                        assetPath = assetPath.TrimStart('/');
                        Program.WriteLine("InternalTexture : {0}", assetPath);

                        XElement texture = new XElement("InternalTexture", new XAttribute("assetPath", assetPath));
                        elements.Add(texture);
                    }
                    else
                    {
                        // Note that compression is not available in Unity. Go with Base64 string. Blerg.
                        Program.WriteLine("ImportTexture : will import '{0}' to {1}", path, Path.Combine(unityAssetsDir, "Tiled2Unity\\Textures\\"));
                        XElement texture =
                            new XElement("ImportTexture",
                                new XAttribute("filename", Path.GetFileName(path)),
                                FileToBase64String(path));
                        //FileToCompressedBase64String(path));

                        elements.Add(texture);
                    }
                }
            }

            return elements;
        }

    } // end class
} // end namespace

// ----------------------------------------------------------------------
// TiledMapExporter.Obj.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // Partial class that concentrates on creating the Wavefront Mesh (.obj) string
    partial class TiledMapExporter
    {
        // Creates the text for a Wavefront OBJ file for the TmxMap
        private StringWriter BuildObjString()
        {
            HashIndexOf<PointF> vertexDatabase = new HashIndexOf<PointF>();
            HashIndexOf<PointF> uvDatabase = new HashIndexOf<PointF>();

            // Go through every face of every mesh of every visible layer and collect vertex and texture coordinate indices as you go
            int groupCount = 0;
            StringBuilder faceBuilder = new StringBuilder();
            foreach (var layer in this.tmxMap.Layers)
            {
                if (layer.Visible != true)
                    continue;

                if (layer.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                // We're going to use this layer
                ++groupCount;

                // Enumerate over the tiles in the direction given by the draw order of the map
                var verticalRange = (this.tmxMap.DrawOrderVertical == 1) ? Enumerable.Range(0, layer.Height) : Enumerable.Range(0, layer.Height).Reverse();
                var horizontalRange = (this.tmxMap.DrawOrderHorizontal == 1) ? Enumerable.Range(0, layer.Width) : Enumerable.Range(0, layer.Width).Reverse();

                foreach (TmxMesh mesh in layer.Meshes)
                {
                    Program.WriteLine("Writing '{0}' mesh group", mesh.UniqueMeshName);
                    faceBuilder.AppendFormat("\ng {0}\n", mesh.UniqueMeshName);

                    foreach (int y in verticalRange)
                    {
                        foreach (int x in horizontalRange)
                        {
                            int tileIndex = layer.GetTileIndex(x, y);
                            uint tileId = mesh.TileIds[tileIndex];

                            // Skip blank tiles
                            if (tileId == 0)
                                continue;

                            TmxTile tile = this.tmxMap.Tiles[TmxMath.GetTileIdWithoutFlags(tileId)];
                            
                            // What are the vertex and texture coorindates of this face on the mesh?
                            var position = this.tmxMap.GetMapPositionAt(x, y);
                            var vertices = CalculateFaceVertices(position, tile.TileSize, this.tmxMap.TileHeight, tile.Offset);

                            // Is the tile being flipped or rotated (needed for texture cooridinates)
                            bool flipDiagonal = TmxMath.IsTileFlippedDiagonally(tileId);
                            bool flipHorizontal = TmxMath.IsTileFlippedHorizontally(tileId);
                            bool flipVertical = TmxMath.IsTileFlippedVertically(tileId);
                            var uvs = CalculateFaceTextureCoordinates(tile, flipDiagonal, flipHorizontal, flipVertical);

                            // Adds vertices and uvs to the database as we build the face strings
                            string v0 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[0]) + 1, uvDatabase.Add(uvs[0]) + 1);
                            string v1 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[1]) + 1, uvDatabase.Add(uvs[1]) + 1);
                            string v2 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[2]) + 1, uvDatabase.Add(uvs[2]) + 1);
                            string v3 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[3]) + 1, uvDatabase.Add(uvs[3]) + 1);
                            faceBuilder.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);
                        }
                    }
                }
            }

            // Now go through any tile objects we may have and write them out as face groups as well
            foreach (var tmxMesh in this.tmxMap.GetUniqueListOfVisibleObjectTileMeshes())
            {
                // We're going to use this tile object
                groupCount++;

                Program.WriteLine("Writing '{0}' tile group", tmxMesh.UniqueMeshName);
                faceBuilder.AppendFormat("\ng {0}\n", tmxMesh.UniqueMeshName);

                // Get the single tile associated with this mesh
                TmxTile tmxTile = this.tmxMap.Tiles[tmxMesh.TileIds[0]];

                var vertices = CalculateFaceVertices_TileObject(tmxTile.TileSize, tmxTile.Offset);
                var uvs = CalculateFaceTextureCoordinates(tmxTile, false, false, false);

                // Adds vertices and uvs to the database as we build the face strings
                string v0 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[0]) + 1, uvDatabase.Add(uvs[0]) + 1);
                string v1 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[1]) + 1, uvDatabase.Add(uvs[1]) + 1);
                string v2 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[2]) + 1, uvDatabase.Add(uvs[2]) + 1);
                string v3 = String.Format("{0}/{1}/1", vertexDatabase.Add(vertices[3]) + 1, uvDatabase.Add(uvs[3]) + 1);
                faceBuilder.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);
            }

            // All of our faces have been built and vertex and uv databases have been filled.
            // Start building out the obj file
            StringWriter objWriter = new StringWriter();
            objWriter.WriteLine("# Wavefront OBJ file automatically generated by Tiled2Unity");
            objWriter.WriteLine();

            Program.WriteLine("Writing face vertices");
            objWriter.WriteLine("# Vertices (Count = {0})", vertexDatabase.List.Count());
            foreach (var v in vertexDatabase.List)
            {
                objWriter.WriteLine("v {0} {1} 0", v.X, v.Y);
            }
            objWriter.WriteLine();

            Program.WriteLine("Writing face uv coordinates");
            objWriter.WriteLine("# Texture cooridinates (Count = {0})", uvDatabase.List.Count());
            foreach (var uv in uvDatabase.List)
            {
                objWriter.WriteLine("vt {0} {1}", uv.X, uv.Y);
            }
            objWriter.WriteLine();

            // Write the one indexed normal
            objWriter.WriteLine("# Normal");
            objWriter.WriteLine("vn 0 0 -1");
            objWriter.WriteLine();

            // Now we can copy over the string used to build the databases
            objWriter.WriteLine("# Groups (Count = {0})", groupCount);
            objWriter.WriteLine(faceBuilder.ToString());

            return objWriter;
        }

        private PointF[] CalculateFaceVertices(Point mapLocation, Size tileSize, int mapTileHeight, PointF offset)
        {
            // Location on map is complicated by tiles that are 'higher' than the tile size given for the overall map
            mapLocation.Offset(0, -tileSize.Height + mapTileHeight);

            PointF pt0 = mapLocation;
            PointF pt1 = PointF.Add(mapLocation, new Size(tileSize.Width, 0));
            PointF pt2 = PointF.Add(mapLocation, tileSize);
            PointF pt3 = PointF.Add(mapLocation, new Size(0, tileSize.Height));

            // Apply the tile offset

            pt0 = TmxMath.AddPoints(pt0, offset);
            pt1 = TmxMath.AddPoints(pt1, offset);
            pt2 = TmxMath.AddPoints(pt2, offset);
            pt3 = TmxMath.AddPoints(pt3, offset);

            // We need to use ccw winding for Wavefront objects
            PointF[] vertices  = new PointF[4];
            vertices[3] = PointFToObjVertex(pt0);
            vertices[2] = PointFToObjVertex(pt1);
            vertices[1] = PointFToObjVertex(pt2);
            vertices[0] = PointFToObjVertex(pt3);
            return vertices;
        }

        private PointF[] CalculateFaceVertices_TileObject(Size tileSize, PointF offset)
        {
            // Tile Object vertices are not concerned about where they are placed in the world
            PointF origin = PointF.Empty;

            PointF pt0 = origin;
            PointF pt1 = PointF.Add(origin, new Size(tileSize.Width, 0));
            PointF pt2 = PointF.Add(origin, tileSize);
            PointF pt3 = PointF.Add(origin, new Size(0, tileSize.Height));

            // Apply the tile offset

            pt0 = TmxMath.AddPoints(pt0, offset);
            pt1 = TmxMath.AddPoints(pt1, offset);
            pt2 = TmxMath.AddPoints(pt2, offset);
            pt3 = TmxMath.AddPoints(pt3, offset);

            // We need to use ccw winding for Wavefront objects
            PointF[] vertices = new PointF[4];
            vertices[3] = PointFToObjVertex(pt0);
            vertices[2] = PointFToObjVertex(pt1);
            vertices[1] = PointFToObjVertex(pt2);
            vertices[0] = PointFToObjVertex(pt3);
            return vertices;
        }

        private PointF[] CalculateFaceTextureCoordinates(TmxTile tmxTile, bool flipDiagonal, bool flipHorizontal, bool flipVertical)
        {
            Point imageLocation = tmxTile.LocationOnSource;
            Size tileSize = tmxTile.TileSize;
            Size imageSize = tmxTile.TmxImage.Size;

            PointF[] points = new PointF[4];
            points[0] = imageLocation;
            points[1] = PointF.Add(imageLocation, new Size(tileSize.Width, 0));
            points[2] = PointF.Add(imageLocation, tileSize);
            points[3] = PointF.Add(imageLocation, new Size(0, tileSize.Height));

            PointF center = new PointF(tileSize.Width * 0.5f, tileSize.Height * 0.5f);
            center.X += imageLocation.X;
            center.Y += imageLocation.Y;
            TmxMath.TransformPoints_DiagFirst(points, center, flipDiagonal, flipHorizontal, flipVertical);
            //TmxMath.TransformPoints(points, center, flipDiagonal, flipHorizontal, flipVertical);

            PointF[] coordinates = new PointF[4];
            coordinates[3] = PointToTextureCoordinate(points[0], imageSize);
            coordinates[2] = PointToTextureCoordinate(points[1], imageSize);
            coordinates[1] = PointToTextureCoordinate(points[2], imageSize);
            coordinates[0] = PointToTextureCoordinate(points[3], imageSize);

            // Apply a small bias to the "inner" edges of the texels
            // This keeps us from seeing seams
            //const float bias = 1.0f / 8192.0f;
            //const float bias = 1.0f / 4096.0f;
            //const float bias = 1.0f / 2048.0f;
            if (Program.TexelBias > 0)
            {
                float bias = 1.0f / Program.TexelBias;

                PointF[] multiply = new PointF[4];
                multiply[0] = new PointF(1, 1);
                multiply[1] = new PointF(-1, 1);
                multiply[2] = new PointF(-1, -1);
                multiply[3] = new PointF(1, -1);

                // This nudge has to be transformed too
                TmxMath.TransformPoints_DiagFirst(multiply, Point.Empty, flipDiagonal, flipHorizontal, flipVertical);

                coordinates[0] = TmxMath.AddPoints(coordinates[0], TmxMath.ScalePoints(multiply[0], bias));
                coordinates[1] = TmxMath.AddPoints(coordinates[1], TmxMath.ScalePoints(multiply[1], bias));
                coordinates[2] = TmxMath.AddPoints(coordinates[2], TmxMath.ScalePoints(multiply[2], bias));
                coordinates[3] = TmxMath.AddPoints(coordinates[3], TmxMath.ScalePoints(multiply[3], bias));
            }

            return coordinates;
        }
    }
}

// ----------------------------------------------------------------------
// TiledMapExporter.Prefab.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;


namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        // As we build the prefab, what context are we in?
        public enum PrefabContext
        {
            Root,
            TiledLayer,
            ObjectLayer,
            Object,
        }

        // Helper delegate to modify points by some transformation
        private delegate void TransformVerticesFunc(PointF[] verts);

        private XElement CreatePrefabElement()
        {
            // And example of the kind of xml element we're building
            // Note that "layer" is overloaded. There is the concept of layers in both Tiled and Unity
            //  <Prefab name="NameOfTmxFile">
            //
            //    <GameObject name="FirstLayerName tag="OptionalTagName" layer="OptionalUnityLayerName">
            //      <GameObject Copy="[mesh_name]" />
            //      <GameObject Copy="[another_mesh_name]" />
            //      <GameOject name="Collision">
            //        <PolygonCollider2D>
            //          <Path>data for first path</Path>
            //          <Path>data for second path</Path>
            //        </PolygonCollider2D>
            //      </GameOject name="Collision">
            //    </GameObject>
            //
            //    <GameObject name="SecondLayerName">
            //      <GameObject Copy="[yet_another_mesh_name]" />
            //    </GameObject>
            //
            //    <GameObject name="Colliders">
            //      <PolygonCollider2D> ...
            //      <CircleCollider2D> ...
            //      <BoxCollider2D>...
            //    </GameObject>
            //
            //    <GameObject name="ObjectGroupName">
            //      <GameObject name="ObjectName">
            //          <Property name="PropertyName"> ... some custom data ...
            //          <Property name="PropertyName"> ... some custom data ...
            //      </GameObject>
            //    </GameObject>
            //
            //  </Prefab>

            Size sizeInPixels = this.tmxMap.MapSizeInPixels();

            XElement prefab = new XElement("Prefab");
            prefab.SetAttributeValue("name", this.tmxMap.Name);
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);
            prefab.SetAttributeValue("exportScale", Program.Scale);
            prefab.SetAttributeValue("mapWidthInPixels", sizeInPixels.Width);
            prefab.SetAttributeValue("mapHeightInPixels", sizeInPixels.Height);
            AssignUnityProperties(this.tmxMap, prefab, PrefabContext.Root);
            AssignTiledProperties(this.tmxMap, prefab);

            // We create an element for each tiled layer and add that to the prefab
            {
                List<XElement> layerElements = new List<XElement>();
                foreach (var layer in this.tmxMap.Layers)
                {
                    if (layer.Visible == false)
                        continue;

                    PointF offset = PointFToUnityVector(layer.Offset);

                    XElement layerElement =
                        new XElement("GameObject",
                            new XAttribute("name", layer.Name),
                            new XAttribute("x", offset.X),
                            new XAttribute("y", offset.Y));

                    if (layer.Ignore != TmxLayer.IgnoreSettings.Visual)
                    {
                        // Submeshes for the layer (layer+material)
                        var meshElements = CreateMeshElementsForLayer(layer);
                        layerElement.Add(meshElements);
                    }

                    // Collision data for the layer
                    if (layer.Ignore != TmxLayer.IgnoreSettings.Collision)
                    {
                        var collisionElements = CreateCollisionElementForLayer(layer);
                        layerElement.Add(collisionElements);
                    }

                    AssignUnityProperties(layer, layerElement, PrefabContext.TiledLayer);
                    AssignTiledProperties(layer, layerElement);

                    // Add the element to our list of layers
                    layerElements.Add(layerElement);
                }

                prefab.Add(layerElements);
            }

            // Add all our object groups (may contain colliders)
            {
                var collidersObjectGroup = from item in this.tmxMap.ObjectGroups
                                           where item.Visible == true
                                           select item;

                List<XElement> objectGroupElements = new List<XElement>();
                foreach (var objGroup in collidersObjectGroup)
                {
                    XElement gameObject = new XElement("GameObject", new XAttribute("name", objGroup.Name));

                    // Offset the object group
                    PointF offset = PointFToUnityVector(objGroup.Offset);
                    gameObject.SetAttributeValue("x", offset.X);
                    gameObject.SetAttributeValue("y", offset.Y);

                    AssignUnityProperties(objGroup, gameObject, PrefabContext.ObjectLayer);
                    AssignTiledProperties(objGroup, gameObject);

                    List<XElement> colliders = CreateObjectElementList(objGroup);
                    if (colliders.Count() > 0)
                    {
                        gameObject.Add(colliders);
                    }

                    objectGroupElements.Add(gameObject);
                }

                if (objectGroupElements.Count() > 0)
                {
                    prefab.Add(objectGroupElements);
                }
            }

            return prefab;
        }

        private List<XElement> CreateObjectElementList(TmxObjectGroup objectGroup)
        {
            List<XElement> elements = new List<XElement>();

            foreach (TmxObject tmxObject in objectGroup.Objects)
            {
                // All the objects/colliders in our object group need to be separate game objects because they can have unique tags/layers
                XElement xmlObject = new XElement("GameObject", new XAttribute("name", tmxObject.GetNonEmptyName()));

                // Transform object locaction into map space (needed for isometric and hex modes) 
                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(this.tmxMap, tmxObject.Position);
                PointF pos = PointFToUnityVector(xfPosition);
                xmlObject.SetAttributeValue("x", pos.X);
                xmlObject.SetAttributeValue("y", pos.Y);
                xmlObject.SetAttributeValue("rotation", tmxObject.Rotation);

                AssignUnityProperties(tmxObject, xmlObject, PrefabContext.Object);
                AssignTiledProperties(tmxObject, xmlObject);

                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromRectangle(this.tmxMap, tmxObject as TmxObjectRectangle);
                        objElement = CreatePolygonColliderElement(tmxIsometricRectangle);
                    }
                    else
                    {
                        objElement = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                    }
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    objElement = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, objectGroup.Name);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    objElement = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    objElement = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    AddTileObjectElements(tmxObject as TmxObjectTile, xmlObject);
                }
                else
                {
                    Program.WriteLine("Object '{0}' has been added for use with custom importers", tmxObject);
                }

                if (objElement != null)
                {
                    xmlObject.Add(objElement);
                }

                elements.Add(xmlObject);
            }

            return elements;
        }

        private List<XElement> CreateMeshElementsForLayer(TmxLayer layer)
        {
            List<XElement> xmlMeshes = new List<XElement>();

            foreach (TmxMesh mesh in layer.Meshes)
            {
                XElement xmlMesh = new XElement("GameObject",
                    new XAttribute("name", mesh.ObjectName),
                    new XAttribute("copy", mesh.UniqueMeshName),
                    new XAttribute("sortingLayerName", layer.SortingLayerName),
                    new XAttribute("sortingOrder", layer.SortingOrder),
                    new XAttribute("opacity", layer.Opacity));
                xmlMeshes.Add(xmlMesh);

                if (mesh.FullAnimationDurationMs > 0)
                {
                    XElement xmlAnimation = new XElement("TileAnimator",
                        new XAttribute("startTimeMs", mesh.StartTimeMs),
                        new XAttribute("durationMs", mesh.DurationMs),
                        new XAttribute("fullTimeMs", mesh.FullAnimationDurationMs));
                    xmlMesh.Add(xmlAnimation);
                }
            }

            return xmlMeshes;
        }

        private void AssignUnityProperties<T>(T tmx, XElement xml, PrefabContext context) where T : TmxHasProperties
        {
            // Only the root of the prefab can have a scale
            {
                string unityScale = tmx.Properties.GetPropertyValueAsString("unity:scale", "");
                if (!String.IsNullOrEmpty(unityScale))
                {
                    float scale = 1.0f;
                    if (context != PrefabContext.Root)
                    {
                        Program.WriteWarning("unity:scale only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Single.TryParse(unityScale, out scale))
                    {
                        Program.WriteError("unity:scale property value '{0}' could not be converted to a float", unityScale);
                    }
                    else
                    {
                        xml.SetAttributeValue("scale", unityScale);
                    }
                }
            }

            // Only the root of the prefab can be marked a resource
            {
                string unityResource = tmx.Properties.GetPropertyValueAsString("unity:resource", "");
                if (!String.IsNullOrEmpty(unityResource))
                {
                    bool resource = false;
                    if (context != PrefabContext.Root)
                    {
                        Program.WriteWarning("unity:resource only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Boolean.TryParse(unityResource, out resource))
                    {
                        Program.WriteError("unity:resource property value '{0}' could not be converted to a boolean", unityResource);
                    }
                    else
                    {
                        xml.SetAttributeValue("resource", unityResource);
                    }
                }
            }

            // Some users may want resource prefabs to be saved to a particular path
            {
                string unityResourcePath = tmx.Properties.GetPropertyValueAsString("unity:resourcePath", "");
                if (!String.IsNullOrEmpty(unityResourcePath))
                {
                    if (context != PrefabContext.Root)
                    {
                        Program.WriteWarning("unity:resourcePath only applies to map properties\n{0}", xml.ToString());
                    }
                    else
                    {
                        bool isInvalid = Path.GetInvalidPathChars().Any(c => unityResourcePath.Contains(c));
                        if (isInvalid)
                        {
                            Program.WriteError("unity:resourcePath has invalid path characters: {0}", unityResourcePath);
                        }
                        else
                        {
                            xml.SetAttributeValue("resourcePath", unityResourcePath);
                        }
                    }
                }
            }

            // Any object can carry the 'isTrigger' setting and we assume any children to inherit the setting
            {
                string unityIsTrigger = tmx.Properties.GetPropertyValueAsString("unity:isTrigger", "");
                if (!String.IsNullOrEmpty(unityIsTrigger))
                {
                    bool isTrigger = false;
                    if (!Boolean.TryParse(unityIsTrigger, out isTrigger))
                    {
                        Program.WriteError("unity:isTrigger property value '{0}' cound not be converted to a boolean", unityIsTrigger);
                    }
                    else
                    {
                        xml.SetAttributeValue("isTrigger", unityIsTrigger);
                    }
                }
            }

            // Any part of the prefab can be assigned a 'layer'
            {
                string unityLayer = tmx.Properties.GetPropertyValueAsString("unity:layer", "");
                if (!String.IsNullOrEmpty(unityLayer))
                {
                    xml.SetAttributeValue("layer", unityLayer);
                }
            }

            // Any part of the prefab can be assigned a 'tag'
            {
                string unityTag = tmx.Properties.GetPropertyValueAsString("unity:tag", "");
                if (!String.IsNullOrEmpty(unityTag))
                {
                    xml.SetAttributeValue("tag", unityTag);
                }
            }

            List<String> knownProperties = new List<string>();
            knownProperties.Add("unity:layer");
            knownProperties.Add("unity:tag");
            knownProperties.Add("unity:sortingLayerName");
            knownProperties.Add("unity:sortingOrder");
            knownProperties.Add("unity:scale");
            knownProperties.Add("unity:isTrigger");
            knownProperties.Add("unity:convex");
            knownProperties.Add("unity:ignore");
            knownProperties.Add("unity:resource");
            knownProperties.Add("unity:resourcePath");

            var unknown = from p in tmx.Properties.PropertyMap
                          where p.Key.StartsWith("unity:")
                          where knownProperties.Contains(p.Key) == false
                          select p.Key;
            foreach (var p in unknown)
            {
                Program.WriteWarning("Unknown unity property '{0}' in GameObject '{1}'", p, tmx.ToString());
            }
        }

        private void AssignTiledProperties<T>(T tmx, XElement xml) where T : TmxHasProperties
        {
            List<XElement> xmlProperties = new List<XElement>();

            foreach (var prop in tmx.Properties.PropertyMap)
            {
                // Ignore properties that start with "unity:"
                if (prop.Key.StartsWith("unity:"))
                    continue;

                var alreadyProperty = from p in xml.Elements("Property")
                                      where p.Attribute("name") != null
                                      where p.Attribute("name").Value == prop.Key
                                      select p;
                if (alreadyProperty.Count() > 0)
                {
                    // Don't override property that is already there
                    continue;
                }


                XElement xmlProp = new XElement("Property", new XAttribute("name", prop.Key), new XAttribute("value", prop.Value));
                xmlProperties.Add(xmlProp);
            }

            xml.Add(xmlProperties);
        }

        private XElement CreateBoxColliderElement(TmxObjectRectangle tmxRectangle)
        {
            XElement xmlCollider =
                new XElement("BoxCollider2D",
                    new XAttribute("width", tmxRectangle.Size.Width * Program.Scale),
                    new XAttribute("height", tmxRectangle.Size.Height * Program.Scale));

            return xmlCollider;
        }

        private XElement CreateCircleColliderElement(TmxObjectEllipse tmxEllipse, string objGroupName)
        {
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Program.WriteError("Collision ellipse in Object Layer '{0}' is not supported in Isometric maps: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else if (!tmxEllipse.IsCircle())
            {
                Program.WriteError("Collision ellipse in Object Layer '{0}' is not a circle: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else
            {
                XElement circleCollider =
                    new XElement("CircleCollider2D",
                        new XAttribute("radius", tmxEllipse.Radius * Program.Scale));

                return circleCollider;
            }
        }

        private XElement CreatePolygonColliderElement(TmxObjectPolygon tmxPolygon)
        {
            var points = from pt in TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolygon)
                       select PointFToUnityVector(pt);

            XElement polygonCollider =
                new XElement("PolygonCollider2D",
                    new XElement("Path", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return polygonCollider;
        }

        private XElement CreateEdgeColliderElement(TmxObjectPolyline tmxPolyline)
        {
            // The points need to be transformed into unity space
            var points = from pt in TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolyline)
                         select PointFToUnityVector(pt);

            XElement edgeCollider =
                new XElement("EdgeCollider2D",
                    new XElement("Points", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return edgeCollider;
        }

        private void AddTileObjectElements(TmxObjectTile tmxObjectTile, XElement xmlTileObjectRoot)
        {
            // We combine the properties of the tile that is referenced and add it to our own properties
            AssignTiledProperties(tmxObjectTile.Tile, xmlTileObjectRoot);

            // TileObjects can be scaled (this is separate from vertex scaling)
            SizeF scale = tmxObjectTile.GetTileObjectScale();
            xmlTileObjectRoot.SetAttributeValue("scaleX", scale.Width);
            xmlTileObjectRoot.SetAttributeValue("scaleY", scale.Height);

            // Need another transform to help us with flipping of the tile (and their collisions)
            XElement xmlTileObject = new XElement("GameObject");
            xmlTileObject.SetAttributeValue("name", "TileObject");

            if (tmxObjectTile.FlippedHorizontal)
            {
                xmlTileObject.SetAttributeValue("x", tmxObjectTile.Tile.TileSize.Width * Program.Scale);
                xmlTileObject.SetAttributeValue("flipX", true);
            }
            if (tmxObjectTile.FlippedVertical)
            {
                xmlTileObject.SetAttributeValue("y", tmxObjectTile.Tile.TileSize.Height * Program.Scale);
                xmlTileObject.SetAttributeValue("flipY", true);
            }

            // Add any colliders that might be on the tile
            foreach (TmxObject tmxObject in tmxObjectTile.Tile.ObjectGroup.Objects)
            {
                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    // Note: Tile objects have orthographic rectangles even in isometric orientations so no need to transform rectangle points
                    objElement = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    objElement = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, tmxObjectTile.Tile.ObjectGroup.Name);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    objElement = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    objElement = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);
                }

                if (objElement != null)
                {
                    // Objects can be offset (and we need to make up for the bottom-left corner being the origin in a TileObject)
                    objElement.SetAttributeValue("offsetX", tmxObject.Position.X * Program.Scale);
                    objElement.SetAttributeValue("offsetY", (tmxObjectTile.Size.Height - tmxObject.Position.Y) * Program.Scale);

                    xmlTileObject.Add(objElement);
                }
            }

            // Add a child for each mesh (with animation if needed)
            foreach (var mesh in tmxObjectTile.Tile.Meshes)
            {
                XElement xmlMeshObject = new XElement("GameObject");

                xmlMeshObject.SetAttributeValue("name", mesh.ObjectName);
                xmlMeshObject.SetAttributeValue("copy", mesh.UniqueMeshName);

                xmlMeshObject.SetAttributeValue("sortingLayerName", tmxObjectTile.SortingLayerName ?? tmxObjectTile.ParentObjectGroup.SortingLayerName);
                xmlMeshObject.SetAttributeValue("sortingOrder", tmxObjectTile.SortingOrder ?? tmxObjectTile.ParentObjectGroup.SortingOrder);

                // This object, that actually displays the tile, has to be bumped up to account for the bottom-left corner problem with Tile Objects in Tiled
                xmlMeshObject.SetAttributeValue("x", 0);
                xmlMeshObject.SetAttributeValue("y", tmxObjectTile.Tile.TileSize.Height * Program.Scale);

                if (mesh.FullAnimationDurationMs > 0)
                {
                    XElement xmlAnimation = new XElement("TileAnimator",
                        new XAttribute("startTimeMs", mesh.StartTimeMs),
                        new XAttribute("durationMs", mesh.DurationMs),
                        new XAttribute("fullTimeMs", mesh.FullAnimationDurationMs));
                    xmlMeshObject.Add(xmlAnimation);
                }

                xmlTileObject.Add(xmlMeshObject);
            }

            xmlTileObjectRoot.Add(xmlTileObject);
        }



    } // end class
} // end namespace

// ----------------------------------------------------------------------
// TmxAnimation.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxAnimation
    {
        public List<TmxFrame> Frames { get; private set; }
        public int TotalTimeMs { get; private set; }

        public TmxAnimation()
        {
            this.Frames = new List<TmxFrame>();
        }

        public static TmxAnimation FromXml(XElement xml, uint globalStartId)
        {
            TmxAnimation tmxAnimation = new TmxAnimation();

            foreach (var xmlFrame in xml.Elements("frame"))
            {
                TmxFrame tmxFrame = TmxFrame.FromXml(xmlFrame, globalStartId);
                tmxAnimation.Frames.Add(tmxFrame);
                tmxAnimation.TotalTimeMs += tmxFrame.DurationMs;
            }

            return tmxAnimation;
        }

        // Returns an single frame animation
        public static TmxAnimation FromTileId(uint globalTileId)
        {
            TmxAnimation tmxAnimation = new TmxAnimation();

            TmxFrame tmxFrame = TmxFrame.FromTileId(globalTileId);
            tmxAnimation.Frames.Add(tmxFrame);

            return tmxAnimation;
        }

    }
}

// ----------------------------------------------------------------------
// TmxException.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    class TmxException : Exception
    {
        public TmxException(string message)
            : base(message)
        {
        }

        public TmxException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public static void ThrowFormat(string fmt, params object[] args)
        {
            string msg = String.Format(fmt, args);
            throw new TmxException(msg);
        }

        public static void FromAttributeException(Exception inner, XElement element)
        {
            StringBuilder builder = new StringBuilder(inner.Message);
            Array.ForEach(element.Attributes().ToArray(), a => builder.AppendFormat("\n  {0}", a.ToString()));
            TmxException.ThrowFormat("Error parsing {0} attributes\n{1}", element.Name, builder.ToString());
        }

    }
}

// ----------------------------------------------------------------------
// TmxFrame.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxFrame
    {
        public uint GlobalTileId { get; private set; }
        public int DurationMs { get; private set; }

        public static TmxFrame FromTileId(uint tileId)
        {
            TmxFrame tmxFrame = new TmxFrame();
            tmxFrame.GlobalTileId = tileId;
            tmxFrame.DurationMs = 0;

            return tmxFrame;
        }

        public static TmxFrame FromXml(XElement xml, uint globalStartId)
        {
            TmxFrame tmxFrame = new TmxFrame();

            uint localTileId = TmxHelper.GetAttributeAsUInt(xml, "tileid");
            tmxFrame.GlobalTileId = localTileId + globalStartId;
            tmxFrame.DurationMs = TmxHelper.GetAttributeAsInt(xml, "duration", 100);

            return tmxFrame;
        }
    }
}

// ----------------------------------------------------------------------
// TmxHasPoints.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    interface TmxHasPoints
    {
        List<PointF> Points { get; set; }
        bool ArePointsClosed();
    }
}

// ----------------------------------------------------------------------
// TmxHasProperties.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    interface TmxHasProperties
    {
        TmxProperties Properties { get; }
    }
}

// ----------------------------------------------------------------------
// TmxHelper.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Windows.Media;
// using System.Xml;
// using System.Xml.Linq;


namespace Tiled2Unity
{
    class TmxHelper
    {
        public static string GetAttributeAsString(XElement elem, string attrName)
        {
            return elem.Attribute(attrName).Value;
        }

        public static string GetAttributeAsString(XElement elem, string attrName, string defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsString(elem, attrName);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName)
        {
            return Convert.ToInt32(elem.Attribute(attrName).Value);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName, int defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsInt(elem, attrName);
        }

        public static uint GetAttributeAsUInt(XElement elem, string attrName)
        {
            return Convert.ToUInt32(elem.Attribute(attrName).Value);
        }

        public static uint GetAttributeAsUInt(XElement elem, string attrName, uint defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsUInt(elem, attrName);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName)
        {
            return Convert.ToSingle(elem.Attribute(attrName).Value);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName, float defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsFloat(elem, attrName);
        }

        public static string GetAttributeAsFullPath(XElement elem, string attrName)
        {
            return Path.GetFullPath(elem.Attribute(attrName).Value);
        }

#if TILED_2_UNITY_LITE
        // System.Windows.Media.Color is a Microsoft-only library not supported (yet) by Mono
        // It turns out we don't need ARGB colors for Tiled2UnityLite anyhow.
        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName)
        {
            return System.Drawing.Color.FromArgb(255, 128, 128, 128);
        }

        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName, System.Drawing.Color defaultValue)
        {
            return System.Drawing.Color.FromArgb(255, 128, 128, 128);
        }
#else
        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName)
        {
            string colorString = elem.Attribute(attrName).Value;
            System.Windows.Media.Color mediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName, System.Drawing.Color defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsColor(elem, attrName);
        }
#endif

        public static T GetStringAsEnum<T>(string enumString)
        {
            enumString = enumString.Replace("-", "_");

            T value = default(T);
            try
            {
                value = (T)Enum.Parse(typeof(T), enumString, true);
            }
            catch
            {
                StringBuilder msg = new StringBuilder();
                msg.AppendFormat("Could not convert '{0}' to enum of type '{1}'\n", enumString, typeof(T).ToString());
                msg.AppendFormat("Choices are:\n");

                foreach (T t in Enum.GetValues(typeof(T)))
                {
                    msg.AppendFormat("  {0}\n", t.ToString());
                }
                TmxException.ThrowFormat(msg.ToString());
            }

            return value;
        }

        public static T GetAttributeAsEnum<T>(XElement elem, string attrName)
        {
            string enumString = elem.Attribute(attrName).Value.Replace("-", "_");
            return GetStringAsEnum<T>(enumString);
        }

        public static T GetAttributeAsEnum<T>(XElement elem, string attrName, T defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsEnum<T>(elem, attrName);
        }



    }
}

// ----------------------------------------------------------------------
// TmxImage.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public partial class TmxImage
    {
        public string AbsolutePath { get; private set; }
        public Size Size { get; private set; }
        public String TransparentColor { get; set; }

#if !TILED_2_UNITY_LITE
        public Bitmap ImageBitmap { get; private set; }
#endif
    }
}

// ----------------------------------------------------------------------
// TmxImage.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TmxImage
    {
        public static TmxImage FromXml(XElement elemImage)
        {
            TmxImage tmxImage = new TmxImage();
            tmxImage.AbsolutePath = TmxHelper.GetAttributeAsFullPath(elemImage, "source");

#if TILED_2_UNITY_LITE
            // Do not open the image in Tiled2UnityLite (due to difficulty with GDI+ in some mono installs)
            int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
            int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
            tmxImage.Size = new System.Drawing.Size(width, height);
#else
            try
            {
                tmxImage.ImageBitmap = (Bitmap)Bitmap.FromFile(tmxImage.AbsolutePath);
            }
            catch (FileNotFoundException fnf)
            {
                string msg = String.Format("Image file not found: {0}", tmxImage.AbsolutePath);
                throw new TmxException(msg, fnf);

                // Testing for when image files are missing. Just make up an image.
                //int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
                //int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
                //tmxImage.ImageBitmap = new Bitmap(width, height);
                //using (Graphics g = Graphics.FromImage(tmxImage.ImageBitmap))
                //{
                //    int color32 = tmxImage.AbsolutePath.GetHashCode();
                //    Color color = Color.FromArgb(color32);
                //    color = Color.FromArgb(255, color);
                //    using (Brush brush = new SolidBrush(color))
                //    {
                //        g.FillRectangle(brush, new Rectangle(Point.Empty, tmxImage.ImageBitmap.Size));
                //    }
                //}
            }

            tmxImage.Size = new System.Drawing.Size(tmxImage.ImageBitmap.Width, tmxImage.ImageBitmap.Height);
#endif

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor))
            {
                if (!tmxImage.TransparentColor.StartsWith("#"))
                {
                    // The hash makes it an HTML color
                    tmxImage.TransparentColor = "#" + tmxImage.TransparentColor;
                }

#if !TILED_2_UNITY_LITE
                System.Drawing.Color transColor = System.Drawing.ColorTranslator.FromHtml(tmxImage.TransparentColor);
                tmxImage.ImageBitmap.MakeTransparent(transColor);
#endif
            }

            return tmxImage;
        }
    }
}

// ----------------------------------------------------------------------
// TmxLayer.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public partial class TmxLayer : TmxLayerBase
    {
        public enum IgnoreSettings
        {
            False,      // Ingore nothing (layer fully-enabled)
            True,       // Ignore everything (like layer doesn't exist)
            Collision,  // Ignore collision on layer
            Visual,     // Ignore visual on layer
        };

        public TmxMap TmxMap { get; private set; }
        public string Name { get; private set; }
        public bool Visible { get; private set; }
        public float Opacity { get; private set; }
        public PointF Offset { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public IgnoreSettings Ignore { get; private set; }
        public uint[] TileIds { get; private set; }
        public List<TmxMesh> Meshes { get; private set; }

        public TmxLayer(TmxMap map)
        {
            this.TmxMap = map;
        }

        public uint GetTileIdAt(int x, int y)
        {
            uint tileId = GetRawTileIdAt(x, y);
            return TmxMath.GetTileIdWithoutFlags(tileId);
        }

        public uint GetRawTileIdAt(int x, int y)
        {
            Debug.Assert(x < this.Width && y < this.Height);
            Debug.Assert(x >= 0 && y >= 0);
            int index = GetTileIndex(x, y);
            return this.TileIds[index];
        }

        public int GetTileIndex(int x, int y)
        {
            return y * this.Width + x;
        }

        public bool IsExportingConvexPolygons()
        {
            // Always obey layer first
            if (this.Properties.PropertyMap.ContainsKey("unity:convex"))
            {
                return this.Properties.GetPropertyValueAsBoolean("unity:convex", true);
            }

            // Use the map next
            if (this.TmxMap.Properties.PropertyMap.ContainsKey("unity:convex"))
            {
                return this.TmxMap.Properties.GetPropertyValueAsBoolean("unity:convex", true);
            }

            // Use the program setting last
            return Program.PreferConvexPolygons;
        }

    }
}

// ----------------------------------------------------------------------
// TmxLayer.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.IO.Compression;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    // Partial class methods for building layer data from xml strings or files
    partial class TmxLayer
    {
        public static TmxLayer FromXml(XElement elem, TmxMap tmxMap)
        {
            Program.WriteVerbose(elem.ToString());
            TmxLayer tmxLayer = new TmxLayer(tmxMap);

            // Order within Xml file is import for layer types
            tmxLayer.XmlElementIndex = elem.NodesBeforeSelf().Count();

            // Have to decorate layer names in order to force them into being unique
            // Also, can't have whitespace in the name because Unity will add underscores
            tmxLayer.Name = TmxHelper.GetAttributeAsString(elem, "name");

            tmxLayer.Visible = TmxHelper.GetAttributeAsInt(elem, "visible", 1) == 1;
            tmxLayer.Opacity = TmxHelper.GetAttributeAsFloat(elem, "opacity", 1);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(elem, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(elem, "offsety", 0);
            tmxLayer.Offset = offset;

            // Set our properties
            tmxLayer.Properties = TmxProperties.FromXml(elem);

            // Set the "ignore" setting on this layer
            tmxLayer.Ignore = tmxLayer.Properties.GetPropertyValueAsEnum<IgnoreSettings>("unity:ignore", IgnoreSettings.False);

            // We can build a layer from a "tile layer" (default) or an "image layer"
            if (elem.Name == "layer")
            {
                tmxLayer.Width = TmxHelper.GetAttributeAsInt(elem, "width");
                tmxLayer.Height = TmxHelper.GetAttributeAsInt(elem, "height");
                tmxLayer.ParseData(elem.Element("data"));
            }
            else if (elem.Name == "imagelayer")
            {
                XElement xmlImage = elem.Element("image");
                if (xmlImage == null)
                {
                    Program.WriteWarning("Image Layer '{0}' is being ignored since it has no image.", tmxLayer.Name);
                    tmxLayer.Ignore = IgnoreSettings.True;
                    return tmxLayer;
                }

                // An image layer is sort of like an tile layer but with just one tile
                tmxLayer.Width = 1;
                tmxLayer.Height = 1;

                // Find the "tile" that matches our image
                string imagePath = TmxHelper.GetAttributeAsFullPath(elem.Element("image"), "source");
                TmxTile tile = tmxMap.Tiles.First(t => t.Value.TmxImage.AbsolutePath == imagePath).Value;
                tmxLayer.TileIds = new uint[1] { tile.GlobalId };

                // The image layer needs to be tranlated in an interesting way when expressed as a tile layer
                PointF translated = tmxLayer.Offset;

                // Make up for height of a regular tile in the map
                translated.Y -= (float)tmxMap.TileHeight;

                // Make up for the height of this image
                translated.Y += (float)tile.TmxImage.Size.Height;

                // Correct for any orientation effects on the map (like isometric)
                // (We essentially undo the translation via orientation here)
                PointF orientation = TmxMath.TileCornerInScreenCoordinates(tmxMap, 0, 0);
                translated.X -= orientation.X;
                translated.Y -= orientation.Y;

                // Translate by the x and y coordiantes
                translated.X += TmxHelper.GetAttributeAsFloat(elem, "x", 0);
                translated.Y += TmxHelper.GetAttributeAsFloat(elem, "y", 0);
                tmxLayer.Offset = translated;
            }

            // Each layer will be broken down into "meshes" which are collections of tiles matching the same texture or animation
            tmxLayer.Meshes = TmxMesh.ListFromTmxLayer(tmxLayer);

            return tmxLayer;
        }

        private void ParseData(XElement elem)
        {
            Program.WriteLine("Parse {0} layer data ...", this.Name);
            Program.WriteVerbose(elem.ToString());

            string encoding = TmxHelper.GetAttributeAsString(elem, "encoding", "");
            string compression = TmxHelper.GetAttributeAsString(elem, "compression", "");
            if (elem.Element("tile") != null)
            {
                ParseTileDataAsXml(elem);
            }
            else if (encoding == "csv")
            {
                ParseTileDataAsCsv(elem);
            }
            else if (encoding == "base64" && String.IsNullOrEmpty(compression))
            {
                ParseTileDataAsBase64(elem);
            }
            else if (encoding == "base64" && compression == "gzip")
            {
                ParseTileDataAsBase64GZip(elem);
            }
            else if (encoding == "base64" && compression == "zlib")
            {
                ParseTileDataAsBase64Zlib(elem);
            }
            else
            {
                TmxException.ThrowFormat("Unsupported schema for {0} layer data", this.Name);
            }
        }

        private void ParseTileDataAsXml(XElement elemData)
        {
            Program.WriteLine("Parsing layer data as Xml elements ...");
            var tiles = from t in elemData.Elements("tile")
                        select TmxHelper.GetAttributeAsUInt(t, "gid");
            this.TileIds = tiles.ToArray();
        }

        private void ParseTileDataAsCsv(XElement elem)
        {
            Program.WriteLine("Parsing layer data as CSV ...");
            var datum = from val in elem.Value.Split(',')
                        select Convert.ToUInt32(val);
            this.TileIds = datum.ToArray();
        }

        private void ParseTileDataAsBase64(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 string ...");
            byte[] bytes = Convert.FromBase64String(elem.Value);
            BytesToTiles(bytes);
        }

        private void ParseTileDataAsBase64GZip(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 gzip-compressed string ...");
            byte[] bytesCompressed = Convert.FromBase64String(elem.Value);

            MemoryStream streamCompressed = new MemoryStream(bytesCompressed);

            // Now, decompress the bytes
            using (MemoryStream streamDecompressed = new MemoryStream())
            using (GZipStream deflateStream = new GZipStream(streamCompressed, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(streamDecompressed);
                byte[] bytesDecompressed = streamDecompressed.ToArray();
                BytesToTiles(bytesDecompressed);
            }
        }

        private void ParseTileDataAsBase64Zlib(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 zlib-compressed string ...");
            byte[] bytesCompressed = Convert.FromBase64String(elem.Value);

            MemoryStream streamCompressed = new MemoryStream(bytesCompressed);

            // Nasty trick: Have to read past the zlib stream header
            streamCompressed.ReadByte();
            streamCompressed.ReadByte();

            // Now, decompress the bytes
            using (MemoryStream streamDecompressed = new MemoryStream())
            using (DeflateStream deflateStream = new DeflateStream(streamCompressed, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(streamDecompressed);
                byte[] bytesDecompressed = streamDecompressed.ToArray();
                BytesToTiles(bytesDecompressed);
            }
        }

        private void BytesToTiles(byte[] bytes)
        {
            this.TileIds = new uint[bytes.Length / 4];
            for (int i = 0; i < this.TileIds.Count(); ++i)
            {
                this.TileIds[i] = BitConverter.ToUInt32(bytes, i * 4);
            }
        }

    }
}

// ----------------------------------------------------------------------
// TmxLayerBase.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // There are several different "layer" types in Tiled that share some behaviour (tile layer, object layer, image layer)
    // (In Tiled2Unity we treat image layers as a special case of tile layer)
    public class TmxLayerBase : TmxHasProperties
    {
        public TmxProperties Properties { get; protected set; }

        public int XmlElementIndex { get; protected set; }

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }
    }
}

// ----------------------------------------------------------------------
// TmxMap.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;


namespace Tiled2Unity
{
    public partial class TmxMap : TmxHasProperties
    {
        public delegate void ReadTmxFileCompleted(TmxMap tmxMap);
        public static event ReadTmxFileCompleted OnReadTmxFileCompleted;

        public enum MapOrientation
        {
            Orthogonal,
            Isometric,
            Staggered,
            Hexagonal,
        }

        public enum MapStaggerAxis
        {
            X,
            Y,
        }

        public enum MapStaggerIndex
        {
            Odd,
            Even,
        }

        public string Name { get; private set; }
        public MapOrientation Orientation { get; private set; }
        public MapStaggerAxis StaggerAxis { get; private set; }
        public MapStaggerIndex StaggerIndex { get; private set; }
        public int HexSideLength { get; set; }
        public int DrawOrderHorizontal { get; private set; }
        public int DrawOrderVertical { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileWidth { get; private set; }
        public int TileHeight { get; private set; }
        public Color BackgroundColor { get; private set; }
        public TmxProperties Properties { get; private set; }

        public IDictionary<uint, TmxTile> Tiles = new Dictionary<uint, TmxTile>();

        public IList<TmxLayer> Layers = new List<TmxLayer>();
        public IList<TmxObjectGroup> ObjectGroups = new List<TmxObjectGroup>();

        private uint nextUniqueId = 0;

        public override string ToString()
        {
            return String.Format("{{ \"{6}\" size = {0}x{1}, tile size = {2}x{3}, # tiles = {4}, # layers = {5}, # obj groups = {6} }}",
                this.Width,
                this.Height,
                this.TileWidth,
                this.TileHeight,
                this.Tiles.Count(),
                this.Layers.Count(),
                this.ObjectGroups.Count(),
                this.Name);
        }

        public TmxTile GetTileFromTileId(uint tileId)
        {
            if (tileId == 0)
                return null;

            tileId = TmxMath.GetTileIdWithoutFlags(tileId);
            return this.Tiles[tileId];
        }

        public Point GetMapPositionAt(int x, int y)
        {
            return TmxMath.TileCornerInScreenCoordinates(this, x, y);
        }

        public Point GetMapPositionAt(int x, int y, TmxTile tile)
        {
            Point point = GetMapPositionAt(x, y);

            // The tile may have different dimensions than the cells of the map so correct for that
            // In this case, the y-position needs to be adjusted
            point.Y = (point.Y + this.TileHeight) - tile.TileSize.Height;

            return point;
        }

        // Get a unique Id tied to this map instance.
        public uint GetUniqueId()
        {
            return ++this.nextUniqueId;
        }

        public Size MapSizeInPixels()
        {
            // Takes the orientation of the map into account when calculating the size
            if (this.Orientation == MapOrientation.Isometric)
            {
                Size size = Size.Empty;
                size.Width = (this.Width + this.Height) * this.TileWidth / 2;
                size.Height = (this.Width + this.Height) * this.TileHeight / 2;
                return size;
            }
            else if (this.Orientation == MapOrientation.Staggered || this.Orientation == MapOrientation.Hexagonal)
            {
                int tileHeight = this.TileHeight & ~1;
                int tileWidth = this.TileWidth & ~1;

                if (this.StaggerAxis == MapStaggerAxis.Y)
                {
                    int halfHexLeftover = (tileHeight - this.HexSideLength) / 2;

                    Size size = Size.Empty;
                    size.Width = (tileWidth * this.Width) + tileWidth / 2;
                    size.Height = (halfHexLeftover + this.HexSideLength) * this.Height + halfHexLeftover;
                    return size;
                }
                else
                {
                    int halfHexLeftover = (tileWidth - this.HexSideLength) / 2;

                    Size size = Size.Empty;
                    size.Width = (halfHexLeftover + this.HexSideLength) * this.Width + halfHexLeftover;
                    size.Height = (tileHeight * this.Height) + tileHeight / 2;
                    return size;
                }
            }

            // Default orientation (orthongonal)
            return new Size(this.Width * this.TileWidth, this.Height * this.TileHeight);
        }

        // Get a unique list of all the tiles that are used as tile objects
        public List<TmxMesh> GetUniqueListOfVisibleObjectTileMeshes()
        {
            var tiles = from objectGroup in this.ObjectGroups
                        where objectGroup.Visible == true
                        from tmxObject in objectGroup.Objects
                        where tmxObject.Visible == true
                        let tmxObjectTile = tmxObject as TmxObjectTile
                        where tmxObjectTile != null
                        from tmxMesh in tmxObjectTile.Tile.Meshes
                        select tmxMesh;

            // Make list unique based on mesh name
            return tiles.GroupBy(m => m.UniqueMeshName).Select(g => g.First()).ToList();
        }

    }
}

// ----------------------------------------------------------------------
// TmxMap.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    // Partial class methods for creating TmxMap data from xml files/data
    partial class TmxMap
    {
        public static TmxMap LoadFromFile(string tmxPath)
        {
            string fullTmxPath = Path.GetFullPath(tmxPath);
            using (ChDir chdir = new ChDir(fullTmxPath))
            {
                TmxMap tmxMap = new TmxMap();
                XDocument doc = tmxMap.LoadDocument(fullTmxPath);

                tmxMap.Name = Path.GetFileNameWithoutExtension(fullTmxPath);
                tmxMap.ParseMapXml(doc);

                // We're done reading and parsing the tmx file
                Program.WriteLine("Map details: {0}", tmxMap.ToString());
                Program.WriteSuccess("Finished parsing file: {0}", fullTmxPath);

                // Let listeners know of our success
                if (TmxMap.OnReadTmxFileCompleted != null)
                {
                    TmxMap.OnReadTmxFileCompleted(tmxMap);
                }

                return tmxMap;
            }
        }

        private XDocument LoadDocument(string xmlPath)
        {
            XDocument doc = null;
            Program.WriteLine("Opening {0} ...", xmlPath);
            try
            {
                doc = XDocument.Load(xmlPath);
            }
            catch (FileNotFoundException fnf)
            {
                string msg = String.Format("File not found: {0}", fnf.FileName);
                throw new TmxException(msg, fnf);
            }
            catch (XmlException xml)
            {
                string msg = String.Format("Xml error in {0}\n  {1}", xmlPath, xml.Message);
                throw new TmxException(msg, xml);
            }
            return doc;
        }

        private void ParseMapXml(XDocument doc)
        {
            Program.WriteLine("Parsing map root ...");
            //Program.WriteVerbose(doc.ToString()); // Some TMX files are far too big (cause out of memory exception) so don't do this
            XElement map = doc.Element("map");
            try
            {
                this.Orientation = TmxHelper.GetAttributeAsEnum<MapOrientation>(map, "orientation");
                this.StaggerAxis = TmxHelper.GetAttributeAsEnum(map, "staggeraxis", MapStaggerAxis.Y);
                this.StaggerIndex = TmxHelper.GetAttributeAsEnum(map, "staggerindex", MapStaggerIndex.Odd);
                this.HexSideLength = TmxHelper.GetAttributeAsInt(map, "hexsidelength", 0);
                this.DrawOrderHorizontal = TmxHelper.GetAttributeAsString(map, "renderorder", "right-down").Contains("right") ? 1 : -1;
                this.DrawOrderVertical = TmxHelper.GetAttributeAsString(map, "renderorder", "right-down").Contains("down") ? 1 : -1;
                this.Width = TmxHelper.GetAttributeAsInt(map, "width");
                this.Height = TmxHelper.GetAttributeAsInt(map, "height");
                this.TileWidth = TmxHelper.GetAttributeAsInt(map, "tilewidth");
                this.TileHeight = TmxHelper.GetAttributeAsInt(map, "tileheight");
                this.BackgroundColor = TmxHelper.GetAttributeAsColor(map, "backgroundcolor", Color.FromArgb(128, 128, 128));
            }
            catch (Exception e)
            {
                TmxException.FromAttributeException(e, map);
            }

            // Collect our map properties
            this.Properties = TmxProperties.FromXml(map);

            ParseAllTilesets(doc);
            ParseAllLayers(doc);
            ParseAllObjectGroups(doc);

            // Once everything is loaded, take a moment to do additional plumbing
            ParseCompleted();
        }

        private void ParseAllTilesets(XDocument doc)
        {
            Program.WriteLine("Parsing tileset elements ...");
            var tilesets = from item in doc.Descendants("tileset")
                           select item;

            foreach (var ts in tilesets)
            {
                ParseSingleTileset(ts);
            }

            // Treat images in imagelayers as tileset with a single entry
            var imageLayers = from item in doc.Descendants("imagelayer") select item;
            foreach (var il in imageLayers)
            {
                ParseTilesetFromImageLayer(il);
            }
        }

        private void ParseSingleTileset(XElement elem)
        {
            // Parse the tileset data and populate the tiles from it
            uint firstId = TmxHelper.GetAttributeAsUInt(elem, "firstgid");

            // Does the element contain all tileset data or reference an external tileset?
            XAttribute attrSource = elem.Attribute("source");
            if (attrSource == null)
            {
                ParseInternalTileset(elem, firstId);
            }
            else
            {
                // Need to load the tileset data from an external file first
                // Then we'll parse it as if it's internal data
                Program.WriteVerbose(elem.ToString());
                ParseExternalTileset(attrSource.Value, firstId);
            }
        }

        // This method is called eventually for external tilesets too
        // Only the gid attribute has been consumed at this point for the tileset
        private void ParseInternalTileset(XElement elemTileset, uint firstId)
        {
            string tilesetName = TmxHelper.GetAttributeAsString(elemTileset, "name");

            Program.WriteLine("Parse internal tileset '{0}' (gid = {1}) ...", tilesetName, firstId);
            Program.WriteVerbose(elemTileset.ToString());

            int tileWidth = TmxHelper.GetAttributeAsInt(elemTileset, "tilewidth");
            int tileHeight = TmxHelper.GetAttributeAsInt(elemTileset, "tileheight");
            int spacing = TmxHelper.GetAttributeAsInt(elemTileset, "spacing", 0);
            int margin = TmxHelper.GetAttributeAsInt(elemTileset, "margin", 0);

            PointF tileOffset = PointF.Empty;
            XElement xmlTileOffset = elemTileset.Element("tileoffset");
            if (xmlTileOffset != null)
            {
                tileOffset.X = TmxHelper.GetAttributeAsInt(xmlTileOffset, "x");
                tileOffset.Y = TmxHelper.GetAttributeAsInt(xmlTileOffset, "y");
            }

            IList<TmxTile> tilesToAdd = new List<TmxTile>();

            // Tilesets may have an image for all tiles within it, or it may have an image per tile
            if (elemTileset.Element("image") != null)
            {
                TmxImage tmxImage = TmxImage.FromXml(elemTileset.Element("image"));

                // Create all the tiles
                // This is a bit complicated because of spacing and margin
                // (Margin is ignored from Width and Height)
                for (int end_y = margin + tileHeight; end_y <= tmxImage.Size.Height; end_y += spacing + tileHeight)
                {
                    for (int end_x = margin + tileWidth; end_x <= tmxImage.Size.Width; end_x += spacing + tileWidth)
                    {
                        uint localId = (uint) tilesToAdd.Count();
                        uint globalId = firstId + localId;
                        TmxTile tile = new TmxTile(globalId, localId, tilesetName, tmxImage);
                        tile.Offset = tileOffset;
                        tile.SetTileSize(tileWidth, tileHeight);
                        tile.SetLocationOnSource(end_x - tileWidth, end_y - tileHeight);
                        tilesToAdd.Add(tile);
                    }
                }
            }
            else
            {
                // Each tile will have it's own image
                foreach (var t in elemTileset.Elements("tile"))
                {
                    TmxImage tmxImage = TmxImage.FromXml(t.Element("image"));

                    uint localId = (uint)tilesToAdd.Count();

                    // Local Id can be overridden by the tile element
                    // This is because tiles can be removed from the tileset, so we won'd always have a zero-based index
                    localId = TmxHelper.GetAttributeAsUInt(t, "id", localId);

                    uint globalId = firstId + localId;
                    TmxTile tile = new TmxTile(globalId, localId, tilesetName, tmxImage);
                    tile.Offset = tileOffset;
                    tile.SetTileSize(tmxImage.Size.Width, tmxImage.Size.Height);
                    tile.SetLocationOnSource(0, 0);
                    tilesToAdd.Add(tile);
                }
            }

            StringBuilder builder = new StringBuilder();
            foreach (TmxTile tile in tilesToAdd)
            {
                builder.AppendFormat("{0}", tile.ToString());
                if (tile != tilesToAdd.Last()) builder.Append("\n");
                this.Tiles[tile.GlobalId] = tile;
            }
            Program.WriteLine("Added {0} tiles", tilesToAdd.Count);
            Program.WriteVerbose(builder.ToString());

            // Add any extra data to tiles
            foreach (var elemTile in elemTileset.Elements("tile"))
            {
                int localTileId = TmxHelper.GetAttributeAsInt(elemTile, "id");
                var tiles = from t in this.Tiles
                            where t.Value.GlobalId == localTileId + firstId
                            select t.Value;

                // Note that some old tile data may be sticking around
                if (tiles.Count() == 0)
                {
                    Program.WriteWarning("Tile '{0}' in tileset '{1}' does not exist but there is tile data for it.\n{2}", localTileId, tilesetName, elemTile.ToString());
                }
                else
                {
                    tiles.First().ParseTileXml(elemTile, this, firstId);
                }
            }
        }

        private void ParseExternalTileset(string tsxPath, uint firstId)
        {
            string fullTsxPath = Path.GetFullPath(tsxPath);
            using (ChDir chdir = new ChDir(fullTsxPath))
            {
                XDocument tsx = LoadDocument(fullTsxPath);
                ParseInternalTileset(tsx.Root, firstId);
            }
        }

        private void ParseTilesetFromImageLayer(XElement elemImageLayer)
        {
            string tilesetName = TmxHelper.GetAttributeAsString(elemImageLayer, "name");

            XElement xmlImage = elemImageLayer.Element("image");
            if (xmlImage == null)
            {
                Program.WriteWarning("Image Layer '{0}' has no image assigned.", tilesetName);
                return;
            }

            TmxImage tmxImage = TmxImage.FromXml(xmlImage);

            // The "firstId" is is always one more than all the tiles that we've already parsed (which may be zero)
            uint firstId = 1;
            if (this.Tiles.Count > 0)
            {
                firstId = this.Tiles.Max(t => t.Key) + 1;
            }
            
            uint localId = 1;
            uint globalId = firstId + localId;

            TmxTile tile = new TmxTile(globalId, localId, tilesetName, tmxImage);
            tile.SetTileSize(tmxImage.Size.Width, tmxImage.Size.Height);
            tile.SetLocationOnSource(0, 0);
            this.Tiles[tile.GlobalId] = tile;
        }

        private void ParseAllLayers(XDocument doc)
        {
            Program.WriteLine("Parsing layer elements ...");

            // Parse "layer"s and "imagelayer"s
            var layers = (from item in doc.Descendants()
                          where (item.Name == "layer" || item.Name == "imagelayer")
                          select item).ToList();

            foreach (var lay in layers)
            {
                TmxLayer tmxLayer = TmxLayer.FromXml(lay, this);

                // Layers may be ignored
                if (tmxLayer.Ignore == TmxLayer.IgnoreSettings.True)
                {
                    // We don't care about this layer
                    Program.WriteLine("Ignoring layer due to unity:ignore = True property: {0}", tmxLayer.Name);
                    continue;
                }

                this.Layers.Add(tmxLayer);
            }
        }

        private void ParseAllObjectGroups(XDocument doc)
        {
            Program.WriteLine("Parsing objectgroup elements ...");
            var groups = from item in doc.Root.Elements("objectgroup")
                         select item;

            foreach (var g in groups)
            {
                TmxObjectGroup tmxObjectGroup = TmxObjectGroup.FromXml(g, this);
                this.ObjectGroups.Add(tmxObjectGroup);
            }
        }

        private void ParseCompleted()
        {
            // Every "layer type" instance needs its sort ordering figured out
            var layers = new List<TmxLayerBase>();
            layers.AddRange(this.Layers);
            layers.AddRange(this.ObjectGroups);

            // We sort by the XmlElementIndex because the order in the XML file is the implicity ordering or how tiles and objects are rendered
            layers = layers.OrderBy(l => l.XmlElementIndex).ToList();

            for (int i = 0; i < layers.Count(); ++i)
            {
                TmxLayerBase layer = layers[i];
                layer.SortingLayerName = layer.Properties.GetPropertyValueAsString("unity:sortingLayerName", "");
                layer.SortingOrder = layer.Properties.GetPropertyValueAsInt("unity:sortingOrder", i);
            }
        }

    }
}

// ----------------------------------------------------------------------
// TmxMath.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

// Helper utitlities for performing math within a Tiled context
namespace Tiled2Unity
{
    class TmxMath
    {
        static public readonly uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        static public readonly uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
        static public readonly uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;

        static public uint GetTileIdWithoutFlags(uint tileId)
        {
            return tileId & ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG);
        }

        static public bool IsTileFlippedDiagonally(uint tileId)
        {
            return (tileId & FLIPPED_DIAGONALLY_FLAG) != 0;
        }

        static public bool IsTileFlippedHorizontally(uint tileId)
        {
            return (tileId & FLIPPED_HORIZONTALLY_FLAG) != 0;
        }

        static public bool IsTileFlippedVertically(uint tileId)
        {
            return (tileId & FLIPPED_VERTICALLY_FLAG) != 0;
        }

        static public void RotatePoints(PointF[] points, TmxObject tmxObject)
        {
            TranslatePoints(points, -tmxObject.Position.X, -tmxObject.Position.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix(-tmxObject.Rotation);
            rotate.TransformPoints(points);

            TranslatePoints(points, tmxObject.Position.X, tmxObject.Position.Y);
        }

        static public void TransformPoints(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            // Put the points into origin/local space
            TranslatePoints(points, -origin.X, -origin.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix();

            // Apply the flips/rotations (order matters)
            if (horizontal)
            {
                TmxRotationMatrix h = new TmxRotationMatrix(-1, 0, 0, 1);
                rotate = TmxRotationMatrix.Multiply(h, rotate);
            }
            if (vertical)
            {
                TmxRotationMatrix v = new TmxRotationMatrix(1, 0, 0, -1);
                rotate = TmxRotationMatrix.Multiply(v, rotate);
            }
            if (diagonal)
            {
                TmxRotationMatrix d = new TmxRotationMatrix(0, 1, 1, 0);
                rotate = TmxRotationMatrix.Multiply(d, rotate);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            TranslatePoints(points, origin.X, origin.Y);
        }

        // Hack function to do diaonal flip first in transformations
        static public void TransformPoints_DiagFirst(PointF[] points, PointF origin, bool diagonal, bool horizontal, bool vertical)
        {
            // Put the points into origin/local space
            TranslatePoints(points, -origin.X, -origin.Y);

            TmxRotationMatrix rotate = new TmxRotationMatrix();

            // Apply the flips/rotations (order matters)
            if (diagonal)
            {
                TmxRotationMatrix d = new TmxRotationMatrix(0, 1, 1, 0);
                rotate = TmxRotationMatrix.Multiply(d, rotate);
            }
            if (horizontal)
            {
                TmxRotationMatrix h = new TmxRotationMatrix(-1, 0, 0, 1);
                rotate = TmxRotationMatrix.Multiply(h, rotate);
            }
            if (vertical)
            {
                TmxRotationMatrix v = new TmxRotationMatrix(1, 0, 0, -1);
                rotate = TmxRotationMatrix.Multiply(v, rotate);
            }

            // Apply the combined flip/rotate transformation
            rotate.TransformPoints(points);

            // Put points back into world space
            TranslatePoints(points, origin.X, origin.Y);
        }

        static public void TranslatePoints(PointF[] points, float tx, float ty)
        {
            TranslatePoints(points, new PointF(tx, ty));
        }

        static public void TranslatePoints(PointF[] points, PointF translate)
        {
            SizeF trans = new SizeF(translate.X, translate.Y);
            for (int p = 0; p < points.Length; ++p)
            {
                points[p] = PointF.Add(points[p], trans);
            }
        }

        static public bool DoStaggerX(TmxMap tmxMap, int x)
        {
            int staggerX = (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X) ? 1 : 0;
            int staggerEven = (tmxMap.StaggerIndex == TmxMap.MapStaggerIndex.Even) ? 1 : 0;

            return staggerX != 0 && ((x & 1) ^ staggerEven) != 0;
        }

        static public bool DoStaggerY(TmxMap tmxMap, int y)
        {
            int staggerX = (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X) ? 1 : 0;
            int staggerEven = (tmxMap.StaggerIndex == TmxMap.MapStaggerIndex.Even) ? 1 : 0;

            return staggerX == 0 && ((y & 1) ^ staggerEven) != 0;
        }

        static public Point TileCornerInGridCoordinates(TmxMap tmxMap, int x, int y)
        {
            // Support different map display types (orthographic, isometric, etc..)
            // Note: simulates "tileToScreenCoords" function from Tiled source
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Point point = Point.Empty;

                int origin_x = tmxMap.Height * tmxMap.TileWidth / 2;
                point.X = (x - y) * tmxMap.TileWidth / 2 + origin_x;
                point.Y = (x + y) * tmxMap.TileHeight / 2;

                return point;
            }
            else if (tmxMap.Orientation == TmxMap.MapOrientation.Staggered || tmxMap.Orientation == TmxMap.MapOrientation.Hexagonal)
            {
                Point point = Point.Empty;

                int tileWidth = tmxMap.TileWidth & ~1;
                int tileHeight = tmxMap.TileHeight & ~1;

                int sideLengthX = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X ? tmxMap.HexSideLength : 0;
                int sideLengthY = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y ? tmxMap.HexSideLength : 0;

                int sideOffsetX = (tileWidth - sideLengthX) / 2;
                int sideOffsetY = (tileHeight - sideLengthY) / 2;

                int columnWidth = sideOffsetX + sideLengthX;
                int rowHeight = sideOffsetY + sideLengthY;

                if (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X)
                {
                    point.Y = y * (tileHeight + sideLengthY);
                    if (TmxMath.DoStaggerX(tmxMap, x))
                    {
                        point.Y += rowHeight;
                    }

                    point.X = x * columnWidth;
                }
                else
                {
                    point.X = x * (tileWidth + sideLengthX);
                    if (TmxMath.DoStaggerY(tmxMap, y))
                    {
                        point.X += columnWidth;
                    }

                    point.Y = y * rowHeight;
                }

                point.Offset(tileWidth / 2, 0);
                return point;
            }

            // Default orthographic orientation
            return new Point(x * tmxMap.TileWidth, y * tmxMap.TileHeight);
        }

        static public Point TileCornerInScreenCoordinates(TmxMap tmxMap, int x, int y)
        {
            Point point = TileCornerInGridCoordinates(tmxMap, x, y);

            if (tmxMap.Orientation != TmxMap.MapOrientation.Orthogonal)
            {
                point.Offset(-tmxMap.TileWidth / 2, 0);
            }

            return point;
        }

        static public PointF ObjectPointFToMapSpace(TmxMap tmxMap, float x, float y)
        {
            return ObjectPointFToMapSpace(tmxMap, new PointF(x, y));
        }

        static public PointF ObjectPointFToMapSpace(TmxMap tmxMap, PointF pt)
        {
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                PointF xf = PointF.Empty;

                float origin_x = tmxMap.Height * tmxMap.TileWidth * 0.5f;
                float tile_y = pt.Y / tmxMap.TileHeight;
                float tile_x = pt.X / tmxMap.TileHeight;

                xf.X = (tile_x - tile_y) * tmxMap.TileWidth * 0.5f + origin_x;
                xf.Y = (tile_x + tile_y) * tmxMap.TileHeight * 0.5f;
                return xf;
            }

            // Other maps types don't transform object points
            return pt;
        }


        public static Point AddPoints(Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        public static PointF AddPoints(PointF a, PointF b)
        {
            return new PointF(a.X + b.X, a.Y + b.Y);
        }

        public static PointF ScalePoints(PointF p, float s)
        {
            return new PointF(p.X * s, p.Y * s);
        }

        public static List<PointF> GetPointsInMapSpace(TmxMap tmxMap, TmxHasPoints objectWithPoints)
        {
            PointF local = TmxMath.ObjectPointFToMapSpace(tmxMap, 0, 0);
            local.X = -local.X;
            local.Y = -local.Y;

            List<PointF> xfPoints = objectWithPoints.Points.Select(pt => TmxMath.ObjectPointFToMapSpace(tmxMap, pt)).ToList();
            xfPoints = xfPoints.Select(pt => TmxMath.AddPoints(pt, local)).ToList();
            return xfPoints;
        }

        // We don't want ugly floating point issues. Take for granted that sanitized values can be rounded to nearest 1/256th of value
        public static float Sanitize(float v)
        {
            return (float)Math.Round(v * 256) / 256.0f;
        }

        public static PointF Sanitize(PointF pt)
        {
            return new PointF(Sanitize(pt.X), Sanitize(pt.Y));
        }
    }
}

// ----------------------------------------------------------------------
// TmxMesh.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // There are no mesh components to a TMX file, this is for convenience in mesh-ifying Tiled layers
    public class TmxMesh
    {
        // Unity meshes have a limit on the number of vertices they can contain (65534)
        // Each face of a mesh has 4 vertices so we are limited to 65534 / 4 = 16383 faces
        // Note: In some cases, Unity still splits up a mesh (incorrectly) into "1 parts" with 16383 faces so we go with 16382 faces to be extra safe.
        private static readonly int MaxNumberOfTiles = 16382;

        public string UniqueMeshName { get; private set; }
        public string ObjectName { get; private set; }
        public TmxImage TmxImage { get; private set; }
        public uint[] TileIds { get; private set; }
        public int NumberOfTiles { get; private set; }

        // Animation properties
        public int StartTimeMs { get; private set; }
        public int DurationMs { get; private set; }
        public int FullAnimationDurationMs { get; private set; }

        public bool IsMeshFull()
        {
            return this.NumberOfTiles >= TmxMesh.MaxNumberOfTiles;
        }

        private void AddTile(int index, uint tileId)
        {
            // Assumes non-zero tileIdss
            this.TileIds[index] = tileId;
            this.NumberOfTiles++;
        }

        // Splits a layer into TmxMesh instances
        public static List<TmxMesh> ListFromTmxLayer(TmxLayer layer)
        {
            List<TmxMesh> meshes = new List<TmxMesh>();

            for (int i = 0; i < layer.TileIds.Count(); ++i)
            {
                // Copy the tile unto the mesh that uses the same image
                // (In other words, we are grouping tiles by images into a mesh)
                uint tileId = layer.TileIds[i];
                TmxTile tile = layer.TmxMap.GetTileFromTileId(tileId);
                if (tile == null)
                    continue;

                int timeMs = 0;
                foreach (var frame in tile.Animation.Frames)
                {
                    uint frameTileId = frame.GlobalTileId;

                    // Have to put any rotations/flipping from the source tile into this one
                    frameTileId |= (tileId & TmxMath.FLIPPED_HORIZONTALLY_FLAG);
                    frameTileId |= (tileId & TmxMath.FLIPPED_VERTICALLY_FLAG);
                    frameTileId |= (tileId & TmxMath.FLIPPED_DIAGONALLY_FLAG);

                    // Find a mesh to stick this tile into (if it exists)
                    TmxMesh mesh = meshes.Find(m => m.CanAddFrame(tile, timeMs, frame.DurationMs));
                    if (mesh == null)
                    {
                        // Create a new mesh and add it to our list
                        mesh = new TmxMesh();
                        mesh.TileIds = new uint[layer.TileIds.Count()];
                        mesh.UniqueMeshName = String.Format("mesh_{0}", layer.TmxMap.GetUniqueId().ToString("D4"));
                        mesh.TmxImage = tile.TmxImage;

                        // Keep track of the timing for this mesh (non-animating meshes will have a start time and duration of 0)
                        mesh.StartTimeMs = timeMs;
                        mesh.DurationMs = frame.DurationMs;
                        mesh.FullAnimationDurationMs = tile.Animation.TotalTimeMs;

                        mesh.ObjectName = Path.GetFileNameWithoutExtension(tile.TmxImage.AbsolutePath);
                        if (mesh.DurationMs != 0)
                        {
                            // Decorate the name a bit with some animation details for the frame
                            mesh.ObjectName += string.Format("[{0}-{1}]", timeMs, timeMs + mesh.DurationMs);
                        }

                        meshes.Add(mesh);
                    }

                    // This mesh contains this tile
                    mesh.AddTile(i, frameTileId);

                    // Advance time
                    timeMs += frame.DurationMs;
                }
            }

            return meshes;
        }

        // Creates a TmxMesh from a tile (for tile objects)
        public static List<TmxMesh> FromTmxTile(TmxTile tmxTile, TmxMap tmxMap)
        {
            List<TmxMesh> meshes = new List<TmxMesh>();

            int timeMs = 0;
            foreach (var frame in tmxTile.Animation.Frames)
            {
                uint frameTileId = frame.GlobalTileId;
                TmxTile frameTile = tmxMap.Tiles[frameTileId];

                TmxMesh mesh = new TmxMesh();
                mesh.TileIds = new uint[1];
                mesh.TileIds[0] = frameTileId;

                mesh.UniqueMeshName = String.Format("mesh_tile_{0}", TmxMath.GetTileIdWithoutFlags(frameTileId).ToString("D4"));
                mesh.TmxImage = frameTile.TmxImage;
                mesh.ObjectName = "tile_obj";

                // Keep track of the timing for this mesh (non-animating meshes will have a start time and duration of 0)
                mesh.StartTimeMs = timeMs;
                mesh.DurationMs = frame.DurationMs;
                mesh.FullAnimationDurationMs = tmxTile.Animation.TotalTimeMs;

                if (mesh.DurationMs != 0)
                {
                    // Decorate the name a bit with some animation details for the frame
                    mesh.ObjectName += string.Format("[{0}-{1}]", timeMs, timeMs + mesh.DurationMs);
                }

                // Advance time
                timeMs += frame.DurationMs;

                // Add the animation frame to our list of meshes
                meshes.Add(mesh);
            }

            return meshes;
        }

        private bool CanAddFrame(TmxTile tile, int startMs, int durationMs)
        {
            if (IsMeshFull())
                return false;

            if (this.TmxImage != tile.TmxImage)
                return false;

            if (this.StartTimeMs != startMs)
                return false;

            if (this.DurationMs != durationMs)
                return false;

            return true;
        }

    }
}

// ----------------------------------------------------------------------
// TmxObject.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public abstract partial class TmxObject : TmxHasProperties
    {
        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool Visible { get; private set; }
        public PointF Position { get; private set; }
        public SizeF Size { get; private set; }
        public float Rotation { get; private set; }
        public TmxProperties Properties { get; private set; }
        public TmxObjectGroup ParentObjectGroup { get; private set; }

        public string GetNonEmptyName()
        {
            if (String.IsNullOrEmpty(this.Name))
                return InternalGetDefaultName();
            return this.Name;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} pos={2}, size={3} rot = {4}", GetType().Name, GetNonEmptyName(), this.Position, this.Size, this.Rotation);
        }

        public void BakeRotation()
        {
            // Rotate (0, 0)
            PointF[] pointfs = new PointF[1] { PointF.Empty };
            TmxMath.RotatePoints(pointfs, this);

            // Bake that rotation into our position, sanitizing the result
            float x = this.Position.X - pointfs[0].X;
            float y = this.Position.Y - pointfs[0].Y;
            this.Position = new PointF(x, y);
            this.Position = TmxMath.Sanitize(this.Position);

            // Null out our rotation
            this.Rotation = 0;
        }

        static protected void CopyBaseProperties(TmxObject from, TmxObject to)
        {
            to.Name = from.Name;
            to.Type = from.Type;
            to.Visible = from.Visible;
            to.Position = from.Position;
            to.Size = from.Size;
            to.Rotation = from.Rotation;
            to.Properties = from.Properties;
            to.ParentObjectGroup = from.ParentObjectGroup;
        }

        public abstract RectangleF GetWorldBounds();
        protected abstract void InternalFromXml(XElement xml, TmxMap tmxMap);
        protected abstract string InternalGetDefaultName();
    }
}

// ----------------------------------------------------------------------
// TmxObject.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TmxObject
    {
        public static TmxObject FromXml(XElement xml, TmxObjectGroup tmxObjectGroup, TmxMap tmxMap)
        {
            Program.WriteLine("Parsing object ...");
            Program.WriteVerbose(xml.ToString());

            // What kind of TmxObject are we creating?
            TmxObject tmxObject = null;

            if (xml.Element("ellipse") != null)
            {
                tmxObject = new TmxObjectEllipse();
            }
            else if (xml.Element("polygon") != null)
            {
                tmxObject = new TmxObjectPolygon();
            }
            else if (xml.Element("polyline") != null)
            {
                tmxObject = new TmxObjectPolyline();
            }
            else if (xml.Attribute("gid") != null)
            {
                uint gid = TmxHelper.GetAttributeAsUInt(xml, "gid");
                gid = TmxMath.GetTileIdWithoutFlags(gid);
                if (tmxMap.Tiles.ContainsKey(gid))
                {
                    tmxObject = new TmxObjectTile();
                }
                else
                {
                    // For some reason, the tile is not in any of our tilesets
                    // Warn the user and use a rectangle
                    Program.WriteWarning("Tile Id {0} not found in tilesets. Using a rectangle instead.\n{1}", gid, xml.ToString());
                    tmxObject = new TmxObjectRectangle();
                }
            }
            else
            {
                // Just a rectangle
                tmxObject = new TmxObjectRectangle();
            }

            // Data found on every object type
            tmxObject.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObject.Type = TmxHelper.GetAttributeAsString(xml, "type", "");
            tmxObject.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;
            tmxObject.ParentObjectGroup = tmxObjectGroup;

            float x = TmxHelper.GetAttributeAsFloat(xml, "x");
            float y = TmxHelper.GetAttributeAsFloat(xml, "y");
            float w = TmxHelper.GetAttributeAsFloat(xml, "width", 0);
            float h = TmxHelper.GetAttributeAsFloat(xml, "height", 0);
            float r = TmxHelper.GetAttributeAsFloat(xml, "rotation", 0);
            tmxObject.Position = new System.Drawing.PointF(x, y);
            tmxObject.Size = new System.Drawing.SizeF(w, h);
            tmxObject.Rotation = r;

            tmxObject.Properties = TmxProperties.FromXml(xml);

            tmxObject.InternalFromXml(xml, tmxMap);

            return tmxObject;
        }
    }
}

// ----------------------------------------------------------------------
// TmxObjectEllipse.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    class TmxObjectEllipse : TmxObject
    {
        public bool IsCircle()
        {
            return (this.Size.Width == this.Size.Height);
        }

        public float Radius
        {
            get
            {
                Debug.Assert(IsCircle());
                return this.Size.Width * 0.5f;
            }
        }

        public override System.Drawing.RectangleF GetWorldBounds()
        {
            return new System.Drawing.RectangleF(this.Position, this.Size);
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            // No extra data for ellipses
        }

        protected override string InternalGetDefaultName()
        {
            if (IsCircle())
                return "CircleObject";
            return "EllipseObject";
        }

    }
}

// ----------------------------------------------------------------------
// TmxObjectGroup.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public partial class TmxObjectGroup : TmxLayerBase
    {
        public string Name { get; private set; }
        public bool Visible { get; private set; }
        public List<TmxObject> Objects { get; private set; }
        public Color Color { get; private set; }
        public PointF Offset { get; private set; }

        public TmxObjectGroup()
        {
            this.Objects = new List<TmxObject>();
        }

        public RectangleF GetWorldBounds(PointF translation)
        {
            RectangleF bounds = new RectangleF();
            foreach (var obj in this.Objects)
            {
                RectangleF objBounds = obj.GetWorldBounds();
                objBounds.Offset(translation);
                bounds = RectangleF.Union(bounds, objBounds);
            }
            return bounds;
        }

        public RectangleF GetWorldBounds()
        {
            return GetWorldBounds(new PointF(0, 0));
        }

        public override string ToString()
        {
            return String.Format("{{ ObjectGroup name={0}, numObjects={1} }}", this.Name, this.Objects.Count());
        }

    }
}

// ----------------------------------------------------------------------
// TmxObjectGroup.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxObjectGroup
    {
        public static TmxObjectGroup FromXml(XElement xml, TmxMap tmxMap)
        {
            Debug.Assert(xml.Name == "objectgroup");

            TmxObjectGroup tmxObjectGroup = new TmxObjectGroup();

            // Order within Xml file is import for layer types
            tmxObjectGroup.XmlElementIndex = xml.NodesBeforeSelf().Count();

            tmxObjectGroup.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObjectGroup.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;
            tmxObjectGroup.Color = TmxHelper.GetAttributeAsColor(xml, "color", Color.FromArgb(128, 128, 128));
            tmxObjectGroup.Properties = TmxProperties.FromXml(xml);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(xml, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(xml, "offsety", 0);
            tmxObjectGroup.Offset = offset;

            // Get all the objects
            Program.WriteLine("Parsing objects in object group '{0}'", tmxObjectGroup.Name);
            var objects = from obj in xml.Elements("object")
                          select TmxObject.FromXml(obj, tmxObjectGroup, tmxMap);

            tmxObjectGroup.Objects = objects.ToList();

            return tmxObjectGroup;
        }

    }
}

// ----------------------------------------------------------------------
// TmxObjectPolygon.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    class TmxObjectPolygon : TmxObject, TmxHasPoints
    {
        public List<PointF> Points { get; set; }

        public TmxObjectPolygon()
        {
            this.Points = new List<PointF>();
        }

        public override RectangleF GetWorldBounds()
        {
            float xmin = float.MaxValue;
            float xmax = float.MinValue;
            float ymin = float.MaxValue;
            float ymax = float.MinValue;

            foreach (var p in this.Points)
            {
                xmin = Math.Min(xmin, p.X);
                xmax = Math.Max(xmax, p.X);
                ymin = Math.Min(ymin, p.Y);
                ymax = Math.Max(ymax, p.Y);
            }

            RectangleF bounds = new RectangleF(xmin, ymin, xmax - xmin, ymax - ymin);
            bounds.Offset(this.Position);
            return bounds;
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            var points = from pt in xml.Element("polygon").Attribute("points").Value.Split(' ')
                         let x = float.Parse(pt.Split(',')[0])
                         let y = float.Parse(pt.Split(',')[1])
                         select new PointF(x, y);

            this.Points = points.ToList();

            // Test if polygons are counter clocksise
            // From: http://stackoverflow.com/questions/1165647/how-to-determine-if-a-list-of-polygon-points-are-in-clockwise-order
            float sum = 0.0f;
            for (int i = 1; i < this.Points.Count(); i++)
            {
                var p1 = this.Points[i - 1];
                var p2 = this.Points[i];

                float v = (p2.X - p1.X) * -(p2.Y + p1.Y);
                sum += v;
            }

            if (sum < 0)
            {
                // Winding of polygons is counter-clockwise. Reverse the list.
                this.Points.Reverse();
            }
        }

        protected override string InternalGetDefaultName()
        {
            return "PolygonObject";
        }


        public override string ToString()
        {
            StringBuilder pts = new StringBuilder();
            if (this.Points == null)
            {
                pts.Append("<empty>");
            }
            else
            {
                foreach (var p in this.Points)
                {
                    pts.AppendFormat("({0}, {1})", p.X, p.Y);
                    if (p != this.Points.Last())
                    {
                        pts.AppendFormat(", ");
                    }
                }
            }

            return String.Format("{0} {1} {2} points=({3})", GetType().Name, GetNonEmptyName(), this.Position, pts.ToString());
        }

        public bool ArePointsClosed()
        {
            return true;
        }

        static public TmxObjectPolygon FromRectangle(TmxMap tmxMap, TmxObjectRectangle tmxRectangle)
        {
            TmxObjectPolygon tmxPolygon = new TmxObjectPolygon();
            TmxObject.CopyBaseProperties(tmxRectangle, tmxPolygon);

            tmxPolygon.Points = tmxRectangle.Points;

            return tmxPolygon;
        }

    }
}

// ----------------------------------------------------------------------
// TmxObjectPolyline.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    class TmxObjectPolyline : TmxObject, TmxHasPoints
    {
        public List<PointF> Points { get; set; }

        public TmxObjectPolyline()
        {
            this.Points = new List<PointF>();
        }

        public override RectangleF GetWorldBounds()
        {
            float xmin = float.MaxValue;
            float xmax = float.MinValue;
            float ymin = float.MaxValue;
            float ymax = float.MinValue;

            foreach (var p in this.Points)
            {
                xmin = Math.Min(xmin, p.X);
                xmax = Math.Max(xmax, p.X);
                ymin = Math.Min(ymin, p.Y);
                ymax = Math.Max(ymax, p.Y);
            }

            RectangleF bounds = new RectangleF(xmin, ymin, xmax - xmin, ymax - ymin);
            bounds.Offset(this.Position);
            return bounds;
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            Debug.Assert(xml.Name == "object");
            Debug.Assert(xml.Element("polyline") != null);

            var points = from pt in xml.Element("polyline").Attribute("points").Value.Split(' ')
                         let x = float.Parse(pt.Split(',')[0])
                         let y = float.Parse(pt.Split(',')[1])
                         select new PointF(x, y);

            this.Points = points.ToList();
        }

        protected override string InternalGetDefaultName()
        {
            return "PolylineObject";
        }

        public bool ArePointsClosed()
        {
            // Lines are open
            return false;
        }
    }
}

// ----------------------------------------------------------------------
// TmxObjectRectangle.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    class TmxObjectRectangle : TmxObjectPolygon
    {
        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            this.Points = new List<System.Drawing.PointF>();
            this.Points.Add(new PointF(0, 0));
            this.Points.Add(new PointF(this.Size.Width, 0));
            this.Points.Add(new PointF(this.Size.Width, this.Size.Height));
            this.Points.Add(new PointF(0, this.Size.Height));

            if (this.Size.Width == 0 || this.Size.Height == 0)
            {
                Program.WriteWarning("Warning: Rectangle has zero width or height in object group\n{0}", xml.Parent.ToString());
            }
        }

        protected override string InternalGetDefaultName()
        {
            return "RectangleObject";
        }

    }
}

// ----------------------------------------------------------------------
// TmxObjectTile.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    class TmxObjectTile : TmxObject
    {
        public TmxTile Tile { get; private set; }
        public bool FlippedHorizontal { get; private set; }
        public bool FlippedVertical { get; private set; }

        public string SortingLayerName { get; private set; }
        public int? SortingOrder { get; private set; }

        public TmxObjectTile()
        {
            this.SortingLayerName = null;
        }

        public override System.Drawing.RectangleF GetWorldBounds()
        {
            RectangleF myBounds = new RectangleF(this.Position.X, this.Position.Y - this.Size.Height, this.Size.Width, this.Size.Height);

            RectangleF groupBounds = this.Tile.ObjectGroup.GetWorldBounds(this.Position);
            if (groupBounds.IsEmpty)
            {
                return myBounds;
            }
            RectangleF combinedBounds = RectangleF.Union(myBounds, groupBounds);
            return combinedBounds;
        }

        public override string ToString()
        {
            return String.Format("{{ TmxObjectTile: name={0}, pos={1}, tile={2} }}", GetNonEmptyName(), this.Position, this.Tile);
        }

        public SizeF GetTileObjectScale()
        {
            float scaleX = this.Size.Width / this.Tile.TileSize.Width;
            float scaleY = this.Size.Height / this.Tile.TileSize.Height;
            return new SizeF(scaleX, scaleY);
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            // Get the tile
            uint gid = TmxHelper.GetAttributeAsUInt(xml, "gid");
            this.FlippedHorizontal = TmxMath.IsTileFlippedHorizontally(gid);
            this.FlippedVertical = TmxMath.IsTileFlippedVertically(gid);
            uint rawTileId = TmxMath.GetTileIdWithoutFlags(gid);

            this.Tile = tmxMap.Tiles[rawTileId];

            // The tile needs to have a mesh on it.
            // Note: The tile may already be referenced by another TmxObjectTile instance, and as such will have its mesh data already made
            if (this.Tile.Meshes.Count() == 0)
            {
                this.Tile.Meshes = TmxMesh.FromTmxTile(this.Tile, tmxMap);
            }

            // Check properties for layer placement
            if (this.Properties.PropertyMap.ContainsKey("unity:sortingLayerName"))
            {
                this.SortingLayerName = this.Properties.GetPropertyValueAsString("unity:sortingLayerName");
            }
            if (this.Properties.PropertyMap.ContainsKey("unity:sortingOrder"))
            {
                this.SortingOrder = this.Properties.GetPropertyValueAsInt("unity:sortingOrder");
            }
        }

        protected override string InternalGetDefaultName()
        {
            return "TileObject";
        }

    }
}

// ----------------------------------------------------------------------
// TmxProperties.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public partial class TmxProperties
    {
        public IDictionary<string, string> PropertyMap { get; private set; }

        public TmxProperties()
        {
            this.PropertyMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string GetPropertyValueAsString(string name)
        {
            return this.PropertyMap[name];
        }

        public string GetPropertyValueAsString(string name, string defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return this.PropertyMap[name];
            return defaultValue;
        }

        public int GetPropertyValueAsInt(string name)
        {
            try
            {
                return Convert.ToInt32(this.PropertyMap[name]);
            }
            catch (System.FormatException inner)
            {
                string message = String.Format("Error evaulating property '{0}={1}'\n  '{1}' is not an integer", name, this.PropertyMap[name]);
                throw new TmxException(message, inner);
            }
        }

        public int GetPropertyValueAsInt(string name, int defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsInt(name);
            return defaultValue;
        }

        public bool GetPropertyValueAsBoolean(string name)
        {
            bool asBoolean = false;
            try
            {
                asBoolean = Convert.ToBoolean(this.PropertyMap[name]);
            }
            catch (FormatException)
            {
                Program.WriteWarning("Property '{0}' value '{1}' cannot be converted to a boolean.", name, this.PropertyMap[name]);
            }

            return asBoolean;
        }

        public bool GetPropertyValueAsBoolean(string name, bool defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsBoolean(name);
            return defaultValue;
        }

        public T GetPropertyValueAsEnum<T>(string name)
        {
            return TmxHelper.GetStringAsEnum<T>(this.PropertyMap[name]);
        }

        public T GetPropertyValueAsEnum<T>(string name, T defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsEnum<T>(name);
            return defaultValue;
        }

    } // end class
} // end namespace

// ----------------------------------------------------------------------
// TmxProperties.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxProperties
    {
        public static TmxProperties FromXml(XElement elem)
        {
            TmxProperties tmxProps = new TmxProperties();

            var props = from elem1 in elem.Elements("properties")
                        from elem2 in elem1.Elements("property")
                        select new
                        {
                            Name = TmxHelper.GetAttributeAsString(elem2, "name"),
                            Value = TmxHelper.GetAttributeAsString(elem2, "value"),
                        };

            if (props.Count() > 0)
            {
                Program.WriteLine("Parse properites ...");
                Program.WriteVerbose(elem.Element("properties").ToString());
            }

            foreach (var p in props)
            {
                tmxProps.PropertyMap[p.Name] = p.Value;
            }

            return tmxProps;
        }
    }
}

// ----------------------------------------------------------------------
// TmxRotationMatrix.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

// This is a working man's rotation matrix
// This keeps us from invoking the .NET GDI+ Matrix which causes issues on Mac builds
namespace Tiled2Unity
{
    class TmxRotationMatrix
    {
        private float[,] m = new float[2,2] { { 1, 0 },
                                              { 0, 1 } };

        public TmxRotationMatrix()
        {
        }

        public TmxRotationMatrix(float degrees)
        {
            double rads = degrees * Math.PI / 180.0f;
            float cos = (float)Math.Cos(rads);
            float sin = (float)Math.Sin(rads);

            m[0, 0] = cos;
            m[0, 1] = -sin;
            m[1, 0] = sin;
            m[1, 1] = cos;
        }

        public TmxRotationMatrix(float m00, float m01, float m10, float m11)
        {
            m[0, 0] = m00;
            m[0, 1] = m01;
            m[1, 0] = m10;
            m[1, 1] = m11;
        }

        public float this[int i, int j]
        {
            get { return m[i, j]; }
            set { m[i, j] = value; }
        }

        static public TmxRotationMatrix Multiply(TmxRotationMatrix M1, TmxRotationMatrix M2)
        {
            float m00 = M1[0, 0] * M2[0, 0] + M1[0, 1] * M2[1, 0];
            float m01 = M1[0, 0] * M2[0, 1] + M1[0, 1] * M2[1, 1];
            float m10 = M1[1, 0] * M2[0, 0] + M1[1, 1] * M2[1, 0];
            float m11 = M1[1, 0] * M2[0, 1] + M1[1, 1] * M2[1, 1];
            return new TmxRotationMatrix(m00, m01, m10, m11);
        }

        public void TransformPoint(ref PointF pt)
        {
            float x = pt.X * m[0, 0] + pt.Y * m[1, 0];
            float y = pt.X * m[0, 1] + pt.Y * m[1, 1];
            pt.X = x;
            pt.Y = y;
        }

        public void TransformPoints(PointF[] points)
        {
            for (int i = 0; i < points.Length; ++i)
            {
                TransformPoint(ref points[i]);
            }
        }

    }
}

// ----------------------------------------------------------------------
// TmxTile.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public partial class TmxTile : TmxHasProperties
    {
        public uint GlobalId { get; private set; }
        public uint LocalId { get; private set; }
        public Size TileSize { get; private set; }
        public PointF Offset { get; set; }
        public TmxImage TmxImage { get; private set; }
        public Point LocationOnSource { get; private set; }
        public TmxProperties Properties { get; private set; }
        public TmxObjectGroup ObjectGroup { get; private set; }
        public TmxAnimation Animation { get; private set; }

        // Some tiles may be represented as a mesh for tile objects (a list is needed for animations)
        public List<TmxMesh> Meshes { get; set; }


        public TmxTile(uint globalId, uint localId, string tilesetName, TmxImage tmxImage)
        {
            this.GlobalId = globalId;
            this.LocalId = localId;
            this.TmxImage = tmxImage;
            this.Properties = new TmxProperties();
            this.ObjectGroup = new TmxObjectGroup();
            this.Animation = TmxAnimation.FromTileId(globalId);
            this.Meshes = new List<TmxMesh>();
        }

        public bool IsEmpty
        {
            get
            {
                return this.GlobalId == 0 && this.LocalId == 0;
            }
        }

        public void SetTileSize(int width, int height)
        {
            this.TileSize = new Size(width, height);
        }

        public void SetLocationOnSource(int x, int y)
        {
            this.LocationOnSource = new Point(x, y);
        }

        public override string ToString()
        {
            return String.Format("{{id = {0}, source({1})}}", this.GlobalId, this.LocationOnSource);
        }

    }
}

// ----------------------------------------------------------------------
// TmxTile.Xml.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    // partial class methods that build tile data from xml
    partial class TmxTile
    {
        public void ParseTileXml(XElement elem, TmxMap tmxMap, uint firstId)
        {
            Program.WriteLine("Parse tile data (gid = {0}, id {1}) ...", this.GlobalId, this.LocalId);
            Program.WriteVerbose(elem.ToString());

            this.Properties = TmxProperties.FromXml(elem);

            // Do we have an object group for this tile?
            XElement elemObjectGroup = elem.Element("objectgroup");
            if (elemObjectGroup != null)
            {
                this.ObjectGroup = TmxObjectGroup.FromXml(elemObjectGroup, tmxMap);
                FixTileColliderObjects(tmxMap);
            }

            // Is this an animated tile?
            XElement elemAnimation = elem.Element("animation");
            if (elemAnimation != null)
            {
                this.Animation = TmxAnimation.FromXml(elemAnimation, firstId);
            }
        }

        private void FixTileColliderObjects(TmxMap tmxMap)
        {
            // Objects inside of tiles are colliders that will be merged with the colliders on neighboring tiles.
            // In order to promote this merging we have to perform the following clean up operations ...
            // - All rectangles objects are made into polygon objects
            // - All polygon objects will have their rotations burned into the polygon points (and Rotation set to zero)
            // - All cooridinates will be "sanitized" to make up for floating point errors due to rotation and poor placement of colliders
            // (The sanitation will round all numbers to the nearest 1/256th)

            // Replace rectangles with polygons
            for (int i = 0; i < this.ObjectGroup.Objects.Count; i++)
            {
                TmxObject tmxObject = this.ObjectGroup.Objects[i];
                if (tmxObject is TmxObjectRectangle)
                {
                    TmxObjectPolygon tmxObjectPolygon = TmxObjectPolygon.FromRectangle(tmxMap, tmxObject as TmxObjectRectangle);
                    this.ObjectGroup.Objects[i] = tmxObjectPolygon;
                }
            }

            // Burn rotation into all polygon points, sanitizing the point locations as we go
            foreach (TmxObject tmxObject in this.ObjectGroup.Objects)
            {
                TmxHasPoints tmxHasPoints = tmxObject as TmxHasPoints;
                if (tmxHasPoints != null)
                {
                    var pointfs = tmxHasPoints.Points.ToArray();

                    // Rotate our points by the rotation and position in the object
                    TmxMath.RotatePoints(pointfs, tmxObject);

                    // Sanitize our points to make up for floating point precision errors
                    pointfs = pointfs.Select(TmxMath.Sanitize).ToArray();

                    // Set the points back into the object
                    tmxHasPoints.Points = pointfs.ToList();

                    // Zero out our rotation
                    tmxObject.BakeRotation();
                }
            }
        }

    }
}

// ----------------------------------------------------------------------
// MultiValueDictionary.cs

//////////////////////////////////////////////////////////////////////
// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Linq;
// using System.Text;
//using SD.Tools.Algorithmia.UtilityClasses;

namespace SD.Tools.Algorithmia.GeneralDataStructures
{
    /// <summary>
    /// Extension to the normal Dictionary. This class can store more than one value for every key. It keeps a HashSet for every Key value.
    /// Calling Add with the same Key and multiple values will store each value under the same Key in the Dictionary. Obtaining the values
    /// for a Key will return the HashSet with the Values of the Key. 
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class MultiValueDictionary<TKey, TValue> : Dictionary<TKey, HashSet<TValue>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiValueDictionary&lt;TKey, TValue&gt;"/> class.
        /// </summary>
        public MultiValueDictionary()
            : base()
        {
        }


        /// <summary>
        /// Adds the specified value under the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Add(TKey key, TValue value)
        {
            Debug.Assert(key != null);
            //ArgumentVerifier.CantBeNull(key, "key");

            HashSet<TValue> container = null;
            if (!this.TryGetValue(key, out container))
            {
                container = new HashSet<TValue>();
                base.Add(key, container);
            }
            container.Add(value);
        }


        /// <summary>
        /// Determines whether this dictionary contains the specified value for the specified key 
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>true if the value is stored for the specified key in this dictionary, false otherwise</returns>
        public bool ContainsValue(TKey key, TValue value)
        {
            Debug.Assert(key != null);
            //ArgumentVerifier.CantBeNull(key, "key");

            bool toReturn = false;
            HashSet<TValue> values = null;
            if (this.TryGetValue(key, out values))
            {
                toReturn = values.Contains(value);
            }
            return toReturn;
        }


        /// <summary>
        /// Removes the specified value for the specified key. It will leave the key in the dictionary.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Remove(TKey key, TValue value)
        {
            Debug.Assert(key != null);
            //ArgumentVerifier.CantBeNull(key, "key");

            HashSet<TValue> container = null;
            if (this.TryGetValue(key, out container))
            {
                container.Remove(value);
                if (container.Count <= 0)
                {
                    this.Remove(key);
                }
            }
        }


        /// <summary>
        /// Merges the specified multivaluedictionary into this instance.
        /// </summary>
        /// <param name="toMergeWith">To merge with.</param>
        public void Merge(MultiValueDictionary<TKey, TValue> toMergeWith)
        {
            if (toMergeWith == null)
            {
                return;
            }

            foreach (KeyValuePair<TKey, HashSet<TValue>> pair in toMergeWith)
            {
                foreach (TValue value in pair.Value)
                {
                    this.Add(pair.Key, value);
                }
            }
        }


        /// <summary>
        /// Gets the values for the key specified. This method is useful if you want to avoid an exception for key value retrieval and you can't use TryGetValue
        /// (e.g. in lambdas)
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="returnEmptySet">if set to true and the key isn't found, an empty hashset is returned, otherwise, if the key isn't found, null is returned</param>
        /// <returns>
        /// This method will return null (or an empty set if returnEmptySet is true) if the key wasn't found, or
        /// the values if key was found.
        /// </returns>
        public HashSet<TValue> GetValues(TKey key, bool returnEmptySet)
        {
            HashSet<TValue> toReturn = null;
            if (!base.TryGetValue(key, out toReturn) && returnEmptySet)
            {
                toReturn = new HashSet<TValue>();
            }
            return toReturn;
        }
    }
}

// ----------------------------------------------------------------------
// clipper.cs

/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.2.1                                                           *
* Date      :  31 October 2014                                                 *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2014                                         *
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* This is a translation of the Delphi Clipper library and the naming style     *
* used has retained a Delphi flavour.                                          *
*                                                                              *
*******************************************************************************/

//use_int32: When enabled 32bit ints are used instead of 64bit ints. This
//improve performance but coordinate values are limited to the range +/- 46340
//#define use_int32

//use_xyz: adds a Z member to IntPoint. Adds a minor cost to performance.
//#define use_xyz

//use_lines: Enables open path clipping. Adds a very minor cost to performance.
// #define use_lines // Commented out and put up top of file

//use_deprecated: Enables temporary support for the obsolete functions
//#define use_deprecated


// using System;
// using System.Collections.Generic;
//using System.Text;          //for Int128.AsString() & StringBuilder
//using System.IO;            //debugging with streamReader & StreamWriter
//using System.Windows.Forms; //debugging to clipboard

namespace ClipperLib
{

#if use_int32
  using cInt = Int32;
#else
    using cInt = Int64;
#endif

    using Path = List<IntPoint>;
    using Paths = List<List<IntPoint>>;

    public struct DoublePoint
    {
        public double X;
        public double Y;

        public DoublePoint(double x = 0, double y = 0)
        {
            this.X = x; this.Y = y;
        }
        public DoublePoint(DoublePoint dp)
        {
            this.X = dp.X; this.Y = dp.Y;
        }
        public DoublePoint(IntPoint ip)
        {
            this.X = ip.X; this.Y = ip.Y;
        }
    };


    //------------------------------------------------------------------------------
    // PolyTree & PolyNode classes
    //------------------------------------------------------------------------------

    public class PolyTree : PolyNode
    {
        internal List<PolyNode> m_AllPolys = new List<PolyNode>();

        ~PolyTree()
        {
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < m_AllPolys.Count; i++)
                m_AllPolys[i] = null;
            m_AllPolys.Clear();
            m_Childs.Clear();
        }

        public PolyNode GetFirst()
        {
            if (m_Childs.Count > 0)
                return m_Childs[0];
            else
                return null;
        }

        public int Total
        {
            get
            {
                int result = m_AllPolys.Count;
                //with negative offsets, ignore the hidden outer polygon ...
                if (result > 0 && m_Childs[0] != m_AllPolys[0]) result--;
                return result;
            }
        }

    }

    public class PolyNode
    {
        internal PolyNode m_Parent;
        internal Path m_polygon = new Path();
        internal int m_Index;
        internal JoinType m_jointype;
        internal EndType m_endtype;
        internal List<PolyNode> m_Childs = new List<PolyNode>();

        private bool IsHoleNode()
        {
            bool result = true;
            PolyNode node = m_Parent;
            while (node != null)
            {
                result = !result;
                node = node.m_Parent;
            }
            return result;
        }

        public int ChildCount
        {
            get { return m_Childs.Count; }
        }

        public Path Contour
        {
            get { return m_polygon; }
        }

        internal void AddChild(PolyNode Child)
        {
            int cnt = m_Childs.Count;
            m_Childs.Add(Child);
            Child.m_Parent = this;
            Child.m_Index = cnt;
        }

        public PolyNode GetNext()
        {
            if (m_Childs.Count > 0)
                return m_Childs[0];
            else
                return GetNextSiblingUp();
        }

        internal PolyNode GetNextSiblingUp()
        {
            if (m_Parent == null)
                return null;
            else if (m_Index == m_Parent.m_Childs.Count - 1)
                return m_Parent.GetNextSiblingUp();
            else
                return m_Parent.m_Childs[m_Index + 1];
        }

        public List<PolyNode> Childs
        {
            get { return m_Childs; }
        }

        public PolyNode Parent
        {
            get { return m_Parent; }
        }

        public bool IsHole
        {
            get { return IsHoleNode(); }
        }

        public bool IsOpen { get; set; }
    }


    //------------------------------------------------------------------------------
    // Int128 struct (enables safe math on signed 64bit integers)
    // eg Int128 val1((Int64)9223372036854775807); //ie 2^63 -1
    //    Int128 val2((Int64)9223372036854775807);
    //    Int128 val3 = val1 * val2;
    //    val3.ToString => "85070591730234615847396907784232501249" (8.5e+37)
    //------------------------------------------------------------------------------

    internal struct Int128
    {
        private Int64 hi;
        private UInt64 lo;

        public Int128(Int64 _lo)
        {
            lo = (UInt64)_lo;
            if (_lo < 0) hi = -1;
            else hi = 0;
        }

        public Int128(Int64 _hi, UInt64 _lo)
        {
            lo = _lo;
            hi = _hi;
        }

        public Int128(Int128 val)
        {
            hi = val.hi;
            lo = val.lo;
        }

        public bool IsNegative()
        {
            return hi < 0;
        }

        public static bool operator ==(Int128 val1, Int128 val2)
        {
            if ((object)val1 == (object)val2) return true;
            else if ((object)val1 == null || (object)val2 == null) return false;
            return (val1.hi == val2.hi && val1.lo == val2.lo);
        }

        public static bool operator !=(Int128 val1, Int128 val2)
        {
            return !(val1 == val2);
        }

        public override bool Equals(System.Object obj)
        {
            if (obj == null || !(obj is Int128))
                return false;
            Int128 i128 = (Int128)obj;
            return (i128.hi == hi && i128.lo == lo);
        }

        public override int GetHashCode()
        {
            return hi.GetHashCode() ^ lo.GetHashCode();
        }

        public static bool operator >(Int128 val1, Int128 val2)
        {
            if (val1.hi != val2.hi)
                return val1.hi > val2.hi;
            else
                return val1.lo > val2.lo;
        }

        public static bool operator <(Int128 val1, Int128 val2)
        {
            if (val1.hi != val2.hi)
                return val1.hi < val2.hi;
            else
                return val1.lo < val2.lo;
        }

        public static Int128 operator +(Int128 lhs, Int128 rhs)
        {
            lhs.hi += rhs.hi;
            lhs.lo += rhs.lo;
            if (lhs.lo < rhs.lo) lhs.hi++;
            return lhs;
        }

        public static Int128 operator -(Int128 lhs, Int128 rhs)
        {
            return lhs + -rhs;
        }

        public static Int128 operator -(Int128 val)
        {
            if (val.lo == 0)
                return new Int128(-val.hi, 0);
            else
                return new Int128(~val.hi, ~val.lo + 1);
        }

        public static explicit operator double(Int128 val)
        {
            const double shift64 = 18446744073709551616.0; //2^64
            if (val.hi < 0)
            {
                if (val.lo == 0)
                    return (double)val.hi * shift64;
                else
                    return -(double)(~val.lo + ~val.hi * shift64);
            }
            else
                return (double)(val.lo + val.hi * shift64);
        }

        //nb: Constructing two new Int128 objects every time we want to multiply longs  
        //is slow. So, although calling the Int128Mul method doesn't look as clean, the 
        //code runs significantly faster than if we'd used the * operator.

        public static Int128 Int128Mul(Int64 lhs, Int64 rhs)
        {
            bool negate = (lhs < 0) != (rhs < 0);
            if (lhs < 0) lhs = -lhs;
            if (rhs < 0) rhs = -rhs;
            UInt64 int1Hi = (UInt64)lhs >> 32;
            UInt64 int1Lo = (UInt64)lhs & 0xFFFFFFFF;
            UInt64 int2Hi = (UInt64)rhs >> 32;
            UInt64 int2Lo = (UInt64)rhs & 0xFFFFFFFF;

            //nb: see comments in clipper.pas
            UInt64 a = int1Hi * int2Hi;
            UInt64 b = int1Lo * int2Lo;
            UInt64 c = int1Hi * int2Lo + int1Lo * int2Hi;

            UInt64 lo;
            Int64 hi;
            hi = (Int64)(a + (c >> 32));

            unchecked { lo = (c << 32) + b; }
            if (lo < b) hi++;
            Int128 result = new Int128(hi, lo);
            return negate ? -result : result;
        }

    };

    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------

    public struct IntPoint
    {
        public cInt X;
        public cInt Y;
#if use_xyz
    public cInt Z;
    
    public IntPoint(cInt x, cInt y, cInt z = 0)
    {
      this.X = x; this.Y = y; this.Z = z;
    }
    
    public IntPoint(double x, double y, double z = 0)
    {
      this.X = (cInt)x; this.Y = (cInt)y; this.Z = (cInt)z;
    }
    
    public IntPoint(DoublePoint dp)
    {
      this.X = (cInt)dp.X; this.Y = (cInt)dp.Y; this.Z = 0;
    }

    public IntPoint(IntPoint pt)
    {
      this.X = pt.X; this.Y = pt.Y; this.Z = pt.Z;
    }
#else
        public IntPoint(cInt X, cInt Y)
        {
            this.X = X; this.Y = Y;
        }
        public IntPoint(double x, double y)
        {
            this.X = (cInt)x; this.Y = (cInt)y;
        }

        public IntPoint(IntPoint pt)
        {
            this.X = pt.X; this.Y = pt.Y;
        }
#endif

        public static bool operator ==(IntPoint a, IntPoint b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(IntPoint a, IntPoint b)
        {
            return a.X != b.X || a.Y != b.Y;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is IntPoint)
            {
                IntPoint a = (IntPoint)obj;
                return (X == a.X) && (Y == a.Y);
            }
            else return false;
        }

        public override int GetHashCode()
        {
            //simply prevents a compiler warning
            return base.GetHashCode();
        }

    }// end struct IntPoint

    public struct IntRect
    {
        public cInt left;
        public cInt top;
        public cInt right;
        public cInt bottom;

        public IntRect(cInt l, cInt t, cInt r, cInt b)
        {
            this.left = l; this.top = t;
            this.right = r; this.bottom = b;
        }
        public IntRect(IntRect ir)
        {
            this.left = ir.left; this.top = ir.top;
            this.right = ir.right; this.bottom = ir.bottom;
        }
    }

    public enum ClipType { ctIntersection, ctUnion, ctDifference, ctXor };
    public enum PolyType { ptSubject, ptClip };

    //By far the most widely used winding rules for polygon filling are
    //EvenOdd & NonZero (GDI, GDI+, XLib, OpenGL, Cairo, AGG, Quartz, SVG, Gr32)
    //Others rules include Positive, Negative and ABS_GTR_EQ_TWO (only in OpenGL)
    //see http://glprogramming.com/red/chapter11.html
    public enum PolyFillType { pftEvenOdd, pftNonZero, pftPositive, pftNegative };

    public enum JoinType { jtSquare, jtRound, jtMiter };
    public enum EndType { etClosedPolygon, etClosedLine, etOpenButt, etOpenSquare, etOpenRound };

    internal enum EdgeSide { esLeft, esRight };
    internal enum Direction { dRightToLeft, dLeftToRight };

    internal class TEdge
    {
        internal IntPoint Bot;
        internal IntPoint Curr;
        internal IntPoint Top;
        internal IntPoint Delta;
        internal double Dx;
        internal PolyType PolyTyp;
        internal EdgeSide Side;
        internal int WindDelta; //1 or -1 depending on winding direction
        internal int WindCnt;
        internal int WindCnt2; //winding count of the opposite polytype
        internal int OutIdx;
        internal TEdge Next;
        internal TEdge Prev;
        internal TEdge NextInLML;
        internal TEdge NextInAEL;
        internal TEdge PrevInAEL;
        internal TEdge NextInSEL;
        internal TEdge PrevInSEL;
    };

    public class IntersectNode
    {
        internal TEdge Edge1;
        internal TEdge Edge2;
        internal IntPoint Pt;
    };

    public class MyIntersectNodeSort : IComparer<IntersectNode>
    {
        public int Compare(IntersectNode node1, IntersectNode node2)
        {
            cInt i = node2.Pt.Y - node1.Pt.Y;
            if (i > 0) return 1;
            else if (i < 0) return -1;
            else return 0;
        }
    }

    internal class LocalMinima
    {
        internal cInt Y;
        internal TEdge LeftBound;
        internal TEdge RightBound;
        internal LocalMinima Next;
    };

    internal class Scanbeam
    {
        internal cInt Y;
        internal Scanbeam Next;
    };

    internal class OutRec
    {
        internal int Idx;
        internal bool IsHole;
        internal bool IsOpen;
        internal OutRec FirstLeft; //see comments in clipper.pas
        internal OutPt Pts;
        internal OutPt BottomPt;
        internal PolyNode PolyNode;
    };

    internal class OutPt
    {
        internal int Idx;
        internal IntPoint Pt;
        internal OutPt Next;
        internal OutPt Prev;
    };

    internal class Join
    {
        internal OutPt OutPt1;
        internal OutPt OutPt2;
        internal IntPoint OffPt;
    };

    public class ClipperBase
    {
        protected const double horizontal = -3.4E+38;
        protected const int Skip = -2;
        protected const int Unassigned = -1;
        protected const double tolerance = 1.0E-20;
        internal static bool near_zero(double val) { return (val > -tolerance) && (val < tolerance); }

#if use_int32
    public const cInt loRange = 0x7FFF;
    public const cInt hiRange = 0x7FFF;
#else
        public const cInt loRange = 0x3FFFFFFF;
        public const cInt hiRange = 0x3FFFFFFFFFFFFFFFL;
#endif

        internal LocalMinima m_MinimaList;
        internal LocalMinima m_CurrentLM;
        internal List<List<TEdge>> m_edges = new List<List<TEdge>>();
        internal bool m_UseFullRange;
        internal bool m_HasOpenPaths;

        //------------------------------------------------------------------------------

        public bool PreserveCollinear
        {
            get;
            set;
        }
        //------------------------------------------------------------------------------

        public void Swap(ref cInt val1, ref cInt val2)
        {
            cInt tmp = val1;
            val1 = val2;
            val2 = tmp;
        }
        //------------------------------------------------------------------------------

        internal static bool IsHorizontal(TEdge e)
        {
            return e.Delta.Y == 0;
        }
        //------------------------------------------------------------------------------

        internal bool PointIsVertex(IntPoint pt, OutPt pp)
        {
            OutPt pp2 = pp;
            do
            {
                if (pp2.Pt == pt) return true;
                pp2 = pp2.Next;
            }
            while (pp2 != pp);
            return false;
        }
        //------------------------------------------------------------------------------

        internal bool PointOnLineSegment(IntPoint pt,
            IntPoint linePt1, IntPoint linePt2, bool UseFullRange)
        {
            if (UseFullRange)
                return ((pt.X == linePt1.X) && (pt.Y == linePt1.Y)) ||
                  ((pt.X == linePt2.X) && (pt.Y == linePt2.Y)) ||
                  (((pt.X > linePt1.X) == (pt.X < linePt2.X)) &&
                  ((pt.Y > linePt1.Y) == (pt.Y < linePt2.Y)) &&
                  ((Int128.Int128Mul((pt.X - linePt1.X), (linePt2.Y - linePt1.Y)) ==
                  Int128.Int128Mul((linePt2.X - linePt1.X), (pt.Y - linePt1.Y)))));
            else
                return ((pt.X == linePt1.X) && (pt.Y == linePt1.Y)) ||
                  ((pt.X == linePt2.X) && (pt.Y == linePt2.Y)) ||
                  (((pt.X > linePt1.X) == (pt.X < linePt2.X)) &&
                  ((pt.Y > linePt1.Y) == (pt.Y < linePt2.Y)) &&
                  ((pt.X - linePt1.X) * (linePt2.Y - linePt1.Y) ==
                    (linePt2.X - linePt1.X) * (pt.Y - linePt1.Y)));
        }
        //------------------------------------------------------------------------------

        internal bool PointOnPolygon(IntPoint pt, OutPt pp, bool UseFullRange)
        {
            OutPt pp2 = pp;
            while (true)
            {
                if (PointOnLineSegment(pt, pp2.Pt, pp2.Next.Pt, UseFullRange))
                    return true;
                pp2 = pp2.Next;
                if (pp2 == pp) break;
            }
            return false;
        }
        //------------------------------------------------------------------------------

        internal static bool SlopesEqual(TEdge e1, TEdge e2, bool UseFullRange)
        {
            if (UseFullRange)
                return Int128.Int128Mul(e1.Delta.Y, e2.Delta.X) ==
                    Int128.Int128Mul(e1.Delta.X, e2.Delta.Y);
            else return (cInt)(e1.Delta.Y) * (e2.Delta.X) ==
              (cInt)(e1.Delta.X) * (e2.Delta.Y);
        }
        //------------------------------------------------------------------------------

        protected static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
            IntPoint pt3, bool UseFullRange)
        {
            if (UseFullRange)
                return Int128.Int128Mul(pt1.Y - pt2.Y, pt2.X - pt3.X) ==
                  Int128.Int128Mul(pt1.X - pt2.X, pt2.Y - pt3.Y);
            else return
              (cInt)(pt1.Y - pt2.Y) * (pt2.X - pt3.X) - (cInt)(pt1.X - pt2.X) * (pt2.Y - pt3.Y) == 0;
        }
        //------------------------------------------------------------------------------

        protected static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
            IntPoint pt3, IntPoint pt4, bool UseFullRange)
        {
            if (UseFullRange)
                return Int128.Int128Mul(pt1.Y - pt2.Y, pt3.X - pt4.X) ==
                  Int128.Int128Mul(pt1.X - pt2.X, pt3.Y - pt4.Y);
            else return
              (cInt)(pt1.Y - pt2.Y) * (pt3.X - pt4.X) - (cInt)(pt1.X - pt2.X) * (pt3.Y - pt4.Y) == 0;
        }
        //------------------------------------------------------------------------------

        internal ClipperBase() //constructor (nb: no external instantiation)
        {
            m_MinimaList = null;
            m_CurrentLM = null;
            m_UseFullRange = false;
            m_HasOpenPaths = false;
        }
        //------------------------------------------------------------------------------

        public virtual void Clear()
        {
            DisposeLocalMinimaList();
            for (int i = 0; i < m_edges.Count; ++i)
            {
                for (int j = 0; j < m_edges[i].Count; ++j) m_edges[i][j] = null;
                m_edges[i].Clear();
            }
            m_edges.Clear();
            m_UseFullRange = false;
            m_HasOpenPaths = false;
        }
        //------------------------------------------------------------------------------

        private void DisposeLocalMinimaList()
        {
            while (m_MinimaList != null)
            {
                LocalMinima tmpLm = m_MinimaList.Next;
                m_MinimaList = null;
                m_MinimaList = tmpLm;
            }
            m_CurrentLM = null;
        }
        //------------------------------------------------------------------------------

        void RangeTest(IntPoint Pt, ref bool useFullRange)
        {
            if (useFullRange)
            {
                if (Pt.X > hiRange || Pt.Y > hiRange || -Pt.X > hiRange || -Pt.Y > hiRange)
                    throw new ClipperException("Coordinate outside allowed range");
            }
            else if (Pt.X > loRange || Pt.Y > loRange || -Pt.X > loRange || -Pt.Y > loRange)
            {
                useFullRange = true;
                RangeTest(Pt, ref useFullRange);
            }
        }
        //------------------------------------------------------------------------------

        private void InitEdge(TEdge e, TEdge eNext,
          TEdge ePrev, IntPoint pt)
        {
            e.Next = eNext;
            e.Prev = ePrev;
            e.Curr = pt;
            e.OutIdx = Unassigned;
        }
        //------------------------------------------------------------------------------

        private void InitEdge2(TEdge e, PolyType polyType)
        {
            if (e.Curr.Y >= e.Next.Curr.Y)
            {
                e.Bot = e.Curr;
                e.Top = e.Next.Curr;
            }
            else
            {
                e.Top = e.Curr;
                e.Bot = e.Next.Curr;
            }
            SetDx(e);
            e.PolyTyp = polyType;
        }
        //------------------------------------------------------------------------------

        private TEdge FindNextLocMin(TEdge E)
        {
            TEdge E2;
            for (; ; )
            {
                while (E.Bot != E.Prev.Bot || E.Curr == E.Top) E = E.Next;
                if (E.Dx != horizontal && E.Prev.Dx != horizontal) break;
                while (E.Prev.Dx == horizontal) E = E.Prev;
                E2 = E;
                while (E.Dx == horizontal) E = E.Next;
                if (E.Top.Y == E.Prev.Bot.Y) continue; //ie just an intermediate horz.
                if (E2.Prev.Bot.X < E.Bot.X) E = E2;
                break;
            }
            return E;
        }
        //------------------------------------------------------------------------------

        private TEdge ProcessBound(TEdge E, bool LeftBoundIsForward)
        {
            TEdge EStart, Result = E;
            TEdge Horz;

            if (Result.OutIdx == Skip)
            {
                //check if there are edges beyond the skip edge in the bound and if so
                //create another LocMin and calling ProcessBound once more ...
                E = Result;
                if (LeftBoundIsForward)
                {
                    while (E.Top.Y == E.Next.Bot.Y) E = E.Next;
                    while (E != Result && E.Dx == horizontal) E = E.Prev;
                }
                else
                {
                    while (E.Top.Y == E.Prev.Bot.Y) E = E.Prev;
                    while (E != Result && E.Dx == horizontal) E = E.Next;
                }
                if (E == Result)
                {
                    if (LeftBoundIsForward) Result = E.Next;
                    else Result = E.Prev;
                }
                else
                {
                    //there are more edges in the bound beyond result starting with E
                    if (LeftBoundIsForward)
                        E = Result.Next;
                    else
                        E = Result.Prev;
                    LocalMinima locMin = new LocalMinima();
                    locMin.Next = null;
                    locMin.Y = E.Bot.Y;
                    locMin.LeftBound = null;
                    locMin.RightBound = E;
                    E.WindDelta = 0;
                    Result = ProcessBound(E, LeftBoundIsForward);
                    InsertLocalMinima(locMin);
                }
                return Result;
            }

            if (E.Dx == horizontal)
            {
                //We need to be careful with open paths because this may not be a
                //true local minima (ie E may be following a skip edge).
                //Also, consecutive horz. edges may start heading left before going right.
                if (LeftBoundIsForward) EStart = E.Prev;
                else EStart = E.Next;
                if (EStart.OutIdx != Skip)
                {
                    if (EStart.Dx == horizontal) //ie an adjoining horizontal skip edge
                    {
                        if (EStart.Bot.X != E.Bot.X && EStart.Top.X != E.Bot.X)
                            ReverseHorizontal(E);
                    }
                    else if (EStart.Bot.X != E.Bot.X)
                        ReverseHorizontal(E);
                }
            }

            EStart = E;
            if (LeftBoundIsForward)
            {
                while (Result.Top.Y == Result.Next.Bot.Y && Result.Next.OutIdx != Skip)
                    Result = Result.Next;
                if (Result.Dx == horizontal && Result.Next.OutIdx != Skip)
                {
                    //nb: at the top of a bound, horizontals are added to the bound
                    //only when the preceding edge attaches to the horizontal's left vertex
                    //unless a Skip edge is encountered when that becomes the top divide
                    Horz = Result;
                    while (Horz.Prev.Dx == horizontal) Horz = Horz.Prev;
                    if (Horz.Prev.Top.X == Result.Next.Top.X)
                    {
                        if (!LeftBoundIsForward) Result = Horz.Prev;
                    }
                    else if (Horz.Prev.Top.X > Result.Next.Top.X) Result = Horz.Prev;
                }
                while (E != Result)
                {
                    E.NextInLML = E.Next;
                    if (E.Dx == horizontal && E != EStart && E.Bot.X != E.Prev.Top.X)
                        ReverseHorizontal(E);
                    E = E.Next;
                }
                if (E.Dx == horizontal && E != EStart && E.Bot.X != E.Prev.Top.X)
                    ReverseHorizontal(E);
                Result = Result.Next; //move to the edge just beyond current bound
            }
            else
            {
                while (Result.Top.Y == Result.Prev.Bot.Y && Result.Prev.OutIdx != Skip)
                    Result = Result.Prev;
                if (Result.Dx == horizontal && Result.Prev.OutIdx != Skip)
                {
                    Horz = Result;
                    while (Horz.Next.Dx == horizontal) Horz = Horz.Next;
                    if (Horz.Next.Top.X == Result.Prev.Top.X)
                    {
                        if (!LeftBoundIsForward) Result = Horz.Next;
                    }
                    else if (Horz.Next.Top.X > Result.Prev.Top.X) Result = Horz.Next;
                }

                while (E != Result)
                {
                    E.NextInLML = E.Prev;
                    if (E.Dx == horizontal && E != EStart && E.Bot.X != E.Next.Top.X)
                        ReverseHorizontal(E);
                    E = E.Prev;
                }
                if (E.Dx == horizontal && E != EStart && E.Bot.X != E.Next.Top.X)
                    ReverseHorizontal(E);
                Result = Result.Prev; //move to the edge just beyond current bound
            }
            return Result;
        }
        //------------------------------------------------------------------------------


        public bool AddPath(Path pg, PolyType polyType, bool Closed)
        {
#if use_lines
      if (!Closed && polyType == PolyType.ptClip)
        throw new ClipperException("AddPath: Open paths must be subject.");
#else
            if (!Closed)
                throw new ClipperException("AddPath: Open paths have been disabled.");
#endif

            int highI = (int)pg.Count - 1;
            if (Closed) while (highI > 0 && (pg[highI] == pg[0])) --highI;
            while (highI > 0 && (pg[highI] == pg[highI - 1])) --highI;
            if ((Closed && highI < 2) || (!Closed && highI < 1)) return false;

            //create a new edge array ...
            List<TEdge> edges = new List<TEdge>(highI + 1);
            for (int i = 0; i <= highI; i++) edges.Add(new TEdge());

            bool IsFlat = true;

            //1. Basic (first) edge initialization ...
            edges[1].Curr = pg[1];
            RangeTest(pg[0], ref m_UseFullRange);
            RangeTest(pg[highI], ref m_UseFullRange);
            InitEdge(edges[0], edges[1], edges[highI], pg[0]);
            InitEdge(edges[highI], edges[0], edges[highI - 1], pg[highI]);
            for (int i = highI - 1; i >= 1; --i)
            {
                RangeTest(pg[i], ref m_UseFullRange);
                InitEdge(edges[i], edges[i + 1], edges[i - 1], pg[i]);
            }
            TEdge eStart = edges[0];

            //2. Remove duplicate vertices, and (when closed) collinear edges ...
            TEdge E = eStart, eLoopStop = eStart;
            for (; ; )
            {
                //nb: allows matching start and end points when not Closed ...
                if (E.Curr == E.Next.Curr && (Closed || E.Next != eStart))
                {
                    if (E == E.Next) break;
                    if (E == eStart) eStart = E.Next;
                    E = RemoveEdge(E);
                    eLoopStop = E;
                    continue;
                }
                if (E.Prev == E.Next)
                    break; //only two vertices
                else if (Closed &&
                  SlopesEqual(E.Prev.Curr, E.Curr, E.Next.Curr, m_UseFullRange) &&
                  (!PreserveCollinear ||
                  !Pt2IsBetweenPt1AndPt3(E.Prev.Curr, E.Curr, E.Next.Curr)))
                {
                    //Collinear edges are allowed for open paths but in closed paths
                    //the default is to merge adjacent collinear edges into a single edge.
                    //However, if the PreserveCollinear property is enabled, only overlapping
                    //collinear edges (ie spikes) will be removed from closed paths.
                    if (E == eStart) eStart = E.Next;
                    E = RemoveEdge(E);
                    E = E.Prev;
                    eLoopStop = E;
                    continue;
                }
                E = E.Next;
                if ((E == eLoopStop) || (!Closed && E.Next == eStart)) break;
            }

            if ((!Closed && (E == E.Next)) || (Closed && (E.Prev == E.Next)))
                return false;

            if (!Closed)
            {
                m_HasOpenPaths = true;
                eStart.Prev.OutIdx = Skip;
            }

            //3. Do second stage of edge initialization ...
            E = eStart;
            do
            {
                InitEdge2(E, polyType);
                E = E.Next;
                if (IsFlat && E.Curr.Y != eStart.Curr.Y) IsFlat = false;
            }
            while (E != eStart);

            //4. Finally, add edge bounds to LocalMinima list ...

            //Totally flat paths must be handled differently when adding them
            //to LocalMinima list to avoid endless loops etc ...
            if (IsFlat)
            {
                if (Closed) return false;
                E.Prev.OutIdx = Skip;
                if (E.Prev.Bot.X < E.Prev.Top.X) ReverseHorizontal(E.Prev);
                LocalMinima locMin = new LocalMinima();
                locMin.Next = null;
                locMin.Y = E.Bot.Y;
                locMin.LeftBound = null;
                locMin.RightBound = E;
                locMin.RightBound.Side = EdgeSide.esRight;
                locMin.RightBound.WindDelta = 0;
                while (E.Next.OutIdx != Skip)
                {
                    E.NextInLML = E.Next;
                    if (E.Bot.X != E.Prev.Top.X) ReverseHorizontal(E);
                    E = E.Next;
                }
                InsertLocalMinima(locMin);
                m_edges.Add(edges);
                return true;
            }

            m_edges.Add(edges);
            bool leftBoundIsForward;
            TEdge EMin = null;

            //workaround to avoid an endless loop in the while loop below when
            //open paths have matching start and end points ...
            if (E.Prev.Bot == E.Prev.Top) E = E.Next;

            for (; ; )
            {
                E = FindNextLocMin(E);
                if (E == EMin) break;
                else if (EMin == null) EMin = E;

                //E and E.Prev now share a local minima (left aligned if horizontal).
                //Compare their slopes to find which starts which bound ...
                LocalMinima locMin = new LocalMinima();
                locMin.Next = null;
                locMin.Y = E.Bot.Y;
                if (E.Dx < E.Prev.Dx)
                {
                    locMin.LeftBound = E.Prev;
                    locMin.RightBound = E;
                    leftBoundIsForward = false; //Q.nextInLML = Q.prev
                }
                else
                {
                    locMin.LeftBound = E;
                    locMin.RightBound = E.Prev;
                    leftBoundIsForward = true; //Q.nextInLML = Q.next
                }
                locMin.LeftBound.Side = EdgeSide.esLeft;
                locMin.RightBound.Side = EdgeSide.esRight;

                if (!Closed) locMin.LeftBound.WindDelta = 0;
                else if (locMin.LeftBound.Next == locMin.RightBound)
                    locMin.LeftBound.WindDelta = -1;
                else locMin.LeftBound.WindDelta = 1;
                locMin.RightBound.WindDelta = -locMin.LeftBound.WindDelta;

                E = ProcessBound(locMin.LeftBound, leftBoundIsForward);
                if (E.OutIdx == Skip) E = ProcessBound(E, leftBoundIsForward);

                TEdge E2 = ProcessBound(locMin.RightBound, !leftBoundIsForward);
                if (E2.OutIdx == Skip) E2 = ProcessBound(E2, !leftBoundIsForward);

                if (locMin.LeftBound.OutIdx == Skip)
                    locMin.LeftBound = null;
                else if (locMin.RightBound.OutIdx == Skip)
                    locMin.RightBound = null;
                InsertLocalMinima(locMin);
                if (!leftBoundIsForward) E = E2;
            }
            return true;

        }
        //------------------------------------------------------------------------------

        public bool AddPaths(Paths ppg, PolyType polyType, bool closed)
        {
            bool result = false;
            for (int i = 0; i < ppg.Count; ++i)
                if (AddPath(ppg[i], polyType, closed)) result = true;
            return result;
        }
        //------------------------------------------------------------------------------

        internal bool Pt2IsBetweenPt1AndPt3(IntPoint pt1, IntPoint pt2, IntPoint pt3)
        {
            if ((pt1 == pt3) || (pt1 == pt2) || (pt3 == pt2)) return false;
            else if (pt1.X != pt3.X) return (pt2.X > pt1.X) == (pt2.X < pt3.X);
            else return (pt2.Y > pt1.Y) == (pt2.Y < pt3.Y);
        }
        //------------------------------------------------------------------------------

        TEdge RemoveEdge(TEdge e)
        {
            //removes e from double_linked_list (but without removing from memory)
            e.Prev.Next = e.Next;
            e.Next.Prev = e.Prev;
            TEdge result = e.Next;
            e.Prev = null; //flag as removed (see ClipperBase.Clear)
            return result;
        }
        //------------------------------------------------------------------------------

        private void SetDx(TEdge e)
        {
            e.Delta.X = (e.Top.X - e.Bot.X);
            e.Delta.Y = (e.Top.Y - e.Bot.Y);
            if (e.Delta.Y == 0) e.Dx = horizontal;
            else e.Dx = (double)(e.Delta.X) / (e.Delta.Y);
        }
        //---------------------------------------------------------------------------

        private void InsertLocalMinima(LocalMinima newLm)
        {
            if (m_MinimaList == null)
            {
                m_MinimaList = newLm;
            }
            else if (newLm.Y >= m_MinimaList.Y)
            {
                newLm.Next = m_MinimaList;
                m_MinimaList = newLm;
            }
            else
            {
                LocalMinima tmpLm = m_MinimaList;
                while (tmpLm.Next != null && (newLm.Y < tmpLm.Next.Y))
                    tmpLm = tmpLm.Next;
                newLm.Next = tmpLm.Next;
                tmpLm.Next = newLm;
            }
        }
        //------------------------------------------------------------------------------

        protected void PopLocalMinima()
        {
            if (m_CurrentLM == null) return;
            m_CurrentLM = m_CurrentLM.Next;
        }
        //------------------------------------------------------------------------------

        private void ReverseHorizontal(TEdge e)
        {
            //swap horizontal edges' top and bottom x's so they follow the natural
            //progression of the bounds - ie so their xbots will align with the
            //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]
            Swap(ref e.Top.X, ref e.Bot.X);
#if use_xyz
      Swap(ref e.Top.Z, ref e.Bot.Z);
#endif
        }
        //------------------------------------------------------------------------------

        protected virtual void Reset()
        {
            m_CurrentLM = m_MinimaList;
            if (m_CurrentLM == null) return; //ie nothing to process

            //reset all edges ...
            LocalMinima lm = m_MinimaList;
            while (lm != null)
            {
                TEdge e = lm.LeftBound;
                if (e != null)
                {
                    e.Curr = e.Bot;
                    e.Side = EdgeSide.esLeft;
                    e.OutIdx = Unassigned;
                }
                e = lm.RightBound;
                if (e != null)
                {
                    e.Curr = e.Bot;
                    e.Side = EdgeSide.esRight;
                    e.OutIdx = Unassigned;
                }
                lm = lm.Next;
            }
        }
        //------------------------------------------------------------------------------

        public static IntRect GetBounds(Paths paths)
        {
            int i = 0, cnt = paths.Count;
            while (i < cnt && paths[i].Count == 0) i++;
            if (i == cnt) return new IntRect(0, 0, 0, 0);
            IntRect result = new IntRect();
            result.left = paths[i][0].X;
            result.right = result.left;
            result.top = paths[i][0].Y;
            result.bottom = result.top;
            for (; i < cnt; i++)
                for (int j = 0; j < paths[i].Count; j++)
                {
                    if (paths[i][j].X < result.left) result.left = paths[i][j].X;
                    else if (paths[i][j].X > result.right) result.right = paths[i][j].X;
                    if (paths[i][j].Y < result.top) result.top = paths[i][j].Y;
                    else if (paths[i][j].Y > result.bottom) result.bottom = paths[i][j].Y;
                }
            return result;
        }

    } //end ClipperBase

    public class Clipper : ClipperBase
    {
        //InitOptions that can be passed to the constructor ...
        public const int ioReverseSolution = 1;
        public const int ioStrictlySimple = 2;
        public const int ioPreserveCollinear = 4;

        private List<OutRec> m_PolyOuts;
        private ClipType m_ClipType;
        private Scanbeam m_Scanbeam;
        private TEdge m_ActiveEdges;
        private TEdge m_SortedEdges;
        private List<IntersectNode> m_IntersectList;
        IComparer<IntersectNode> m_IntersectNodeComparer;
        private bool m_ExecuteLocked;
        private PolyFillType m_ClipFillType;
        private PolyFillType m_SubjFillType;
        private List<Join> m_Joins;
        private List<Join> m_GhostJoins;
        private bool m_UsingPolyTree;
#if use_xyz
      public delegate void ZFillCallback(IntPoint bot1, IntPoint top1, 
        IntPoint bot2, IntPoint top2, ref IntPoint pt);
      public ZFillCallback ZFillFunction { get; set; }
#endif
        public Clipper(int InitOptions = 0)
            : base() //constructor
        {
            m_Scanbeam = null;
            m_ActiveEdges = null;
            m_SortedEdges = null;
            m_IntersectList = new List<IntersectNode>();
            m_IntersectNodeComparer = new MyIntersectNodeSort();
            m_ExecuteLocked = false;
            m_UsingPolyTree = false;
            m_PolyOuts = new List<OutRec>();
            m_Joins = new List<Join>();
            m_GhostJoins = new List<Join>();
            ReverseSolution = (ioReverseSolution & InitOptions) != 0;
            StrictlySimple = (ioStrictlySimple & InitOptions) != 0;
            PreserveCollinear = (ioPreserveCollinear & InitOptions) != 0;
#if use_xyz
          ZFillFunction = null;
#endif
        }
        //------------------------------------------------------------------------------

        void DisposeScanbeamList()
        {
            while (m_Scanbeam != null)
            {
                Scanbeam sb2 = m_Scanbeam.Next;
                m_Scanbeam = null;
                m_Scanbeam = sb2;
            }
        }
        //------------------------------------------------------------------------------

        protected override void Reset()
        {
            base.Reset();
            m_Scanbeam = null;
            m_ActiveEdges = null;
            m_SortedEdges = null;
            LocalMinima lm = m_MinimaList;
            while (lm != null)
            {
                InsertScanbeam(lm.Y);
                lm = lm.Next;
            }
        }
        //------------------------------------------------------------------------------

        public bool ReverseSolution
        {
            get;
            set;
        }
        //------------------------------------------------------------------------------

        public bool StrictlySimple
        {
            get;
            set;
        }
        //------------------------------------------------------------------------------

        private void InsertScanbeam(cInt Y)
        {
            if (m_Scanbeam == null)
            {
                m_Scanbeam = new Scanbeam();
                m_Scanbeam.Next = null;
                m_Scanbeam.Y = Y;
            }
            else if (Y > m_Scanbeam.Y)
            {
                Scanbeam newSb = new Scanbeam();
                newSb.Y = Y;
                newSb.Next = m_Scanbeam;
                m_Scanbeam = newSb;
            }
            else
            {
                Scanbeam sb2 = m_Scanbeam;
                while (sb2.Next != null && (Y <= sb2.Next.Y)) sb2 = sb2.Next;
                if (Y == sb2.Y) return; //ie ignores duplicates
                Scanbeam newSb = new Scanbeam();
                newSb.Y = Y;
                newSb.Next = sb2.Next;
                sb2.Next = newSb;
            }
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, Paths solution,
            PolyFillType subjFillType, PolyFillType clipFillType)
        {
            if (m_ExecuteLocked) return false;
            if (m_HasOpenPaths) throw
              new ClipperException("Error: PolyTree struct is need for open path clipping.");

            m_ExecuteLocked = true;
            solution.Clear();
            m_SubjFillType = subjFillType;
            m_ClipFillType = clipFillType;
            m_ClipType = clipType;
            m_UsingPolyTree = false;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded) BuildResult(solution);
            }
            finally
            {
                DisposeAllPolyPts();
                m_ExecuteLocked = false;
            }
            return succeeded;
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, PolyTree polytree,
            PolyFillType subjFillType, PolyFillType clipFillType)
        {
            if (m_ExecuteLocked) return false;
            m_ExecuteLocked = true;
            m_SubjFillType = subjFillType;
            m_ClipFillType = clipFillType;
            m_ClipType = clipType;
            m_UsingPolyTree = true;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded) BuildResult2(polytree);
            }
            finally
            {
                DisposeAllPolyPts();
                m_ExecuteLocked = false;
            }
            return succeeded;
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, Paths solution)
        {
            return Execute(clipType, solution,
                PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipType clipType, PolyTree polytree)
        {
            return Execute(clipType, polytree,
                PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
        }
        //------------------------------------------------------------------------------

        internal void FixHoleLinkage(OutRec outRec)
        {
            //skip if an outermost polygon or
            //already already points to the correct FirstLeft ...
            if (outRec.FirstLeft == null ||
                  (outRec.IsHole != outRec.FirstLeft.IsHole &&
                  outRec.FirstLeft.Pts != null)) return;

            OutRec orfl = outRec.FirstLeft;
            while (orfl != null && ((orfl.IsHole == outRec.IsHole) || orfl.Pts == null))
                orfl = orfl.FirstLeft;
            outRec.FirstLeft = orfl;
        }
        //------------------------------------------------------------------------------

        private bool ExecuteInternal()
        {
            try
            {
                Reset();
                if (m_CurrentLM == null) return false;

                cInt botY = PopScanbeam();
                do
                {
                    InsertLocalMinimaIntoAEL(botY);
                    m_GhostJoins.Clear();
                    ProcessHorizontals(false);
                    if (m_Scanbeam == null) break;
                    cInt topY = PopScanbeam();
                    if (!ProcessIntersections(topY)) return false;
                    ProcessEdgesAtTopOfScanbeam(topY);
                    botY = topY;
                } while (m_Scanbeam != null || m_CurrentLM != null);

                //fix orientations ...
                for (int i = 0; i < m_PolyOuts.Count; i++)
                {
                    OutRec outRec = m_PolyOuts[i];
                    if (outRec.Pts == null || outRec.IsOpen) continue;
                    if ((outRec.IsHole ^ ReverseSolution) == (Area(outRec) > 0))
                        ReversePolyPtLinks(outRec.Pts);
                }

                JoinCommonEdges();

                for (int i = 0; i < m_PolyOuts.Count; i++)
                {
                    OutRec outRec = m_PolyOuts[i];
                    if (outRec.Pts != null && !outRec.IsOpen)
                        FixupOutPolygon(outRec);
                }

                if (StrictlySimple) DoSimplePolygons();
                return true;
            }
            //catch { return false; }
            finally
            {
                m_Joins.Clear();
                m_GhostJoins.Clear();
            }
        }
        //------------------------------------------------------------------------------

        private cInt PopScanbeam()
        {
            cInt Y = m_Scanbeam.Y;
            m_Scanbeam = m_Scanbeam.Next;
            return Y;
        }
        //------------------------------------------------------------------------------

        private void DisposeAllPolyPts()
        {
            for (int i = 0; i < m_PolyOuts.Count; ++i) DisposeOutRec(i);
            m_PolyOuts.Clear();
        }
        //------------------------------------------------------------------------------

        void DisposeOutRec(int index)
        {
            OutRec outRec = m_PolyOuts[index];
            outRec.Pts = null;
            outRec = null;
            m_PolyOuts[index] = null;
        }
        //------------------------------------------------------------------------------

        private void AddJoin(OutPt Op1, OutPt Op2, IntPoint OffPt)
        {
            Join j = new Join();
            j.OutPt1 = Op1;
            j.OutPt2 = Op2;
            j.OffPt = OffPt;
            m_Joins.Add(j);
        }
        //------------------------------------------------------------------------------

        private void AddGhostJoin(OutPt Op, IntPoint OffPt)
        {
            Join j = new Join();
            j.OutPt1 = Op;
            j.OffPt = OffPt;
            m_GhostJoins.Add(j);
        }
        //------------------------------------------------------------------------------

#if use_xyz
      internal void SetZ(ref IntPoint pt, TEdge e1, TEdge e2)
      {
        if (pt.Z != 0 || ZFillFunction == null) return;
        else if (pt == e1.Bot) pt.Z = e1.Bot.Z;
        else if (pt == e1.Top) pt.Z = e1.Top.Z;
        else if (pt == e2.Bot) pt.Z = e2.Bot.Z;
        else if (pt == e2.Top) pt.Z = e2.Top.Z;
        else ZFillFunction(e1.Bot, e1.Top, e2.Bot, e2.Top, ref pt);
      }
      //------------------------------------------------------------------------------
#endif

        private void InsertLocalMinimaIntoAEL(cInt botY)
        {
            while (m_CurrentLM != null && (m_CurrentLM.Y == botY))
            {
                TEdge lb = m_CurrentLM.LeftBound;
                TEdge rb = m_CurrentLM.RightBound;
                PopLocalMinima();

                OutPt Op1 = null;
                if (lb == null)
                {
                    InsertEdgeIntoAEL(rb, null);
                    SetWindingCount(rb);
                    if (IsContributing(rb))
                        Op1 = AddOutPt(rb, rb.Bot);
                }
                else if (rb == null)
                {
                    InsertEdgeIntoAEL(lb, null);
                    SetWindingCount(lb);
                    if (IsContributing(lb))
                        Op1 = AddOutPt(lb, lb.Bot);
                    InsertScanbeam(lb.Top.Y);
                }
                else
                {
                    InsertEdgeIntoAEL(lb, null);
                    InsertEdgeIntoAEL(rb, lb);
                    SetWindingCount(lb);
                    rb.WindCnt = lb.WindCnt;
                    rb.WindCnt2 = lb.WindCnt2;
                    if (IsContributing(lb))
                        Op1 = AddLocalMinPoly(lb, rb, lb.Bot);
                    InsertScanbeam(lb.Top.Y);
                }

                if (rb != null)
                {
                    if (IsHorizontal(rb))
                        AddEdgeToSEL(rb);
                    else
                        InsertScanbeam(rb.Top.Y);
                }

                if (lb == null || rb == null) continue;

                //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
                if (Op1 != null && IsHorizontal(rb) &&
                  m_GhostJoins.Count > 0 && rb.WindDelta != 0)
                {
                    for (int i = 0; i < m_GhostJoins.Count; i++)
                    {
                        //if the horizontal Rb and a 'ghost' horizontal overlap, then convert
                        //the 'ghost' join to a real join ready for later ...
                        Join j = m_GhostJoins[i];
                        if (HorzSegmentsOverlap(j.OutPt1.Pt.X, j.OffPt.X, rb.Bot.X, rb.Top.X))
                            AddJoin(j.OutPt1, Op1, j.OffPt);
                    }
                }

                if (lb.OutIdx >= 0 && lb.PrevInAEL != null &&
                  lb.PrevInAEL.Curr.X == lb.Bot.X &&
                  lb.PrevInAEL.OutIdx >= 0 &&
                  SlopesEqual(lb.PrevInAEL, lb, m_UseFullRange) &&
                  lb.WindDelta != 0 && lb.PrevInAEL.WindDelta != 0)
                {
                    OutPt Op2 = AddOutPt(lb.PrevInAEL, lb.Bot);
                    AddJoin(Op1, Op2, lb.Top);
                }

                if (lb.NextInAEL != rb)
                {

                    if (rb.OutIdx >= 0 && rb.PrevInAEL.OutIdx >= 0 &&
                      SlopesEqual(rb.PrevInAEL, rb, m_UseFullRange) &&
                      rb.WindDelta != 0 && rb.PrevInAEL.WindDelta != 0)
                    {
                        OutPt Op2 = AddOutPt(rb.PrevInAEL, rb.Bot);
                        AddJoin(Op1, Op2, rb.Top);
                    }

                    TEdge e = lb.NextInAEL;
                    if (e != null)
                        while (e != rb)
                        {
                            //nb: For calculating winding counts etc, IntersectEdges() assumes
                            //that param1 will be to the right of param2 ABOVE the intersection ...
                            IntersectEdges(rb, e, lb.Curr); //order important here
                            e = e.NextInAEL;
                        }
                }
            }
        }
        //------------------------------------------------------------------------------

        private void InsertEdgeIntoAEL(TEdge edge, TEdge startEdge)
        {
            if (m_ActiveEdges == null)
            {
                edge.PrevInAEL = null;
                edge.NextInAEL = null;
                m_ActiveEdges = edge;
            }
            else if (startEdge == null && E2InsertsBeforeE1(m_ActiveEdges, edge))
            {
                edge.PrevInAEL = null;
                edge.NextInAEL = m_ActiveEdges;
                m_ActiveEdges.PrevInAEL = edge;
                m_ActiveEdges = edge;
            }
            else
            {
                if (startEdge == null) startEdge = m_ActiveEdges;
                while (startEdge.NextInAEL != null &&
                  !E2InsertsBeforeE1(startEdge.NextInAEL, edge))
                    startEdge = startEdge.NextInAEL;
                edge.NextInAEL = startEdge.NextInAEL;
                if (startEdge.NextInAEL != null) startEdge.NextInAEL.PrevInAEL = edge;
                edge.PrevInAEL = startEdge;
                startEdge.NextInAEL = edge;
            }
        }
        //----------------------------------------------------------------------

        private bool E2InsertsBeforeE1(TEdge e1, TEdge e2)
        {
            if (e2.Curr.X == e1.Curr.X)
            {
                if (e2.Top.Y > e1.Top.Y)
                    return e2.Top.X < TopX(e1, e2.Top.Y);
                else return e1.Top.X > TopX(e2, e1.Top.Y);
            }
            else return e2.Curr.X < e1.Curr.X;
        }
        //------------------------------------------------------------------------------

        private bool IsEvenOddFillType(TEdge edge)
        {
            if (edge.PolyTyp == PolyType.ptSubject)
                return m_SubjFillType == PolyFillType.pftEvenOdd;
            else
                return m_ClipFillType == PolyFillType.pftEvenOdd;
        }
        //------------------------------------------------------------------------------

        private bool IsEvenOddAltFillType(TEdge edge)
        {
            if (edge.PolyTyp == PolyType.ptSubject)
                return m_ClipFillType == PolyFillType.pftEvenOdd;
            else
                return m_SubjFillType == PolyFillType.pftEvenOdd;
        }
        //------------------------------------------------------------------------------

        private bool IsContributing(TEdge edge)
        {
            PolyFillType pft, pft2;
            if (edge.PolyTyp == PolyType.ptSubject)
            {
                pft = m_SubjFillType;
                pft2 = m_ClipFillType;
            }
            else
            {
                pft = m_ClipFillType;
                pft2 = m_SubjFillType;
            }

            switch (pft)
            {
                case PolyFillType.pftEvenOdd:
                    //return false if a subj line has been flagged as inside a subj polygon
                    if (edge.WindDelta == 0 && edge.WindCnt != 1) return false;
                    break;
                case PolyFillType.pftNonZero:
                    if (Math.Abs(edge.WindCnt) != 1) return false;
                    break;
                case PolyFillType.pftPositive:
                    if (edge.WindCnt != 1) return false;
                    break;
                default: //PolyFillType.pftNegative
                    if (edge.WindCnt != -1) return false;
                    break;
            }

            switch (m_ClipType)
            {
                case ClipType.ctIntersection:
                    switch (pft2)
                    {
                        case PolyFillType.pftEvenOdd:
                        case PolyFillType.pftNonZero:
                            return (edge.WindCnt2 != 0);
                        case PolyFillType.pftPositive:
                            return (edge.WindCnt2 > 0);
                        default:
                            return (edge.WindCnt2 < 0);
                    }
                case ClipType.ctUnion:
                    switch (pft2)
                    {
                        case PolyFillType.pftEvenOdd:
                        case PolyFillType.pftNonZero:
                            return (edge.WindCnt2 == 0);
                        case PolyFillType.pftPositive:
                            return (edge.WindCnt2 <= 0);
                        default:
                            return (edge.WindCnt2 >= 0);
                    }
                case ClipType.ctDifference:
                    if (edge.PolyTyp == PolyType.ptSubject)
                        switch (pft2)
                        {
                            case PolyFillType.pftEvenOdd:
                            case PolyFillType.pftNonZero:
                                return (edge.WindCnt2 == 0);
                            case PolyFillType.pftPositive:
                                return (edge.WindCnt2 <= 0);
                            default:
                                return (edge.WindCnt2 >= 0);
                        }
                    else
                        switch (pft2)
                        {
                            case PolyFillType.pftEvenOdd:
                            case PolyFillType.pftNonZero:
                                return (edge.WindCnt2 != 0);
                            case PolyFillType.pftPositive:
                                return (edge.WindCnt2 > 0);
                            default:
                                return (edge.WindCnt2 < 0);
                        }
                case ClipType.ctXor:
                    if (edge.WindDelta == 0) //XOr always contributing unless open
                        switch (pft2)
                        {
                            case PolyFillType.pftEvenOdd:
                            case PolyFillType.pftNonZero:
                                return (edge.WindCnt2 == 0);
                            case PolyFillType.pftPositive:
                                return (edge.WindCnt2 <= 0);
                            default:
                                return (edge.WindCnt2 >= 0);
                        }
                    else
                        return true;
            }
            return true;
        }
        //------------------------------------------------------------------------------

        private void SetWindingCount(TEdge edge)
        {
            TEdge e = edge.PrevInAEL;
            //find the edge of the same polytype that immediately preceeds 'edge' in AEL
            while (e != null && ((e.PolyTyp != edge.PolyTyp) || (e.WindDelta == 0))) e = e.PrevInAEL;
            if (e == null)
            {
                edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
                edge.WindCnt2 = 0;
                e = m_ActiveEdges; //ie get ready to calc WindCnt2
            }
            else if (edge.WindDelta == 0 && m_ClipType != ClipType.ctUnion)
            {
                edge.WindCnt = 1;
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }
            else if (IsEvenOddFillType(edge))
            {
                //EvenOdd filling ...
                if (edge.WindDelta == 0)
                {
                    //are we inside a subj polygon ...
                    bool Inside = true;
                    TEdge e2 = e.PrevInAEL;
                    while (e2 != null)
                    {
                        if (e2.PolyTyp == e.PolyTyp && e2.WindDelta != 0)
                            Inside = !Inside;
                        e2 = e2.PrevInAEL;
                    }
                    edge.WindCnt = (Inside ? 0 : 1);
                }
                else
                {
                    edge.WindCnt = edge.WindDelta;
                }
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                if (e.WindCnt * e.WindDelta < 0)
                {
                    //prev edge is 'decreasing' WindCount (WC) toward zero
                    //so we're outside the previous polygon ...
                    if (Math.Abs(e.WindCnt) > 1)
                    {
                        //outside prev poly but still inside another.
                        //when reversing direction of prev poly use the same WC 
                        if (e.WindDelta * edge.WindDelta < 0) edge.WindCnt = e.WindCnt;
                        //otherwise continue to 'decrease' WC ...
                        else edge.WindCnt = e.WindCnt + edge.WindDelta;
                    }
                    else
                        //now outside all polys of same polytype so set own WC ...
                        edge.WindCnt = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
                }
                else
                {
                    //prev edge is 'increasing' WindCount (WC) away from zero
                    //so we're inside the previous polygon ...
                    if (edge.WindDelta == 0)
                        edge.WindCnt = (e.WindCnt < 0 ? e.WindCnt - 1 : e.WindCnt + 1);
                    //if wind direction is reversing prev then use same WC
                    else if (e.WindDelta * edge.WindDelta < 0)
                        edge.WindCnt = e.WindCnt;
                    //otherwise add to WC ...
                    else edge.WindCnt = e.WindCnt + edge.WindDelta;
                }
                edge.WindCnt2 = e.WindCnt2;
                e = e.NextInAEL; //ie get ready to calc WindCnt2
            }

            //update WindCnt2 ...
            if (IsEvenOddAltFillType(edge))
            {
                //EvenOdd filling ...
                while (e != edge)
                {
                    if (e.WindDelta != 0)
                        edge.WindCnt2 = (edge.WindCnt2 == 0 ? 1 : 0);
                    e = e.NextInAEL;
                }
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                while (e != edge)
                {
                    edge.WindCnt2 += e.WindDelta;
                    e = e.NextInAEL;
                }
            }
        }
        //------------------------------------------------------------------------------

        private void AddEdgeToSEL(TEdge edge)
        {
            //SEL pointers in PEdge are reused to build a list of horizontal edges.
            //However, we don't need to worry about order with horizontal edge processing.
            if (m_SortedEdges == null)
            {
                m_SortedEdges = edge;
                edge.PrevInSEL = null;
                edge.NextInSEL = null;
            }
            else
            {
                edge.NextInSEL = m_SortedEdges;
                edge.PrevInSEL = null;
                m_SortedEdges.PrevInSEL = edge;
                m_SortedEdges = edge;
            }
        }
        //------------------------------------------------------------------------------

        private void CopyAELToSEL()
        {
            TEdge e = m_ActiveEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSEL = e.PrevInAEL;
                e.NextInSEL = e.NextInAEL;
                e = e.NextInAEL;
            }
        }
        //------------------------------------------------------------------------------

        private void SwapPositionsInAEL(TEdge edge1, TEdge edge2)
        {
            //check that one or other edge hasn't already been removed from AEL ...
            if (edge1.NextInAEL == edge1.PrevInAEL ||
              edge2.NextInAEL == edge2.PrevInAEL) return;

            if (edge1.NextInAEL == edge2)
            {
                TEdge next = edge2.NextInAEL;
                if (next != null)
                    next.PrevInAEL = edge1;
                TEdge prev = edge1.PrevInAEL;
                if (prev != null)
                    prev.NextInAEL = edge2;
                edge2.PrevInAEL = prev;
                edge2.NextInAEL = edge1;
                edge1.PrevInAEL = edge2;
                edge1.NextInAEL = next;
            }
            else if (edge2.NextInAEL == edge1)
            {
                TEdge next = edge1.NextInAEL;
                if (next != null)
                    next.PrevInAEL = edge2;
                TEdge prev = edge2.PrevInAEL;
                if (prev != null)
                    prev.NextInAEL = edge1;
                edge1.PrevInAEL = prev;
                edge1.NextInAEL = edge2;
                edge2.PrevInAEL = edge1;
                edge2.NextInAEL = next;
            }
            else
            {
                TEdge next = edge1.NextInAEL;
                TEdge prev = edge1.PrevInAEL;
                edge1.NextInAEL = edge2.NextInAEL;
                if (edge1.NextInAEL != null)
                    edge1.NextInAEL.PrevInAEL = edge1;
                edge1.PrevInAEL = edge2.PrevInAEL;
                if (edge1.PrevInAEL != null)
                    edge1.PrevInAEL.NextInAEL = edge1;
                edge2.NextInAEL = next;
                if (edge2.NextInAEL != null)
                    edge2.NextInAEL.PrevInAEL = edge2;
                edge2.PrevInAEL = prev;
                if (edge2.PrevInAEL != null)
                    edge2.PrevInAEL.NextInAEL = edge2;
            }

            if (edge1.PrevInAEL == null)
                m_ActiveEdges = edge1;
            else if (edge2.PrevInAEL == null)
                m_ActiveEdges = edge2;
        }
        //------------------------------------------------------------------------------

        private void SwapPositionsInSEL(TEdge edge1, TEdge edge2)
        {
            if (edge1.NextInSEL == null && edge1.PrevInSEL == null)
                return;
            if (edge2.NextInSEL == null && edge2.PrevInSEL == null)
                return;

            if (edge1.NextInSEL == edge2)
            {
                TEdge next = edge2.NextInSEL;
                if (next != null)
                    next.PrevInSEL = edge1;
                TEdge prev = edge1.PrevInSEL;
                if (prev != null)
                    prev.NextInSEL = edge2;
                edge2.PrevInSEL = prev;
                edge2.NextInSEL = edge1;
                edge1.PrevInSEL = edge2;
                edge1.NextInSEL = next;
            }
            else if (edge2.NextInSEL == edge1)
            {
                TEdge next = edge1.NextInSEL;
                if (next != null)
                    next.PrevInSEL = edge2;
                TEdge prev = edge2.PrevInSEL;
                if (prev != null)
                    prev.NextInSEL = edge1;
                edge1.PrevInSEL = prev;
                edge1.NextInSEL = edge2;
                edge2.PrevInSEL = edge1;
                edge2.NextInSEL = next;
            }
            else
            {
                TEdge next = edge1.NextInSEL;
                TEdge prev = edge1.PrevInSEL;
                edge1.NextInSEL = edge2.NextInSEL;
                if (edge1.NextInSEL != null)
                    edge1.NextInSEL.PrevInSEL = edge1;
                edge1.PrevInSEL = edge2.PrevInSEL;
                if (edge1.PrevInSEL != null)
                    edge1.PrevInSEL.NextInSEL = edge1;
                edge2.NextInSEL = next;
                if (edge2.NextInSEL != null)
                    edge2.NextInSEL.PrevInSEL = edge2;
                edge2.PrevInSEL = prev;
                if (edge2.PrevInSEL != null)
                    edge2.PrevInSEL.NextInSEL = edge2;
            }

            if (edge1.PrevInSEL == null)
                m_SortedEdges = edge1;
            else if (edge2.PrevInSEL == null)
                m_SortedEdges = edge2;
        }
        //------------------------------------------------------------------------------


        private void AddLocalMaxPoly(TEdge e1, TEdge e2, IntPoint pt)
        {
            AddOutPt(e1, pt);
            if (e2.WindDelta == 0) AddOutPt(e2, pt);
            if (e1.OutIdx == e2.OutIdx)
            {
                e1.OutIdx = Unassigned;
                e2.OutIdx = Unassigned;
            }
            else if (e1.OutIdx < e2.OutIdx)
                AppendPolygon(e1, e2);
            else
                AppendPolygon(e2, e1);
        }
        //------------------------------------------------------------------------------

        private OutPt AddLocalMinPoly(TEdge e1, TEdge e2, IntPoint pt)
        {
            OutPt result;
            TEdge e, prevE;
            if (IsHorizontal(e2) || (e1.Dx > e2.Dx))
            {
                result = AddOutPt(e1, pt);
                e2.OutIdx = e1.OutIdx;
                e1.Side = EdgeSide.esLeft;
                e2.Side = EdgeSide.esRight;
                e = e1;
                if (e.PrevInAEL == e2)
                    prevE = e2.PrevInAEL;
                else
                    prevE = e.PrevInAEL;
            }
            else
            {
                result = AddOutPt(e2, pt);
                e1.OutIdx = e2.OutIdx;
                e1.Side = EdgeSide.esRight;
                e2.Side = EdgeSide.esLeft;
                e = e2;
                if (e.PrevInAEL == e1)
                    prevE = e1.PrevInAEL;
                else
                    prevE = e.PrevInAEL;
            }

            if (prevE != null && prevE.OutIdx >= 0 &&
                (TopX(prevE, pt.Y) == TopX(e, pt.Y)) &&
                SlopesEqual(e, prevE, m_UseFullRange) &&
                (e.WindDelta != 0) && (prevE.WindDelta != 0))
            {
                OutPt outPt = AddOutPt(prevE, pt);
                AddJoin(result, outPt, e.Top);
            }
            return result;
        }
        //------------------------------------------------------------------------------

        private OutRec CreateOutRec()
        {
            OutRec result = new OutRec();
            result.Idx = Unassigned;
            result.IsHole = false;
            result.IsOpen = false;
            result.FirstLeft = null;
            result.Pts = null;
            result.BottomPt = null;
            result.PolyNode = null;
            m_PolyOuts.Add(result);
            result.Idx = m_PolyOuts.Count - 1;
            return result;
        }
        //------------------------------------------------------------------------------

        private OutPt AddOutPt(TEdge e, IntPoint pt)
        {
            bool ToFront = (e.Side == EdgeSide.esLeft);
            if (e.OutIdx < 0)
            {
                OutRec outRec = CreateOutRec();
                outRec.IsOpen = (e.WindDelta == 0);
                OutPt newOp = new OutPt();
                outRec.Pts = newOp;
                newOp.Idx = outRec.Idx;
                newOp.Pt = pt;
                newOp.Next = newOp;
                newOp.Prev = newOp;
                if (!outRec.IsOpen)
                    SetHoleState(e, outRec);
                e.OutIdx = outRec.Idx; //nb: do this after SetZ !
                return newOp;
            }
            else
            {
                OutRec outRec = m_PolyOuts[e.OutIdx];
                //OutRec.Pts is the 'Left-most' point & OutRec.Pts.Prev is the 'Right-most'
                OutPt op = outRec.Pts;
                if (ToFront && pt == op.Pt) return op;
                else if (!ToFront && pt == op.Prev.Pt) return op.Prev;

                OutPt newOp = new OutPt();
                newOp.Idx = outRec.Idx;
                newOp.Pt = pt;
                newOp.Next = op;
                newOp.Prev = op.Prev;
                newOp.Prev.Next = newOp;
                op.Prev = newOp;
                if (ToFront) outRec.Pts = newOp;
                return newOp;
            }
        }
        //------------------------------------------------------------------------------

        internal void SwapPoints(ref IntPoint pt1, ref IntPoint pt2)
        {
            IntPoint tmp = new IntPoint(pt1);
            pt1 = pt2;
            pt2 = tmp;
        }
        //------------------------------------------------------------------------------

        private bool HorzSegmentsOverlap(cInt seg1a, cInt seg1b, cInt seg2a, cInt seg2b)
        {
            if (seg1a > seg1b) Swap(ref seg1a, ref seg1b);
            if (seg2a > seg2b) Swap(ref seg2a, ref seg2b);
            return (seg1a < seg2b) && (seg2a < seg1b);
        }
        //------------------------------------------------------------------------------

        private void SetHoleState(TEdge e, OutRec outRec)
        {
            bool isHole = false;
            TEdge e2 = e.PrevInAEL;
            while (e2 != null)
            {
                if (e2.OutIdx >= 0 && e2.WindDelta != 0)
                {
                    isHole = !isHole;
                    if (outRec.FirstLeft == null)
                        outRec.FirstLeft = m_PolyOuts[e2.OutIdx];
                }
                e2 = e2.PrevInAEL;
            }
            if (isHole)
                outRec.IsHole = true;
        }
        //------------------------------------------------------------------------------

        private double GetDx(IntPoint pt1, IntPoint pt2)
        {
            if (pt1.Y == pt2.Y) return horizontal;
            else return (double)(pt2.X - pt1.X) / (pt2.Y - pt1.Y);
        }
        //---------------------------------------------------------------------------

        private bool FirstIsBottomPt(OutPt btmPt1, OutPt btmPt2)
        {
            OutPt p = btmPt1.Prev;
            while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Prev;
            double dx1p = Math.Abs(GetDx(btmPt1.Pt, p.Pt));
            p = btmPt1.Next;
            while ((p.Pt == btmPt1.Pt) && (p != btmPt1)) p = p.Next;
            double dx1n = Math.Abs(GetDx(btmPt1.Pt, p.Pt));

            p = btmPt2.Prev;
            while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Prev;
            double dx2p = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
            p = btmPt2.Next;
            while ((p.Pt == btmPt2.Pt) && (p != btmPt2)) p = p.Next;
            double dx2n = Math.Abs(GetDx(btmPt2.Pt, p.Pt));
            return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
        }
        //------------------------------------------------------------------------------

        private OutPt GetBottomPt(OutPt pp)
        {
            OutPt dups = null;
            OutPt p = pp.Next;
            while (p != pp)
            {
                if (p.Pt.Y > pp.Pt.Y)
                {
                    pp = p;
                    dups = null;
                }
                else if (p.Pt.Y == pp.Pt.Y && p.Pt.X <= pp.Pt.X)
                {
                    if (p.Pt.X < pp.Pt.X)
                    {
                        dups = null;
                        pp = p;
                    }
                    else
                    {
                        if (p.Next != pp && p.Prev != pp) dups = p;
                    }
                }
                p = p.Next;
            }
            if (dups != null)
            {
                //there appears to be at least 2 vertices at bottomPt so ...
                while (dups != p)
                {
                    if (!FirstIsBottomPt(p, dups)) pp = dups;
                    dups = dups.Next;
                    while (dups.Pt != pp.Pt) dups = dups.Next;
                }
            }
            return pp;
        }
        //------------------------------------------------------------------------------

        private OutRec GetLowermostRec(OutRec outRec1, OutRec outRec2)
        {
            //work out which polygon fragment has the correct hole state ...
            if (outRec1.BottomPt == null)
                outRec1.BottomPt = GetBottomPt(outRec1.Pts);
            if (outRec2.BottomPt == null)
                outRec2.BottomPt = GetBottomPt(outRec2.Pts);
            OutPt bPt1 = outRec1.BottomPt;
            OutPt bPt2 = outRec2.BottomPt;
            if (bPt1.Pt.Y > bPt2.Pt.Y) return outRec1;
            else if (bPt1.Pt.Y < bPt2.Pt.Y) return outRec2;
            else if (bPt1.Pt.X < bPt2.Pt.X) return outRec1;
            else if (bPt1.Pt.X > bPt2.Pt.X) return outRec2;
            else if (bPt1.Next == bPt1) return outRec2;
            else if (bPt2.Next == bPt2) return outRec1;
            else if (FirstIsBottomPt(bPt1, bPt2)) return outRec1;
            else return outRec2;
        }
        //------------------------------------------------------------------------------

        bool Param1RightOfParam2(OutRec outRec1, OutRec outRec2)
        {
            do
            {
                outRec1 = outRec1.FirstLeft;
                if (outRec1 == outRec2) return true;
            } while (outRec1 != null);
            return false;
        }
        //------------------------------------------------------------------------------

        private OutRec GetOutRec(int idx)
        {
            OutRec outrec = m_PolyOuts[idx];
            while (outrec != m_PolyOuts[outrec.Idx])
                outrec = m_PolyOuts[outrec.Idx];
            return outrec;
        }
        //------------------------------------------------------------------------------

        private void AppendPolygon(TEdge e1, TEdge e2)
        {
            //get the start and ends of both output polygons ...
            OutRec outRec1 = m_PolyOuts[e1.OutIdx];
            OutRec outRec2 = m_PolyOuts[e2.OutIdx];

            OutRec holeStateRec;
            if (Param1RightOfParam2(outRec1, outRec2))
                holeStateRec = outRec2;
            else if (Param1RightOfParam2(outRec2, outRec1))
                holeStateRec = outRec1;
            else
                holeStateRec = GetLowermostRec(outRec1, outRec2);

            OutPt p1_lft = outRec1.Pts;
            OutPt p1_rt = p1_lft.Prev;
            OutPt p2_lft = outRec2.Pts;
            OutPt p2_rt = p2_lft.Prev;

            EdgeSide side;
            //join e2 poly onto e1 poly and delete pointers to e2 ...
            if (e1.Side == EdgeSide.esLeft)
            {
                if (e2.Side == EdgeSide.esLeft)
                {
                    //z y x a b c
                    ReversePolyPtLinks(p2_lft);
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    outRec1.Pts = p2_rt;
                }
                else
                {
                    //x y z a b c
                    p2_rt.Next = p1_lft;
                    p1_lft.Prev = p2_rt;
                    p2_lft.Prev = p1_rt;
                    p1_rt.Next = p2_lft;
                    outRec1.Pts = p2_lft;
                }
                side = EdgeSide.esLeft;
            }
            else
            {
                if (e2.Side == EdgeSide.esRight)
                {
                    //a b c z y x
                    ReversePolyPtLinks(p2_lft);
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                }
                else
                {
                    //a b c x y z
                    p1_rt.Next = p2_lft;
                    p2_lft.Prev = p1_rt;
                    p1_lft.Prev = p2_rt;
                    p2_rt.Next = p1_lft;
                }
                side = EdgeSide.esRight;
            }

            outRec1.BottomPt = null;
            if (holeStateRec == outRec2)
            {
                if (outRec2.FirstLeft != outRec1)
                    outRec1.FirstLeft = outRec2.FirstLeft;
                outRec1.IsHole = outRec2.IsHole;
            }
            outRec2.Pts = null;
            outRec2.BottomPt = null;

            outRec2.FirstLeft = outRec1;

            int OKIdx = e1.OutIdx;
            int ObsoleteIdx = e2.OutIdx;

            e1.OutIdx = Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
            e2.OutIdx = Unassigned;

            TEdge e = m_ActiveEdges;
            while (e != null)
            {
                if (e.OutIdx == ObsoleteIdx)
                {
                    e.OutIdx = OKIdx;
                    e.Side = side;
                    break;
                }
                e = e.NextInAEL;
            }
            outRec2.Idx = outRec1.Idx;
        }
        //------------------------------------------------------------------------------

        private void ReversePolyPtLinks(OutPt pp)
        {
            if (pp == null) return;
            OutPt pp1;
            OutPt pp2;
            pp1 = pp;
            do
            {
                pp2 = pp1.Next;
                pp1.Next = pp1.Prev;
                pp1.Prev = pp2;
                pp1 = pp2;
            } while (pp1 != pp);
        }
        //------------------------------------------------------------------------------

        private static void SwapSides(TEdge edge1, TEdge edge2)
        {
            EdgeSide side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }
        //------------------------------------------------------------------------------

        private static void SwapPolyIndexes(TEdge edge1, TEdge edge2)
        {
            int outIdx = edge1.OutIdx;
            edge1.OutIdx = edge2.OutIdx;
            edge2.OutIdx = outIdx;
        }
        //------------------------------------------------------------------------------

        private void IntersectEdges(TEdge e1, TEdge e2, IntPoint pt)
        {
            //e1 will be to the left of e2 BELOW the intersection. Therefore e1 is before
            //e2 in AEL except when e1 is being inserted at the intersection point ...

            bool e1Contributing = (e1.OutIdx >= 0);
            bool e2Contributing = (e2.OutIdx >= 0);

#if use_xyz
          SetZ(ref pt, e1, e2);
#endif

#if use_lines
          //if either edge is on an OPEN path ...
          if (e1.WindDelta == 0 || e2.WindDelta == 0)
          {
            //ignore subject-subject open path intersections UNLESS they
            //are both open paths, AND they are both 'contributing maximas' ...
            if (e1.WindDelta == 0 && e2.WindDelta == 0) return;
            //if intersecting a subj line with a subj poly ...
            else if (e1.PolyTyp == e2.PolyTyp && 
              e1.WindDelta != e2.WindDelta && m_ClipType == ClipType.ctUnion)
            {
              if (e1.WindDelta == 0)
              {
                if (e2Contributing)
                {
                  AddOutPt(e1, pt);
                  if (e1Contributing) e1.OutIdx = Unassigned;
                }
              }
              else
              {
                if (e1Contributing)
                {
                  AddOutPt(e2, pt);
                  if (e2Contributing) e2.OutIdx = Unassigned;
                }
              }
            }
            else if (e1.PolyTyp != e2.PolyTyp)
            {
              if ((e1.WindDelta == 0) && Math.Abs(e2.WindCnt) == 1 && 
                (m_ClipType != ClipType.ctUnion || e2.WindCnt2 == 0))
              {
                AddOutPt(e1, pt);
                if (e1Contributing) e1.OutIdx = Unassigned;
              }
              else if ((e2.WindDelta == 0) && (Math.Abs(e1.WindCnt) == 1) && 
                (m_ClipType != ClipType.ctUnion || e1.WindCnt2 == 0))
              {
                AddOutPt(e2, pt);
                if (e2Contributing) e2.OutIdx = Unassigned;
              }
            }
            return;
          }
#endif

            //update winding counts...
            //assumes that e1 will be to the Right of e2 ABOVE the intersection
            if (e1.PolyTyp == e2.PolyTyp)
            {
                if (IsEvenOddFillType(e1))
                {
                    int oldE1WindCnt = e1.WindCnt;
                    e1.WindCnt = e2.WindCnt;
                    e2.WindCnt = oldE1WindCnt;
                }
                else
                {
                    if (e1.WindCnt + e2.WindDelta == 0) e1.WindCnt = -e1.WindCnt;
                    else e1.WindCnt += e2.WindDelta;
                    if (e2.WindCnt - e1.WindDelta == 0) e2.WindCnt = -e2.WindCnt;
                    else e2.WindCnt -= e1.WindDelta;
                }
            }
            else
            {
                if (!IsEvenOddFillType(e2)) e1.WindCnt2 += e2.WindDelta;
                else e1.WindCnt2 = (e1.WindCnt2 == 0) ? 1 : 0;
                if (!IsEvenOddFillType(e1)) e2.WindCnt2 -= e1.WindDelta;
                else e2.WindCnt2 = (e2.WindCnt2 == 0) ? 1 : 0;
            }

            PolyFillType e1FillType, e2FillType, e1FillType2, e2FillType2;
            if (e1.PolyTyp == PolyType.ptSubject)
            {
                e1FillType = m_SubjFillType;
                e1FillType2 = m_ClipFillType;
            }
            else
            {
                e1FillType = m_ClipFillType;
                e1FillType2 = m_SubjFillType;
            }
            if (e2.PolyTyp == PolyType.ptSubject)
            {
                e2FillType = m_SubjFillType;
                e2FillType2 = m_ClipFillType;
            }
            else
            {
                e2FillType = m_ClipFillType;
                e2FillType2 = m_SubjFillType;
            }

            int e1Wc, e2Wc;
            switch (e1FillType)
            {
                case PolyFillType.pftPositive: e1Wc = e1.WindCnt; break;
                case PolyFillType.pftNegative: e1Wc = -e1.WindCnt; break;
                default: e1Wc = Math.Abs(e1.WindCnt); break;
            }
            switch (e2FillType)
            {
                case PolyFillType.pftPositive: e2Wc = e2.WindCnt; break;
                case PolyFillType.pftNegative: e2Wc = -e2.WindCnt; break;
                default: e2Wc = Math.Abs(e2.WindCnt); break;
            }

            if (e1Contributing && e2Contributing)
            {
                if ((e1Wc != 0 && e1Wc != 1) || (e2Wc != 0 && e2Wc != 1) ||
                  (e1.PolyTyp != e2.PolyTyp && m_ClipType != ClipType.ctXor))
                {
                    AddLocalMaxPoly(e1, e2, pt);
                }
                else
                {
                    AddOutPt(e1, pt);
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if (e1Contributing)
            {
                if (e2Wc == 0 || e2Wc == 1)
                {
                    AddOutPt(e1, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }

            }
            else if (e2Contributing)
            {
                if (e1Wc == 0 || e1Wc == 1)
                {
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if ((e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
            {
                //neither edge is currently contributing ...
                cInt e1Wc2, e2Wc2;
                switch (e1FillType2)
                {
                    case PolyFillType.pftPositive: e1Wc2 = e1.WindCnt2; break;
                    case PolyFillType.pftNegative: e1Wc2 = -e1.WindCnt2; break;
                    default: e1Wc2 = Math.Abs(e1.WindCnt2); break;
                }
                switch (e2FillType2)
                {
                    case PolyFillType.pftPositive: e2Wc2 = e2.WindCnt2; break;
                    case PolyFillType.pftNegative: e2Wc2 = -e2.WindCnt2; break;
                    default: e2Wc2 = Math.Abs(e2.WindCnt2); break;
                }

                if (e1.PolyTyp != e2.PolyTyp)
                {
                    AddLocalMinPoly(e1, e2, pt);
                }
                else if (e1Wc == 1 && e2Wc == 1)
                    switch (m_ClipType)
                    {
                        case ClipType.ctIntersection:
                            if (e1Wc2 > 0 && e2Wc2 > 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.ctUnion:
                            if (e1Wc2 <= 0 && e2Wc2 <= 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.ctDifference:
                            if (((e1.PolyTyp == PolyType.ptClip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((e1.PolyTyp == PolyType.ptSubject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipType.ctXor:
                            AddLocalMinPoly(e1, e2, pt);
                            break;
                    }
                else
                    SwapSides(e1, e2);
            }
        }
        //------------------------------------------------------------------------------

        private void DeleteFromAEL(TEdge e)
        {
            TEdge AelPrev = e.PrevInAEL;
            TEdge AelNext = e.NextInAEL;
            if (AelPrev == null && AelNext == null && (e != m_ActiveEdges))
                return; //already deleted
            if (AelPrev != null)
                AelPrev.NextInAEL = AelNext;
            else m_ActiveEdges = AelNext;
            if (AelNext != null)
                AelNext.PrevInAEL = AelPrev;
            e.NextInAEL = null;
            e.PrevInAEL = null;
        }
        //------------------------------------------------------------------------------

        private void DeleteFromSEL(TEdge e)
        {
            TEdge SelPrev = e.PrevInSEL;
            TEdge SelNext = e.NextInSEL;
            if (SelPrev == null && SelNext == null && (e != m_SortedEdges))
                return; //already deleted
            if (SelPrev != null)
                SelPrev.NextInSEL = SelNext;
            else m_SortedEdges = SelNext;
            if (SelNext != null)
                SelNext.PrevInSEL = SelPrev;
            e.NextInSEL = null;
            e.PrevInSEL = null;
        }
        //------------------------------------------------------------------------------

        private void UpdateEdgeIntoAEL(ref TEdge e)
        {
            if (e.NextInLML == null)
                throw new ClipperException("UpdateEdgeIntoAEL: invalid call");
            TEdge AelPrev = e.PrevInAEL;
            TEdge AelNext = e.NextInAEL;
            e.NextInLML.OutIdx = e.OutIdx;
            if (AelPrev != null)
                AelPrev.NextInAEL = e.NextInLML;
            else m_ActiveEdges = e.NextInLML;
            if (AelNext != null)
                AelNext.PrevInAEL = e.NextInLML;
            e.NextInLML.Side = e.Side;
            e.NextInLML.WindDelta = e.WindDelta;
            e.NextInLML.WindCnt = e.WindCnt;
            e.NextInLML.WindCnt2 = e.WindCnt2;
            e = e.NextInLML;
            e.Curr = e.Bot;
            e.PrevInAEL = AelPrev;
            e.NextInAEL = AelNext;
            if (!IsHorizontal(e)) InsertScanbeam(e.Top.Y);
        }
        //------------------------------------------------------------------------------

        private void ProcessHorizontals(bool isTopOfScanbeam)
        {
            TEdge horzEdge = m_SortedEdges;
            while (horzEdge != null)
            {
                DeleteFromSEL(horzEdge);
                ProcessHorizontal(horzEdge, isTopOfScanbeam);
                horzEdge = m_SortedEdges;
            }
        }
        //------------------------------------------------------------------------------

        void GetHorzDirection(TEdge HorzEdge, out Direction Dir, out cInt Left, out cInt Right)
        {
            if (HorzEdge.Bot.X < HorzEdge.Top.X)
            {
                Left = HorzEdge.Bot.X;
                Right = HorzEdge.Top.X;
                Dir = Direction.dLeftToRight;
            }
            else
            {
                Left = HorzEdge.Top.X;
                Right = HorzEdge.Bot.X;
                Dir = Direction.dRightToLeft;
            }
        }
        //------------------------------------------------------------------------

        private void ProcessHorizontal(TEdge horzEdge, bool isTopOfScanbeam)
        {
            Direction dir;
            cInt horzLeft, horzRight;

            GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

            TEdge eLastHorz = horzEdge, eMaxPair = null;
            while (eLastHorz.NextInLML != null && IsHorizontal(eLastHorz.NextInLML))
                eLastHorz = eLastHorz.NextInLML;
            if (eLastHorz.NextInLML == null)
                eMaxPair = GetMaximaPair(eLastHorz);

            for (; ; )
            {
                bool IsLastHorz = (horzEdge == eLastHorz);
                TEdge e = GetNextInAEL(horzEdge, dir);
                while (e != null)
                {
                    //Break if we've got to the end of an intermediate horizontal edge ...
                    //nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
                    if (e.Curr.X == horzEdge.Top.X && horzEdge.NextInLML != null &&
                      e.Dx < horzEdge.NextInLML.Dx) break;

                    TEdge eNext = GetNextInAEL(e, dir); //saves eNext for later

                    if ((dir == Direction.dLeftToRight && e.Curr.X <= horzRight) ||
                      (dir == Direction.dRightToLeft && e.Curr.X >= horzLeft))
                    {
                        //so far we're still in range of the horizontal Edge  but make sure
                        //we're at the last of consec. horizontals when matching with eMaxPair
                        if (e == eMaxPair && IsLastHorz)
                        {
                            if (horzEdge.OutIdx >= 0)
                            {
                                OutPt op1 = AddOutPt(horzEdge, horzEdge.Top);
                                TEdge eNextHorz = m_SortedEdges;
                                while (eNextHorz != null)
                                {
                                    if (eNextHorz.OutIdx >= 0 &&
                                      HorzSegmentsOverlap(horzEdge.Bot.X,
                                      horzEdge.Top.X, eNextHorz.Bot.X, eNextHorz.Top.X))
                                    {
                                        OutPt op2 = AddOutPt(eNextHorz, eNextHorz.Bot);
                                        AddJoin(op2, op1, eNextHorz.Top);
                                    }
                                    eNextHorz = eNextHorz.NextInSEL;
                                }
                                AddGhostJoin(op1, horzEdge.Bot);
                                AddLocalMaxPoly(horzEdge, eMaxPair, horzEdge.Top);
                            }
                            DeleteFromAEL(horzEdge);
                            DeleteFromAEL(eMaxPair);
                            return;
                        }
                        else if (dir == Direction.dLeftToRight)
                        {
                            IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
                            IntersectEdges(horzEdge, e, Pt);
                        }
                        else
                        {
                            IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
                            IntersectEdges(e, horzEdge, Pt);
                        }
                        SwapPositionsInAEL(horzEdge, e);
                    }
                    else if ((dir == Direction.dLeftToRight && e.Curr.X >= horzRight) ||
                      (dir == Direction.dRightToLeft && e.Curr.X <= horzLeft)) break;
                    e = eNext;
                } //end while

                if (horzEdge.NextInLML != null && IsHorizontal(horzEdge.NextInLML))
                {
                    UpdateEdgeIntoAEL(ref horzEdge);
                    if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Bot);
                    GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);
                }
                else
                    break;
            } //end for (;;)

            if (horzEdge.NextInLML != null)
            {
                if (horzEdge.OutIdx >= 0)
                {
                    OutPt op1 = AddOutPt(horzEdge, horzEdge.Top);
                    if (isTopOfScanbeam) AddGhostJoin(op1, horzEdge.Bot);

                    UpdateEdgeIntoAEL(ref horzEdge);
                    if (horzEdge.WindDelta == 0) return;
                    //nb: HorzEdge is no longer horizontal here
                    TEdge ePrev = horzEdge.PrevInAEL;
                    TEdge eNext = horzEdge.NextInAEL;
                    if (ePrev != null && ePrev.Curr.X == horzEdge.Bot.X &&
                      ePrev.Curr.Y == horzEdge.Bot.Y && ePrev.WindDelta != 0 &&
                      (ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
                      SlopesEqual(horzEdge, ePrev, m_UseFullRange)))
                    {
                        OutPt op2 = AddOutPt(ePrev, horzEdge.Bot);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                    else if (eNext != null && eNext.Curr.X == horzEdge.Bot.X &&
                      eNext.Curr.Y == horzEdge.Bot.Y && eNext.WindDelta != 0 &&
                      eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
                      SlopesEqual(horzEdge, eNext, m_UseFullRange))
                    {
                        OutPt op2 = AddOutPt(eNext, horzEdge.Bot);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                }
                else
                    UpdateEdgeIntoAEL(ref horzEdge);
            }
            else
            {
                if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Top);
                DeleteFromAEL(horzEdge);
            }
        }
        //------------------------------------------------------------------------------

        private TEdge GetNextInAEL(TEdge e, Direction Direction)
        {
            return Direction == Direction.dLeftToRight ? e.NextInAEL : e.PrevInAEL;
        }
        //------------------------------------------------------------------------------

        private bool IsMinima(TEdge e)
        {
            return e != null && (e.Prev.NextInLML != e) && (e.Next.NextInLML != e);
        }
        //------------------------------------------------------------------------------

        private bool IsMaxima(TEdge e, double Y)
        {
            return (e != null && e.Top.Y == Y && e.NextInLML == null);
        }
        //------------------------------------------------------------------------------

        private bool IsIntermediate(TEdge e, double Y)
        {
            return (e.Top.Y == Y && e.NextInLML != null);
        }
        //------------------------------------------------------------------------------

        private TEdge GetMaximaPair(TEdge e)
        {
            TEdge result = null;
            if ((e.Next.Top == e.Top) && e.Next.NextInLML == null)
                result = e.Next;
            else if ((e.Prev.Top == e.Top) && e.Prev.NextInLML == null)
                result = e.Prev;
            if (result != null && (result.OutIdx == Skip ||
              (result.NextInAEL == result.PrevInAEL && !IsHorizontal(result))))
                return null;
            return result;
        }
        //------------------------------------------------------------------------------

        private bool ProcessIntersections(cInt topY)
        {
            if (m_ActiveEdges == null) return true;
            try
            {
                BuildIntersectList(topY);
                if (m_IntersectList.Count == 0) return true;
                if (m_IntersectList.Count == 1 || FixupIntersectionOrder())
                    ProcessIntersectList();
                else
                    return false;
            }
            catch
            {
                m_SortedEdges = null;
                m_IntersectList.Clear();
                throw new ClipperException("ProcessIntersections error");
            }
            m_SortedEdges = null;
            return true;
        }
        //------------------------------------------------------------------------------

        private void BuildIntersectList(cInt topY)
        {
            if (m_ActiveEdges == null) return;

            //prepare for sorting ...
            TEdge e = m_ActiveEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSEL = e.PrevInAEL;
                e.NextInSEL = e.NextInAEL;
                e.Curr.X = TopX(e, topY);
                e = e.NextInAEL;
            }

            //bubblesort ...
            bool isModified = true;
            while (isModified && m_SortedEdges != null)
            {
                isModified = false;
                e = m_SortedEdges;
                while (e.NextInSEL != null)
                {
                    TEdge eNext = e.NextInSEL;
                    IntPoint pt;
                    if (e.Curr.X > eNext.Curr.X)
                    {
                        IntersectPoint(e, eNext, out pt);
                        IntersectNode newNode = new IntersectNode();
                        newNode.Edge1 = e;
                        newNode.Edge2 = eNext;
                        newNode.Pt = pt;
                        m_IntersectList.Add(newNode);

                        SwapPositionsInSEL(e, eNext);
                        isModified = true;
                    }
                    else
                        e = eNext;
                }
                if (e.PrevInSEL != null) e.PrevInSEL.NextInSEL = null;
                else break;
            }
            m_SortedEdges = null;
        }
        //------------------------------------------------------------------------------

        private bool EdgesAdjacent(IntersectNode inode)
        {
            return (inode.Edge1.NextInSEL == inode.Edge2) ||
              (inode.Edge1.PrevInSEL == inode.Edge2);
        }
        //------------------------------------------------------------------------------

        private static int IntersectNodeSort(IntersectNode node1, IntersectNode node2)
        {
            //the following typecast is safe because the differences in Pt.Y will
            //be limited to the height of the scanbeam.
            return (int)(node2.Pt.Y - node1.Pt.Y);
        }
        //------------------------------------------------------------------------------

        private bool FixupIntersectionOrder()
        {
            //pre-condition: intersections are sorted bottom-most first.
            //Now it's crucial that intersections are made only between adjacent edges,
            //so to ensure this the order of intersections may need adjusting ...
            m_IntersectList.Sort(m_IntersectNodeComparer);

            CopyAELToSEL();
            int cnt = m_IntersectList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (!EdgesAdjacent(m_IntersectList[i]))
                {
                    int j = i + 1;
                    while (j < cnt && !EdgesAdjacent(m_IntersectList[j])) j++;
                    if (j == cnt) return false;

                    IntersectNode tmp = m_IntersectList[i];
                    m_IntersectList[i] = m_IntersectList[j];
                    m_IntersectList[j] = tmp;

                }
                SwapPositionsInSEL(m_IntersectList[i].Edge1, m_IntersectList[i].Edge2);
            }
            return true;
        }
        //------------------------------------------------------------------------------

        private void ProcessIntersectList()
        {
            for (int i = 0; i < m_IntersectList.Count; i++)
            {
                IntersectNode iNode = m_IntersectList[i];
                {
                    IntersectEdges(iNode.Edge1, iNode.Edge2, iNode.Pt);
                    SwapPositionsInAEL(iNode.Edge1, iNode.Edge2);
                }
            }
            m_IntersectList.Clear();
        }
        //------------------------------------------------------------------------------

        internal static cInt Round(double value)
        {
            return value < 0 ? (cInt)(value - 0.5) : (cInt)(value + 0.5);
        }
        //------------------------------------------------------------------------------

        private static cInt TopX(TEdge edge, cInt currentY)
        {
            if (currentY == edge.Top.Y)
                return edge.Top.X;
            return edge.Bot.X + Round(edge.Dx * (currentY - edge.Bot.Y));
        }
        //------------------------------------------------------------------------------

        private void IntersectPoint(TEdge edge1, TEdge edge2, out IntPoint ip)
        {
            ip = new IntPoint();
            double b1, b2;
            //nb: with very large coordinate values, it's possible for SlopesEqual() to 
            //return false but for the edge.Dx value be equal due to double precision rounding.
            if (edge1.Dx == edge2.Dx)
            {
                ip.Y = edge1.Curr.Y;
                ip.X = TopX(edge1, ip.Y);
                return;
            }

            if (edge1.Delta.X == 0)
            {
                ip.X = edge1.Bot.X;
                if (IsHorizontal(edge2))
                {
                    ip.Y = edge2.Bot.Y;
                }
                else
                {
                    b2 = edge2.Bot.Y - (edge2.Bot.X / edge2.Dx);
                    ip.Y = Round(ip.X / edge2.Dx + b2);
                }
            }
            else if (edge2.Delta.X == 0)
            {
                ip.X = edge2.Bot.X;
                if (IsHorizontal(edge1))
                {
                    ip.Y = edge1.Bot.Y;
                }
                else
                {
                    b1 = edge1.Bot.Y - (edge1.Bot.X / edge1.Dx);
                    ip.Y = Round(ip.X / edge1.Dx + b1);
                }
            }
            else
            {
                b1 = edge1.Bot.X - edge1.Bot.Y * edge1.Dx;
                b2 = edge2.Bot.X - edge2.Bot.Y * edge2.Dx;
                double q = (b2 - b1) / (edge1.Dx - edge2.Dx);
                ip.Y = Round(q);
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    ip.X = Round(edge1.Dx * q + b1);
                else
                    ip.X = Round(edge2.Dx * q + b2);
            }

            if (ip.Y < edge1.Top.Y || ip.Y < edge2.Top.Y)
            {
                if (edge1.Top.Y > edge2.Top.Y)
                    ip.Y = edge1.Top.Y;
                else
                    ip.Y = edge2.Top.Y;
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    ip.X = TopX(edge1, ip.Y);
                else
                    ip.X = TopX(edge2, ip.Y);
            }
            //finally, don't allow 'ip' to be BELOW curr.Y (ie bottom of scanbeam) ...
            if (ip.Y > edge1.Curr.Y)
            {
                ip.Y = edge1.Curr.Y;
                //better to use the more vertical edge to derive X ...
                if (Math.Abs(edge1.Dx) > Math.Abs(edge2.Dx))
                    ip.X = TopX(edge2, ip.Y);
                else
                    ip.X = TopX(edge1, ip.Y);
            }
        }
        //------------------------------------------------------------------------------

        private void ProcessEdgesAtTopOfScanbeam(cInt topY)
        {
            TEdge e = m_ActiveEdges;
            while (e != null)
            {
                //1. process maxima, treating them as if they're 'bent' horizontal edges,
                //   but exclude maxima with horizontal edges. nb: e can't be a horizontal.
                bool IsMaximaEdge = IsMaxima(e, topY);

                if (IsMaximaEdge)
                {
                    TEdge eMaxPair = GetMaximaPair(e);
                    IsMaximaEdge = (eMaxPair == null || !IsHorizontal(eMaxPair));
                }

                if (IsMaximaEdge)
                {
                    TEdge ePrev = e.PrevInAEL;
                    DoMaxima(e);
                    if (ePrev == null) e = m_ActiveEdges;
                    else e = ePrev.NextInAEL;
                }
                else
                {
                    //2. promote horizontal edges, otherwise update Curr.X and Curr.Y ...
                    if (IsIntermediate(e, topY) && IsHorizontal(e.NextInLML))
                    {
                        UpdateEdgeIntoAEL(ref e);
                        if (e.OutIdx >= 0)
                            AddOutPt(e, e.Bot);
                        AddEdgeToSEL(e);
                    }
                    else
                    {
                        e.Curr.X = TopX(e, topY);
                        e.Curr.Y = topY;
                    }

                    if (StrictlySimple)
                    {
                        TEdge ePrev = e.PrevInAEL;
                        if ((e.OutIdx >= 0) && (e.WindDelta != 0) && ePrev != null &&
                          (ePrev.OutIdx >= 0) && (ePrev.Curr.X == e.Curr.X) &&
                          (ePrev.WindDelta != 0))
                        {
                            IntPoint ip = new IntPoint(e.Curr);
#if use_xyz
                SetZ(ref ip, ePrev, e);
#endif
                            OutPt op = AddOutPt(ePrev, ip);
                            OutPt op2 = AddOutPt(e, ip);
                            AddJoin(op, op2, ip); //StrictlySimple (type-3) join
                        }
                    }

                    e = e.NextInAEL;
                }
            }

            //3. Process horizontals at the Top of the scanbeam ...
            ProcessHorizontals(true);

            //4. Promote intermediate vertices ...
            e = m_ActiveEdges;
            while (e != null)
            {
                if (IsIntermediate(e, topY))
                {
                    OutPt op = null;
                    if (e.OutIdx >= 0)
                        op = AddOutPt(e, e.Top);
                    UpdateEdgeIntoAEL(ref e);

                    //if output polygons share an edge, they'll need joining later ...
                    TEdge ePrev = e.PrevInAEL;
                    TEdge eNext = e.NextInAEL;
                    if (ePrev != null && ePrev.Curr.X == e.Bot.X &&
                      ePrev.Curr.Y == e.Bot.Y && op != null &&
                      ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
                      SlopesEqual(e, ePrev, m_UseFullRange) &&
                      (e.WindDelta != 0) && (ePrev.WindDelta != 0))
                    {
                        OutPt op2 = AddOutPt(ePrev, e.Bot);
                        AddJoin(op, op2, e.Top);
                    }
                    else if (eNext != null && eNext.Curr.X == e.Bot.X &&
                      eNext.Curr.Y == e.Bot.Y && op != null &&
                      eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
                      SlopesEqual(e, eNext, m_UseFullRange) &&
                      (e.WindDelta != 0) && (eNext.WindDelta != 0))
                    {
                        OutPt op2 = AddOutPt(eNext, e.Bot);
                        AddJoin(op, op2, e.Top);
                    }
                }
                e = e.NextInAEL;
            }
        }
        //------------------------------------------------------------------------------

        private void DoMaxima(TEdge e)
        {
            TEdge eMaxPair = GetMaximaPair(e);
            if (eMaxPair == null)
            {
                if (e.OutIdx >= 0)
                    AddOutPt(e, e.Top);
                DeleteFromAEL(e);
                return;
            }

            TEdge eNext = e.NextInAEL;
            while (eNext != null && eNext != eMaxPair)
            {
                IntersectEdges(e, eNext, e.Top);
                SwapPositionsInAEL(e, eNext);
                eNext = e.NextInAEL;
            }

            if (e.OutIdx == Unassigned && eMaxPair.OutIdx == Unassigned)
            {
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
            else if (e.OutIdx >= 0 && eMaxPair.OutIdx >= 0)
            {
                if (e.OutIdx >= 0) AddLocalMaxPoly(e, eMaxPair, e.Top);
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
#if use_lines
        else if (e.WindDelta == 0)
        {
          if (e.OutIdx >= 0) 
          {
            AddOutPt(e, e.Top);
            e.OutIdx = Unassigned;
          }
          DeleteFromAEL(e);

          if (eMaxPair.OutIdx >= 0)
          {
            AddOutPt(eMaxPair, e.Top);
            eMaxPair.OutIdx = Unassigned;
          }
          DeleteFromAEL(eMaxPair);
        } 
#endif
            else throw new ClipperException("DoMaxima error");
        }
        //------------------------------------------------------------------------------

        public static void ReversePaths(Paths polys)
        {
            foreach (var poly in polys) { poly.Reverse(); }
        }
        //------------------------------------------------------------------------------

        public static bool Orientation(Path poly)
        {
            return Area(poly) >= 0;
        }
        //------------------------------------------------------------------------------

        private int PointCount(OutPt pts)
        {
            if (pts == null) return 0;
            int result = 0;
            OutPt p = pts;
            do
            {
                result++;
                p = p.Next;
            }
            while (p != pts);
            return result;
        }
        //------------------------------------------------------------------------------

        private void BuildResult(Paths polyg)
        {
            polyg.Clear();
            polyg.Capacity = m_PolyOuts.Count;
            for (int i = 0; i < m_PolyOuts.Count; i++)
            {
                OutRec outRec = m_PolyOuts[i];
                if (outRec.Pts == null) continue;
                OutPt p = outRec.Pts.Prev;
                int cnt = PointCount(p);
                if (cnt < 2) continue;
                Path pg = new Path(cnt);
                for (int j = 0; j < cnt; j++)
                {
                    pg.Add(p.Pt);
                    p = p.Prev;
                }
                polyg.Add(pg);
            }
        }
        //------------------------------------------------------------------------------

        private void BuildResult2(PolyTree polytree)
        {
            polytree.Clear();

            //add each output polygon/contour to polytree ...
            polytree.m_AllPolys.Capacity = m_PolyOuts.Count;
            for (int i = 0; i < m_PolyOuts.Count; i++)
            {
                OutRec outRec = m_PolyOuts[i];
                int cnt = PointCount(outRec.Pts);
                if ((outRec.IsOpen && cnt < 2) ||
                  (!outRec.IsOpen && cnt < 3)) continue;
                FixHoleLinkage(outRec);
                PolyNode pn = new PolyNode();
                polytree.m_AllPolys.Add(pn);
                outRec.PolyNode = pn;
                pn.m_polygon.Capacity = cnt;
                OutPt op = outRec.Pts.Prev;
                for (int j = 0; j < cnt; j++)
                {
                    pn.m_polygon.Add(op.Pt);
                    op = op.Prev;
                }
            }

            //fixup PolyNode links etc ...
            polytree.m_Childs.Capacity = m_PolyOuts.Count;
            for (int i = 0; i < m_PolyOuts.Count; i++)
            {
                OutRec outRec = m_PolyOuts[i];
                if (outRec.PolyNode == null) continue;
                else if (outRec.IsOpen)
                {
                    outRec.PolyNode.IsOpen = true;
                    polytree.AddChild(outRec.PolyNode);
                }
                else if (outRec.FirstLeft != null &&
                  outRec.FirstLeft.PolyNode != null)
                    outRec.FirstLeft.PolyNode.AddChild(outRec.PolyNode);
                else
                    polytree.AddChild(outRec.PolyNode);
            }
        }
        //------------------------------------------------------------------------------

        private void FixupOutPolygon(OutRec outRec)
        {
            //FixupOutPolygon() - removes duplicate points and simplifies consecutive
            //parallel edges by removing the middle vertex.
            OutPt lastOK = null;
            outRec.BottomPt = null;
            OutPt pp = outRec.Pts;
            for (; ; )
            {
                if (pp.Prev == pp || pp.Prev == pp.Next)
                {
                    outRec.Pts = null;
                    return;
                }
                //test for duplicate points and collinear edges ...
                if ((pp.Pt == pp.Next.Pt) || (pp.Pt == pp.Prev.Pt) ||
                  (SlopesEqual(pp.Prev.Pt, pp.Pt, pp.Next.Pt, m_UseFullRange) &&
                  (!PreserveCollinear || !Pt2IsBetweenPt1AndPt3(pp.Prev.Pt, pp.Pt, pp.Next.Pt))))
                {
                    lastOK = null;
                    pp.Prev.Next = pp.Next;
                    pp.Next.Prev = pp.Prev;
                    pp = pp.Prev;
                }
                else if (pp == lastOK) break;
                else
                {
                    if (lastOK == null) lastOK = pp;
                    pp = pp.Next;
                }
            }
            outRec.Pts = pp;
        }
        //------------------------------------------------------------------------------

        OutPt DupOutPt(OutPt outPt, bool InsertAfter)
        {
            OutPt result = new OutPt();
            result.Pt = outPt.Pt;
            result.Idx = outPt.Idx;
            if (InsertAfter)
            {
                result.Next = outPt.Next;
                result.Prev = outPt;
                outPt.Next.Prev = result;
                outPt.Next = result;
            }
            else
            {
                result.Prev = outPt.Prev;
                result.Next = outPt;
                outPt.Prev.Next = result;
                outPt.Prev = result;
            }
            return result;
        }
        //------------------------------------------------------------------------------

        bool GetOverlap(cInt a1, cInt a2, cInt b1, cInt b2, out cInt Left, out cInt Right)
        {
            if (a1 < a2)
            {
                if (b1 < b2) { Left = Math.Max(a1, b1); Right = Math.Min(a2, b2); }
                else { Left = Math.Max(a1, b2); Right = Math.Min(a2, b1); }
            }
            else
            {
                if (b1 < b2) { Left = Math.Max(a2, b1); Right = Math.Min(a1, b2); }
                else { Left = Math.Max(a2, b2); Right = Math.Min(a1, b1); }
            }
            return Left < Right;
        }
        //------------------------------------------------------------------------------

        bool JoinHorz(OutPt op1, OutPt op1b, OutPt op2, OutPt op2b,
          IntPoint Pt, bool DiscardLeft)
        {
            Direction Dir1 = (op1.Pt.X > op1b.Pt.X ?
              Direction.dRightToLeft : Direction.dLeftToRight);
            Direction Dir2 = (op2.Pt.X > op2b.Pt.X ?
              Direction.dRightToLeft : Direction.dLeftToRight);
            if (Dir1 == Dir2) return false;

            //When DiscardLeft, we want Op1b to be on the Left of Op1, otherwise we
            //want Op1b to be on the Right. (And likewise with Op2 and Op2b.)
            //So, to facilitate this while inserting Op1b and Op2b ...
            //when DiscardLeft, make sure we're AT or RIGHT of Pt before adding Op1b,
            //otherwise make sure we're AT or LEFT of Pt. (Likewise with Op2b.)
            if (Dir1 == Direction.dLeftToRight)
            {
                while (op1.Next.Pt.X <= Pt.X &&
                  op1.Next.Pt.X >= op1.Pt.X && op1.Next.Pt.Y == Pt.Y)
                    op1 = op1.Next;
                if (DiscardLeft && (op1.Pt.X != Pt.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, !DiscardLeft);
                if (op1b.Pt != Pt)
                {
                    op1 = op1b;
                    op1.Pt = Pt;
                    op1b = DupOutPt(op1, !DiscardLeft);
                }
            }
            else
            {
                while (op1.Next.Pt.X >= Pt.X &&
                  op1.Next.Pt.X <= op1.Pt.X && op1.Next.Pt.Y == Pt.Y)
                    op1 = op1.Next;
                if (!DiscardLeft && (op1.Pt.X != Pt.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, DiscardLeft);
                if (op1b.Pt != Pt)
                {
                    op1 = op1b;
                    op1.Pt = Pt;
                    op1b = DupOutPt(op1, DiscardLeft);
                }
            }

            if (Dir2 == Direction.dLeftToRight)
            {
                while (op2.Next.Pt.X <= Pt.X &&
                  op2.Next.Pt.X >= op2.Pt.X && op2.Next.Pt.Y == Pt.Y)
                    op2 = op2.Next;
                if (DiscardLeft && (op2.Pt.X != Pt.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, !DiscardLeft);
                if (op2b.Pt != Pt)
                {
                    op2 = op2b;
                    op2.Pt = Pt;
                    op2b = DupOutPt(op2, !DiscardLeft);
                };
            }
            else
            {
                while (op2.Next.Pt.X >= Pt.X &&
                  op2.Next.Pt.X <= op2.Pt.X && op2.Next.Pt.Y == Pt.Y)
                    op2 = op2.Next;
                if (!DiscardLeft && (op2.Pt.X != Pt.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, DiscardLeft);
                if (op2b.Pt != Pt)
                {
                    op2 = op2b;
                    op2.Pt = Pt;
                    op2b = DupOutPt(op2, DiscardLeft);
                };
            };

            if ((Dir1 == Direction.dLeftToRight) == DiscardLeft)
            {
                op1.Prev = op2;
                op2.Next = op1;
                op1b.Next = op2b;
                op2b.Prev = op1b;
            }
            else
            {
                op1.Next = op2;
                op2.Prev = op1;
                op1b.Prev = op2b;
                op2b.Next = op1b;
            }
            return true;
        }
        //------------------------------------------------------------------------------

        private bool JoinPoints(Join j, OutRec outRec1, OutRec outRec2)
        {
            OutPt op1 = j.OutPt1, op1b;
            OutPt op2 = j.OutPt2, op2b;

            //There are 3 kinds of joins for output polygons ...
            //1. Horizontal joins where Join.OutPt1 & Join.OutPt2 are a vertices anywhere
            //along (horizontal) collinear edges (& Join.OffPt is on the same horizontal).
            //2. Non-horizontal joins where Join.OutPt1 & Join.OutPt2 are at the same
            //location at the Bottom of the overlapping segment (& Join.OffPt is above).
            //3. StrictlySimple joins where edges touch but are not collinear and where
            //Join.OutPt1, Join.OutPt2 & Join.OffPt all share the same point.
            bool isHorizontal = (j.OutPt1.Pt.Y == j.OffPt.Y);

            if (isHorizontal && (j.OffPt == j.OutPt1.Pt) && (j.OffPt == j.OutPt2.Pt))
            {
                //Strictly Simple join ...
                if (outRec1 != outRec2) return false;
                op1b = j.OutPt1.Next;
                while (op1b != op1 && (op1b.Pt == j.OffPt))
                    op1b = op1b.Next;
                bool reverse1 = (op1b.Pt.Y > j.OffPt.Y);
                op2b = j.OutPt2.Next;
                while (op2b != op2 && (op2b.Pt == j.OffPt))
                    op2b = op2b.Next;
                bool reverse2 = (op2b.Pt.Y > j.OffPt.Y);
                if (reverse1 == reverse2) return false;
                if (reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
            }
            else if (isHorizontal)
            {
                //treat horizontal joins differently to non-horizontal joins since with
                //them we're not yet sure where the overlapping is. OutPt1.Pt & OutPt2.Pt
                //may be anywhere along the horizontal edge.
                op1b = op1;
                while (op1.Prev.Pt.Y == op1.Pt.Y && op1.Prev != op1b && op1.Prev != op2)
                    op1 = op1.Prev;
                while (op1b.Next.Pt.Y == op1b.Pt.Y && op1b.Next != op1 && op1b.Next != op2)
                    op1b = op1b.Next;
                if (op1b.Next == op1 || op1b.Next == op2) return false; //a flat 'polygon'

                op2b = op2;
                while (op2.Prev.Pt.Y == op2.Pt.Y && op2.Prev != op2b && op2.Prev != op1b)
                    op2 = op2.Prev;
                while (op2b.Next.Pt.Y == op2b.Pt.Y && op2b.Next != op2 && op2b.Next != op1)
                    op2b = op2b.Next;
                if (op2b.Next == op2 || op2b.Next == op1) return false; //a flat 'polygon'

                cInt Left, Right;
                //Op1 -. Op1b & Op2 -. Op2b are the extremites of the horizontal edges
                if (!GetOverlap(op1.Pt.X, op1b.Pt.X, op2.Pt.X, op2b.Pt.X, out Left, out Right))
                    return false;

                //DiscardLeftSide: when overlapping edges are joined, a spike will created
                //which needs to be cleaned up. However, we don't want Op1 or Op2 caught up
                //on the discard Side as either may still be needed for other joins ...
                IntPoint Pt;
                bool DiscardLeftSide;
                if (op1.Pt.X >= Left && op1.Pt.X <= Right)
                {
                    Pt = op1.Pt; DiscardLeftSide = (op1.Pt.X > op1b.Pt.X);
                }
                else if (op2.Pt.X >= Left && op2.Pt.X <= Right)
                {
                    Pt = op2.Pt; DiscardLeftSide = (op2.Pt.X > op2b.Pt.X);
                }
                else if (op1b.Pt.X >= Left && op1b.Pt.X <= Right)
                {
                    Pt = op1b.Pt; DiscardLeftSide = op1b.Pt.X > op1.Pt.X;
                }
                else
                {
                    Pt = op2b.Pt; DiscardLeftSide = (op2b.Pt.X > op2.Pt.X);
                }
                j.OutPt1 = op1;
                j.OutPt2 = op2;
                return JoinHorz(op1, op1b, op2, op2b, Pt, DiscardLeftSide);
            }
            else
            {
                //nb: For non-horizontal joins ...
                //    1. Jr.OutPt1.Pt.Y == Jr.OutPt2.Pt.Y
                //    2. Jr.OutPt1.Pt > Jr.OffPt.Y

                //make sure the polygons are correctly oriented ...
                op1b = op1.Next;
                while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Next;
                bool Reverse1 = ((op1b.Pt.Y > op1.Pt.Y) ||
                  !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt, m_UseFullRange));
                if (Reverse1)
                {
                    op1b = op1.Prev;
                    while ((op1b.Pt == op1.Pt) && (op1b != op1)) op1b = op1b.Prev;
                    if ((op1b.Pt.Y > op1.Pt.Y) ||
                      !SlopesEqual(op1.Pt, op1b.Pt, j.OffPt, m_UseFullRange)) return false;
                };
                op2b = op2.Next;
                while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Next;
                bool Reverse2 = ((op2b.Pt.Y > op2.Pt.Y) ||
                  !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt, m_UseFullRange));
                if (Reverse2)
                {
                    op2b = op2.Prev;
                    while ((op2b.Pt == op2.Pt) && (op2b != op2)) op2b = op2b.Prev;
                    if ((op2b.Pt.Y > op2.Pt.Y) ||
                      !SlopesEqual(op2.Pt, op2b.Pt, j.OffPt, m_UseFullRange)) return false;
                }

                if ((op1b == op1) || (op2b == op2) || (op1b == op2b) ||
                  ((outRec1 == outRec2) && (Reverse1 == Reverse2))) return false;

                if (Reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPt1 = op1;
                    j.OutPt2 = op1b;
                    return true;
                }
            }
        }
        //----------------------------------------------------------------------

        public static int PointInPolygon(IntPoint pt, Path path)
        {
            //returns 0 if false, +1 if true, -1 if pt ON polygon boundary
            //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
            int result = 0, cnt = path.Count;
            if (cnt < 3) return 0;
            IntPoint ip = path[0];
            for (int i = 1; i <= cnt; ++i)
            {
                IntPoint ipNext = (i == cnt ? path[0] : path[i]);
                if (ipNext.Y == pt.Y)
                {
                    if ((ipNext.X == pt.X) || (ip.Y == pt.Y &&
                      ((ipNext.X > pt.X) == (ip.X < pt.X)))) return -1;
                }
                if ((ip.Y < pt.Y) != (ipNext.Y < pt.Y))
                {
                    if (ip.X >= pt.X)
                    {
                        if (ipNext.X > pt.X) result = 1 - result;
                        else
                        {
                            double d = (double)(ip.X - pt.X) * (ipNext.Y - pt.Y) -
                              (double)(ipNext.X - pt.X) * (ip.Y - pt.Y);
                            if (d == 0) return -1;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = 1 - result;
                        }
                    }
                    else
                    {
                        if (ipNext.X > pt.X)
                        {
                            double d = (double)(ip.X - pt.X) * (ipNext.Y - pt.Y) -
                              (double)(ipNext.X - pt.X) * (ip.Y - pt.Y);
                            if (d == 0) return -1;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = 1 - result;
                        }
                    }
                }
                ip = ipNext;
            }
            return result;
        }
        //------------------------------------------------------------------------------

        private static int PointInPolygon(IntPoint pt, OutPt op)
        {
            //returns 0 if false, +1 if true, -1 if pt ON polygon boundary
            //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
            int result = 0;
            OutPt startOp = op;
            cInt ptx = pt.X, pty = pt.Y;
            cInt poly0x = op.Pt.X, poly0y = op.Pt.Y;
            do
            {
                op = op.Next;
                cInt poly1x = op.Pt.X, poly1y = op.Pt.Y;

                if (poly1y == pty)
                {
                    if ((poly1x == ptx) || (poly0y == pty &&
                      ((poly1x > ptx) == (poly0x < ptx)))) return -1;
                }
                if ((poly0y < pty) != (poly1y < pty))
                {
                    if (poly0x >= ptx)
                    {
                        if (poly1x > ptx) result = 1 - result;
                        else
                        {
                            double d = (double)(poly0x - ptx) * (poly1y - pty) -
                              (double)(poly1x - ptx) * (poly0y - pty);
                            if (d == 0) return -1;
                            if ((d > 0) == (poly1y > poly0y)) result = 1 - result;
                        }
                    }
                    else
                    {
                        if (poly1x > ptx)
                        {
                            double d = (double)(poly0x - ptx) * (poly1y - pty) -
                              (double)(poly1x - ptx) * (poly0y - pty);
                            if (d == 0) return -1;
                            if ((d > 0) == (poly1y > poly0y)) result = 1 - result;
                        }
                    }
                }
                poly0x = poly1x; poly0y = poly1y;
            } while (startOp != op);
            return result;
        }
        //------------------------------------------------------------------------------

        private static bool Poly2ContainsPoly1(OutPt outPt1, OutPt outPt2)
        {
            OutPt op = outPt1;
            do
            {
                //nb: PointInPolygon returns 0 if false, +1 if true, -1 if pt on polygon
                int res = PointInPolygon(op.Pt, outPt2);
                if (res >= 0) return res > 0;
                op = op.Next;
            }
            while (op != outPt1);
            return true;
        }
        //----------------------------------------------------------------------

        private void FixupFirstLefts1(OutRec OldOutRec, OutRec NewOutRec)
        {
            for (int i = 0; i < m_PolyOuts.Count; i++)
            {
                OutRec outRec = m_PolyOuts[i];
                if (outRec.Pts == null || outRec.FirstLeft == null) continue;
                OutRec firstLeft = ParseFirstLeft(outRec.FirstLeft);
                if (firstLeft == OldOutRec)
                {
                    if (Poly2ContainsPoly1(outRec.Pts, NewOutRec.Pts))
                        outRec.FirstLeft = NewOutRec;
                }
            }
        }
        //----------------------------------------------------------------------

        private void FixupFirstLefts2(OutRec OldOutRec, OutRec NewOutRec)
        {
            foreach (OutRec outRec in m_PolyOuts)
                if (outRec.FirstLeft == OldOutRec) outRec.FirstLeft = NewOutRec;
        }
        //----------------------------------------------------------------------

        private static OutRec ParseFirstLeft(OutRec FirstLeft)
        {
            while (FirstLeft != null && FirstLeft.Pts == null)
                FirstLeft = FirstLeft.FirstLeft;
            return FirstLeft;
        }
        //------------------------------------------------------------------------------

        private void JoinCommonEdges()
        {
            for (int i = 0; i < m_Joins.Count; i++)
            {
                Join join = m_Joins[i];

                OutRec outRec1 = GetOutRec(join.OutPt1.Idx);
                OutRec outRec2 = GetOutRec(join.OutPt2.Idx);

                if (outRec1.Pts == null || outRec2.Pts == null) continue;

                //get the polygon fragment with the correct hole state (FirstLeft)
                //before calling JoinPoints() ...
                OutRec holeStateRec;
                if (outRec1 == outRec2) holeStateRec = outRec1;
                else if (Param1RightOfParam2(outRec1, outRec2)) holeStateRec = outRec2;
                else if (Param1RightOfParam2(outRec2, outRec1)) holeStateRec = outRec1;
                else holeStateRec = GetLowermostRec(outRec1, outRec2);

                if (!JoinPoints(join, outRec1, outRec2)) continue;

                if (outRec1 == outRec2)
                {
                    //instead of joining two polygons, we've just created a new one by
                    //splitting one polygon into two.
                    outRec1.Pts = join.OutPt1;
                    outRec1.BottomPt = null;
                    outRec2 = CreateOutRec();
                    outRec2.Pts = join.OutPt2;

                    //update all OutRec2.Pts Idx's ...
                    UpdateOutPtIdxs(outRec2);

                    //We now need to check every OutRec.FirstLeft pointer. If it points
                    //to OutRec1 it may need to point to OutRec2 instead ...
                    if (m_UsingPolyTree)
                        for (int j = 0; j < m_PolyOuts.Count - 1; j++)
                        {
                            OutRec oRec = m_PolyOuts[j];
                            if (oRec.Pts == null || ParseFirstLeft(oRec.FirstLeft) != outRec1 ||
                              oRec.IsHole == outRec1.IsHole) continue;
                            if (Poly2ContainsPoly1(oRec.Pts, join.OutPt2))
                                oRec.FirstLeft = outRec2;
                        }

                    if (Poly2ContainsPoly1(outRec2.Pts, outRec1.Pts))
                    {
                        //outRec2 is contained by outRec1 ...
                        outRec2.IsHole = !outRec1.IsHole;
                        outRec2.FirstLeft = outRec1;

                        //fixup FirstLeft pointers that may need reassigning to OutRec1
                        if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);

                        if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                            ReversePolyPtLinks(outRec2.Pts);

                    }
                    else if (Poly2ContainsPoly1(outRec1.Pts, outRec2.Pts))
                    {
                        //outRec1 is contained by outRec2 ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec1.IsHole = !outRec2.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;
                        outRec1.FirstLeft = outRec2;

                        //fixup FirstLeft pointers that may need reassigning to OutRec1
                        if (m_UsingPolyTree) FixupFirstLefts2(outRec1, outRec2);

                        if ((outRec1.IsHole ^ ReverseSolution) == (Area(outRec1) > 0))
                            ReversePolyPtLinks(outRec1.Pts);
                    }
                    else
                    {
                        //the 2 polygons are completely separate ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;

                        //fixup FirstLeft pointers that may need reassigning to OutRec2
                        if (m_UsingPolyTree) FixupFirstLefts1(outRec1, outRec2);
                    }

                }
                else
                {
                    //joined 2 polygons together ...

                    outRec2.Pts = null;
                    outRec2.BottomPt = null;
                    outRec2.Idx = outRec1.Idx;

                    outRec1.IsHole = holeStateRec.IsHole;
                    if (holeStateRec == outRec2)
                        outRec1.FirstLeft = outRec2.FirstLeft;
                    outRec2.FirstLeft = outRec1;

                    //fixup FirstLeft pointers that may need reassigning to OutRec1
                    if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);
                }
            }
        }
        //------------------------------------------------------------------------------

        private void UpdateOutPtIdxs(OutRec outrec)
        {
            OutPt op = outrec.Pts;
            do
            {
                op.Idx = outrec.Idx;
                op = op.Prev;
            }
            while (op != outrec.Pts);
        }
        //------------------------------------------------------------------------------

        private void DoSimplePolygons()
        {
            int i = 0;
            while (i < m_PolyOuts.Count)
            {
                OutRec outrec = m_PolyOuts[i++];
                OutPt op = outrec.Pts;
                if (op == null || outrec.IsOpen) continue;
                do //for each Pt in Polygon until duplicate found do ...
                {
                    OutPt op2 = op.Next;
                    while (op2 != outrec.Pts)
                    {
                        if ((op.Pt == op2.Pt) && op2.Next != op && op2.Prev != op)
                        {
                            //split the polygon into two ...
                            OutPt op3 = op.Prev;
                            OutPt op4 = op2.Prev;
                            op.Prev = op4;
                            op4.Next = op;
                            op2.Prev = op3;
                            op3.Next = op2;

                            outrec.Pts = op;
                            OutRec outrec2 = CreateOutRec();
                            outrec2.Pts = op2;
                            UpdateOutPtIdxs(outrec2);
                            if (Poly2ContainsPoly1(outrec2.Pts, outrec.Pts))
                            {
                                //OutRec2 is contained by OutRec1 ...
                                outrec2.IsHole = !outrec.IsHole;
                                outrec2.FirstLeft = outrec;
                                if (m_UsingPolyTree) FixupFirstLefts2(outrec2, outrec);
                            }
                            else
                                if (Poly2ContainsPoly1(outrec.Pts, outrec2.Pts))
                                {
                                    //OutRec1 is contained by OutRec2 ...
                                    outrec2.IsHole = outrec.IsHole;
                                    outrec.IsHole = !outrec2.IsHole;
                                    outrec2.FirstLeft = outrec.FirstLeft;
                                    outrec.FirstLeft = outrec2;
                                    if (m_UsingPolyTree) FixupFirstLefts2(outrec, outrec2);
                                }
                                else
                                {
                                    //the 2 polygons are separate ...
                                    outrec2.IsHole = outrec.IsHole;
                                    outrec2.FirstLeft = outrec.FirstLeft;
                                    if (m_UsingPolyTree) FixupFirstLefts1(outrec, outrec2);
                                }
                            op2 = op; //ie get ready for the next iteration
                        }
                        op2 = op2.Next;
                    }
                    op = op.Next;
                }
                while (op != outrec.Pts);
            }
        }
        //------------------------------------------------------------------------------

        public static double Area(Path poly)
        {
            int cnt = (int)poly.Count;
            if (cnt < 3) return 0;
            double a = 0;
            for (int i = 0, j = cnt - 1; i < cnt; ++i)
            {
                a += ((double)poly[j].X + poly[i].X) * ((double)poly[j].Y - poly[i].Y);
                j = i;
            }
            return -a * 0.5;
        }
        //------------------------------------------------------------------------------

        double Area(OutRec outRec)
        {
            OutPt op = outRec.Pts;
            if (op == null) return 0;
            double a = 0;
            do
            {
                a = a + (double)(op.Prev.Pt.X + op.Pt.X) * (double)(op.Prev.Pt.Y - op.Pt.Y);
                op = op.Next;
            } while (op != outRec.Pts);
            return a * 0.5;
        }

        //------------------------------------------------------------------------------
        // SimplifyPolygon functions ...
        // Convert self-intersecting polygons into simple polygons
        //------------------------------------------------------------------------------

        public static Paths SimplifyPolygon(Path poly,
              PolyFillType fillType = PolyFillType.pftEvenOdd)
        {
            Paths result = new Paths();
            Clipper c = new Clipper();
            c.StrictlySimple = true;
            c.AddPath(poly, PolyType.ptSubject, true);
            c.Execute(ClipType.ctUnion, result, fillType, fillType);
            return result;
        }
        //------------------------------------------------------------------------------

        public static Paths SimplifyPolygons(Paths polys,
            PolyFillType fillType = PolyFillType.pftEvenOdd)
        {
            Paths result = new Paths();
            Clipper c = new Clipper();
            c.StrictlySimple = true;
            c.AddPaths(polys, PolyType.ptSubject, true);
            c.Execute(ClipType.ctUnion, result, fillType, fillType);
            return result;
        }
        //------------------------------------------------------------------------------

        private static double DistanceSqrd(IntPoint pt1, IntPoint pt2)
        {
            double dx = ((double)pt1.X - pt2.X);
            double dy = ((double)pt1.Y - pt2.Y);
            return (dx * dx + dy * dy);
        }
        //------------------------------------------------------------------------------

        private static double DistanceFromLineSqrd(IntPoint pt, IntPoint ln1, IntPoint ln2)
        {
            //The equation of a line in general form (Ax + By + C = 0)
            //given 2 points (x¹,y¹) & (x²,y²) is ...
            //(y¹ - y²)x + (x² - x¹)y + (y² - y¹)x¹ - (x² - x¹)y¹ = 0
            //A = (y¹ - y²); B = (x² - x¹); C = (y² - y¹)x¹ - (x² - x¹)y¹
            //perpendicular distance of point (x³,y³) = (Ax³ + By³ + C)/Sqrt(A² + B²)
            //see http://en.wikipedia.org/wiki/Perpendicular_distance
            double A = ln1.Y - ln2.Y;
            double B = ln2.X - ln1.X;
            double C = A * ln1.X + B * ln1.Y;
            C = A * pt.X + B * pt.Y - C;
            return (C * C) / (A * A + B * B);
        }
        //---------------------------------------------------------------------------

        private static bool SlopesNearCollinear(IntPoint pt1,
            IntPoint pt2, IntPoint pt3, double distSqrd)
        {
            //this function is more accurate when the point that's GEOMETRICALLY 
            //between the other 2 points is the one that's tested for distance.  
            //nb: with 'spikes', either pt1 or pt3 is geometrically between the other pts                    
            if (Math.Abs(pt1.X - pt2.X) > Math.Abs(pt1.Y - pt2.Y))
            {
                if ((pt1.X > pt2.X) == (pt1.X < pt3.X))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.X > pt1.X) == (pt2.X < pt3.X))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
            else
            {
                if ((pt1.Y > pt2.Y) == (pt1.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.Y > pt1.Y) == (pt2.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
        }
        //------------------------------------------------------------------------------

        private static bool PointsAreClose(IntPoint pt1, IntPoint pt2, double distSqrd)
        {
            double dx = (double)pt1.X - pt2.X;
            double dy = (double)pt1.Y - pt2.Y;
            return ((dx * dx) + (dy * dy) <= distSqrd);
        }
        //------------------------------------------------------------------------------

        private static OutPt ExcludeOp(OutPt op)
        {
            OutPt result = op.Prev;
            result.Next = op.Next;
            op.Next.Prev = result;
            result.Idx = 0;
            return result;
        }
        //------------------------------------------------------------------------------

        public static Path CleanPolygon(Path path, double distance = 1.415)
        {
            //distance = proximity in units/pixels below which vertices will be stripped. 
            //Default ~= sqrt(2) so when adjacent vertices or semi-adjacent vertices have 
            //both x & y coords within 1 unit, then the second vertex will be stripped.

            int cnt = path.Count;

            if (cnt == 0) return new Path();

            OutPt[] outPts = new OutPt[cnt];
            for (int i = 0; i < cnt; ++i) outPts[i] = new OutPt();

            for (int i = 0; i < cnt; ++i)
            {
                outPts[i].Pt = path[i];
                outPts[i].Next = outPts[(i + 1) % cnt];
                outPts[i].Next.Prev = outPts[i];
                outPts[i].Idx = 0;
            }

            double distSqrd = distance * distance;
            OutPt op = outPts[0];
            while (op.Idx == 0 && op.Next != op.Prev)
            {
                if (PointsAreClose(op.Pt, op.Prev.Pt, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else if (PointsAreClose(op.Prev.Pt, op.Next.Pt, distSqrd))
                {
                    ExcludeOp(op.Next);
                    op = ExcludeOp(op);
                    cnt -= 2;
                }
                else if (SlopesNearCollinear(op.Prev.Pt, op.Pt, op.Next.Pt, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else
                {
                    op.Idx = 1;
                    op = op.Next;
                }
            }

            if (cnt < 3) cnt = 0;
            Path result = new Path(cnt);
            for (int i = 0; i < cnt; ++i)
            {
                result.Add(op.Pt);
                op = op.Next;
            }
            outPts = null;
            return result;
        }
        //------------------------------------------------------------------------------

        public static Paths CleanPolygons(Paths polys,
            double distance = 1.415)
        {
            Paths result = new Paths(polys.Count);
            for (int i = 0; i < polys.Count; i++)
                result.Add(CleanPolygon(polys[i], distance));
            return result;
        }
        //------------------------------------------------------------------------------

        internal static Paths Minkowski(Path pattern, Path path, bool IsSum, bool IsClosed)
        {
            int delta = (IsClosed ? 1 : 0);
            int polyCnt = pattern.Count;
            int pathCnt = path.Count;
            Paths result = new Paths(pathCnt);
            if (IsSum)
                for (int i = 0; i < pathCnt; i++)
                {
                    Path p = new Path(polyCnt);
                    foreach (IntPoint ip in pattern)
                        p.Add(new IntPoint(path[i].X + ip.X, path[i].Y + ip.Y));
                    result.Add(p);
                }
            else
                for (int i = 0; i < pathCnt; i++)
                {
                    Path p = new Path(polyCnt);
                    foreach (IntPoint ip in pattern)
                        p.Add(new IntPoint(path[i].X - ip.X, path[i].Y - ip.Y));
                    result.Add(p);
                }

            Paths quads = new Paths((pathCnt + delta) * (polyCnt + 1));
            for (int i = 0; i < pathCnt - 1 + delta; i++)
                for (int j = 0; j < polyCnt; j++)
                {
                    Path quad = new Path(4);
                    quad.Add(result[i % pathCnt][j % polyCnt]);
                    quad.Add(result[(i + 1) % pathCnt][j % polyCnt]);
                    quad.Add(result[(i + 1) % pathCnt][(j + 1) % polyCnt]);
                    quad.Add(result[i % pathCnt][(j + 1) % polyCnt]);
                    if (!Orientation(quad)) quad.Reverse();
                    quads.Add(quad);
                }
            return quads;
        }
        //------------------------------------------------------------------------------

        public static Paths MinkowskiSum(Path pattern, Path path, bool pathIsClosed)
        {
            Paths paths = Minkowski(pattern, path, true, pathIsClosed);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolyType.ptSubject, true);
            c.Execute(ClipType.ctUnion, paths, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return paths;
        }
        //------------------------------------------------------------------------------

        private static Path TranslatePath(Path path, IntPoint delta)
        {
            Path outPath = new Path(path.Count);
            for (int i = 0; i < path.Count; i++)
                outPath.Add(new IntPoint(path[i].X + delta.X, path[i].Y + delta.Y));
            return outPath;
        }
        //------------------------------------------------------------------------------

        public static Paths MinkowskiSum(Path pattern, Paths paths, bool pathIsClosed)
        {
            Paths solution = new Paths();
            Clipper c = new Clipper();
            for (int i = 0; i < paths.Count; ++i)
            {
                Paths tmp = Minkowski(pattern, paths[i], true, pathIsClosed);
                c.AddPaths(tmp, PolyType.ptSubject, true);
                if (pathIsClosed)
                {
                    Path path = TranslatePath(paths[i], pattern[0]);
                    c.AddPath(path, PolyType.ptClip, true);
                }
            }
            c.Execute(ClipType.ctUnion, solution,
              PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return solution;
        }
        //------------------------------------------------------------------------------

        public static Paths MinkowskiDiff(Path poly1, Path poly2)
        {
            Paths paths = Minkowski(poly1, poly2, false, true);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolyType.ptSubject, true);
            c.Execute(ClipType.ctUnion, paths, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return paths;
        }
        //------------------------------------------------------------------------------

        internal enum NodeType { ntAny, ntOpen, ntClosed };

        public static Paths PolyTreeToPaths(PolyTree polytree)
        {

            Paths result = new Paths();
            result.Capacity = polytree.Total;
            AddPolyNodeToPaths(polytree, NodeType.ntAny, result);
            return result;
        }
        //------------------------------------------------------------------------------

        internal static void AddPolyNodeToPaths(PolyNode polynode, NodeType nt, Paths paths)
        {
            bool match = true;
            switch (nt)
            {
                case NodeType.ntOpen: return;
                case NodeType.ntClosed: match = !polynode.IsOpen; break;
                default: break;
            }

            if (polynode.m_polygon.Count > 0 && match)
                paths.Add(polynode.m_polygon);
            foreach (PolyNode pn in polynode.Childs)
                AddPolyNodeToPaths(pn, nt, paths);
        }
        //------------------------------------------------------------------------------

        public static Paths OpenPathsFromPolyTree(PolyTree polytree)
        {
            Paths result = new Paths();
            result.Capacity = polytree.ChildCount;
            for (int i = 0; i < polytree.ChildCount; i++)
                if (polytree.Childs[i].IsOpen)
                    result.Add(polytree.Childs[i].m_polygon);
            return result;
        }
        //------------------------------------------------------------------------------

        public static Paths ClosedPathsFromPolyTree(PolyTree polytree)
        {
            Paths result = new Paths();
            result.Capacity = polytree.Total;
            AddPolyNodeToPaths(polytree, NodeType.ntClosed, result);
            return result;
        }
        //------------------------------------------------------------------------------

    } //end Clipper

    public class ClipperOffset
    {
        private Paths m_destPolys;
        private Path m_srcPoly;
        private Path m_destPoly;
        private List<DoublePoint> m_normals = new List<DoublePoint>();
        private double m_delta, m_sinA, m_sin, m_cos;
        private double m_miterLim, m_StepsPerRad;

        private IntPoint m_lowest;
        private PolyNode m_polyNodes = new PolyNode();

        public double ArcTolerance { get; set; }
        public double MiterLimit { get; set; }

        private const double two_pi = Math.PI * 2;
        private const double def_arc_tolerance = 0.25;

        public ClipperOffset(
          double miterLimit = 2.0, double arcTolerance = def_arc_tolerance)
        {
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
            m_lowest.X = -1;
        }
        //------------------------------------------------------------------------------

        public void Clear()
        {
            m_polyNodes.Childs.Clear();
            m_lowest.X = -1;
        }
        //------------------------------------------------------------------------------

        internal static cInt Round(double value)
        {
            return value < 0 ? (cInt)(value - 0.5) : (cInt)(value + 0.5);
        }
        //------------------------------------------------------------------------------

        public void AddPath(Path path, JoinType joinType, EndType endType)
        {
            int highI = path.Count - 1;
            if (highI < 0) return;
            PolyNode newNode = new PolyNode();
            newNode.m_jointype = joinType;
            newNode.m_endtype = endType;

            //strip duplicate points from path and also get index to the lowest point ...
            if (endType == EndType.etClosedLine || endType == EndType.etClosedPolygon)
                while (highI > 0 && path[0] == path[highI]) highI--;
            newNode.m_polygon.Capacity = highI + 1;
            newNode.m_polygon.Add(path[0]);
            int j = 0, k = 0;
            for (int i = 1; i <= highI; i++)
                if (newNode.m_polygon[j] != path[i])
                {
                    j++;
                    newNode.m_polygon.Add(path[i]);
                    if (path[i].Y > newNode.m_polygon[k].Y ||
                      (path[i].Y == newNode.m_polygon[k].Y &&
                      path[i].X < newNode.m_polygon[k].X)) k = j;
                }
            if (endType == EndType.etClosedPolygon && j < 2) return;

            m_polyNodes.AddChild(newNode);

            //if this path's lowest pt is lower than all the others then update m_lowest
            if (endType != EndType.etClosedPolygon) return;
            if (m_lowest.X < 0)
                m_lowest = new IntPoint(m_polyNodes.ChildCount - 1, k);
            else
            {
                IntPoint ip = m_polyNodes.Childs[(int)m_lowest.X].m_polygon[(int)m_lowest.Y];
                if (newNode.m_polygon[k].Y > ip.Y ||
                  (newNode.m_polygon[k].Y == ip.Y &&
                  newNode.m_polygon[k].X < ip.X))
                    m_lowest = new IntPoint(m_polyNodes.ChildCount - 1, k);
            }
        }
        //------------------------------------------------------------------------------

        public void AddPaths(Paths paths, JoinType joinType, EndType endType)
        {
            foreach (Path p in paths)
                AddPath(p, joinType, endType);
        }
        //------------------------------------------------------------------------------

        private void FixOrientations()
        {
            //fixup orientations of all closed paths if the orientation of the
            //closed path with the lowermost vertex is wrong ...
            if (m_lowest.X >= 0 &&
              !Clipper.Orientation(m_polyNodes.Childs[(int)m_lowest.X].m_polygon))
            {
                for (int i = 0; i < m_polyNodes.ChildCount; i++)
                {
                    PolyNode node = m_polyNodes.Childs[i];
                    if (node.m_endtype == EndType.etClosedPolygon ||
                      (node.m_endtype == EndType.etClosedLine &&
                      Clipper.Orientation(node.m_polygon)))
                        node.m_polygon.Reverse();
                }
            }
            else
            {
                for (int i = 0; i < m_polyNodes.ChildCount; i++)
                {
                    PolyNode node = m_polyNodes.Childs[i];
                    if (node.m_endtype == EndType.etClosedLine &&
                      !Clipper.Orientation(node.m_polygon))
                        node.m_polygon.Reverse();
                }
            }
        }
        //------------------------------------------------------------------------------

        internal static DoublePoint GetUnitNormal(IntPoint pt1, IntPoint pt2)
        {
            double dx = (pt2.X - pt1.X);
            double dy = (pt2.Y - pt1.Y);
            if ((dx == 0) && (dy == 0)) return new DoublePoint();

            double f = 1 * 1.0 / Math.Sqrt(dx * dx + dy * dy);
            dx *= f;
            dy *= f;

            return new DoublePoint(dy, -dx);
        }
        //------------------------------------------------------------------------------

        private void DoOffset(double delta)
        {
            m_destPolys = new Paths();
            m_delta = delta;

            //if Zero offset, just copy any CLOSED polygons to m_p and return ...
            if (ClipperBase.near_zero(delta))
            {
                m_destPolys.Capacity = m_polyNodes.ChildCount;
                for (int i = 0; i < m_polyNodes.ChildCount; i++)
                {
                    PolyNode node = m_polyNodes.Childs[i];
                    if (node.m_endtype == EndType.etClosedPolygon)
                        m_destPolys.Add(node.m_polygon);
                }
                return;
            }

            //see offset_triginometry3.svg in the documentation folder ...
            if (MiterLimit > 2) m_miterLim = 2 / (MiterLimit * MiterLimit);
            else m_miterLim = 0.5;

            double y;
            if (ArcTolerance <= 0.0)
                y = def_arc_tolerance;
            else if (ArcTolerance > Math.Abs(delta) * def_arc_tolerance)
                y = Math.Abs(delta) * def_arc_tolerance;
            else
                y = ArcTolerance;
            //see offset_triginometry2.svg in the documentation folder ...
            double steps = Math.PI / Math.Acos(1 - y / Math.Abs(delta));
            m_sin = Math.Sin(two_pi / steps);
            m_cos = Math.Cos(two_pi / steps);
            m_StepsPerRad = steps / two_pi;
            if (delta < 0.0) m_sin = -m_sin;

            m_destPolys.Capacity = m_polyNodes.ChildCount * 2;
            for (int i = 0; i < m_polyNodes.ChildCount; i++)
            {
                PolyNode node = m_polyNodes.Childs[i];
                m_srcPoly = node.m_polygon;

                int len = m_srcPoly.Count;

                if (len == 0 || (delta <= 0 && (len < 3 ||
                  node.m_endtype != EndType.etClosedPolygon)))
                    continue;

                m_destPoly = new Path();

                if (len == 1)
                {
                    if (node.m_jointype == JoinType.jtRound)
                    {
                        double X = 1.0, Y = 0.0;
                        for (int j = 1; j <= steps; j++)
                        {
                            m_destPoly.Add(new IntPoint(
                              Round(m_srcPoly[0].X + X * delta),
                              Round(m_srcPoly[0].Y + Y * delta)));
                            double X2 = X;
                            X = X * m_cos - m_sin * Y;
                            Y = X2 * m_sin + Y * m_cos;
                        }
                    }
                    else
                    {
                        double X = -1.0, Y = -1.0;
                        for (int j = 0; j < 4; ++j)
                        {
                            m_destPoly.Add(new IntPoint(
                              Round(m_srcPoly[0].X + X * delta),
                              Round(m_srcPoly[0].Y + Y * delta)));
                            if (X < 0) X = 1;
                            else if (Y < 0) Y = 1;
                            else X = -1;
                        }
                    }
                    m_destPolys.Add(m_destPoly);
                    continue;
                }

                //build m_normals ...
                m_normals.Clear();
                m_normals.Capacity = len;
                for (int j = 0; j < len - 1; j++)
                    m_normals.Add(GetUnitNormal(m_srcPoly[j], m_srcPoly[j + 1]));
                if (node.m_endtype == EndType.etClosedLine ||
                  node.m_endtype == EndType.etClosedPolygon)
                    m_normals.Add(GetUnitNormal(m_srcPoly[len - 1], m_srcPoly[0]));
                else
                    m_normals.Add(new DoublePoint(m_normals[len - 2]));

                if (node.m_endtype == EndType.etClosedPolygon)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.m_jointype);
                    m_destPolys.Add(m_destPoly);
                }
                else if (node.m_endtype == EndType.etClosedLine)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.m_jointype);
                    m_destPolys.Add(m_destPoly);
                    m_destPoly = new Path();
                    //re-build m_normals ...
                    DoublePoint n = m_normals[len - 1];
                    for (int j = len - 1; j > 0; j--)
                        m_normals[j] = new DoublePoint(-m_normals[j - 1].X, -m_normals[j - 1].Y);
                    m_normals[0] = new DoublePoint(-n.X, -n.Y);
                    k = 0;
                    for (int j = len - 1; j >= 0; j--)
                        OffsetPoint(j, ref k, node.m_jointype);
                    m_destPolys.Add(m_destPoly);
                }
                else
                {
                    int k = 0;
                    for (int j = 1; j < len - 1; ++j)
                        OffsetPoint(j, ref k, node.m_jointype);

                    IntPoint pt1;
                    if (node.m_endtype == EndType.etOpenButt)
                    {
                        int j = len - 1;
                        pt1 = new IntPoint((cInt)Round(m_srcPoly[j].X + m_normals[j].X *
                          delta), (cInt)Round(m_srcPoly[j].Y + m_normals[j].Y * delta));
                        m_destPoly.Add(pt1);
                        pt1 = new IntPoint((cInt)Round(m_srcPoly[j].X - m_normals[j].X *
                          delta), (cInt)Round(m_srcPoly[j].Y - m_normals[j].Y * delta));
                        m_destPoly.Add(pt1);
                    }
                    else
                    {
                        int j = len - 1;
                        k = len - 2;
                        m_sinA = 0;
                        m_normals[j] = new DoublePoint(-m_normals[j].X, -m_normals[j].Y);
                        if (node.m_endtype == EndType.etOpenSquare)
                            DoSquare(j, k);
                        else
                            DoRound(j, k);
                    }

                    //re-build m_normals ...
                    for (int j = len - 1; j > 0; j--)
                        m_normals[j] = new DoublePoint(-m_normals[j - 1].X, -m_normals[j - 1].Y);

                    m_normals[0] = new DoublePoint(-m_normals[1].X, -m_normals[1].Y);

                    k = len - 1;
                    for (int j = k - 1; j > 0; --j)
                        OffsetPoint(j, ref k, node.m_jointype);

                    if (node.m_endtype == EndType.etOpenButt)
                    {
                        pt1 = new IntPoint((cInt)Round(m_srcPoly[0].X - m_normals[0].X * delta),
                          (cInt)Round(m_srcPoly[0].Y - m_normals[0].Y * delta));
                        m_destPoly.Add(pt1);
                        pt1 = new IntPoint((cInt)Round(m_srcPoly[0].X + m_normals[0].X * delta),
                          (cInt)Round(m_srcPoly[0].Y + m_normals[0].Y * delta));
                        m_destPoly.Add(pt1);
                    }
                    else
                    {
                        k = 1;
                        m_sinA = 0;
                        if (node.m_endtype == EndType.etOpenSquare)
                            DoSquare(0, 1);
                        else
                            DoRound(0, 1);
                    }
                    m_destPolys.Add(m_destPoly);
                }
            }
        }
        //------------------------------------------------------------------------------

        public void Execute(ref Paths solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);
            //now clean up 'corners' ...
            Clipper clpr = new Clipper();
            clpr.AddPaths(m_destPolys, PolyType.ptSubject, true);
            if (delta > 0)
            {
                clpr.Execute(ClipType.ctUnion, solution,
                  PolyFillType.pftPositive, PolyFillType.pftPositive);
            }
            else
            {
                IntRect r = Clipper.GetBounds(m_destPolys);
                Path outer = new Path(4);

                outer.Add(new IntPoint(r.left - 10, r.bottom + 10));
                outer.Add(new IntPoint(r.right + 10, r.bottom + 10));
                outer.Add(new IntPoint(r.right + 10, r.top - 10));
                outer.Add(new IntPoint(r.left - 10, r.top - 10));

                clpr.AddPath(outer, PolyType.ptSubject, true);
                clpr.ReverseSolution = true;
                clpr.Execute(ClipType.ctUnion, solution, PolyFillType.pftNegative, PolyFillType.pftNegative);
                if (solution.Count > 0) solution.RemoveAt(0);
            }
        }
        //------------------------------------------------------------------------------

        public void Execute(ref PolyTree solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);

            //now clean up 'corners' ...
            Clipper clpr = new Clipper();
            clpr.AddPaths(m_destPolys, PolyType.ptSubject, true);
            if (delta > 0)
            {
                clpr.Execute(ClipType.ctUnion, solution,
                  PolyFillType.pftPositive, PolyFillType.pftPositive);
            }
            else
            {
                IntRect r = Clipper.GetBounds(m_destPolys);
                Path outer = new Path(4);

                outer.Add(new IntPoint(r.left - 10, r.bottom + 10));
                outer.Add(new IntPoint(r.right + 10, r.bottom + 10));
                outer.Add(new IntPoint(r.right + 10, r.top - 10));
                outer.Add(new IntPoint(r.left - 10, r.top - 10));

                clpr.AddPath(outer, PolyType.ptSubject, true);
                clpr.ReverseSolution = true;
                clpr.Execute(ClipType.ctUnion, solution, PolyFillType.pftNegative, PolyFillType.pftNegative);
                //remove the outer PolyNode rectangle ...
                if (solution.ChildCount == 1 && solution.Childs[0].ChildCount > 0)
                {
                    PolyNode outerNode = solution.Childs[0];
                    solution.Childs.Capacity = outerNode.ChildCount;
                    solution.Childs[0] = outerNode.Childs[0];
                    solution.Childs[0].m_Parent = solution;
                    for (int i = 1; i < outerNode.ChildCount; i++)
                        solution.AddChild(outerNode.Childs[i]);
                }
                else
                    solution.Clear();
            }
        }
        //------------------------------------------------------------------------------

        void OffsetPoint(int j, ref int k, JoinType jointype)
        {
            //cross product ...
            m_sinA = (m_normals[k].X * m_normals[j].Y - m_normals[j].X * m_normals[k].Y);

            if (Math.Abs(m_sinA * m_delta) < 1.0)
            {
                //dot product ...
                double cosA = (m_normals[k].X * m_normals[j].X + m_normals[j].Y * m_normals[k].Y);
                if (cosA > 0) // angle ==> 0 degrees
                {
                    m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[k].X * m_delta),
                      Round(m_srcPoly[j].Y + m_normals[k].Y * m_delta)));
                    return;
                }
                //else angle ==> 180 degrees   
            }
            else if (m_sinA > 1.0) m_sinA = 1.0;
            else if (m_sinA < -1.0) m_sinA = -1.0;

            if (m_sinA * m_delta < 0)
            {
                m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[k].X * m_delta),
                  Round(m_srcPoly[j].Y + m_normals[k].Y * m_delta)));
                m_destPoly.Add(m_srcPoly[j]);
                m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[j].X * m_delta),
                  Round(m_srcPoly[j].Y + m_normals[j].Y * m_delta)));
            }
            else
                switch (jointype)
                {
                    case JoinType.jtMiter:
                        {
                            double r = 1 + (m_normals[j].X * m_normals[k].X +
                              m_normals[j].Y * m_normals[k].Y);
                            if (r >= m_miterLim) DoMiter(j, k, r); else DoSquare(j, k);
                            break;
                        }
                    case JoinType.jtSquare: DoSquare(j, k); break;
                    case JoinType.jtRound: DoRound(j, k); break;
                }
            k = j;
        }
        //------------------------------------------------------------------------------

        internal void DoSquare(int j, int k)
        {
            double dx = Math.Tan(Math.Atan2(m_sinA,
                m_normals[k].X * m_normals[j].X + m_normals[k].Y * m_normals[j].Y) / 4);
            m_destPoly.Add(new IntPoint(
                Round(m_srcPoly[j].X + m_delta * (m_normals[k].X - m_normals[k].Y * dx)),
                Round(m_srcPoly[j].Y + m_delta * (m_normals[k].Y + m_normals[k].X * dx))));
            m_destPoly.Add(new IntPoint(
                Round(m_srcPoly[j].X + m_delta * (m_normals[j].X + m_normals[j].Y * dx)),
                Round(m_srcPoly[j].Y + m_delta * (m_normals[j].Y - m_normals[j].X * dx))));
        }
        //------------------------------------------------------------------------------

        internal void DoMiter(int j, int k, double r)
        {
            double q = m_delta / r;
            m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + (m_normals[k].X + m_normals[j].X) * q),
                Round(m_srcPoly[j].Y + (m_normals[k].Y + m_normals[j].Y) * q)));
        }
        //------------------------------------------------------------------------------

        internal void DoRound(int j, int k)
        {
            double a = Math.Atan2(m_sinA,
            m_normals[k].X * m_normals[j].X + m_normals[k].Y * m_normals[j].Y);
            int steps = Math.Max((int)Round(m_StepsPerRad * Math.Abs(a)), 1);

            double X = m_normals[k].X, Y = m_normals[k].Y, X2;
            for (int i = 0; i < steps; ++i)
            {
                m_destPoly.Add(new IntPoint(
                    Round(m_srcPoly[j].X + X * m_delta),
                    Round(m_srcPoly[j].Y + Y * m_delta)));
                X2 = X;
                X = X * m_cos - m_sin * Y;
                Y = X2 * m_sin + Y * m_cos;
            }
            m_destPoly.Add(new IntPoint(
            Round(m_srcPoly[j].X + m_normals[j].X * m_delta),
            Round(m_srcPoly[j].Y + m_normals[j].Y * m_delta)));
        }
        //------------------------------------------------------------------------------
    }

    class ClipperException : Exception
    {
        public ClipperException(string description) : base(description) { }
    }
    //------------------------------------------------------------------------------

} //end ClipperLib namespace

// ----------------------------------------------------------------------
// Options.cs

//
// Options.cs
//
// Authors:
//  Jonathan Pryor <jpryor@novell.com>
//
// Copyright (C) 2008 Novell (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Compile With:
//   gmcs -debug+ -r:System.Core Options.cs -o:NDesk.Options.dll
//   gmcs -debug+ -d:LINQ -r:System.Core Options.cs -o:NDesk.Options.dll
//
// The LINQ version just changes the implementation of
// OptionSet.Parse(IEnumerable<string>), and confers no semantic changes.

//
// A Getopt::Long-inspired option parsing library for C#.
//
// NDesk.Options.OptionSet is built upon a key/value table, where the
// key is a option format string and the value is a delegate that is 
// invoked when the format string is matched.
//
// Option format strings:
//  Regex-like BNF Grammar: 
//    name: .+
//    type: [=:]
//    sep: ( [^{}]+ | '{' .+ '}' )?
//    aliases: ( name type sep ) ( '|' name type sep )*
// 
// Each '|'-delimited name is an alias for the associated action.  If the
// format string ends in a '=', it has a required value.  If the format
// string ends in a ':', it has an optional value.  If neither '=' or ':'
// is present, no value is supported.  `=' or `:' need only be defined on one
// alias, but if they are provided on more than one they must be consistent.
//
// Each alias portion may also end with a "key/value separator", which is used
// to split option values if the option accepts > 1 value.  If not specified,
// it defaults to '=' and ':'.  If specified, it can be any character except
// '{' and '}' OR the *string* between '{' and '}'.  If no separator should be
// used (i.e. the separate values should be distinct arguments), then "{}"
// should be used as the separator.
//
// Options are extracted either from the current option by looking for
// the option name followed by an '=' or ':', or is taken from the
// following option IFF:
//  - The current option does not contain a '=' or a ':'
//  - The current option requires a value (i.e. not a Option type of ':')
//
// The `name' used in the option format string does NOT include any leading
// option indicator, such as '-', '--', or '/'.  All three of these are
// permitted/required on any named option.
//
// Option bundling is permitted so long as:
//   - '-' is used to start the option group
//   - all of the bundled options are a single character
//   - at most one of the bundled options accepts a value, and the value
//     provided starts from the next character to the end of the string.
//
// This allows specifying '-a -b -c' as '-abc', and specifying '-D name=value'
// as '-Dname=value'.
//
// Option processing is disabled by specifying "--".  All options after "--"
// are returned by OptionSet.Parse() unchanged and unprocessed.
//
// Unprocessed options are returned from OptionSet.Parse().
//
// Examples:
//  int verbose = 0;
//  OptionSet p = new OptionSet ()
//    .Add ("v", v => ++verbose)
//    .Add ("name=|value=", v => Console.WriteLine (v));
//  p.Parse (new string[]{"-v", "--v", "/v", "-name=A", "/name", "B", "extra"});
//
// The above would parse the argument string array, and would invoke the
// lambda expression three times, setting `verbose' to 3 when complete.  
// It would also print out "A" and "B" to standard output.
// The returned array would contain the string "extra".
//
// C# 3.0 collection initializers are supported and encouraged:
//  var p = new OptionSet () {
//    { "h|?|help", v => ShowHelp () },
//  };
//
// System.ComponentModel.TypeConverter is also supported, allowing the use of
// custom data types in the callback type; TypeConverter.ConvertFromString()
// is used to convert the value option to an instance of the specified
// type:
//
//  var p = new OptionSet () {
//    { "foo=", (Foo f) => Console.WriteLine (f.ToString ()) },
//  };
//
// Random other tidbits:
//  - Boolean options (those w/o '=' or ':' in the option format string)
//    are explicitly enabled if they are followed with '+', and explicitly
//    disabled if they are followed with '-':
//      string a = null;
//      var p = new OptionSet () {
//        { "a", s => a = s },
//      };
//      p.Parse (new string[]{"-a"});   // sets v != null
//      p.Parse (new string[]{"-a+"});  // sets v != null
//      p.Parse (new string[]{"-a-"});  // sets v == null
//

// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Collections.ObjectModel;
// using System.ComponentModel;
// using System.Globalization;
// using System.IO;
// using System.Runtime.Serialization;
// using System.Security.Permissions;
// using System.Text;
// using System.Text.RegularExpressions;

#if LINQ
// using System.Linq;
#endif

#if TEST
// using NDesk.Options;
#endif

namespace NDesk.Options {

	public class OptionValueCollection : IList, IList<string> {

		List<string> values = new List<string> ();
		OptionContext c;

		internal OptionValueCollection (OptionContext c)
		{
			this.c = c;
		}

		#region ICollection
		void ICollection.CopyTo (Array array, int index)  {(values as ICollection).CopyTo (array, index);}
		bool ICollection.IsSynchronized                   {get {return (values as ICollection).IsSynchronized;}}
		object ICollection.SyncRoot                       {get {return (values as ICollection).SyncRoot;}}
		#endregion

		#region ICollection<T>
		public void Add (string item)                       {values.Add (item);}
		public void Clear ()                                {values.Clear ();}
		public bool Contains (string item)                  {return values.Contains (item);}
		public void CopyTo (string[] array, int arrayIndex) {values.CopyTo (array, arrayIndex);}
		public bool Remove (string item)                    {return values.Remove (item);}
		public int Count                                    {get {return values.Count;}}
		public bool IsReadOnly                              {get {return false;}}
		#endregion

		#region IEnumerable
		IEnumerator IEnumerable.GetEnumerator () {return values.GetEnumerator ();}
		#endregion

		#region IEnumerable<T>
		public IEnumerator<string> GetEnumerator () {return values.GetEnumerator ();}
		#endregion

		#region IList
		int IList.Add (object value)                {return (values as IList).Add (value);}
		bool IList.Contains (object value)          {return (values as IList).Contains (value);}
		int IList.IndexOf (object value)            {return (values as IList).IndexOf (value);}
		void IList.Insert (int index, object value) {(values as IList).Insert (index, value);}
		void IList.Remove (object value)            {(values as IList).Remove (value);}
		void IList.RemoveAt (int index)             {(values as IList).RemoveAt (index);}
		bool IList.IsFixedSize                      {get {return false;}}
		object IList.this [int index]               {get {return this [index];} set {(values as IList)[index] = value;}}
		#endregion

		#region IList<T>
		public int IndexOf (string item)            {return values.IndexOf (item);}
		public void Insert (int index, string item) {values.Insert (index, item);}
		public void RemoveAt (int index)            {values.RemoveAt (index);}

		private void AssertValid (int index)
		{
			if (c.Option == null)
				throw new InvalidOperationException ("OptionContext.Option is null.");
			if (index >= c.Option.MaxValueCount)
				throw new ArgumentOutOfRangeException ("index");
			if (c.Option.OptionValueType == OptionValueType.Required &&
					index >= values.Count)
				throw new OptionException (string.Format (
							c.OptionSet.MessageLocalizer ("Missing required value for option '{0}'."), c.OptionName), 
						c.OptionName);
		}

		public string this [int index] {
			get {
				AssertValid (index);
				return index >= values.Count ? null : values [index];
			}
			set {
				values [index] = value;
			}
		}
		#endregion

		public List<string> ToList ()
		{
			return new List<string> (values);
		}

		public string[] ToArray ()
		{
			return values.ToArray ();
		}

		public override string ToString ()
		{
			return string.Join (", ", values.ToArray ());
		}
	}

	public class OptionContext {
		private Option                option;
		private string                name;
		private int                   index;
		private OptionSet             set;
		private OptionValueCollection c;

		public OptionContext (OptionSet set)
		{
			this.set = set;
			this.c   = new OptionValueCollection (this);
		}

		public Option Option {
			get {return option;}
			set {option = value;}
		}

		public string OptionName { 
			get {return name;}
			set {name = value;}
		}

		public int OptionIndex {
			get {return index;}
			set {index = value;}
		}

		public OptionSet OptionSet {
			get {return set;}
		}

		public OptionValueCollection OptionValues {
			get {return c;}
		}
	}

	public enum OptionValueType {
		None, 
		Optional,
		Required,
	}

	public abstract class Option {
		string prototype, description;
		string[] names;
		OptionValueType type;
		int count;
		string[] separators;

		protected Option (string prototype, string description)
			: this (prototype, description, 1)
		{
		}

		protected Option (string prototype, string description, int maxValueCount)
		{
			if (prototype == null)
				throw new ArgumentNullException ("prototype");
			if (prototype.Length == 0)
				throw new ArgumentException ("Cannot be the empty string.", "prototype");
			if (maxValueCount < 0)
				throw new ArgumentOutOfRangeException ("maxValueCount");

			this.prototype   = prototype;
			this.names       = prototype.Split ('|');
			this.description = description;
			this.count       = maxValueCount;
			this.type        = ParsePrototype ();

			if (this.count == 0 && type != OptionValueType.None)
				throw new ArgumentException (
						"Cannot provide maxValueCount of 0 for OptionValueType.Required or " +
							"OptionValueType.Optional.",
						"maxValueCount");
			if (this.type == OptionValueType.None && maxValueCount > 1)
				throw new ArgumentException (
						string.Format ("Cannot provide maxValueCount of {0} for OptionValueType.None.", maxValueCount),
						"maxValueCount");
			if (Array.IndexOf (names, "<>") >= 0 && 
					((names.Length == 1 && this.type != OptionValueType.None) ||
					 (names.Length > 1 && this.MaxValueCount > 1)))
				throw new ArgumentException (
						"The default option handler '<>' cannot require values.",
						"prototype");
		}

		public string           Prototype       {get {return prototype;}}
		public string           Description     {get {return description;}}
		public OptionValueType  OptionValueType {get {return type;}}
		public int              MaxValueCount   {get {return count;}}

		public string[] GetNames ()
		{
			return (string[]) names.Clone ();
		}

		public string[] GetValueSeparators ()
		{
			if (separators == null)
				return new string [0];
			return (string[]) separators.Clone ();
		}

		protected static T Parse<T> (string value, OptionContext c)
		{
			TypeConverter conv = TypeDescriptor.GetConverter (typeof (T));
			T t = default (T);
			try {
				if (value != null)
					t = (T) conv.ConvertFromString (value);
			}
			catch (Exception e) {
				throw new OptionException (
						string.Format (
							c.OptionSet.MessageLocalizer ("Could not convert string `{0}' to type {1} for option `{2}'."),
							value, typeof (T).Name, c.OptionName),
						c.OptionName, e);
			}
			return t;
		}

		internal string[] Names           {get {return names;}}
		internal string[] ValueSeparators {get {return separators;}}

		static readonly char[] NameTerminator = new char[]{'=', ':'};

		private OptionValueType ParsePrototype ()
		{
			char type = '\0';
			List<string> seps = new List<string> ();
			for (int i = 0; i < names.Length; ++i) {
				string name = names [i];
				if (name.Length == 0)
					throw new ArgumentException ("Empty option names are not supported.", "prototype");

				int end = name.IndexOfAny (NameTerminator);
				if (end == -1)
					continue;
				names [i] = name.Substring (0, end);
				if (type == '\0' || type == name [end])
					type = name [end];
				else 
					throw new ArgumentException (
							string.Format ("Conflicting option types: '{0}' vs. '{1}'.", type, name [end]),
							"prototype");
				AddSeparators (name, end, seps);
			}

			if (type == '\0')
				return OptionValueType.None;

			if (count <= 1 && seps.Count != 0)
				throw new ArgumentException (
						string.Format ("Cannot provide key/value separators for Options taking {0} value(s).", count),
						"prototype");
			if (count > 1) {
				if (seps.Count == 0)
					this.separators = new string[]{":", "="};
				else if (seps.Count == 1 && seps [0].Length == 0)
					this.separators = null;
				else
					this.separators = seps.ToArray ();
			}

			return type == '=' ? OptionValueType.Required : OptionValueType.Optional;
		}

		private static void AddSeparators (string name, int end, ICollection<string> seps)
		{
			int start = -1;
			for (int i = end+1; i < name.Length; ++i) {
				switch (name [i]) {
					case '{':
						if (start != -1)
							throw new ArgumentException (
									string.Format ("Ill-formed name/value separator found in \"{0}\".", name),
									"prototype");
						start = i+1;
						break;
					case '}':
						if (start == -1)
							throw new ArgumentException (
									string.Format ("Ill-formed name/value separator found in \"{0}\".", name),
									"prototype");
						seps.Add (name.Substring (start, i-start));
						start = -1;
						break;
					default:
						if (start == -1)
							seps.Add (name [i].ToString ());
						break;
				}
			}
			if (start != -1)
				throw new ArgumentException (
						string.Format ("Ill-formed name/value separator found in \"{0}\".", name),
						"prototype");
		}

		public void Invoke (OptionContext c)
		{
			OnParseComplete (c);
			c.OptionName  = null;
			c.Option      = null;
			c.OptionValues.Clear ();
		}

		protected abstract void OnParseComplete (OptionContext c);

		public override string ToString ()
		{
			return Prototype;
		}
	}

	[Serializable]
	public class OptionException : Exception {
		private string option;

		public OptionException ()
		{
		}

		public OptionException (string message, string optionName)
			: base (message)
		{
			this.option = optionName;
		}

		public OptionException (string message, string optionName, Exception innerException)
			: base (message, innerException)
		{
			this.option = optionName;
		}

		protected OptionException (SerializationInfo info, StreamingContext context)
			: base (info, context)
		{
			this.option = info.GetString ("OptionName");
		}

		public string OptionName {
			get {return this.option;}
		}

		[SecurityPermission (SecurityAction.LinkDemand, SerializationFormatter = true)]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);
			info.AddValue ("OptionName", option);
		}
	}

	public delegate void OptionAction<TKey, TValue> (TKey key, TValue value);

	public class OptionSet : KeyedCollection<string, Option>
	{
		public OptionSet ()
			: this (delegate (string f) {return f;})
		{
		}

		public OptionSet (Converter<string, string> localizer)
		{
			this.localizer = localizer;
		}

		Converter<string, string> localizer;

		public Converter<string, string> MessageLocalizer {
			get {return localizer;}
		}

		protected override string GetKeyForItem (Option item)
		{
			if (item == null)
				throw new ArgumentNullException ("option");
			if (item.Names != null && item.Names.Length > 0)
				return item.Names [0];
			// This should never happen, as it's invalid for Option to be
			// constructed w/o any names.
			throw new InvalidOperationException ("Option has no names!");
		}

		[Obsolete ("Use KeyedCollection.this[string]")]
		protected Option GetOptionForName (string option)
		{
			if (option == null)
				throw new ArgumentNullException ("option");
			try {
				return base [option];
			}
			catch (KeyNotFoundException) {
				return null;
			}
		}

		protected override void InsertItem (int index, Option item)
		{
			base.InsertItem (index, item);
			AddImpl (item);
		}

		protected override void RemoveItem (int index)
		{
			base.RemoveItem (index);
			Option p = Items [index];
			// KeyedCollection.RemoveItem() handles the 0th item
			for (int i = 1; i < p.Names.Length; ++i) {
				Dictionary.Remove (p.Names [i]);
			}
		}

		protected override void SetItem (int index, Option item)
		{
			base.SetItem (index, item);
			RemoveItem (index);
			AddImpl (item);
		}

		private void AddImpl (Option option)
		{
			if (option == null)
				throw new ArgumentNullException ("option");
			List<string> added = new List<string> (option.Names.Length);
			try {
				// KeyedCollection.InsertItem/SetItem handle the 0th name.
				for (int i = 1; i < option.Names.Length; ++i) {
					Dictionary.Add (option.Names [i], option);
					added.Add (option.Names [i]);
				}
			}
			catch (Exception) {
				foreach (string name in added)
					Dictionary.Remove (name);
				throw;
			}
		}

		public new OptionSet Add (Option option)
		{
			base.Add (option);
			return this;
		}

		sealed class ActionOption : Option {
			Action<OptionValueCollection> action;

			public ActionOption (string prototype, string description, int count, Action<OptionValueCollection> action)
				: base (prototype, description, count)
			{
				if (action == null)
					throw new ArgumentNullException ("action");
				this.action = action;
			}

			protected override void OnParseComplete (OptionContext c)
			{
				action (c.OptionValues);
			}
		}

		public OptionSet Add (string prototype, Action<string> action)
		{
			return Add (prototype, null, action);
		}

		public OptionSet Add (string prototype, string description, Action<string> action)
		{
			if (action == null)
				throw new ArgumentNullException ("action");
			Option p = new ActionOption (prototype, description, 1, 
					delegate (OptionValueCollection v) { action (v [0]); });
			base.Add (p);
			return this;
		}

		public OptionSet Add (string prototype, OptionAction<string, string> action)
		{
			return Add (prototype, null, action);
		}

		public OptionSet Add (string prototype, string description, OptionAction<string, string> action)
		{
			if (action == null)
				throw new ArgumentNullException ("action");
			Option p = new ActionOption (prototype, description, 2, 
					delegate (OptionValueCollection v) {action (v [0], v [1]);});
			base.Add (p);
			return this;
		}

		sealed class ActionOption<T> : Option {
			Action<T> action;

			public ActionOption (string prototype, string description, Action<T> action)
				: base (prototype, description, 1)
			{
				if (action == null)
					throw new ArgumentNullException ("action");
				this.action = action;
			}

			protected override void OnParseComplete (OptionContext c)
			{
				action (Parse<T> (c.OptionValues [0], c));
			}
		}

		sealed class ActionOption<TKey, TValue> : Option {
			OptionAction<TKey, TValue> action;

			public ActionOption (string prototype, string description, OptionAction<TKey, TValue> action)
				: base (prototype, description, 2)
			{
				if (action == null)
					throw new ArgumentNullException ("action");
				this.action = action;
			}

			protected override void OnParseComplete (OptionContext c)
			{
				action (
						Parse<TKey> (c.OptionValues [0], c),
						Parse<TValue> (c.OptionValues [1], c));
			}
		}

		public OptionSet Add<T> (string prototype, Action<T> action)
		{
			return Add (prototype, null, action);
		}

		public OptionSet Add<T> (string prototype, string description, Action<T> action)
		{
			return Add (new ActionOption<T> (prototype, description, action));
		}

		public OptionSet Add<TKey, TValue> (string prototype, OptionAction<TKey, TValue> action)
		{
			return Add (prototype, null, action);
		}

		public OptionSet Add<TKey, TValue> (string prototype, string description, OptionAction<TKey, TValue> action)
		{
			return Add (new ActionOption<TKey, TValue> (prototype, description, action));
		}

		protected virtual OptionContext CreateOptionContext ()
		{
			return new OptionContext (this);
		}

#if LINQ
		public List<string> Parse (IEnumerable<string> arguments)
		{
			bool process = true;
			OptionContext c = CreateOptionContext ();
			c.OptionIndex = -1;
			var def = GetOptionForName ("<>");
			var unprocessed = 
				from argument in arguments
				where ++c.OptionIndex >= 0 && (process || def != null)
					? process
						? argument == "--" 
							? (process = false)
							: !Parse (argument, c)
								? def != null 
									? Unprocessed (null, def, c, argument) 
									: true
								: false
						: def != null 
							? Unprocessed (null, def, c, argument)
							: true
					: true
				select argument;
			List<string> r = unprocessed.ToList ();
			if (c.Option != null)
				c.Option.Invoke (c);
			return r;
		}
#else
		public List<string> Parse (IEnumerable<string> arguments)
		{
			OptionContext c = CreateOptionContext ();
			c.OptionIndex = -1;
			bool process = true;
			List<string> unprocessed = new List<string> ();
			Option def = Contains ("<>") ? this ["<>"] : null;
			foreach (string argument in arguments) {
				++c.OptionIndex;
				if (argument == "--") {
					process = false;
					continue;
				}
				if (!process) {
					Unprocessed (unprocessed, def, c, argument);
					continue;
				}
				if (!Parse (argument, c))
					Unprocessed (unprocessed, def, c, argument);
			}
			if (c.Option != null)
				c.Option.Invoke (c);
			return unprocessed;
		}
#endif

		private static bool Unprocessed (ICollection<string> extra, Option def, OptionContext c, string argument)
		{
			if (def == null) {
				extra.Add (argument);
				return false;
			}
			c.OptionValues.Add (argument);
			c.Option = def;
			c.Option.Invoke (c);
			return false;
		}

		private readonly Regex ValueOption = new Regex (
			@"^(?<flag>--|-|/)(?<name>[^:=]+)((?<sep>[:=])(?<value>.*))?$");

		protected bool GetOptionParts (string argument, out string flag, out string name, out string sep, out string value)
		{
			if (argument == null)
				throw new ArgumentNullException ("argument");

			flag = name = sep = value = null;
			Match m = ValueOption.Match (argument);
			if (!m.Success) {
				return false;
			}
			flag  = m.Groups ["flag"].Value;
			name  = m.Groups ["name"].Value;
			if (m.Groups ["sep"].Success && m.Groups ["value"].Success) {
				sep   = m.Groups ["sep"].Value;
				value = m.Groups ["value"].Value;
			}
			return true;
		}

		protected virtual bool Parse (string argument, OptionContext c)
		{
			if (c.Option != null) {
				ParseValue (argument, c);
				return true;
			}

			string f, n, s, v;
			if (!GetOptionParts (argument, out f, out n, out s, out v))
				return false;

			Option p;
			if (Contains (n)) {
				p = this [n];
				c.OptionName = f + n;
				c.Option     = p;
				switch (p.OptionValueType) {
					case OptionValueType.None:
						c.OptionValues.Add (n);
						c.Option.Invoke (c);
						break;
					case OptionValueType.Optional:
					case OptionValueType.Required: 
						ParseValue (v, c);
						break;
				}
				return true;
			}
			// no match; is it a bool option?
			if (ParseBool (argument, n, c))
				return true;
			// is it a bundled option?
			if (ParseBundledValue (f, string.Concat (n + s + v), c))
				return true;

			return false;
		}

		private void ParseValue (string option, OptionContext c)
		{
			if (option != null)
				foreach (string o in c.Option.ValueSeparators != null 
						? option.Split (c.Option.ValueSeparators, StringSplitOptions.None)
						: new string[]{option}) {
					c.OptionValues.Add (o);
				}
			if (c.OptionValues.Count == c.Option.MaxValueCount || 
					c.Option.OptionValueType == OptionValueType.Optional)
				c.Option.Invoke (c);
			else if (c.OptionValues.Count > c.Option.MaxValueCount) {
				throw new OptionException (localizer (string.Format (
								"Error: Found {0} option values when expecting {1}.", 
								c.OptionValues.Count, c.Option.MaxValueCount)),
						c.OptionName);
			}
		}

		private bool ParseBool (string option, string n, OptionContext c)
		{
			Option p;
			string rn;
			if (n.Length >= 1 && (n [n.Length-1] == '+' || n [n.Length-1] == '-') &&
					Contains ((rn = n.Substring (0, n.Length-1)))) {
				p = this [rn];
				string v = n [n.Length-1] == '+' ? option : null;
				c.OptionName  = option;
				c.Option      = p;
				c.OptionValues.Add (v);
				p.Invoke (c);
				return true;
			}
			return false;
		}

		private bool ParseBundledValue (string f, string n, OptionContext c)
		{
			if (f != "-")
				return false;
			for (int i = 0; i < n.Length; ++i) {
				Option p;
				string opt = f + n [i].ToString ();
				string rn = n [i].ToString ();
				if (!Contains (rn)) {
					if (i == 0)
						return false;
					throw new OptionException (string.Format (localizer (
									"Cannot bundle unregistered option '{0}'."), opt), opt);
				}
				p = this [rn];
				switch (p.OptionValueType) {
					case OptionValueType.None:
						Invoke (c, opt, n, p);
						break;
					case OptionValueType.Optional:
					case OptionValueType.Required: {
						string v     = n.Substring (i+1);
						c.Option     = p;
						c.OptionName = opt;
						ParseValue (v.Length != 0 ? v : null, c);
						return true;
					}
					default:
						throw new InvalidOperationException ("Unknown OptionValueType: " + p.OptionValueType);
				}
			}
			return true;
		}

		private static void Invoke (OptionContext c, string name, string value, Option option)
		{
			c.OptionName  = name;
			c.Option      = option;
			c.OptionValues.Add (value);
			option.Invoke (c);
		}

		private const int OptionWidth = 29;

		public void WriteOptionDescriptions (TextWriter o)
		{
			foreach (Option p in this) {
				int written = 0;
				if (!WriteOptionPrototype (o, p, ref written))
					continue;

				if (written < OptionWidth)
					o.Write (new string (' ', OptionWidth - written));
				else {
					o.WriteLine ();
					o.Write (new string (' ', OptionWidth));
				}

				List<string> lines = GetLines (localizer (GetDescription (p.Description)));
				o.WriteLine (lines [0]);
				string prefix = new string (' ', OptionWidth+2);
				for (int i = 1; i < lines.Count; ++i) {
					o.Write (prefix);
					o.WriteLine (lines [i]);
				}
			}
		}

		bool WriteOptionPrototype (TextWriter o, Option p, ref int written)
		{
			string[] names = p.Names;

			int i = GetNextOptionIndex (names, 0);
			if (i == names.Length)
				return false;

			if (names [i].Length == 1) {
				Write (o, ref written, "  -");
				Write (o, ref written, names [0]);
			}
			else {
				Write (o, ref written, "      --");
				Write (o, ref written, names [0]);
			}

			for ( i = GetNextOptionIndex (names, i+1); 
					i < names.Length; i = GetNextOptionIndex (names, i+1)) {
				Write (o, ref written, ", ");
				Write (o, ref written, names [i].Length == 1 ? "-" : "--");
				Write (o, ref written, names [i]);
			}

			if (p.OptionValueType == OptionValueType.Optional ||
					p.OptionValueType == OptionValueType.Required) {
				if (p.OptionValueType == OptionValueType.Optional) {
					Write (o, ref written, localizer ("["));
				}
				Write (o, ref written, localizer ("=" + GetArgumentName (0, p.MaxValueCount, p.Description)));
				string sep = p.ValueSeparators != null && p.ValueSeparators.Length > 0 
					? p.ValueSeparators [0]
					: " ";
				for (int c = 1; c < p.MaxValueCount; ++c) {
					Write (o, ref written, localizer (sep + GetArgumentName (c, p.MaxValueCount, p.Description)));
				}
				if (p.OptionValueType == OptionValueType.Optional) {
					Write (o, ref written, localizer ("]"));
				}
			}
			return true;
		}

		static int GetNextOptionIndex (string[] names, int i)
		{
			while (i < names.Length && names [i] == "<>") {
				++i;
			}
			return i;
		}

		static void Write (TextWriter o, ref int n, string s)
		{
			n += s.Length;
			o.Write (s);
		}

		private static string GetArgumentName (int index, int maxIndex, string description)
		{
			if (description == null)
				return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
			string[] nameStart;
			if (maxIndex == 1)
				nameStart = new string[]{"{0:", "{"};
			else
				nameStart = new string[]{"{" + index + ":"};
			for (int i = 0; i < nameStart.Length; ++i) {
				int start, j = 0;
				do {
					start = description.IndexOf (nameStart [i], j);
				} while (start >= 0 && j != 0 ? description [j++ - 1] == '{' : false);
				if (start == -1)
					continue;
				int end = description.IndexOf ("}", start);
				if (end == -1)
					continue;
				return description.Substring (start + nameStart [i].Length, end - start - nameStart [i].Length);
			}
			return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
		}

		private static string GetDescription (string description)
		{
			if (description == null)
				return string.Empty;
			StringBuilder sb = new StringBuilder (description.Length);
			int start = -1;
			for (int i = 0; i < description.Length; ++i) {
				switch (description [i]) {
					case '{':
						if (i == start) {
							sb.Append ('{');
							start = -1;
						}
						else if (start < 0)
							start = i + 1;
						break;
					case '}':
						if (start < 0) {
							if ((i+1) == description.Length || description [i+1] != '}')
								throw new InvalidOperationException ("Invalid option description: " + description);
							++i;
							sb.Append ("}");
						}
						else {
							sb.Append (description.Substring (start, i - start));
							start = -1;
						}
						break;
					case ':':
						if (start < 0)
							goto default;
						start = i + 1;
						break;
					default:
						if (start < 0)
							sb.Append (description [i]);
						break;
				}
			}
			return sb.ToString ();
		}

		private static List<string> GetLines (string description)
		{
			List<string> lines = new List<string> ();
			if (string.IsNullOrEmpty (description)) {
				lines.Add (string.Empty);
				return lines;
			}
			int length = 80 - OptionWidth - 2;
			int start = 0, end;
			do {
				end = GetLineEnd (start, length, description);
				bool cont = false;
				if (end < description.Length) {
					char c = description [end];
					if (c == '-' || (char.IsWhiteSpace (c) && c != '\n'))
						++end;
					else if (c != '\n') {
						cont = true;
						--end;
					}
				}
				lines.Add (description.Substring (start, end - start));
				if (cont) {
					lines [lines.Count-1] += "-";
				}
				start = end;
				if (start < description.Length && description [start] == '\n')
					++start;
			} while (end < description.Length);
			return lines;
		}

		private static int GetLineEnd (int start, int length, string description)
		{
			int end = Math.Min (start + length, description.Length);
			int sep = -1;
			for (int i = start; i < end; ++i) {
				switch (description [i]) {
					case ' ':
					case '\t':
					case '\v':
					case '-':
					case ',':
					case '.':
					case ';':
						sep = i;
						break;
					case '\n':
						return i;
				}
			}
			if (sep == -1 || end == description.Length)
				return end;
			return sep;
		}
	}
}


// ----------------------------------------------------------------------
// P2T.cs

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

// using System.Diagnostics;


namespace Poly2Tri
{
    public static class P2T
    {
        private static TriangulationAlgorithm _defaultAlgorithm = TriangulationAlgorithm.DTSweep;

        public static void Triangulate(PolygonSet ps)
        {
            TriangulationContext tcx = CreateContext(_defaultAlgorithm);
            foreach (Polygon p in ps.Polygons)
            {
                Triangulate(p);
            }
        }


        public static void Triangulate(Polygon p)
        {
            Triangulate(_defaultAlgorithm, p);
        }

        
        public static void Triangulate(ConstrainedPointSet cps)
        {
            Triangulate(_defaultAlgorithm, cps);
        }

        
        public static void Triangulate(PointSet ps)
        {
            Triangulate(_defaultAlgorithm, ps);
        }

        
        public static TriangulationContext CreateContext(TriangulationAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case TriangulationAlgorithm.DTSweep:
                default:
                    return new DTSweepContext();
            }
        }

        
        public static void Triangulate(TriangulationAlgorithm algorithm, ITriangulatable t)
        {
            TriangulationContext tcx;

            System.Console.WriteLine("Triangulating " + t.FileName);
            //        long time = System.nanoTime();
            tcx = CreateContext(algorithm);
            tcx.PrepareTriangulation(t);
            Triangulate(tcx);
            //        logger.info( "Triangulation of {} points [{}ms]", tcx.getPoints().size(), ( System.nanoTime() - time ) / 1e6 );
        }

        
        public static void Triangulate(TriangulationContext tcx)
        {
            switch (tcx.Algorithm)
            {
                case TriangulationAlgorithm.DTSweep:
                default:
                    DTSweep.Triangulate((DTSweepContext)tcx);
                    break;
            }
        }


        /// <summary>
        /// Will do a warmup run to let the JVM optimize the triangulation code -- or would if this were Java --MM
        /// </summary>
        public static void Warmup()
        {
#if false
			/*
			 * After a method is run 10000 times, the Hotspot compiler will compile
			 * it into native code. Periodically, the Hotspot compiler may recompile
			 * the method. After an unspecified amount of time, then the compilation
			 * system should become quiet.
			 */
			Polygon poly = PolygonGenerator.RandomCircleSweep2(50, 50000);
			TriangulationProcess process = new TriangulationProcess();
			process.triangulate(poly);
#endif
        }
    }
}

// ----------------------------------------------------------------------
// ITriangulatable.cs

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

// using System.Collections.Generic;

namespace Poly2Tri
{
    public interface ITriangulatable
    {
        //IList<TriangulationPoint> Points { get; } // MM: Neither of these are used via interface (yet?)
        IList<DelaunayTriangle> Triangles { get; }
        TriangulationMode TriangulationMode { get; }
        string FileName { get; set; }
        bool DisplayFlipX { get; set; }
        bool DisplayFlipY { get; set; }
        float DisplayRotate { get; set; }
        double Precision { get; set; }
        double MinX { get; }
        double MaxX { get; }
        double MinY { get; }
        double MaxY { get; }
        Rect2D Bounds { get; }

        void Prepare(TriangulationContext tcx);
        void AddTriangle(DelaunayTriangle t);
        void AddTriangles(IEnumerable<DelaunayTriangle> list);
        void ClearTriangles();
    }
}

// ----------------------------------------------------------------------
// Orientation.cs

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

namespace Poly2Tri
{
    public enum Orientation
    {
        CW,
        CCW,
        Collinear
    }
}

// ----------------------------------------------------------------------
// TriangulationAlgorithm.cs

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

namespace Poly2Tri
{
    public enum TriangulationAlgorithm
    {
        DTSweep
    }
}

// ----------------------------------------------------------------------
// TriangulationConstraint.cs

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

// using System;


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

// ----------------------------------------------------------------------
// TriangulationContext.cs

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

// using System;
// using System.Collections.Generic;
// using System.Text;


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

// ----------------------------------------------------------------------
// TriangulationDebugContext.cs

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

namespace Poly2Tri
{
    public abstract class TriangulationDebugContext
    {
        protected TriangulationContext _tcx;

        public TriangulationDebugContext(TriangulationContext tcx)
        {
            _tcx = tcx;
        }

        public abstract void Clear();
    }
}

// ----------------------------------------------------------------------
// TriangulationMode.cs

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

namespace Poly2Tri
{
    public enum TriangulationMode
    {
        Unconstrained,
        Constrained,
        Polygon
    }
}

// ----------------------------------------------------------------------
// TriangulationPoint.cs

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

// using System;
// using System.Collections;
// using System.Collections.Generic;


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

// ----------------------------------------------------------------------
// DelaunayTriangle.cs

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

/// Changes from the Java version
///   attributification
/// Future possibilities
///   Flattening out the number of indirections
///     Replacing arrays of 3 with fixed-length arrays?
///     Replacing bool[3] with a bit array of some sort?
///     Bundling everything into an AoS mess?
///     Hardcode them all as ABC ?

// using System;
// using System.Diagnostics;
// using System.Collections.Generic;

namespace Poly2Tri
{
    public class DelaunayTriangle
    {

        public FixedArray3<TriangulationPoint> Points;
        public FixedArray3<DelaunayTriangle> Neighbors;
        private FixedBitArray3 mEdgeIsConstrained;
        public FixedBitArray3 EdgeIsConstrained { get { return mEdgeIsConstrained; } }
        public FixedBitArray3 EdgeIsDelaunay;
        public bool IsInterior { get; set; }

        public DelaunayTriangle(TriangulationPoint p1, TriangulationPoint p2, TriangulationPoint p3)
        {
            Points[0] = p1;
            Points[1] = p2;
            Points[2] = p3;
        }


        public int IndexOf(TriangulationPoint p)
        {
            int i = Points.IndexOf(p);
            if (i == -1)
            {
                throw new Exception("Calling index with a point that doesn't exist in triangle");
            }

            return i;
        }

        
        public int IndexCWFrom(TriangulationPoint p)
        {
            return (IndexOf(p) + 2) % 3;
        }


        public int IndexCCWFrom(TriangulationPoint p)
        {
            return (IndexOf(p) + 1) % 3;
        }

        
        public bool Contains(TriangulationPoint p)
        {
            return Points.Contains(p);
        }

        
        /// <summary>
        /// Update neighbor pointers
        /// </summary>
        /// <param name="p1">Point 1 of the shared edge</param>
        /// <param name="p2">Point 2 of the shared edge</param>
        /// <param name="t">This triangle's new neighbor</param>
        private void MarkNeighbor(TriangulationPoint p1, TriangulationPoint p2, DelaunayTriangle t)
        {
            int i = EdgeIndex(p1, p2);
            if (i == -1)
            {
                throw new Exception("Error marking neighbors -- t doesn't contain edge p1-p2!");
            }
            Neighbors[i] = t;
        }


        /// <summary>
        /// Exhaustive search to update neighbor pointers
        /// </summary>
        public void MarkNeighbor(DelaunayTriangle t)
        {
            // Points of this triangle also belonging to t
            bool a = t.Contains(Points[0]);
            bool b = t.Contains(Points[1]);
            bool c = t.Contains(Points[2]);

            if (b && c)
            {
                Neighbors[0] = t;
                t.MarkNeighbor(Points[1], Points[2], this);
            }
            else if (a && c)
            {
                Neighbors[1] = t;
                t.MarkNeighbor(Points[0], Points[2], this);
            }
            else if (a && b)
            {
                Neighbors[2] = t;
                t.MarkNeighbor(Points[0], Points[1], this);
            }
            else
            {
                throw new Exception("Failed to mark neighbor, doesn't share an edge!");
            }
        }


        public void ClearNeighbors()
        {
            Neighbors[0] = Neighbors[1] = Neighbors[2] = null;
        }


        public void ClearNeighbor(DelaunayTriangle triangle)
        {
            if (Neighbors[0] == triangle)
            {
                Neighbors[0] = null;
            }
            else if (Neighbors[1] == triangle)
            {
                Neighbors[1] = null;
            }
            else if( Neighbors[2] == triangle)
            {
                Neighbors[2] = null;
            }
        }

        /// <summary>
        /// Clears all references to all other triangles and points
        /// </summary>
        public void Clear()
        {
            DelaunayTriangle t;
            for (int i = 0; i < 3; i++)
            {
                t = Neighbors[i];
                if (t != null)
                {
                    t.ClearNeighbor(this);
                }
            }
            ClearNeighbors();
            Points[0] = Points[1] = Points[2] = null;
        }

        /// <param name="t">Opposite triangle</param>
        /// <param name="p">The point in t that isn't shared between the triangles</param>
        public TriangulationPoint OppositePoint(DelaunayTriangle t, TriangulationPoint p)
        {
            Debug.Assert(t != this, "self-pointer error");
            return PointCWFrom(t.PointCWFrom(p));
        }


        public DelaunayTriangle NeighborCWFrom(TriangulationPoint point)
        {
            return Neighbors[(Points.IndexOf(point) + 1) % 3];
        }
        
        
        public DelaunayTriangle NeighborCCWFrom(TriangulationPoint point)
        {
            return Neighbors[(Points.IndexOf(point) + 2) % 3];
        }
        
        
        public DelaunayTriangle NeighborAcrossFrom(TriangulationPoint point)
        {
            return Neighbors[Points.IndexOf(point)];
        }

        
        public TriangulationPoint PointCCWFrom(TriangulationPoint point)
        {
            return Points[(IndexOf(point) + 1) % 3];
        }
        
        
        public TriangulationPoint PointCWFrom(TriangulationPoint point)
        {
            return Points[(IndexOf(point) + 2) % 3];
        }

        
        private void RotateCW()
        {
            var t = Points[2];
            Points[2] = Points[1];
            Points[1] = Points[0];
            Points[0] = t;
        }

        
        /// <summary>
        /// Legalize triangle by rotating clockwise around oPoint
        /// </summary>
        /// <param name="oPoint">The origin point to rotate around</param>
        /// <param name="nPoint">???</param>
        public void Legalize(TriangulationPoint oPoint, TriangulationPoint nPoint)
        {
            RotateCW();
            Points[IndexCCWFrom(oPoint)] = nPoint;
        }

        
        public override string ToString()
        {
            return Points[0] + "," + Points[1] + "," + Points[2];
        }

        
        /// <summary>
        /// Finalize edge marking
        /// </summary>
        public void MarkNeighborEdges()
        {
            for (int i = 0; i < 3; i++)
            {
                if (EdgeIsConstrained[i] && Neighbors[i] != null)
                {
                    Neighbors[i].MarkConstrainedEdge(Points[(i + 1) % 3], Points[(i + 2) % 3]);
                }
            }
        }


        public void MarkEdge(DelaunayTriangle triangle)
        {
            for (int i = 0; i < 3; i++) if (EdgeIsConstrained[i])
                {
                    triangle.MarkConstrainedEdge(Points[(i + 1) % 3], Points[(i + 2) % 3]);
                }
        }

        public void MarkEdge(List<DelaunayTriangle> tList)
        {
            foreach (DelaunayTriangle t in tList)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (t.EdgeIsConstrained[i])
                    {
                        MarkConstrainedEdge(t.Points[(i + 1) % 3], t.Points[(i + 2) % 3]);
                    }
                }
            }
        }


        public void MarkConstrainedEdge(int index)
        {
            mEdgeIsConstrained[index] = true;
        }


        public void MarkConstrainedEdge(DTSweepConstraint edge)
        {
            MarkConstrainedEdge(edge.P, edge.Q);
        }


        /// <summary>
        /// Mark edge as constrained
        /// </summary>
        public void MarkConstrainedEdge(TriangulationPoint p, TriangulationPoint q)
        {
            int i = EdgeIndex(p, q);
            if (i != -1)
            {
                mEdgeIsConstrained[i] = true;
            }
        }


        public double Area()
        {
            double b = Points[0].X - Points[1].X;
            double h = Points[2].Y - Points[1].Y;

            return Math.Abs((b * h * 0.5f));
        }

        public TriangulationPoint Centroid()
        {
            double cx = (Points[0].X + Points[1].X + Points[2].X) / 3f;
            double cy = (Points[0].Y + Points[1].Y + Points[2].Y) / 3f;
            return new TriangulationPoint(cx, cy);
        }


        /// <summary>
        /// Get the index of the neighbor that shares this edge (or -1 if it isn't shared)
        /// </summary>
        /// <returns>index of the shared edge or -1 if edge isn't shared</returns>
        public int EdgeIndex(TriangulationPoint p1, TriangulationPoint p2)
        {
            int i1 = Points.IndexOf(p1);
            int i2 = Points.IndexOf(p2);

            // Points of this triangle in the edge p1-p2
            bool a = (i1 == 0 || i2 == 0);
            bool b = (i1 == 1 || i2 == 1);
            bool c = (i1 == 2 || i2 == 2);

            if (b && c)
            {
                return 0;
            }
            if (a && c)
            {
                return 1;
            }
            if (a && b)
            {
                return 2;
            }

            return -1;
        }


        public bool GetConstrainedEdgeCCW(TriangulationPoint p) { return EdgeIsConstrained[(IndexOf(p) + 2) % 3]; }
        public bool GetConstrainedEdgeCW(TriangulationPoint p) { return EdgeIsConstrained[(IndexOf(p) + 1) % 3]; }
        public bool GetConstrainedEdgeAcross(TriangulationPoint p) { return EdgeIsConstrained[IndexOf(p)]; }

        protected void SetConstrainedEdge(int idx, bool ce)
        {
            //if (ce == false && EdgeIsConstrained[idx])
            //{
            //    DTSweepConstraint edge = null;
            //    if (GetEdge(idx, out edge))
            //    {
            //        Console.WriteLine("Removing pre-defined constraint from edge " + edge.ToString());
            //    }
            //}
            mEdgeIsConstrained[idx] = ce;
        }
        public void SetConstrainedEdgeCCW(TriangulationPoint p, bool ce)
        {
            int idx = (IndexOf(p) + 2) % 3;
            SetConstrainedEdge(idx, ce);
        }
        public void SetConstrainedEdgeCW(TriangulationPoint p, bool ce)
        {
            int idx = (IndexOf(p) + 1) % 3;
            SetConstrainedEdge(idx, ce);
        }
        public void SetConstrainedEdgeAcross(TriangulationPoint p, bool ce)
        {
            int idx = IndexOf(p);
            SetConstrainedEdge(idx, ce);
        }

        public bool GetDelaunayEdgeCCW(TriangulationPoint p) { return EdgeIsDelaunay[(IndexOf(p) + 2) % 3]; }
        public bool GetDelaunayEdgeCW(TriangulationPoint p) { return EdgeIsDelaunay[(IndexOf(p) + 1) % 3]; }
        public bool GetDelaunayEdgeAcross(TriangulationPoint p) { return EdgeIsDelaunay[IndexOf(p)]; }
        public void SetDelaunayEdgeCCW(TriangulationPoint p, bool ce) { EdgeIsDelaunay[(IndexOf(p) + 2) % 3] = ce; }
        public void SetDelaunayEdgeCW(TriangulationPoint p, bool ce) { EdgeIsDelaunay[(IndexOf(p) + 1) % 3] = ce; }
        public void SetDelaunayEdgeAcross(TriangulationPoint p, bool ce) { EdgeIsDelaunay[IndexOf(p)] = ce; }


        public bool GetEdge(int idx, out DTSweepConstraint edge)
        {
            edge = null;
            if (idx < 0 || idx > 2)
            {
                return false;
            }
            TriangulationPoint p1 = Points[(idx + 1) % 3];
            TriangulationPoint p2 = Points[(idx + 2) % 3];
            if (p1.GetEdge(p2, out edge))
            {
                return true;
            }
            else if (p2.GetEdge(p1, out edge))
            {
                return true;
            }

            return false;
        }


        public bool GetEdgeCCW(TriangulationPoint p, out DTSweepConstraint edge)
        {
            int pointIndex = IndexOf(p);
            int edgeIdx = (pointIndex + 2)%3;

            return GetEdge(edgeIdx, out edge);
        }

        public bool GetEdgeCW(TriangulationPoint p, out DTSweepConstraint edge)
        {
            int pointIndex = IndexOf(p);
            int edgeIdx = (pointIndex + 1) % 3;

            return GetEdge(edgeIdx, out edge);
        }
        
        public bool GetEdgeAcross(TriangulationPoint p, out DTSweepConstraint edge)
        {
            int pointIndex = IndexOf(p);
            int edgeIdx = pointIndex;

            return GetEdge(edgeIdx, out edge);
        }

    }
}

// ----------------------------------------------------------------------
// AdvancingFront.cs

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

/// Changes from the Java version
///   Removed BST code, but not all artifacts of it
/// Future possibilities
///   Eliminate Add/RemoveNode ?
///   Comments comments and more comments!

// using System.Text;
// using System;

namespace Poly2Tri
{
    /**
     * @author Thomas Åhlen (thahlen@gmail.com)
     */
    public class AdvancingFront
    {
        public AdvancingFrontNode Head;
        public AdvancingFrontNode Tail;
        protected AdvancingFrontNode Search;

        public AdvancingFront(AdvancingFrontNode head, AdvancingFrontNode tail)
        {
            this.Head = head;
            this.Tail = tail;
            this.Search = head;
            AddNode(head);
            AddNode(tail);
        }

        public void AddNode(AdvancingFrontNode node) { }
        public void RemoveNode(AdvancingFrontNode node) { }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            AdvancingFrontNode node = Head;
            while (node != Tail)
            {
                sb.Append(node.Point.X).Append("->");
                node = node.Next;
            }
            sb.Append(Tail.Point.X);
            return sb.ToString();
        }

        /// <summary>
        /// MM:  This seems to be used by LocateNode to guess a position in the implicit linked list of AdvancingFrontNodes near x
        ///      Removed an overload that depended on this being exact
        /// </summary>
        private AdvancingFrontNode FindSearchNode(double x)
        {
            return Search;
        }

        /// <summary>
        /// We use a balancing tree to locate a node smaller or equal to given key value (in theory)
        /// </summary>
        public AdvancingFrontNode LocateNode(TriangulationPoint point)
        {
            return LocateNode(point.X);
        }

        private AdvancingFrontNode LocateNode(double x)
        {
            AdvancingFrontNode node = FindSearchNode(x);
            if (x < node.Value)
            {
                while ((node = node.Prev) != null)
                {
                    if (x >= node.Value)
                    {
                        Search = node;
                        return node;
                    }
                }
            }
            else
            {
                while ((node = node.Next) != null)
                {
                    if (x < node.Value)
                    {
                        Search = node.Prev;
                        return node.Prev;
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// This implementation will use simple node traversal algorithm to find a point on the front
        /// </summary>
        public AdvancingFrontNode LocatePoint(TriangulationPoint point)
        {
            double px = point.X;
            AdvancingFrontNode node = FindSearchNode(px);
            double nx = node.Point.X;

            if (px == nx)
            {
                if (point != node.Point)
                {
                    // We might have two nodes with same x value for a short time
                    if (point == node.Prev.Point)
                    {
                        node = node.Prev;
                    }
                    else if (point == node.Next.Point)
                    {
                        node = node.Next;
                    }
                    else
                    {
                        throw new Exception("Failed to find Node for given afront point");
                    }
                }
            }
            else if (px < nx)
            {
                while ((node = node.Prev) != null)
                {
                    if (point == node.Point)
                    {
                        break;
                    }
                }
            }
            else
            {
                while ((node = node.Next) != null)
                {
                    if (point == node.Point)
                    {
                        break;
                    }
                }
            }
            Search = node;

            return node;
        }
    }
}

// ----------------------------------------------------------------------
// AdvancingFrontNode.cs

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

/// Changes from the Java version
///   Removed getters
///   Has* turned into attributes
/// Future possibilities
///   Comments!

namespace Poly2Tri
{
    public class AdvancingFrontNode
    {
        public AdvancingFrontNode Next;
        public AdvancingFrontNode Prev;
        public double Value;
        public TriangulationPoint Point;
        public DelaunayTriangle Triangle;

        public AdvancingFrontNode(TriangulationPoint point)
        {
            this.Point = point;
            Value = point.X;
        }

        public bool HasNext { get { return Next != null; } }
        public bool HasPrev { get { return Prev != null; } }
    }
}

// ----------------------------------------------------------------------
// DTSweep.cs

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

/*
 * Sweep-line, Constrained Delauney Triangulation (CDT) See: Domiter, V. and
 * Zalik, B.(2008)'Sweep-line algorithm for constrained Delaunay triangulation',
 * International Journal of Geographical Information Science
 * 
 * "FlipScan" Constrained Edge Algorithm invented by author of this code.
 * 
 * Author: Thomas Åhlén, thahlen@gmail.com 
 */

/// Changes from the Java version
///   Turned DTSweep into a static class
///   Lots of deindentation via early bailout
/// Future possibilities
///   Comments!

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Linq;

namespace Poly2Tri
{
    public static class DTSweep
    {
        private const double PI_div2 = Math.PI / 2;
        private const double PI_3div4 = 3 * Math.PI / 4;

        
        /// <summary>
        /// Triangulate simple polygon with holes
        /// </summary>
        public static void Triangulate(DTSweepContext tcx)
        {
            tcx.CreateAdvancingFront();

            Sweep(tcx);

            FixupConstrainedEdges(tcx);

            // Finalize triangulation
            if (tcx.TriangulationMode == TriangulationMode.Polygon)
            {
                FinalizationPolygon(tcx);
            }
            else 
            {
                FinalizationConvexHull(tcx);
                if (tcx.TriangulationMode == TriangulationMode.Constrained)
                {
                    // work in progress.  When it's done, call FinalizationConstraints INSTEAD of tcx.FinalizeTriangulation
                    //FinalizationConstraints(tcx);

                    tcx.FinalizeTriangulation();
                }
                else
                {
                    tcx.FinalizeTriangulation();
                }
            }

            tcx.Done();
        }

        
        /// <summary>
        /// Start sweeping the Y-sorted point set from bottom to top
        /// </summary>
        private static void Sweep(DTSweepContext tcx)
        {
            var points = tcx.Points;
            TriangulationPoint point;
            AdvancingFrontNode node;

            for (int i = 1; i < points.Count; i++)
            {
                point = points[i];
                node = PointEvent(tcx, point);

                if (node != null && point.HasEdges)
                {
                    foreach (DTSweepConstraint e in point.Edges)
                    {
                        if (tcx.IsDebugEnabled)
                        {
                            tcx.DTDebugContext.ActiveConstraint = e;
                        }
                        EdgeEvent(tcx, e, node);
                    }
                }
                tcx.Update(null);
            }
        }


        private static void FixupConstrainedEdges(DTSweepContext tcx)
        {
            foreach(DelaunayTriangle t in tcx.Triangles)
            {
                for (int i = 0; i < 3; ++i)
                {
                    bool isConstrained = t.GetConstrainedEdgeCCW(t.Points[i]);
                    if (!isConstrained)
                    {
                        DTSweepConstraint edge = null;
                        bool hasConstrainedEdge = t.GetEdgeCCW(t.Points[i], out edge);
                        if (hasConstrainedEdge)
                        {
                            t.MarkConstrainedEdge((i + 2) % 3);
                            //t.MarkConstrainedEdgeCCW(t.Points[i]);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// If this is a Delaunay Triangulation of a pointset we need to fill so the triangle mesh gets a ConvexHull 
        /// </summary>
        private static void FinalizationConvexHull(DTSweepContext tcx)
        {
            AdvancingFrontNode n1, n2;
            DelaunayTriangle t1, t2;
            TriangulationPoint first, p1;

            n1 = tcx.Front.Head.Next;
            n2 = n1.Next;
            first = n1.Point;

            TurnAdvancingFrontConvex(tcx, n1, n2);

            // Lets remove triangles connected to the two "algorithm" points
            // XXX: When the first three nodes are points in a triangle we need to do a flip before
            // removing triangles or we will lose a valid triangle.
            // Same for last three nodes!
            // !!! If I implement ConvexHull for lower right and left boundary this fix should not be
            // needed and the removed triangles will be added again by default

            n1 = tcx.Front.Tail.Prev;
            if (n1.Triangle.Contains(n1.Next.Point) && n1.Triangle.Contains(n1.Prev.Point))
            {
                t1 = n1.Triangle.NeighborAcrossFrom(n1.Point);
                RotateTrianglePair(n1.Triangle, n1.Point, t1, t1.OppositePoint(n1.Triangle, n1.Point));
                tcx.MapTriangleToNodes(n1.Triangle);
                tcx.MapTriangleToNodes(t1);
            }
            n1 = tcx.Front.Head.Next;
            if (n1.Triangle.Contains(n1.Prev.Point) && n1.Triangle.Contains(n1.Next.Point))
            {
                t1 = n1.Triangle.NeighborAcrossFrom(n1.Point);
                RotateTrianglePair(n1.Triangle, n1.Point, t1, t1.OppositePoint(n1.Triangle, n1.Point));
                tcx.MapTriangleToNodes(n1.Triangle);
                tcx.MapTriangleToNodes(t1);
            }

            // Lower right boundary 
            first = tcx.Front.Head.Point;
            n2 = tcx.Front.Tail.Prev;
            t1 = n2.Triangle;
            p1 = n2.Point;
            n2.Triangle = null;
            do
            {
                tcx.RemoveFromList(t1);
                p1 = t1.PointCCWFrom(p1);
                if (p1 == first)
                {
                    break;
                }
                t2 = t1.NeighborCCWFrom(p1);
                t1.Clear();
                t1 = t2;
            } while (true);

            // Lower left boundary
            first = tcx.Front.Head.Next.Point;
            p1 = t1.PointCWFrom(tcx.Front.Head.Point);
            t2 = t1.NeighborCWFrom(tcx.Front.Head.Point);
            t1.Clear();
            t1 = t2;
            while (p1 != first)
            {
                tcx.RemoveFromList(t1);
                p1 = t1.PointCCWFrom(p1);
                t2 = t1.NeighborCCWFrom(p1);
                t1.Clear();
                t1 = t2;
            }

            // Remove current head and tail node now that we have removed all triangles attached
            // to them. Then set new head and tail node points
            tcx.Front.Head = tcx.Front.Head.Next;
            tcx.Front.Head.Prev = null;
            tcx.Front.Tail = tcx.Front.Tail.Prev;
            tcx.Front.Tail.Next = null; 
        }

        
        /// <summary>
        /// We will traverse the entire advancing front and fill it to form a convex hull.
        /// </summary>
        private static void TurnAdvancingFrontConvex(DTSweepContext tcx, AdvancingFrontNode b, AdvancingFrontNode c)
        {
            AdvancingFrontNode first = b;
            while (c != tcx.Front.Tail)
            {
                if (tcx.IsDebugEnabled)
                {
                    tcx.DTDebugContext.ActiveNode = c;
                }

                if (TriangulationUtil.Orient2d(b.Point, c.Point, c.Next.Point) == Orientation.CCW)
                {
                    // [b,c,d] Concave - fill around c
                    Fill(tcx, c);
                    c = c.Next;
                }
                else
                {
                    // [b,c,d] Convex
                    if (b != first && TriangulationUtil.Orient2d(b.Prev.Point, b.Point, c.Point) == Orientation.CCW)
                    {
                        // [a,b,c] Concave - fill around b
                        Fill(tcx, b);
                        b = b.Prev;
                    }
                    else
                    {
                        // [a,b,c] Convex - nothing to fill
                        b = c;
                        c = c.Next;
                    }
                }
            }
        }

        
        private static void FinalizationPolygon(DTSweepContext tcx)
        {
            // Get an Internal triangle to start with
            DelaunayTriangle t = tcx.Front.Head.Next.Triangle;
            TriangulationPoint p = tcx.Front.Head.Next.Point;
            while (!t.GetConstrainedEdgeCW(p))
            {
                DelaunayTriangle tTmp = t.NeighborCCWFrom(p);
                if (tTmp == null)
                {
                    break;
                }
                t = tTmp;
            }

            // Collect interior triangles constrained by edges
            tcx.MeshClean(t);
        }


        /// <summary>
        /// NOTE: WORK IN PROGRESS - for now this will just clean out all triangles from
        /// inside the outermost holes without paying attention to holes within holes..
        /// hence the work in progress :)
        /// 
        /// Removes triangles inside "holes" (that are not inside of other holes already)
        /// 
        /// In the example below, assume that triangle ABC is a user-defined "hole".  Thus
        /// any triangles inside it (that aren't inside yet another user-defined hole inside
        /// triangle ABC) should get removed.  In this case, since there are no user-defined
        /// holes inside ABC, we would remove triangles ADE, BCE, and CDE.  We would also 
        /// need to combine the appropriate edges so that we end up with just triangle ABC
        ///
        ///          E
        /// A +------+-----+ B              A +-----------+ B
        ///    \    /|    /                    \         /
        ///     \  / |   /                      \       /
        ///    D +   |  /        ======>         \     /
        ///       \  | /                          \   /
        ///        \ |/                            \ /
        ///          +                              +
        ///          C                              C
        ///          
        /// </summary>
        private static void FinalizationConstraints(DTSweepContext tcx)
        {
            // Get an Internal triangle to start with
            DelaunayTriangle t = tcx.Front.Head.Triangle;
            TriangulationPoint p = tcx.Front.Head.Point;
            while (!t.GetConstrainedEdgeCW(p))
            {
                DelaunayTriangle tTmp = t.NeighborCCWFrom(p);
                if (tTmp == null)
                {
                    break;
                }
                t = tTmp;
            }

            // Collect interior triangles constrained by edges
            tcx.MeshClean(t);
        }


        /// <summary>
        /// Find closes node to the left of the new point and
        /// create a new triangle. If needed new holes and basins
        /// will be filled to.
        /// </summary>
        private static AdvancingFrontNode PointEvent(DTSweepContext tcx, TriangulationPoint point)
        {
            AdvancingFrontNode node, newNode;

            node = tcx.LocateNode(point);
            if (tcx.IsDebugEnabled)
            {
                tcx.DTDebugContext.ActiveNode = node;
            }
            if (node == null || point == null)
            {
                return null;
            }
            newNode = NewFrontTriangle(tcx, point, node);

            // Only need to check +epsilon since point never have smaller 
            // x value than node due to how we fetch nodes from the front
            if (point.X <= node.Point.X + MathUtil.EPSILON)
            {
                Fill(tcx, node);
            }

            tcx.AddNode(newNode);

            FillAdvancingFront(tcx, newNode);
            return newNode;
        }

        
        /// <summary>
        /// Creates a new front triangle and legalize it
        /// </summary>
        private static AdvancingFrontNode NewFrontTriangle(DTSweepContext tcx, TriangulationPoint point, AdvancingFrontNode node)
        {
            AdvancingFrontNode newNode;
            DelaunayTriangle triangle;

            triangle = new DelaunayTriangle(point, node.Point, node.Next.Point);
            triangle.MarkNeighbor(node.Triangle);
            tcx.Triangles.Add(triangle);

            newNode = new AdvancingFrontNode(point);
            newNode.Next = node.Next;
            newNode.Prev = node;
            node.Next.Prev = newNode;
            node.Next = newNode;

            tcx.AddNode(newNode); // XXX: BST

            if (tcx.IsDebugEnabled)
            {
                tcx.DTDebugContext.ActiveNode = newNode;
            }

            if (!Legalize(tcx, triangle))
            {
                tcx.MapTriangleToNodes(triangle);
            }

            return newNode;
        }

        
        private static void EdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            try
            {
                tcx.EdgeEvent.ConstrainedEdge = edge;
                tcx.EdgeEvent.Right = edge.P.X > edge.Q.X;

                if (tcx.IsDebugEnabled)
                {
                    tcx.DTDebugContext.PrimaryTriangle = node.Triangle;
                }

                if (IsEdgeSideOfTriangle(node.Triangle, edge.P, edge.Q))
                {
                    return;
                }

                // For now we will do all needed filling
                // TODO: integrate with flip process might give some better performance 
                //       but for now this avoid the issue with cases that needs both flips and fills
                FillEdgeEvent(tcx, edge, node);

                EdgeEvent(tcx, edge.P, edge.Q, node.Triangle, edge.Q);
            }
            catch (PointOnEdgeException)
            {
                //Debug.WriteLine( String.Format( "Warning: Skipping Edge: {0}", e.Message ) );
                throw;
            }
        }


        private static void FillEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            if (tcx.EdgeEvent.Right)
            {
                FillRightAboveEdgeEvent(tcx, edge, node);
            }
            else
            {
                FillLeftAboveEdgeEvent(tcx, edge, node);
            }
        }

        
        private static void FillRightConcaveEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            Fill(tcx, node.Next);
            if (node.Next.Point != edge.P)
            {
                // Next above or below edge?
                if (TriangulationUtil.Orient2d(edge.Q, node.Next.Point, edge.P) == Orientation.CCW)
                {
                    // Below
                    if (TriangulationUtil.Orient2d(node.Point, node.Next.Point, node.Next.Next.Point) == Orientation.CCW)
                    {
                        // Next is concave
                        FillRightConcaveEdgeEvent(tcx, edge, node);
                    }
                    else
                    {
                        // Next is convex
                    }
                }
            }
        }


        private static void FillRightConvexEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            // Next concave or convex?
            if (TriangulationUtil.Orient2d(node.Next.Point, node.Next.Next.Point, node.Next.Next.Next.Point) == Orientation.CCW)
            {
                // Concave
                FillRightConcaveEdgeEvent(tcx, edge, node.Next);
            }
            else
            {
                // Convex
                // Next above or below edge?
                if (TriangulationUtil.Orient2d(edge.Q, node.Next.Next.Point, edge.P) == Orientation.CCW)
                {
                    // Below
                    FillRightConvexEdgeEvent(tcx, edge, node.Next);
                }
                else
                {
                    // Above
                }
            }
        }

        private static void FillRightBelowEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            if (tcx.IsDebugEnabled)
            {
                tcx.DTDebugContext.ActiveNode = node;
            }

            if (node.Point.X < edge.P.X)
            {
                // needed?
                if (TriangulationUtil.Orient2d(node.Point, node.Next.Point, node.Next.Next.Point) == Orientation.CCW)
                {
                    // Concave 
                    FillRightConcaveEdgeEvent(tcx, edge, node);
                }
                else
                {
                    // Convex
                    FillRightConvexEdgeEvent(tcx, edge, node);
                    // Retry this one
                    FillRightBelowEdgeEvent(tcx, edge, node);
                }
            }
        }


        private static void FillRightAboveEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            while (node.Next.Point.X < edge.P.X)
            {
                if (tcx.IsDebugEnabled) { tcx.DTDebugContext.ActiveNode = node; }
                // Check if next node is below the edge
                Orientation o1 = TriangulationUtil.Orient2d(edge.Q, node.Next.Point, edge.P);
                if (o1 == Orientation.CCW)
                {
                    FillRightBelowEdgeEvent(tcx, edge, node);
                }
                else
                {
                    node = node.Next;
                }
            }
        }

        
        private static void FillLeftConvexEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            // Next concave or convex?
            if (TriangulationUtil.Orient2d(node.Prev.Point, node.Prev.Prev.Point, node.Prev.Prev.Prev.Point) == Orientation.CW)
            {
                // Concave
                FillLeftConcaveEdgeEvent(tcx, edge, node.Prev);
            }
            else
            {
                // Convex
                // Next above or below edge?
                if (TriangulationUtil.Orient2d(edge.Q, node.Prev.Prev.Point, edge.P) == Orientation.CW)
                {
                    // Below
                    FillLeftConvexEdgeEvent(tcx, edge, node.Prev);
                }
                else
                {
                    // Above
                }
            }
        }

        
        private static void FillLeftConcaveEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            Fill(tcx, node.Prev);
            if (node.Prev.Point != edge.P)
            {
                // Next above or below edge?
                if (TriangulationUtil.Orient2d(edge.Q, node.Prev.Point, edge.P) == Orientation.CW)
                {
                    // Below
                    if (TriangulationUtil.Orient2d(node.Point, node.Prev.Point, node.Prev.Prev.Point) == Orientation.CW)
                    {
                        // Next is concave
                        FillLeftConcaveEdgeEvent(tcx, edge, node);
                    }
                    else
                    {
                        // Next is convex
                    }
                }
            }
        }

        
        private static void FillLeftBelowEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            if (tcx.IsDebugEnabled)
                tcx.DTDebugContext.ActiveNode = node;

            if (node.Point.X > edge.P.X)
            {
                if (TriangulationUtil.Orient2d(node.Point, node.Prev.Point, node.Prev.Prev.Point) == Orientation.CW)
                {
                    // Concave 
                    FillLeftConcaveEdgeEvent(tcx, edge, node);
                }
                else
                {
                    // Convex
                    FillLeftConvexEdgeEvent(tcx, edge, node);
                    // Retry this one
                    FillLeftBelowEdgeEvent(tcx, edge, node);
                }

            }
        }

        
        private static void FillLeftAboveEdgeEvent(DTSweepContext tcx, DTSweepConstraint edge, AdvancingFrontNode node)
        {
            while (node.Prev.Point.X > edge.P.X)
            {
                if (tcx.IsDebugEnabled)
                {
                    tcx.DTDebugContext.ActiveNode = node;
                }
                // Check if next node is below the edge
                Orientation o1 = TriangulationUtil.Orient2d(edge.Q, node.Prev.Point, edge.P);
                if (o1 == Orientation.CW)
                {
                    FillLeftBelowEdgeEvent(tcx, edge, node);
                }
                else
                {
                    node = node.Prev;
                }
            }
        }

        
        private static bool IsEdgeSideOfTriangle(DelaunayTriangle triangle, TriangulationPoint ep, TriangulationPoint eq)
        {
            int index = triangle.EdgeIndex(ep, eq);
            if (index == -1)
            {
                return false;
            }
            triangle.MarkConstrainedEdge(index);
            triangle = triangle.Neighbors[index];
            if (triangle != null)
            {
                triangle.MarkConstrainedEdge(ep, eq);
            }
            return true;
        }

        
        private static void EdgeEvent(DTSweepContext tcx, TriangulationPoint ep, TriangulationPoint eq, DelaunayTriangle triangle, TriangulationPoint point)
        {
            TriangulationPoint p1, p2;

            if (tcx.IsDebugEnabled)
            {
                tcx.DTDebugContext.PrimaryTriangle = triangle;
            }

            if (IsEdgeSideOfTriangle(triangle, ep, eq))
            {
                return;
            }

            p1 = triangle.PointCCWFrom(point);
            Orientation o1 = TriangulationUtil.Orient2d(eq, p1, ep);
            if (o1 == Orientation.Collinear)
            {
                if (triangle.Contains(eq) && triangle.Contains(p1))
                {
                    triangle.MarkConstrainedEdge(eq, p1);
                    // We are modifying the constraint maybe it would be better to
                    // not change the given constraint and just keep a variable for the new constraint
                    tcx.EdgeEvent.ConstrainedEdge.Q = p1;
                    triangle = triangle.NeighborAcrossFrom(point);
                    EdgeEvent(tcx, ep, p1, triangle, p1);
                }
                else
                {
                    throw new PointOnEdgeException("EdgeEvent - Point on constrained edge not supported yet", ep, eq, p1);
                }
                if (tcx.IsDebugEnabled)
                {
                    Console.WriteLine("EdgeEvent - Point on constrained edge");
                }

                return;
            }

            p2 = triangle.PointCWFrom(point);
            Orientation o2 = TriangulationUtil.Orient2d(eq, p2, ep);
            if (o2 == Orientation.Collinear)
            {
                if (triangle.Contains(eq) && triangle.Contains(p2))
                {
                    triangle.MarkConstrainedEdge(eq, p2);
                    // We are modifying the constraint maybe it would be better to
                    // not change the given constraint and just keep a variable for the new constraint
                    tcx.EdgeEvent.ConstrainedEdge.Q = p2;
                    triangle = triangle.NeighborAcrossFrom(point);
                    EdgeEvent(tcx, ep, p2, triangle, p2);
                }
                else
                {
                    throw new PointOnEdgeException("EdgeEvent - Point on constrained edge not supported yet", ep, eq, p2);
                }
                if (tcx.IsDebugEnabled)
                {
                    Console.WriteLine("EdgeEvent - Point on constrained edge");
                }

                return;
            }

            if (o1 == o2)
            {
                // Need to decide if we are rotating CW or CCW to get to a triangle
                // that will cross edge
                if (o1 == Orientation.CW)
                {
                    triangle = triangle.NeighborCCWFrom(point);
                }
                else
                {
                    triangle = triangle.NeighborCWFrom(point);
                }
                EdgeEvent(tcx, ep, eq, triangle, point);
            }
            else
            {
                // This triangle crosses constraint so lets flippin start!
                FlipEdgeEvent(tcx, ep, eq, triangle, point);
            }
        }


        private static void FlipEdgeEvent(DTSweepContext tcx, TriangulationPoint ep, TriangulationPoint eq, DelaunayTriangle t, TriangulationPoint p)
        {
            DelaunayTriangle ot = t.NeighborAcrossFrom(p);
            TriangulationPoint op = ot.OppositePoint(t, p);

            if (ot == null)
            {
                // If we want to integrate the fillEdgeEvent do it here
                // With current implementation we should never get here
                throw new InvalidOperationException("[BUG:FIXME] FLIP failed due to missing triangle");
            }

            if (tcx.IsDebugEnabled)
            {
                tcx.DTDebugContext.PrimaryTriangle = t;
                tcx.DTDebugContext.SecondaryTriangle = ot;
            } // TODO: remove

            bool inScanArea = TriangulationUtil.InScanArea(p, t.PointCCWFrom(p), t.PointCWFrom(p), op);
            if (inScanArea)
            {
                // Lets rotate shared edge one vertex CW
                RotateTrianglePair(t, p, ot, op);
                tcx.MapTriangleToNodes(t);
                tcx.MapTriangleToNodes(ot);

                if (p == eq && op == ep)
                {
                    if (eq == tcx.EdgeEvent.ConstrainedEdge.Q && ep == tcx.EdgeEvent.ConstrainedEdge.P)
                    {
                        if (tcx.IsDebugEnabled)
                        {
                            Console.WriteLine("[FLIP] - constrained edge done"); // TODO: remove
                        }
                        t.MarkConstrainedEdge(ep, eq);
                        ot.MarkConstrainedEdge(ep, eq);
                        Legalize(tcx, t);
                        Legalize(tcx, ot);
                    }
                    else
                    {
                        if (tcx.IsDebugEnabled)
                        {
                            Console.WriteLine("[FLIP] - subedge done"); // TODO: remove
                        }
                        // XXX: I think one of the triangles should be legalized here?
                    }
                }
                else
                {
                    if (tcx.IsDebugEnabled)
                    {
                        Console.WriteLine("[FLIP] - flipping and continuing with triangle still crossing edge"); // TODO: remove
                    }
                    Orientation o = TriangulationUtil.Orient2d(eq, op, ep);
                    t = NextFlipTriangle(tcx, o, t, ot, p, op);
                    FlipEdgeEvent(tcx, ep, eq, t, p);
                }
            }
            else
            {
                TriangulationPoint newP = null;
                if (NextFlipPoint(ep, eq, ot, op, out newP))
                {
                    FlipScanEdgeEvent(tcx, ep, eq, t, ot, newP);
                    EdgeEvent(tcx, ep, eq, t, p);
                }
            }
        }


        /// <summary>
        /// When we need to traverse from one triangle to the next we need 
        /// the point in current triangle that is the opposite point to the next
        /// triangle. 
        /// </summary>
        private static bool NextFlipPoint(TriangulationPoint ep, TriangulationPoint eq, DelaunayTriangle ot, TriangulationPoint op, out TriangulationPoint newP)
        {
            newP = null;
            Orientation o2d = TriangulationUtil.Orient2d(eq, op, ep);
            switch (o2d)
            {
                case Orientation.CW:
                    newP = ot.PointCCWFrom(op);
                    return true;
                case Orientation.CCW:
                    newP = ot.PointCWFrom(op);
                    return true;
                case Orientation.Collinear:
                    // TODO: implement support for point on constraint edge
                    //throw new PointOnEdgeException("Point on constrained edge not supported yet", eq, op, ep);
                    return false;
                default:
                    throw new NotImplementedException("Orientation not handled");
            }
        }


        /// <summary>
        /// After a flip we have two triangles and know that only one will still be
        /// intersecting the edge. So decide which to contiune with and legalize the other
        /// </summary>
        /// <param name="tcx"></param>
        /// <param name="o">should be the result of an TriangulationUtil.orient2d( eq, op, ep )</param>
        /// <param name="t">triangle 1</param>
        /// <param name="ot">triangle 2</param>
        /// <param name="p">a point shared by both triangles</param>
        /// <param name="op">another point shared by both triangles</param>
        /// <returns>returns the triangle still intersecting the edge</returns>
        private static DelaunayTriangle NextFlipTriangle(DTSweepContext tcx, Orientation o, DelaunayTriangle t, DelaunayTriangle ot, TriangulationPoint p, TriangulationPoint op)
        {
            int edgeIndex;
            if (o == Orientation.CCW)
            {
                // ot is not crossing edge after flip
                edgeIndex = ot.EdgeIndex(p, op);
                ot.EdgeIsDelaunay[edgeIndex] = true;
                Legalize(tcx, ot);
                ot.EdgeIsDelaunay.Clear();
                return t;
            }
            // t is not crossing edge after flip
            edgeIndex = t.EdgeIndex(p, op);
            t.EdgeIsDelaunay[edgeIndex] = true;
            Legalize(tcx, t);
            t.EdgeIsDelaunay.Clear();
            return ot;
        }


        /// <summary>
        /// Scan part of the FlipScan algorithm<br>
        /// When a triangle pair isn't flippable we will scan for the next 
        /// point that is inside the flip triangle scan area. When found 
        /// we generate a new flipEdgeEvent
        /// </summary>
        /// <param name="tcx"></param>
        /// <param name="ep">last point on the edge we are traversing</param>
        /// <param name="eq">first point on the edge we are traversing</param>
        /// <param name="flipTriangle">the current triangle sharing the point eq with edge</param>
        /// <param name="t"></param>
        /// <param name="p"></param>
        private static void FlipScanEdgeEvent(DTSweepContext tcx, TriangulationPoint ep, TriangulationPoint eq, DelaunayTriangle flipTriangle, DelaunayTriangle t, TriangulationPoint p)
        {
            DelaunayTriangle ot;
            TriangulationPoint op, newP;
            bool inScanArea;

            ot = t.NeighborAcrossFrom(p);
            op = ot.OppositePoint(t, p);

            if (ot == null)
            {
                // If we want to integrate the fillEdgeEvent do it here
                // With current implementation we should never get here
                throw new Exception("[BUG:FIXME] FLIP failed due to missing triangle");
            }

            if (tcx.IsDebugEnabled)
            {
                Console.WriteLine("[FLIP:SCAN] - scan next point"); // TODO: remove
                tcx.DTDebugContext.PrimaryTriangle = t;
                tcx.DTDebugContext.SecondaryTriangle = ot;
            }

            inScanArea = TriangulationUtil.InScanArea(eq, flipTriangle.PointCCWFrom(eq), flipTriangle.PointCWFrom(eq), op);
            if (inScanArea)
            {
                // flip with new edge op->eq
                FlipEdgeEvent(tcx, eq, op, ot, op);
                // TODO: Actually I just figured out that it should be possible to 
                //       improve this by getting the next ot and op before the the above 
                //       flip and continue the flipScanEdgeEvent here
                // set new ot and op here and loop back to inScanArea test
                // also need to set a new flipTriangle first
                // Turns out at first glance that this is somewhat complicated
                // so it will have to wait.
            }
            else
            {
                if (NextFlipPoint(ep, eq, ot, op, out newP))
                {
                    FlipScanEdgeEvent(tcx, ep, eq, flipTriangle, ot, newP);
                }
                //newP = NextFlipPoint(ep, eq, ot, op);
            }
        }


        /// <summary>
        /// Fills holes in the Advancing Front
        /// </summary>
        private static void FillAdvancingFront(DTSweepContext tcx, AdvancingFrontNode n)
        {
            AdvancingFrontNode node;
            double angle;

            // Fill right holes
            node = n.Next;
            while (node.HasNext)
            {
                angle = HoleAngle(node);
                if (angle > PI_div2 || angle < -PI_div2)
                {
                    break;
                }
                Fill(tcx, node);
                node = node.Next;
            }

            // Fill left holes
            node = n.Prev;
            while (node.HasPrev)
            {
                angle = HoleAngle(node);
                if (angle > PI_div2 || angle < -PI_div2)
                {
                    break;
                }
                Fill(tcx, node);
                node = node.Prev;
            }

            // Fill right basins
            if (n.HasNext && n.Next.HasNext)
            {
                angle = BasinAngle(n);
                if (angle < PI_3div4)
                {
                    FillBasin(tcx, n);
                }
            }
        }


        /// <summary>
        /// Fills a basin that has formed on the Advancing Front to the right
        /// of given node.<br>
        /// First we decide a left,bottom and right node that forms the 
        /// boundaries of the basin. Then we do a reqursive fill.
        /// </summary>
        /// <param name="tcx"></param>
        /// <param name="node">starting node, this or next node will be left node</param>
        private static void FillBasin(DTSweepContext tcx, AdvancingFrontNode node)
        {
            if (TriangulationUtil.Orient2d(node.Point, node.Next.Point, node.Next.Next.Point) == Orientation.CCW)
            {
                // tcx.basin.leftNode = node.next.next;
                tcx.Basin.leftNode = node;
            }
            else
            {
                tcx.Basin.leftNode = node.Next;
            }

            // Find the bottom and right node
            tcx.Basin.bottomNode = tcx.Basin.leftNode;
            while (tcx.Basin.bottomNode.HasNext && tcx.Basin.bottomNode.Point.Y >= tcx.Basin.bottomNode.Next.Point.Y)
            {
                tcx.Basin.bottomNode = tcx.Basin.bottomNode.Next;
            }

            if (tcx.Basin.bottomNode == tcx.Basin.leftNode)
            {
                return; // No valid basin
            }

            tcx.Basin.rightNode = tcx.Basin.bottomNode;
            while (tcx.Basin.rightNode.HasNext && tcx.Basin.rightNode.Point.Y < tcx.Basin.rightNode.Next.Point.Y)
            {
                tcx.Basin.rightNode = tcx.Basin.rightNode.Next;
            }

            if (tcx.Basin.rightNode == tcx.Basin.bottomNode)
            {
                return; // No valid basins
            }

            tcx.Basin.width = tcx.Basin.rightNode.Point.X - tcx.Basin.leftNode.Point.X;
            tcx.Basin.leftHighest = tcx.Basin.leftNode.Point.Y > tcx.Basin.rightNode.Point.Y;

            FillBasinReq(tcx, tcx.Basin.bottomNode);
        }


        /// <summary>
        /// Recursive algorithm to fill a Basin with triangles
        /// </summary>
        private static void FillBasinReq(DTSweepContext tcx, AdvancingFrontNode node)
        {
            if (IsShallow(tcx, node))
            {
                return; // if shallow stop filling
            }

            Fill(tcx, node);
            if (node.Prev == tcx.Basin.leftNode && node.Next == tcx.Basin.rightNode)
            {
                return;
            }
            else if (node.Prev == tcx.Basin.leftNode)
            {
                Orientation o = TriangulationUtil.Orient2d(node.Point, node.Next.Point, node.Next.Next.Point);
                if (o == Orientation.CW)
                {
                    return;
                }
                node = node.Next;
            }
            else if (node.Next == tcx.Basin.rightNode)
            {
                Orientation o = TriangulationUtil.Orient2d(node.Point, node.Prev.Point, node.Prev.Prev.Point);
                if (o == Orientation.CCW)
                {
                    return;
                }
                node = node.Prev;
            }
            else
            {
                // Continue with the neighbor node with lowest Y value
                if (node.Prev.Point.Y < node.Next.Point.Y)
                {
                    node = node.Prev;
                }
                else
                {
                    node = node.Next;
                }
            }
            FillBasinReq(tcx, node);
        }


        private static bool IsShallow(DTSweepContext tcx, AdvancingFrontNode node)
        {
            double height;

            if (tcx.Basin.leftHighest)
            {
                height = tcx.Basin.leftNode.Point.Y - node.Point.Y;
            }
            else
            {
                height = tcx.Basin.rightNode.Point.Y - node.Point.Y;
            }
            if (tcx.Basin.width > height)
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// ???
        /// </summary>
        /// <param name="node">middle node</param>
        /// <returns>the angle between 3 front nodes</returns>
        private static double HoleAngle(AdvancingFrontNode node)
        {
            // XXX: do we really need a signed angle for holeAngle?
            //      could possible save some cycles here
            /* Complex plane
             * ab = cosA +i*sinA
             * ab = (ax + ay*i)(bx + by*i) = (ax*bx + ay*by) + i(ax*by-ay*bx)
             * atan2(y,x) computes the principal value of the argument function
             * applied to the complex number x+iy
             * Where x = ax*bx + ay*by
             *       y = ax*by - ay*bx
             */
            double px = node.Point.X;
            double py = node.Point.Y;
            double ax = node.Next.Point.X - px;
            double ay = node.Next.Point.Y - py;
            double bx = node.Prev.Point.X - px;
            double by = node.Prev.Point.Y - py;
            return Math.Atan2((ax * by) - (ay * bx), (ax * bx) + (ay * by));
        }


        /// <summary>
        /// The basin angle is decided against the horizontal line [1,0]
        /// </summary>
        private static double BasinAngle(AdvancingFrontNode node)
        {
            double ax = node.Point.X - node.Next.Next.Point.X;
            double ay = node.Point.Y - node.Next.Next.Point.Y;
            return Math.Atan2(ay, ax);
        }


        /// <summary>
        /// Adds a triangle to the advancing front to fill a hole.
        /// </summary>
        /// <param name="tcx"></param>
        /// <param name="node">middle node, that is the bottom of the hole</param>
        private static void Fill(DTSweepContext tcx, AdvancingFrontNode node)
        {
            DelaunayTriangle triangle = new DelaunayTriangle(node.Prev.Point, node.Point, node.Next.Point);
            // TODO: should copy the cEdge value from neighbor triangles
            //       for now cEdge values are copied during the legalize 
            triangle.MarkNeighbor(node.Prev.Triangle);
            triangle.MarkNeighbor(node.Triangle);
            tcx.Triangles.Add(triangle);

            // Update the advancing front
            node.Prev.Next = node.Next;
            node.Next.Prev = node.Prev;
            tcx.RemoveNode(node);

            // If it was legalized the triangle has already been mapped
            if (!Legalize(tcx, triangle))
            {
                tcx.MapTriangleToNodes(triangle);
            }
        }


        /// <summary>
        /// Returns true if triangle was legalized
        /// </summary>
        private static bool Legalize(DTSweepContext tcx, DelaunayTriangle t)
        {
            // To legalize a triangle we start by finding if any of the three edges
            // violate the Delaunay condition
            for (int i = 0; i < 3; i++)
            {
                // TODO: fix so that cEdge is always valid when creating new triangles then we can check it here
                //       instead of below with ot
                if (t.EdgeIsDelaunay[i])
                {
                    continue;
                }

                DelaunayTriangle ot = t.Neighbors[i];
                if (ot == null)
                {
                    continue;
                }

                TriangulationPoint p = t.Points[i];
                TriangulationPoint op = ot.OppositePoint(t, p);
                int oi = ot.IndexOf(op);
                // If this is a Constrained Edge or a Delaunay Edge(only during recursive legalization)
                // then we should not try to legalize
                if (ot.EdgeIsConstrained[oi] || ot.EdgeIsDelaunay[oi])
                {
                    t.SetConstrainedEdgeAcross(p, ot.EdgeIsConstrained[oi]); // XXX: have no good way of setting this property when creating new triangles so lets set it here
                    continue;
                }

                if (!TriangulationUtil.SmartIncircle(p, t.PointCCWFrom(p), t.PointCWFrom(p), op))
                {
                    continue;
                }

                // Lets mark this shared edge as Delaunay 
                t.EdgeIsDelaunay[i] = true;
                ot.EdgeIsDelaunay[oi] = true;

                // Lets rotate shared edge one vertex CW to legalize it
                RotateTrianglePair(t, p, ot, op);

                // We now got one valid Delaunay Edge shared by two triangles
                // This gives us 4 new edges to check for Delaunay

                // Make sure that triangle to node mapping is done only one time for a specific triangle
                if (!Legalize(tcx, t))
                {
                    tcx.MapTriangleToNodes(t);
                }
                if (!Legalize(tcx, ot))
                {
                    tcx.MapTriangleToNodes(ot);
                }

                // Reset the Delaunay edges, since they only are valid Delaunay edges
                // until we add a new triangle or point.
                // XXX: need to think about this. Can these edges be tried after we 
                //      return to previous recursive level?
                t.EdgeIsDelaunay[i] = false;
                ot.EdgeIsDelaunay[oi] = false;

                // If triangle have been legalized no need to check the other edges since
                // the recursive legalization will handles those so we can end here.
                return true;
            }
            return false;
        }


        /// <summary>
        /// Rotates a triangle pair one vertex CW
        ///       n2                    n2
        ///  P +-----+             P +-----+
        ///    | t  /|               |\  t |  
        ///    |   / |               | \   |
        ///  n1|  /  |n3           n1|  \  |n3
        ///    | /   |    after CW   |   \ |
        ///    |/ oT |               | oT \|
        ///    +-----+ oP            +-----+
        ///       n4                    n4
        /// </summary>
        private static void RotateTrianglePair(DelaunayTriangle t, TriangulationPoint p, DelaunayTriangle ot, TriangulationPoint op)
        {
            DelaunayTriangle n1, n2, n3, n4;
            n1 = t.NeighborCCWFrom(p);
            n2 = t.NeighborCWFrom(p);
            n3 = ot.NeighborCCWFrom(op);
            n4 = ot.NeighborCWFrom(op);

            bool ce1, ce2, ce3, ce4;
            ce1 = t.GetConstrainedEdgeCCW(p);
            ce2 = t.GetConstrainedEdgeCW(p);
            ce3 = ot.GetConstrainedEdgeCCW(op);
            ce4 = ot.GetConstrainedEdgeCW(op);

            bool de1, de2, de3, de4;
            de1 = t.GetDelaunayEdgeCCW(p);
            de2 = t.GetDelaunayEdgeCW(p);
            de3 = ot.GetDelaunayEdgeCCW(op);
            de4 = ot.GetDelaunayEdgeCW(op);

            t.Legalize(p, op);
            ot.Legalize(op, p);

            // Remap dEdge
            ot.SetDelaunayEdgeCCW(p, de1);
            t.SetDelaunayEdgeCW(p, de2);
            t.SetDelaunayEdgeCCW(op, de3);
            ot.SetDelaunayEdgeCW(op, de4);

            // Remap cEdge
            ot.SetConstrainedEdgeCCW(p, ce1);
            t.SetConstrainedEdgeCW(p, ce2);
            t.SetConstrainedEdgeCCW(op, ce3);
            ot.SetConstrainedEdgeCW(op, ce4);

            // Remap neighbors
            // XXX: might optimize the markNeighbor by keeping track of
            //      what side should be assigned to what neighbor after the 
            //      rotation. Now mark neighbor does lots of testing to find 
            //      the right side.
            t.Neighbors.Clear();
            ot.Neighbors.Clear();
            if (n1 != null)
            {
                ot.MarkNeighbor(n1);
            }
            if (n2 != null)
            {
                t.MarkNeighbor(n2);
            }
            if (n3 != null)
            {
                t.MarkNeighbor(n3);
            }
            if (n4 != null)
            {
                ot.MarkNeighbor(n4);
            }
            t.MarkNeighbor(ot);
        }
    }
}

// ----------------------------------------------------------------------
// DTSweepBasin.cs

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

namespace Poly2Tri
{
    public class DTSweepBasin
    {
        public AdvancingFrontNode leftNode;
        public AdvancingFrontNode bottomNode;
        public AdvancingFrontNode rightNode;
        public double width;
        public bool leftHighest;
    }
}

// ----------------------------------------------------------------------
// DTSweepConstraint.cs

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

// using System;
//using System.Collections.Generic;
// using System.Diagnostics;
//using System.Linq;


namespace Poly2Tri
{
    public class DTSweepConstraint : TriangulationConstraint
    {
        /// <summary>
        /// Give two points in any order. Will always be ordered so
        /// that q.y > p.y and q.x > p.x if same y value 
        /// </summary>
        public DTSweepConstraint(TriangulationPoint p1, TriangulationPoint p2)
            : base(p1, p2)
        {
            Q.AddEdge(this);
        }
    }
}

// ----------------------------------------------------------------------
// DTSweepContext.cs

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

namespace Poly2Tri
{
    /**
     * 
     * @author Thomas Åhlén, thahlen@gmail.com
     *
     */
    public class DTSweepContext : TriangulationContext
    {
        // Inital triangle factor, seed triangle will extend 30% of 
        // PointSet width to both left and right.
        private readonly float ALPHA = 0.3f;

        public AdvancingFront Front;
        public TriangulationPoint Head { get; set; }
        public TriangulationPoint Tail { get; set; }

        public DTSweepBasin Basin = new DTSweepBasin();
        public DTSweepEdgeEvent EdgeEvent = new DTSweepEdgeEvent();

        private DTSweepPointComparator _comparator = new DTSweepPointComparator();

        public override TriangulationAlgorithm Algorithm { get { return TriangulationAlgorithm.DTSweep; } }


        public DTSweepContext()
        {
            Clear();
        }


        public override bool IsDebugEnabled
        {
            get
            {
                return base.IsDebugEnabled;
            }
            protected set
            {
                if (value && DebugContext == null)
                {
                    DebugContext = new DTSweepDebugContext(this);
                }
                base.IsDebugEnabled = value;
            }
        }


        public void RemoveFromList(DelaunayTriangle triangle)
        {
            Triangles.Remove(triangle);
            // TODO: remove all neighbor pointers to this triangle
            //        for( int i=0; i<3; i++ )
            //        {
            //            if( triangle.neighbors[i] != null )
            //            {
            //                triangle.neighbors[i].clearNeighbor( triangle );
            //            }
            //        }
            //        triangle.clearNeighbors();
        }


        public void MeshClean(DelaunayTriangle triangle)
        {
            MeshCleanReq(triangle);
        }


        private void MeshCleanReq(DelaunayTriangle triangle)
        {
            if (triangle != null && !triangle.IsInterior)
            {
                triangle.IsInterior = true;
                Triangulatable.AddTriangle(triangle);

                for (int i = 0; i < 3; i++)
                {
                    if (!triangle.EdgeIsConstrained[i])
                    {
                        MeshCleanReq(triangle.Neighbors[i]);
                    }
                }
            }
        }


        public override void Clear()
        {
            base.Clear();
            Triangles.Clear();
        }


        public void AddNode(AdvancingFrontNode node)
        {
            //        Console.WriteLine( "add:" + node.key + ":" + System.identityHashCode(node.key));
            //        m_nodeTree.put( node.getKey(), node );
            Front.AddNode(node);
        }


        public void RemoveNode(AdvancingFrontNode node)
        {
            //        Console.WriteLine( "remove:" + node.key + ":" + System.identityHashCode(node.key));
            //        m_nodeTree.delete( node.getKey() );
            Front.RemoveNode(node);
        }


        public AdvancingFrontNode LocateNode(TriangulationPoint point)
        {
            return Front.LocateNode(point);
        }


        public void CreateAdvancingFront()
        {
            AdvancingFrontNode head, tail, middle;
            // Initial triangle
            DelaunayTriangle iTriangle = new DelaunayTriangle(Points[0], Tail, Head);
            Triangles.Add(iTriangle);

            head = new AdvancingFrontNode(iTriangle.Points[1]);
            head.Triangle = iTriangle;
            middle = new AdvancingFrontNode(iTriangle.Points[0]);
            middle.Triangle = iTriangle;
            tail = new AdvancingFrontNode(iTriangle.Points[2]);

            Front = new AdvancingFront(head, tail);
            Front.AddNode(middle);

            // TODO: I think it would be more intuitive if head is middles next and not previous
            //       so swap head and tail
            Front.Head.Next = middle;
            middle.Next = Front.Tail;
            middle.Prev = Front.Head;
            Front.Tail.Prev = middle;
        }


        /// <summary>
        /// Try to map a node to all sides of this triangle that don't have 
        /// a neighbor.
        /// </summary>
        public void MapTriangleToNodes(DelaunayTriangle t)
        {
            for (int i = 0; i < 3; i++)
            {
                if (t.Neighbors[i] == null)
                {
                    AdvancingFrontNode n = Front.LocatePoint(t.PointCWFrom(t.Points[i]));
                    if (n != null)
                    {
                        n.Triangle = t;
                    }
                }
            }
        }


        public override void PrepareTriangulation(ITriangulatable t)
        {
            base.PrepareTriangulation(t);

            double xmax, xmin;
            double ymax, ymin;

            xmax = xmin = Points[0].X;
            ymax = ymin = Points[0].Y;

            // Calculate bounds. Should be combined with the sorting
            foreach (TriangulationPoint p in Points)
            {
                if (p.X > xmax)
                {
                    xmax = p.X;
                }
                if (p.X < xmin)
                {
                    xmin = p.X;
                }
                if (p.Y > ymax)
                {
                    ymax = p.Y;
                }
                if (p.Y < ymin)
                {
                    ymin = p.Y;
                }
            }

            double deltaX = ALPHA * (xmax - xmin);
            double deltaY = ALPHA * (ymax - ymin);
            TriangulationPoint p1 = new TriangulationPoint(xmax + deltaX, ymin - deltaY);
            TriangulationPoint p2 = new TriangulationPoint(xmin - deltaX, ymin - deltaY);

            Head = p1;
            Tail = p2;

            //        long time = System.nanoTime();
            // Sort the points along y-axis
            Points.Sort(_comparator);
            //        logger.info( "Triangulation setup [{}ms]", ( System.nanoTime() - time ) / 1e6 );
        }


        public void FinalizeTriangulation()
        {
            Triangulatable.AddTriangles(Triangles);
            Triangles.Clear();
        }


        public override TriangulationConstraint NewConstraint(TriangulationPoint a, TriangulationPoint b)
        {
            return new DTSweepConstraint(a, b);
        }

    }
}

// ----------------------------------------------------------------------
// DTSweepDebugContext.cs

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

namespace Poly2Tri
{
    public class DTSweepDebugContext : TriangulationDebugContext
    {
        /*
         * Fields used for visual representation of current triangulation
         */

        public DelaunayTriangle PrimaryTriangle { get { return _primaryTriangle; } set { _primaryTriangle = value; _tcx.Update("set PrimaryTriangle"); } }
        public DelaunayTriangle SecondaryTriangle { get { return _secondaryTriangle; } set { _secondaryTriangle = value; _tcx.Update("set SecondaryTriangle"); } }
        public TriangulationPoint ActivePoint { get { return _activePoint; } set { _activePoint = value; _tcx.Update("set ActivePoint"); } }
        public AdvancingFrontNode ActiveNode { get { return _activeNode; } set { _activeNode = value; _tcx.Update("set ActiveNode"); } }
        public DTSweepConstraint ActiveConstraint { get { return _activeConstraint; } set { _activeConstraint = value; _tcx.Update("set ActiveConstraint"); } }

        public DTSweepDebugContext(DTSweepContext tcx) : base(tcx) { }

        public bool IsDebugContext { get { return true; } }

        public override void Clear()
        {
            PrimaryTriangle = null;
            SecondaryTriangle = null;
            ActivePoint = null;
            ActiveNode = null;
            ActiveConstraint = null;
        }

        private DelaunayTriangle _primaryTriangle;
        private DelaunayTriangle _secondaryTriangle;
        private TriangulationPoint _activePoint;
        private AdvancingFrontNode _activeNode;
        private DTSweepConstraint _activeConstraint;
    }
}

// ----------------------------------------------------------------------
// DTSweepEdgeEvent.cs

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

/// Changes from the Java version
///   Turned DTSweepEdgeEvent into a value type

namespace Poly2Tri
{
    public class DTSweepEdgeEvent
    {
        public DTSweepConstraint ConstrainedEdge;
        public bool Right;
    }
}

// ----------------------------------------------------------------------
// DTSweepPointComparator.cs

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

// using System.Collections.Generic;

namespace Poly2Tri
{
    public class DTSweepPointComparator : IComparer<TriangulationPoint>
    {
        public int Compare(TriangulationPoint p1, TriangulationPoint p2)
        {
            if (p1.Y < p2.Y)
            {
                return -1;
            }
            else if (p1.Y > p2.Y)
            {
                return 1;
            }
            else
            {
                if (p1.X < p2.X)
                {
                    return -1;
                }
                else if (p1.X > p2.X)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}

// ----------------------------------------------------------------------
// PointOnEdgeException.cs

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

// using System;

namespace Poly2Tri
{
    public class PointOnEdgeException : NotImplementedException
    {
        public readonly TriangulationPoint A, B, C;

        public PointOnEdgeException(string message, TriangulationPoint a, TriangulationPoint b, TriangulationPoint c)
            : base(message)
        {
            A = a;
            B = b;
            C = c;
        }
    }
}

// ----------------------------------------------------------------------
// Contour.cs

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

// using System;
// using System.Collections.Generic;
// using System.Text;


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

// ----------------------------------------------------------------------
// Polygon.cs

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

/// Changes from the Java version
///   Polygon constructors sprused up, checks for 3+ polys
///   Naming of everything
///   getTriangulationMode() -> TriangulationMode { get; }
///   Exceptions replaced
/// Future possibilities
///   We have a lot of Add/Clear methods -- we may prefer to just expose the container
///   Some self-explanatory methods may deserve commenting anyways

// using System;
// using System.Collections.Generic;
// using System.Linq;


namespace Poly2Tri
{
    public class Polygon : Point2DList, ITriangulatable, IEnumerable<TriangulationPoint>, IList<TriangulationPoint>
    {
        // ITriangulatable Implementation
        protected Dictionary<uint, TriangulationPoint> mPointMap = new Dictionary<uint, TriangulationPoint>();
        public IList<TriangulationPoint> Points { get { return this; } }
        protected List<DelaunayTriangle> mTriangles;
        public IList<DelaunayTriangle> Triangles { get { return mTriangles; } }
        public TriangulationMode TriangulationMode { get { return TriangulationMode.Polygon; } }
        public string FileName { get; set; }
        public bool DisplayFlipX { get; set; }
        public bool DisplayFlipY { get; set; }
        public float DisplayRotate { get; set; }
        private double mPrecision = TriangulationPoint.kVertexCodeDefaultPrecision;
        public double Precision { get { return mPrecision; } set { mPrecision = value; } }
        public double MinX { get { return mBoundingBox.MinX; } }
        public double MaxX { get { return mBoundingBox.MaxX; } }
        public double MinY { get { return mBoundingBox.MinY; } }
        public double MaxY { get { return mBoundingBox.MaxY; } }
        public Rect2D Bounds { get { return mBoundingBox; } }

        // Point2DList overrides
        public new TriangulationPoint this[int index]
        {
            get { return mPoints[index] as TriangulationPoint; }
            set { mPoints[index] = value; }
        }

        // Polygon Implementation
        protected List<Polygon> mHoles;
        public IList<Polygon> Holes { get { return mHoles; } }
        protected List<TriangulationPoint> mSteinerPoints;
        protected PolygonPoint _last;



        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points</param>
        public Polygon(IList<PolygonPoint> points)
        {
            if (points.Count < 3)
            {
                throw new ArgumentException("List has fewer than 3 points", "points");
            }

            AddRange(points, WindingOrderType.Unknown);
        }


        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points.</param>
        public Polygon(IEnumerable<PolygonPoint> points) 
            : this((points as IList<PolygonPoint>) ?? points.ToArray()) 
        {}


        /// <summary>
        /// Create a polygon from a list of at least 3 points with no duplicates.
        /// </summary>
        /// <param name="points">A list of unique points.</param>
        public Polygon(params PolygonPoint[] points)
            : this((IList<PolygonPoint>)points)
        {}


        IEnumerator<TriangulationPoint> IEnumerable<TriangulationPoint>.GetEnumerator()
        {
            return new TriangulationPointEnumerator(mPoints);
        }


        public int IndexOf(TriangulationPoint p)
        {
            return mPoints.IndexOf(p);
        }


        public override void Add(Point2D p)
        {
            Add(p, -1, true);
        }


        public void Add(TriangulationPoint p)
        {
            Add(p, -1, true);
        }


        public void Add(PolygonPoint p)
        {
            Add(p, -1, true);
        }


        protected override void Add(Point2D p, int idx, bool bCalcWindingOrderAndEpsilon)
        {
            TriangulationPoint pt = p as TriangulationPoint;
            if (pt == null)
            {
                // we only store TriangulationPoints and PolygonPoints in this class
                return;
            }

            // do not insert duplicate points
            if (mPointMap.ContainsKey(pt.VertexCode))
            {
                return;
            }
            mPointMap.Add(pt.VertexCode, pt);

            base.Add(p, idx, bCalcWindingOrderAndEpsilon);

            PolygonPoint pp = p as PolygonPoint;
            if (pp != null)
            {
                pp.Previous = _last;
                if (_last != null)
                {
                    pp.Next = _last.Next;
                    _last.Next = pp;
                }
                _last = pp;
            }

            return;
        }


        public void AddRange(IList<PolygonPoint> points, Point2DList.WindingOrderType windingOrder)
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
            return base.Remove(p);
        }


        /// <summary>
        /// Removes a point from the polygon.  Note this can be a somewhat expensive operation
        /// as it must recalculate the bounding area from scratch.
        /// </summary>
        /// <param name="p"></param>
        public void RemovePoint(PolygonPoint p)
        {
            PolygonPoint next, prev;

            next = p.Next;
            prev = p.Previous;
            prev.Next = next;
            next.Previous = prev;
            mPoints.Remove(p);

            mBoundingBox.Clear();
            foreach (PolygonPoint tmp in mPoints)
            {
                mBoundingBox.AddPoint(tmp);
            }
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


        public void AddSteinerPoint(TriangulationPoint point)
        {
            if (mSteinerPoints == null)
            {
                mSteinerPoints = new List<TriangulationPoint>();
            }
            mSteinerPoints.Add(point);
        }


        public void AddSteinerPoints(List<TriangulationPoint> points)
        {
            if (mSteinerPoints == null)
            {
                mSteinerPoints = new List<TriangulationPoint>();
            }
            mSteinerPoints.AddRange(points);
        }


        public void ClearSteinerPoints()
        {
            if (mSteinerPoints != null)
            {
                mSteinerPoints.Clear();
            }
        }


        /// <summary>
        /// Add a hole to the polygon.
        /// </summary>
        /// <param name="poly">A subtraction polygon fully contained inside this polygon.</param>
        public void AddHole(Polygon poly)
        {
            if (mHoles == null)
            {
                mHoles = new List<Polygon>();
            }
            mHoles.Add(poly);
            // XXX: tests could be made here to be sure it is fully inside
            //        addSubtraction( poly.getPoints() );
        }


        public void AddTriangle(DelaunayTriangle t)
        {
            mTriangles.Add(t);
        }


        public void AddTriangles(IEnumerable<DelaunayTriangle> list)
        {
            mTriangles.AddRange(list);
        }

        
        public void ClearTriangles()
        {
            if (mTriangles != null)
            {
                mTriangles.Clear();
            }
        }


        public bool IsPointInside(TriangulationPoint p)
        {
            return PolygonUtil.PointInPolygon2D(this, p);
        }


        /// <summary>
        /// Creates constraints and populates the context with points
        /// </summary>
        /// <param name="tcx">The context</param>
        public void Prepare(TriangulationContext tcx)
        {
            if (mTriangles == null)
            {
                mTriangles = new List<DelaunayTriangle>(mPoints.Count);
            }
            else
            {
                mTriangles.Clear();
            }

            // Outer constraints
            for (int i = 0; i < mPoints.Count - 1; i++)
            {
                //tcx.NewConstraint(mPoints[i], mPoints[i + 1]);
                tcx.NewConstraint(this[i], this[i + 1]);
            }
            tcx.NewConstraint(this[0], this[Count - 1]);
            tcx.Points.AddRange(this);

            // Hole constraints
            if (mHoles != null)
            {
                foreach (Polygon p in mHoles)
                {
                    for (int i = 0; i < p.mPoints.Count - 1; i++)
                    {
                        tcx.NewConstraint(p[i], p[i + 1]);
                    }
                    tcx.NewConstraint(p[0], p[p.Count - 1]);
                    tcx.Points.AddRange(p);
                }
            }

            if (mSteinerPoints != null)
            {
                tcx.Points.AddRange(mSteinerPoints);
            }
        }
    }
}

// ----------------------------------------------------------------------
// PolygonPoint.cs

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

/// Changes from the Java version
///   Replaced get/set Next/Previous with attributes
/// Future possibilities
///   Documentation!

namespace Poly2Tri
{
    public class PolygonPoint : TriangulationPoint
    {
        public PolygonPoint(double x, double y) : base(x, y) { }

        public PolygonPoint Next { get; set; }
        public PolygonPoint Previous { get; set; }

        public static Point2D ToBasePoint(PolygonPoint p)
        {
            return (Point2D)p;
        }

        public static TriangulationPoint ToTriangulationPoint(PolygonPoint p)
        {
            return (TriangulationPoint)p;
        }
    }
}

// ----------------------------------------------------------------------
// PolygonSet.cs

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

/// Changes from the Java version
///   Replaced getPolygons with attribute
/// Future possibilities
///   Replace Add(Polygon) with exposed container?
///   Replace entire class with HashSet<Polygon> ?

// using System.Collections.Generic;

namespace Poly2Tri
{
    public class PolygonSet
    {
        protected List<Polygon> _polygons = new List<Polygon>();

        public PolygonSet() { }

        public PolygonSet(Polygon poly)
        {
            _polygons.Add(poly);
        }

        public void Add(Polygon p)
        {
            _polygons.Add(p);
        }

        public IEnumerable<Polygon> Polygons { get { return _polygons; } }
    }
}

// ----------------------------------------------------------------------
// PolygonUtil.cs

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


/*
 * The Following notice applies to the Method SplitComplexPolygon and the 
 * class SplitComplexPolygonNode.   Both are altered only enough to convert to C#
 * and take advantage of some of C#'s language features.   Any errors
 * are thus mine from the conversion and not Eric's.
 * 
 * Copyright (c) 2007 Eric Jordan
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty.  In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 * */


/*
 * Portions of the following code (notably: the methods PolygonUnion, 
 * PolygonSubtract, PolygonIntersect, PolygonOperationContext.Init,
 * PolygonOperationContext.VerticesIntersect,
 * PolygonOperationContext.PointInPolygonAngle, and
 * PolygonOperationContext.VectorAngle are from the Farseer Physics Engine 3.0
 * and are covered under the Microsoft Permissive License V1.1
 * (http://farseerphysics.codeplex.com/license)
 * 
 * Microsoft Permissive License (Ms-PL)
 * 
 * This license governs use of the accompanying software. If you use the 
 * software, you accept this license. If you do not accept the license, do not
 * use the software.
 * 
 * 1. Definitions
 * 
 * The terms "reproduce," "reproduction," "derivative works," and
 * "distribution" have the same meaning here as under U.S. copyright law.
 * 
 * A "contribution" is the original software, or any additions or changes to
 * the software.
 * 
 * A "contributor" is any person that distributes its contribution under this 
 * license.
 *
 * "Licensed patents" are a contributor's patent claims that read directly on
 * its contribution.
 * 
 * 2. Grant of Rights
 * 
 * (A) Copyright Grant- Subject to the terms of this license, including the
 * license conditions and limitations in section 3, each contributor grants
 * you a non-exclusive, worldwide, royalty-free copyright license to reproduce
 * its contribution, prepare derivative works of its contribution, and
 * distribute its contribution or any derivative works that you create.
 * 
 * (B) Patent Grant- Subject to the terms of this license, including the
 * license conditions and limitations in section 3, each contributor grants
 * you a non-exclusive, worldwide, royalty-free license under its licensed
 * patents to make, have made, use, sell, offer for sale, import, and/or
 * otherwise dispose of its contribution in the software or derivative works
 * of the contribution in the software.
 * 
 * 3. Conditions and Limitations
 * 
 * (A) No Trademark License- This license does not grant you rights to use
 * any contributors' name, logo, or trademarks.
 * 
 * (B) If you bring a patent claim against any contributor over patents that
 * you claim are infringed by the software, your patent license from such
 * contributor to the software ends automatically.
 * 
 * (C) If you distribute any portion of the software, you must retain all
 * copyright, patent, trademark, and attribution notices that are present
 * in the software.
 * 
 * (D) If you distribute any portion of the software in source code form, you
 * may do so only under this license by including a complete copy of this
 * license with your distribution. If you distribute any portion of the
 * software in compiled or object code form, you may only do so under a
 * license that complies with this license.
 * 
 * (E) The software is licensed "as-is." You bear the risk of using it. The
 * contributors give no express warranties, guarantees or conditions. You may
 * have additional consumer rights under your local laws which this license
 * cannot change. To the extent permitted under your local laws, the
 * contributors exclude the implied warranties of merchantability, fitness for
 * a particular purpose and non-infringement. 
 */

// using System;
// using System.Collections.Generic;
// using System.Text;


namespace Poly2Tri
{
    public class PolygonUtil
    {
        public enum PolyUnionError
        {
            None,
            NoIntersections,
            Poly1InsidePoly2,
            InfiniteLoop
        }

        [Flags]
        public enum PolyOperation : uint
        {
            None = 0,
            Union = 1 << 0,
            Intersect = 1 << 1,
            Subtract = 1 << 2,
        }


        public static Point2DList.WindingOrderType CalculateWindingOrder(IList<Point2D> l)
        {
            double area = 0.0;
            for (int i = 0; i < l.Count; i++)
            {
                int j = (i + 1) % l.Count;
                area += l[i].X * l[j].Y;
                area -= l[i].Y * l[j].X;
            }
            area /= 2.0f;

            // the sign of the 'area' of the polygon is all we are interested in.
            if (area < 0.0)
            {
                return Point2DList.WindingOrderType.CW;
            }
            else if (area > 0.0)
            {
                return Point2DList.WindingOrderType.CCW;
            }

            // error condition - not even verts to calculate, non-simple poly, etc.
            return Point2DList.WindingOrderType.Unknown;
        }


        /// <summary>
        /// Check if the polys are similar to within a tolerance (Doesn't include reflections,
        /// but allows for the points to be numbered differently, but not reversed).
        /// </summary>
        /// <param name="poly1"></param>
        /// <param name="poly2"></param>
        /// <returns></returns>
        public static bool PolygonsAreSame2D(IList<Point2D> poly1, IList<Point2D> poly2)
        {
            int numVerts1 = poly1.Count;
            int numVerts2 = poly2.Count;

            if (numVerts1 != numVerts2)
            {
                return false;
            }
            const double kEpsilon = 0.01;
            const double kEpsilonSq = kEpsilon * kEpsilon;

            // Bounds the same to within tolerance, are there polys the same?
            Point2D vdelta = new Point2D(0.0, 0.0);
            for (int k = 0; k < numVerts2; ++k)
            {
                // Look for a match in verts2 to the first vertex in verts1
                vdelta.Set(poly1[0]);
                vdelta.Subtract(poly2[k]);

                if (vdelta.MagnitudeSquared() < kEpsilonSq)
                {
                    // Found match to the first point, now check the other points continuing round
                    // if the points don't match in the first direction we check, then it's possible
                    // that the polygons have a different winding order, so we check going round 
                    // the opposite way as well
                    int matchedVertIndex = k;
                    bool bReverseSearch = false;
                    while (true)
                    {
                        bool bMatchFound = true;
                        for (int i = 1; i < numVerts1; ++i)
                        {
                            if (!bReverseSearch)
                            {
                                ++k;
                            }
                            else
                            {
                                --k;
                                if (k < 0)
                                {
                                    k = numVerts2 - 1;
                                }
                            }

                            vdelta.Set(poly1[i]);
                            vdelta.Subtract(poly2[k % numVerts2]);
                            if (vdelta.MagnitudeSquared() >= kEpsilonSq)
                            {
                                if (bReverseSearch)
                                {
                                    // didn't find a match going in either direction, so the polygons are not the same
                                    return false;
                                }
                                else
                                {
                                    // mismatch in the first direction checked, so check the other direction.
                                    k = matchedVertIndex;
                                    bReverseSearch = true;
                                    bMatchFound = false;
                                    break;
                                }
                            }
                        }

                        if (bMatchFound)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        public static bool PointInPolygon2D(IList<Point2D> polygon, Point2D p)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            int numVerts = polygon.Count;
            Point2D p0 = polygon[numVerts - 1];
            bool bYFlag0 = (p0.Y >= p.Y) ? true : false;
            Point2D p1 = null;

            bool bInside = false;
            for (int j = 0; j < numVerts; ++j)
            {
                p1 = polygon[j];
                bool bYFlag1 = (p1.Y >= p.Y) ? true : false;
                if (bYFlag0 != bYFlag1)
                {
                    if (((p1.Y - p.Y) * (p0.X - p1.X) >= (p1.X - p.X) * (p0.Y - p1.Y)) == bYFlag1)
                    {
                        bInside = !bInside;
                    }
                }

                // Move to the next pair of vertices, retaining info as possible.
                bYFlag0 = bYFlag1;
                p0 = p1;
            }

            return bInside;
        }


        // Given two polygons and their bounding rects, returns true if the two polygons intersect.
        // This test will NOT determine if one of the two polygons is contained within the other or if the 
        // two polygons are similar - it will return false in all those cases.  The only case it will return
        // true for is if the two polygons actually intersect.
        public static bool PolygonsIntersect2D( IList<Point2D> poly1, Rect2D boundRect1,
                                                IList<Point2D> poly2, Rect2D boundRect2)
        {
            // do some quick tests first before doing any real work
            if (poly1 == null || poly1.Count < 3 || boundRect1 == null || poly2 == null || poly2.Count < 3 || boundRect2 == null)
            {
                return false;
            }

            if (!boundRect1.Intersects(boundRect2))
            {
                return false;
            }

            // We first check whether any edge of one poly intersects any edge of the other poly. If they do,
            // then the two polys intersect.

            // Make the epsilon a function of the size of the polys. We could take the heights of the rects 
            // also into consideration here if needed; but, that should not usually be necessary.
            double epsilon = Math.Max(Math.Min(boundRect1.Width, boundRect2.Width) * 0.001f, MathUtil.EPSILON);

            int numVerts1 = poly1.Count;
            int numVerts2 = poly2.Count;
            for (int i = 0; i < numVerts1; ++i)
            {
                int lineEndVert1 = i + 1;
                if (lineEndVert1 == numVerts1)
                {
                    lineEndVert1 = 0;
                }
                for (int j = 0; j < numVerts2; ++j)
                {
                    int lineEndVert2 = j + 1;
                    if (lineEndVert2 == numVerts2)
                    {
                        lineEndVert2 = 0;
                    }
                    Point2D tmp = null;
                    if (TriangulationUtil.LinesIntersect2D(poly1[i], poly1[lineEndVert1], poly2[j], poly2[lineEndVert2], ref tmp, epsilon))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        public bool PolygonContainsPolygon(IList<Point2D> poly1, Rect2D boundRect1,
                                            IList<Point2D> poly2, Rect2D boundRect2)
        {
            return PolygonContainsPolygon(poly1, boundRect1, poly2, boundRect2, true);
        }


        /// <summary>
        /// Checks to see if poly1 contains poly2.  return true if so, false otherwise.
        ///
        /// If the polygons intersect, then poly1 cannot contain poly2 (or vice-versa for that matter)
        /// Since the poly intersection test can be somewhat expensive, we'll only run it if the user
        /// requests it.   If runIntersectionTest is false, then it is assumed that the user has already
        /// verified that the polygons do not intersect.  If the polygons DO intersect and runIntersectionTest
        /// is false, then the return value is meaningless.  Caveat emptor.
        /// 
        /// As an added bonus, just to cause more user-carnage, if runIntersectionTest is false, then the 
        /// boundRects are not used and can safely be passed in as nulls.   However, if runIntersectionTest
        /// is true and you pass nulls for boundRect1 or boundRect2, you will cause a program crash.
        /// 
        /// Finally, the polygon points are assumed to be passed in Clockwise winding order.   It is possible
        /// that CounterClockwise ordering would work, but I have not verified the behavior in that case. 
        /// 
        /// </summary>
        /// <param name="poly1">points of polygon1</param>
        /// <param name="boundRect1">bounding rect of polygon1.  Only used if runIntersectionTest is true</param>
        /// <param name="poly2">points of polygon2</param>
        /// <param name="boundRect2">bounding rect of polygon2.  Only used if runIntersectionTest is true</param>
        /// <param name="runIntersectionTest">see summary above</param>
        /// <returns>true if poly1 fully contains poly2</returns>
        public static bool PolygonContainsPolygon(IList<Point2D> poly1, Rect2D boundRect1,
                                                    IList<Point2D> poly2, Rect2D boundRect2,
                                                    bool runIntersectionTest)
        {
            // quick early-out tests
            if (poly1 == null || poly1.Count < 3 || poly2 == null || poly2.Count < 3)
            {
                return false;
            }

            if (runIntersectionTest)
            {
                // make sure the polygons are not actually the same...
                if (poly1.Count == poly2.Count)
                {
                    // Check if the polys are similar to within a tolerance (Doesn't include reflections,
                    // but allows for the points to be numbered differently)
                    if (PolygonUtil.PolygonsAreSame2D(poly1, poly2))
                    {
                        return false;
                    }
                }

                bool bIntersect = PolygonUtil.PolygonsIntersect2D(poly1, boundRect1, poly2, boundRect2);
                if (bIntersect)
                {
                    return false;
                }
            }

            // Since we (now) know that the polygons don't intersect and they are not the same, we can just do a
            // single check to see if ANY point in poly2 is inside poly1.  If so, then all points of poly2
            // are inside poly1.  If not, then ALL points of poly2 are outside poly1.
            if (PolygonUtil.PointInPolygon2D(poly1, poly2[0]))
            {
                return true;
            }

            return false;
        }



        ////////////////////////////////////////////////////////////////////////////////
        // ClipPolygonToEdge2D
        //
        // This function clips a polygon against an edge. The portion of the polygon
        // which is to the left of the edge (while going from edgeBegin to edgeEnd) 
        // is returned in "outPoly". Note that the clipped polygon may have more vertices
        // than the input polygon. Make sure that outPolyArraySize is sufficiently large. 
        // Otherwise, you may get incorrect results and may be an assert (hopefully, no crash).
        // Pass in the actual size of the array in "outPolyArraySize".
        //
        // Read Sutherland-Hidgman algorithm description in Foley & van Dam book for 
        // details about this.
        //
        ///////////////////////////////////////////////////////////////////////////
        public static void ClipPolygonToEdge2D( Point2D edgeBegin,
                                                Point2D edgeEnd,
                                                IList<Point2D> poly,
                                                out List<Point2D> outPoly)
        {
            outPoly = null;
            if (edgeBegin == null ||
                edgeEnd == null ||
                poly == null ||
                poly.Count < 3)
            {
                return;
            }

            outPoly = new List<Point2D>();
            int lastVertex = poly.Count - 1;
            Point2D edgeRayVector = new Point2D(edgeEnd.X - edgeBegin.X, edgeEnd.Y - edgeBegin.Y);
            // Note: >= 0 as opposed to <= 0 is intentional. We are 
            // dealing with x and z here. And in our case, z axis goes
            // downward while the x axis goes rightward.
            //bool bLastVertexIsToRight = TriangulationUtil.PointRelativeToLine2D(poly[lastVertex], edgeBegin, edgeEnd) >= 0;
            bool bLastVertexIsToRight = TriangulationUtil.Orient2d(edgeBegin, edgeEnd, poly[lastVertex]) == Orientation.CW ? true : false;
            Point2D tempRay = new Point2D(0.0, 0.0);

            for (int curVertex = 0; curVertex < poly.Count; curVertex++)
            {
                //bool bCurVertexIsToRight = TriangulationUtil.PointRelativeToLine2D(poly[curVertex], edgeBegin, edgeEnd) >= 0;
                bool bCurVertexIsToRight = TriangulationUtil.Orient2d(edgeBegin, edgeEnd, poly[curVertex]) == Orientation.CW ? true : false;
                if (bCurVertexIsToRight)
                {
                    if (bLastVertexIsToRight)
                    {
                        outPoly.Add(poly[curVertex]);
                    }
                    else
                    {
                        tempRay.Set(poly[curVertex].X - poly[lastVertex].X, poly[curVertex].Y - poly[lastVertex].Y);
                        Point2D ptIntersection = new Point2D(0.0, 0.0);
                        bool bIntersect = TriangulationUtil.RaysIntersect2D(poly[lastVertex], tempRay, edgeBegin, edgeRayVector, ref ptIntersection);
                        if (bIntersect)
                        {
                            outPoly.Add(ptIntersection);
                            outPoly.Add(poly[curVertex]);
                        }
                    }
                }
                else if (bLastVertexIsToRight)
                {
                    tempRay.Set(poly[curVertex].X - poly[lastVertex].X, poly[curVertex].Y - poly[lastVertex].Y);
                    Point2D ptIntersection = new Point2D(0.0, 0.0);
                    bool bIntersect = TriangulationUtil.RaysIntersect2D(poly[lastVertex], tempRay, edgeBegin, edgeRayVector, ref ptIntersection);
                    if (bIntersect)
                    {
                        outPoly.Add(ptIntersection);
                    }
                }

                lastVertex = curVertex;
                bLastVertexIsToRight = bCurVertexIsToRight;
            }
        }


        public static void ClipPolygonToPolygon(IList<Point2D> poly, IList<Point2D> clipPoly, out List<Point2D> outPoly)
        {
            outPoly = null;
            if (poly == null || poly.Count < 3 || clipPoly == null || clipPoly.Count < 3)
            {
                return;
            }

            outPoly = new List<Point2D>(poly);
            int numClipVertices = clipPoly.Count;
            int lastVertex = numClipVertices - 1;

            // The algorithm keeps clipping the polygon against each edge of "clipPoly".
            for (int curVertex = 0; curVertex < numClipVertices; curVertex++)
            {
                List<Point2D> clippedPoly = null;
                Point2D edgeBegin = clipPoly[lastVertex];
                Point2D edgeEnd = clipPoly[curVertex];
                PolygonUtil.ClipPolygonToEdge2D(edgeBegin, edgeEnd, outPoly, out clippedPoly);
                outPoly.Clear();
                outPoly.AddRange(clippedPoly);

                lastVertex = curVertex;
            }
        }


        /// Merges two polygons, given that they intersect.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="union">The union of the two polygons</param>
        /// <returns>The error returned from union</returns>
        public static PolygonUtil.PolyUnionError PolygonUnion(Point2DList polygon1, Point2DList polygon2, out Point2DList union)
        {
            PolygonOperationContext ctx = new PolygonOperationContext();
            ctx.Init(PolygonUtil.PolyOperation.Union, polygon1, polygon2);
            PolygonUnionInternal(ctx);
            union = ctx.Union;
            return ctx.mError;
        }


        protected static void PolygonUnionInternal(PolygonOperationContext ctx)
        {
            Point2DList union = ctx.Union;
            if (ctx.mStartingIndex == -1)
            {
                switch (ctx.mError)
                {
                    case PolygonUtil.PolyUnionError.NoIntersections:
                    case PolygonUtil.PolyUnionError.InfiniteLoop:
                        return;
                    case PolygonUtil.PolyUnionError.Poly1InsidePoly2:
                        union.AddRange(ctx.mOriginalPolygon2);
                        return;
                }
            }

            Point2DList currentPoly = ctx.mPoly1;
            Point2DList otherPoly = ctx.mPoly2;
            List<int> currentPolyVectorAngles = ctx.mPoly1VectorAngles;

            // Store the starting vertex so we can refer to it later.
            Point2D startingVertex = ctx.mPoly1[ctx.mStartingIndex];
            int currentIndex = ctx.mStartingIndex;
            int firstPoly2Index = -1;
            union.Clear();

            do
            {
                // Add the current vertex to the final union
                union.Add(currentPoly[currentIndex]);

                foreach (EdgeIntersectInfo intersect in ctx.mIntersections)
                {
                    // If the current point is an intersection point
                    if (currentPoly[currentIndex].Equals(intersect.IntersectionPoint, currentPoly.Epsilon))
                    {
                        // Make sure we want to swap polygons here.
                        int otherIndex = otherPoly.IndexOf(intersect.IntersectionPoint);

                        // If the next vertex, if we do swap, is not inside the current polygon,
                        // then its safe to swap, otherwise, just carry on with the current poly.
                        int comparePointIndex = otherPoly.NextIndex(otherIndex);
                        Point2D comparePoint = otherPoly[comparePointIndex];
                        bool bPointInPolygonAngle = false;
                        if (currentPolyVectorAngles[comparePointIndex] == -1)
                        {
                            bPointInPolygonAngle = ctx.PointInPolygonAngle(comparePoint, currentPoly);
                            currentPolyVectorAngles[comparePointIndex] = bPointInPolygonAngle ? 1 : 0;
                        }
                        else
                        {
                            bPointInPolygonAngle = (currentPolyVectorAngles[comparePointIndex] == 1) ? true : false;
                        }

                        if (!bPointInPolygonAngle)
                        {
                            // switch polygons
                            if (currentPoly == ctx.mPoly1)
                            {
                                currentPoly = ctx.mPoly2;
                                currentPolyVectorAngles = ctx.mPoly2VectorAngles;
                                otherPoly = ctx.mPoly1;
                                if (firstPoly2Index < 0)
                                {
                                    firstPoly2Index = otherIndex;
                                }
                            }
                            else
                            {
                                currentPoly = ctx.mPoly1;
                                currentPolyVectorAngles = ctx.mPoly1VectorAngles;
                                otherPoly = ctx.mPoly2;
                            }

                            // set currentIndex
                            currentIndex = otherIndex;

                            // Stop checking intersections for this point.
                            break;
                        }
                    }
                }

                // Move to next index
                currentIndex = currentPoly.NextIndex(currentIndex);

                if (currentPoly == ctx.mPoly1)
                {
                    if (currentIndex == 0)
                    {
                        break;
                    }
                }
                else
                {
                    if (firstPoly2Index >= 0 && currentIndex == firstPoly2Index)
                    {
                        break;
                    }
                }
            } while ((currentPoly[currentIndex] != startingVertex) && (union.Count <= (ctx.mPoly1.Count + ctx.mPoly2.Count)));

            // If the number of vertices in the union is more than the combined vertices
            // of the input polygons, then something is wrong and the algorithm will
            // loop forever. Luckily, we check for that.
            if (union.Count > (ctx.mPoly1.Count + ctx.mPoly2.Count))
            {
                ctx.mError = PolygonUtil.PolyUnionError.InfiniteLoop;
            }

            return;
        }


        /// <summary>
        /// Finds the intersection between two polygons.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="intersectOut">The intersection of the two polygons</param>
        /// <returns>error code</returns>
        public static PolygonUtil.PolyUnionError PolygonIntersect(Point2DList polygon1, Point2DList polygon2, out Point2DList intersectOut)
        {
            PolygonOperationContext ctx = new PolygonOperationContext();
            ctx.Init(PolygonUtil.PolyOperation.Intersect, polygon1, polygon2);
            PolygonIntersectInternal(ctx);
            intersectOut = ctx.Intersect;
            return ctx.mError;
        }


        protected static void PolygonIntersectInternal(PolygonOperationContext ctx)
        {
            Point2DList intersectOut = ctx.Intersect;
            if (ctx.mStartingIndex == -1)
            {
                switch (ctx.mError)
                {
                    case PolygonUtil.PolyUnionError.NoIntersections:
                    case PolygonUtil.PolyUnionError.InfiniteLoop:
                        return;
                    case PolygonUtil.PolyUnionError.Poly1InsidePoly2:
                        intersectOut.AddRange(ctx.mOriginalPolygon2);
                        return;
                }
            }

            Point2DList currentPoly = ctx.mPoly1;
            Point2DList otherPoly = ctx.mPoly2;
            List<int> currentPolyVectorAngles = ctx.mPoly1VectorAngles;

            // Store the starting vertex so we can refer to it later.            
            int currentIndex = ctx.mPoly1.IndexOf(ctx.mIntersections[0].IntersectionPoint);
            Point2D startingVertex = ctx.mPoly1[currentIndex];
            int firstPoly1Index = currentIndex;
            int firstPoly2Index = -1;
            intersectOut.Clear();

            do
            {
                // Add the current vertex to the final intersection
                if (intersectOut.Contains(currentPoly[currentIndex]))
                {
                    // This can happen when the two polygons only share a single edge, and neither is inside the other
                    break;
                }
                intersectOut.Add(currentPoly[currentIndex]);

                foreach (EdgeIntersectInfo intersect in ctx.mIntersections)
                {
                    // If the current point is an intersection point
                    if (currentPoly[currentIndex].Equals(intersect.IntersectionPoint, currentPoly.Epsilon))
                    {
                        // Make sure we want to swap polygons here.
                        int otherIndex = otherPoly.IndexOf(intersect.IntersectionPoint);

                        // If the next vertex, if we do swap, is inside the current polygon,
                        // then its safe to swap, otherwise, just carry on with the current poly.
                        int comparePointIndex = otherPoly.NextIndex(otherIndex);
                        Point2D comparePoint = otherPoly[comparePointIndex];
                        bool bPointInPolygonAngle = false;
                        if (currentPolyVectorAngles[comparePointIndex] == -1)
                        {
                            bPointInPolygonAngle = ctx.PointInPolygonAngle(comparePoint, currentPoly);
                            currentPolyVectorAngles[comparePointIndex] = bPointInPolygonAngle ? 1 : 0;
                        }
                        else
                        {
                            bPointInPolygonAngle = (currentPolyVectorAngles[comparePointIndex] == 1) ? true : false;
                        }

                        if (bPointInPolygonAngle)
                        {
                            // switch polygons
                            if (currentPoly == ctx.mPoly1)
                            {
                                currentPoly = ctx.mPoly2;
                                currentPolyVectorAngles = ctx.mPoly2VectorAngles;
                                otherPoly = ctx.mPoly1;
                                if (firstPoly2Index < 0)
                                {
                                    firstPoly2Index = otherIndex;
                                }
                            }
                            else
                            {
                                currentPoly = ctx.mPoly1;
                                currentPolyVectorAngles = ctx.mPoly1VectorAngles;
                                otherPoly = ctx.mPoly2;
                            }

                            // set currentIndex
                            currentIndex = otherIndex;

                            // Stop checking intersections for this point.
                            break;
                        }
                    }
                }

                // Move to next index
                currentIndex = currentPoly.NextIndex(currentIndex);

                if (currentPoly == ctx.mPoly1)
                {
                    if (currentIndex == firstPoly1Index)
                    {
                        break;
                    }
                }
                else
                {
                    if (firstPoly2Index >= 0 && currentIndex == firstPoly2Index)
                    {
                        break;
                    }
                }
            } while ((currentPoly[currentIndex] != startingVertex) && (intersectOut.Count <= (ctx.mPoly1.Count + ctx.mPoly2.Count)));

            // If the number of vertices in the union is more than the combined vertices
            // of the input polygons, then something is wrong and the algorithm will
            // loop forever. Luckily, we check for that.
            if (intersectOut.Count > (ctx.mPoly1.Count + ctx.mPoly2.Count))
            {
                ctx.mError = PolygonUtil.PolyUnionError.InfiniteLoop;
            }

            return;
        }


        /// <summary>
        /// Subtracts one polygon from another.
        /// </summary>
        /// <param name="polygon1">The base polygon.</param>
        /// <param name="polygon2">The polygon to subtract from the base.</param>
        /// <param name="subtract">The result of the polygon subtraction</param>
        /// <returns>error code</returns>
        public static PolygonUtil.PolyUnionError PolygonSubtract(Point2DList polygon1, Point2DList polygon2, out Point2DList subtract)
        {
            PolygonOperationContext ctx = new PolygonOperationContext();
            ctx.Init(PolygonUtil.PolyOperation.Subtract, polygon1, polygon2);
            PolygonSubtractInternal(ctx);
            subtract = ctx.Subtract;
            return ctx.mError;
        }


        public static void PolygonSubtractInternal(PolygonOperationContext ctx)
        {
            Point2DList subtract = ctx.Subtract;
            if (ctx.mStartingIndex == -1)
            {
                switch (ctx.mError)
                {
                    case PolygonUtil.PolyUnionError.NoIntersections:
                    case PolygonUtil.PolyUnionError.InfiniteLoop:
                    case PolygonUtil.PolyUnionError.Poly1InsidePoly2:
                        return;
                }
            }

            Point2DList currentPoly = ctx.mPoly1;
            Point2DList otherPoly = ctx.mPoly2;
            List<int> currentPolyVectorAngles = ctx.mPoly1VectorAngles;

            // Store the starting vertex so we can refer to it later.
            Point2D startingVertex = ctx.mPoly1[ctx.mStartingIndex];
            int currentIndex = ctx.mStartingIndex;
            subtract.Clear();

            // Trace direction
            bool forward = true;

            do
            {
                // Add the current vertex to the final union
                subtract.Add(currentPoly[currentIndex]);

                foreach (EdgeIntersectInfo intersect in ctx.mIntersections)
                {
                    // If the current point is an intersection point
                    if (currentPoly[currentIndex].Equals(intersect.IntersectionPoint, currentPoly.Epsilon))
                    {
                        // Make sure we want to swap polygons here.
                        int otherIndex = otherPoly.IndexOf(intersect.IntersectionPoint);

                        //Point2D otherVertex;
                        if (forward)
                        {
                            // If the next vertex, if we do swap, is inside the current polygon,
                            // then its safe to swap, otherwise, just carry on with the current poly.
                            int compareIndex = otherPoly.PreviousIndex(otherIndex);
                            Point2D compareVertex = otherPoly[compareIndex];
                            bool bPointInPolygonAngle = false;
                            if (currentPolyVectorAngles[compareIndex] == -1)
                            {
                                bPointInPolygonAngle = ctx.PointInPolygonAngle(compareVertex, currentPoly);
                                currentPolyVectorAngles[compareIndex] = bPointInPolygonAngle ? 1 : 0;
                            }
                            else
                            {
                                bPointInPolygonAngle = (currentPolyVectorAngles[compareIndex] == 1) ? true : false;
                            }

                            if (bPointInPolygonAngle)
                            {
                                // switch polygons
                                if (currentPoly == ctx.mPoly1)
                                {
                                    currentPoly = ctx.mPoly2;
                                    currentPolyVectorAngles = ctx.mPoly2VectorAngles;
                                    otherPoly = ctx.mPoly1;
                                }
                                else
                                {
                                    currentPoly = ctx.mPoly1;
                                    currentPolyVectorAngles = ctx.mPoly1VectorAngles;
                                    otherPoly = ctx.mPoly2;
                                }

                                // set currentIndex
                                currentIndex = otherIndex;

                                // Reverse direction
                                forward = !forward;

                                // Stop checking ctx.mIntersections for this point.
                                break;
                            }
                        }
                        else
                        {
                            // If the next vertex, if we do swap, is outside the current polygon,
                            // then its safe to swap, otherwise, just carry on with the current poly.
                            int compareIndex = otherPoly.NextIndex(otherIndex);
                            Point2D compareVertex = otherPoly[compareIndex];
                            bool bPointInPolygonAngle = false;
                            if (currentPolyVectorAngles[compareIndex] == -1)
                            {
                                bPointInPolygonAngle = ctx.PointInPolygonAngle(compareVertex, currentPoly);
                                currentPolyVectorAngles[compareIndex] = bPointInPolygonAngle ? 1 : 0;
                            }
                            else
                            {
                                bPointInPolygonAngle = (currentPolyVectorAngles[compareIndex] == 1) ? true : false;
                            }

                            if (!bPointInPolygonAngle)
                            {
                                // switch polygons
                                if (currentPoly == ctx.mPoly1)
                                {
                                    currentPoly = ctx.mPoly2;
                                    currentPolyVectorAngles = ctx.mPoly2VectorAngles;
                                    otherPoly = ctx.mPoly1;
                                }
                                else
                                {
                                    currentPoly = ctx.mPoly1;
                                    currentPolyVectorAngles = ctx.mPoly1VectorAngles;
                                    otherPoly = ctx.mPoly2;
                                }

                                // set currentIndex
                                currentIndex = otherIndex;

                                // Reverse direction
                                forward = !forward;

                                // Stop checking intersections for this point.
                                break;
                            }
                        }
                    }
                }

                if (forward)
                {
                    // Move to next index
                    currentIndex = currentPoly.NextIndex(currentIndex);
                }
                else
                {
                    currentIndex = currentPoly.PreviousIndex(currentIndex);
                }
            } while ((currentPoly[currentIndex] != startingVertex) && (subtract.Count <= (ctx.mPoly1.Count + ctx.mPoly2.Count)));


            // If the number of vertices in the union is more than the combined vertices
            // of the input polygons, then something is wrong and the algorithm will
            // loop forever. Luckily, we check for that.
            if (subtract.Count > (ctx.mPoly1.Count + ctx.mPoly2.Count))
            {
                ctx.mError = PolygonUtil.PolyUnionError.InfiniteLoop;
            }

            return;
        }


        /// <summary>
        /// Performs one or more polygon operations on the 2 provided polygons
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon</param>
        /// <param name="subtract">The result of the polygon subtraction</param>
        /// <returns>error code</returns>
        public static PolygonUtil.PolyUnionError PolygonOperation(PolygonUtil.PolyOperation operations, Point2DList polygon1, Point2DList polygon2, out Dictionary<uint, Point2DList> results)
        {
            PolygonOperationContext ctx = new PolygonOperationContext();
            ctx.Init(operations, polygon1, polygon2);
            results = ctx.mOutput;
            return PolygonUtil.PolygonOperation(ctx);
        }


        public static PolygonUtil.PolyUnionError PolygonOperation(PolygonOperationContext ctx)
        {
            if ((ctx.mOperations & PolygonUtil.PolyOperation.Union) == PolygonUtil.PolyOperation.Union)
            {
                PolygonUtil.PolygonUnionInternal(ctx);
            }
            if ((ctx.mOperations & PolygonUtil.PolyOperation.Intersect) == PolygonUtil.PolyOperation.Intersect)
            {
                PolygonUtil.PolygonIntersectInternal(ctx);
            }
            if ((ctx.mOperations & PolygonUtil.PolyOperation.Subtract) == PolygonUtil.PolyOperation.Subtract)
            {
                PolygonUtil.PolygonSubtractInternal(ctx);
            }

            return ctx.mError;
        }


        /// <summary>
        /// Trace the edge of a non-simple polygon and return a simple polygon.
        /// 
        ///Method:
        ///Start at vertex with minimum y (pick maximum x one if there are two).  
        ///We aim our "lastDir" vector at (1.0, 0)
        ///We look at the two rays going off from our start vertex, and follow whichever
        ///has the smallest angle (in -Pi . Pi) wrt lastDir ("rightest" turn)
        ///
        ///Loop until we hit starting vertex:
        ///
        ///We add our current vertex to the list.
        ///We check the seg from current vertex to next vertex for intersections
        ///  - if no intersections, follow to next vertex and continue
        ///  - if intersections, pick one with minimum distance
        ///    - if more than one, pick one with "rightest" next point (two possibilities for each)
        ///    
        /// </summary>
        /// <param name="verts"></param>
        /// <returns></returns>
        public static List<Point2DList> SplitComplexPolygon(Point2DList verts, double epsilon)
        {
            int numVerts = verts.Count;
            int nNodes = 0;
            List<SplitComplexPolygonNode> nodes = new List<SplitComplexPolygonNode>();

            //Add base nodes (raw outline)
            for (int i = 0; i < verts.Count; ++i)
            {
                SplitComplexPolygonNode newNode = new SplitComplexPolygonNode(new Point2D(verts[i].X, verts[i].Y));
                nodes.Add(newNode);
            }
            for (int i = 0; i < verts.Count; ++i)
            {
                int iplus = (i == numVerts - 1) ? 0 : i + 1;
                int iminus = (i == 0) ? numVerts - 1 : i - 1;
                nodes[i].AddConnection(nodes[iplus]);
                nodes[i].AddConnection(nodes[iminus]);
            }
            nNodes = nodes.Count;

            //Process intersection nodes - horribly inefficient
            bool dirty = true;
            int counter = 0;
            while (dirty)
            {
                dirty = false;
                for (int i = 0; !dirty && i < nNodes; ++i)
                {
                    for (int j = 0; !dirty && j < nodes[i].NumConnected; ++j)
                    {
                        for (int k = 0; !dirty && k < nNodes; ++k)
                        {
                            if (k == i || nodes[k] == nodes[i][j])
                            {
                                continue;
                            }
                            for (int l = 0; !dirty && l < nodes[k].NumConnected; ++l)
                            {
                                if (nodes[k][l] == nodes[i][j] || nodes[k][l] == nodes[i])
                                {
                                    continue;
                                }
                                //Check intersection
                                Point2D intersectPt = new Point2D();
                                //if (counter > 100) printf("checking intersection: %d, %d, %d, %d\n",i,j,k,l);
                                bool crosses = TriangulationUtil.LinesIntersect2D(  nodes[i].Position,
                                                                                    nodes[i][j].Position,
                                                                                    nodes[k].Position,
                                                                                    nodes[k][l].Position,
                                                                                    true, true, true,
                                                                                    ref intersectPt,
                                                                                    epsilon);
                                if (crosses)
                                {
                                    /*if (counter > 100) {
                                        printf("Found crossing at %f, %f\n",intersectPt.x, intersectPt.y);
                                        printf("Locations: %f,%f - %f,%f | %f,%f - %f,%f\n",
                                                        nodes[i].position.x, nodes[i].position.y,
                                                        nodes[i].connected[j].position.x, nodes[i].connected[j].position.y,
                                                        nodes[k].position.x,nodes[k].position.y,
                                                        nodes[k].connected[l].position.x,nodes[k].connected[l].position.y);
                                        printf("Memory addresses: %d, %d, %d, %d\n",(int)&nodes[i],(int)nodes[i].connected[j],(int)&nodes[k],(int)nodes[k].connected[l]);
                                    }*/
                                    dirty = true;
                                    //Destroy and re-hook connections at crossing point
                                    SplitComplexPolygonNode intersectionNode = new SplitComplexPolygonNode(intersectPt);
                                    int idx = nodes.IndexOf(intersectionNode);
                                    if (idx >= 0 && idx < nodes.Count)
                                    {
                                        intersectionNode = nodes[idx];
                                    }
                                    else
                                    {
                                        nodes.Add(intersectionNode);
                                        nNodes = nodes.Count;
                                    }

                                    SplitComplexPolygonNode nodei = nodes[i];
                                    SplitComplexPolygonNode connij = nodes[i][j];
                                    SplitComplexPolygonNode nodek = nodes[k];
                                    SplitComplexPolygonNode connkl = nodes[k][l];
                                    connij.RemoveConnection(nodei);
                                    nodei.RemoveConnection(connij);
                                    connkl.RemoveConnection(nodek);
                                    nodek.RemoveConnection(connkl);
                                    if (!intersectionNode.Position.Equals(nodei.Position, epsilon))
                                    {
                                        intersectionNode.AddConnection(nodei);
                                        nodei.AddConnection(intersectionNode);
                                    }
                                    if (!intersectionNode.Position.Equals(nodek.Position, epsilon))
                                    {
                                        intersectionNode.AddConnection(nodek);
                                        nodek.AddConnection(intersectionNode);
                                    }
                                    if (!intersectionNode.Position.Equals(connij.Position, epsilon))
                                    {
                                        intersectionNode.AddConnection(connij);
                                        connij.AddConnection(intersectionNode);
                                    }
                                    if (!intersectionNode.Position.Equals(connkl.Position, epsilon))
                                    {
                                        intersectionNode.AddConnection(connkl);
                                        connkl.AddConnection(intersectionNode);
                                    }
                                }
                            }
                        }
                    }
                }
                ++counter;
                //if (counter > 100) printf("Counter: %d\n",counter);
            }

            //    /*
            //    // Debugging: check for connection consistency
            //    for (int i=0; i<nNodes; ++i) {
            //        int nConn = nodes[i].nConnected;
            //        for (int j=0; j<nConn; ++j) {
            //            if (nodes[i].connected[j].nConnected == 0) Assert(false);
            //            SplitComplexPolygonNode* connect = nodes[i].connected[j];
            //            bool found = false;
            //            for (int k=0; k<connect.nConnected; ++k) {
            //                if (connect.connected[k] == &nodes[i]) found = true;
            //            }
            //            Assert(found);
            //        }
            //    }*/

            //Collapse duplicate points
            bool foundDupe = true;
            int nActive = nNodes;
            double epsilonSquared = epsilon * epsilon;
            while (foundDupe)
            {
                foundDupe = false;
                for (int i = 0; i < nNodes; ++i)
                {
                    if (nodes[i].NumConnected == 0)
                    {
                        continue;
                    }
                    for (int j = i + 1; j < nNodes; ++j)
                    {
                        if (nodes[j].NumConnected == 0)
                        {
                            continue;
                        }
                        Point2D diff = nodes[i].Position - nodes[j].Position;
                        if (diff.MagnitudeSquared() <= epsilonSquared)
                        {
                            if (nActive <= 3)
                            {
                                throw new Exception("Eliminated so many duplicate points that resulting polygon has < 3 vertices!");
                            }

                            //printf("Found dupe, %d left\n",nActive);
                            --nActive;
                            foundDupe = true;
                            SplitComplexPolygonNode inode = nodes[i];
                            SplitComplexPolygonNode jnode = nodes[j];
                            //Move all of j's connections to i, and remove j
                            int njConn = jnode.NumConnected;
                            for (int k = 0; k < njConn; ++k)
                            {
                                SplitComplexPolygonNode knode = jnode[k];
                                //Debug.Assert(knode != jnode);
                                if (knode != inode)
                                {
                                    inode.AddConnection(knode);
                                    knode.AddConnection(inode);
                                }
                                knode.RemoveConnection(jnode);
                                //printf("knode %d on node %d now has %d connections\n",k,j,knode.nConnected);
                                //printf("Found duplicate point.\n");
                            }
                            jnode.ClearConnections();   // to help with garbage collection
                            nodes.RemoveAt(j);
                            --nNodes;
                        }
                    }
                }
            }

            //    /*
            //    // Debugging: check for connection consistency
            //    for (int i=0; i<nNodes; ++i) {
            //        int nConn = nodes[i].nConnected;
            //        printf("Node %d has %d connections\n",i,nConn);
            //        for (int j=0; j<nConn; ++j) {
            //            if (nodes[i].connected[j].nConnected == 0) {
            //                printf("Problem with node %d connection at address %d\n",i,(int)(nodes[i].connected[j]));
            //                Assert(false);
            //            }
            //            SplitComplexPolygonNode* connect = nodes[i].connected[j];
            //            bool found = false;
            //            for (int k=0; k<connect.nConnected; ++k) {
            //                if (connect.connected[k] == &nodes[i]) found = true;
            //            }
            //            if (!found) printf("Connection %d (of %d) on node %d (of %d) did not have reciprocal connection.\n",j,nConn,i,nNodes);
            //            Assert(found);
            //        }
            //    }//*/

            //Now walk the edge of the list

            //Find node with minimum y value (max x if equal)
            double minY = double.MaxValue;
            double maxX = -double.MaxValue;
            int minYIndex = -1;
            for (int i = 0; i < nNodes; ++i)
            {
                if (nodes[i].Position.Y < minY && nodes[i].NumConnected > 1)
                {
                    minY = nodes[i].Position.Y;
                    minYIndex = i;
                    maxX = nodes[i].Position.X;
                }
                else if (nodes[i].Position.Y == minY && nodes[i].Position.X > maxX && nodes[i].NumConnected > 1)
                {
                    minYIndex = i;
                    maxX = nodes[i].Position.X;
                }
            }

            Point2D origDir = new Point2D(1.0f, 0.0f);
            List<Point2D> resultVecs = new List<Point2D>();
            SplitComplexPolygonNode currentNode = nodes[minYIndex];
            SplitComplexPolygonNode startNode = currentNode;
            //Debug.Assert(currentNode.nConnected > 0);
            SplitComplexPolygonNode nextNode = currentNode.GetRightestConnection(origDir);
            if (nextNode == null)
            {
                // Borked, clean up our mess and return
                return PolygonUtil.SplitComplexPolygonCleanup(verts);
            }

            resultVecs.Add(startNode.Position);
            while (nextNode != startNode)
            {
                if (resultVecs.Count > (4 * nNodes))
                {
                    //printf("%d, %d, %d\n",(int)startNode,(int)currentNode,(int)nextNode);
                    //printf("%f, %f . %f, %f\n",currentNode.position.x,currentNode.position.y, nextNode.position.x, nextNode.position.y);
                    //verts.printFormatted();
                    //printf("Dumping connection graph: \n");
                    //for (int i=0; i<nNodes; ++i)
                    //{
                    //    printf("nodex[%d] = %f; nodey[%d] = %f;\n",i,nodes[i].position.x,i,nodes[i].position.y);
                    //    printf("//connected to\n");
                    //    for (int j=0; j<nodes[i].nConnected; ++j)
                    //    {
                    //        printf("connx[%d][%d] = %f; conny[%d][%d] = %f;\n",i,j,nodes[i].connected[j].position.x, i,j,nodes[i].connected[j].position.y);
                    //    }
                    //}
                    //printf("Dumping results thus far: \n");
                    //for (int i=0; i<nResultVecs; ++i)
                    //{
                    //    printf("x[%d]=map(%f,-3,3,0,width); y[%d] = map(%f,-3,3,height,0);\n",i,resultVecs[i].x,i,resultVecs[i].y);
                    //}
                    //Debug.Assert(false);
                    //nodes should never be visited four times apiece (proof?), so we've probably hit a loop...crap
                    throw new Exception("nodes should never be visited four times apiece (proof?), so we've probably hit a loop...crap");
                }
                resultVecs.Add(nextNode.Position);
                SplitComplexPolygonNode oldNode = currentNode;
                currentNode = nextNode;
                //printf("Old node connections = %d; address %d\n",oldNode.nConnected, (int)oldNode);
                //printf("Current node connections = %d; address %d\n",currentNode.nConnected, (int)currentNode);
                nextNode = currentNode.GetRightestConnection(oldNode);
                if (nextNode == null)
                {
                    return PolygonUtil.SplitComplexPolygonCleanup(resultVecs);
                }
                // There was a problem, so jump out of the loop and use whatever garbage we've generated so far
                //printf("nextNode address: %d\n",(int)nextNode);
            }

            if (resultVecs.Count < 1)
            {
                // Borked, clean up our mess and return
                return PolygonUtil.SplitComplexPolygonCleanup(verts);
            }
            else
            {
                return PolygonUtil.SplitComplexPolygonCleanup(resultVecs);
            }
        }


        private static List<Point2DList> SplitComplexPolygonCleanup(IList<Point2D> orig)
        {
            List<Point2DList> l = new List<Point2DList>();
            Point2DList origP2DL = new Point2DList(orig);
            l.Add(origP2DL);
            int listIdx = 0;
            int numLists = l.Count;
            while (listIdx < numLists)
            {
                int numPoints = l[listIdx].Count;
                for (int i = 0; i < numPoints; ++i)
                {
                    for (int j = i + 1; j < numPoints; ++j)
                    {
                        if (l[listIdx][i].Equals(l[listIdx][j], origP2DL.Epsilon))
                        {
                            // found a self-intersection loop - split it off into it's own list
                            int numToRemove = j - i;
                            Point2DList newList = new Point2DList();
                            for (int k = i + 1; k <= j; ++k)
                            {
                                newList.Add(l[listIdx][k]);
                            }
                            l[listIdx].RemoveRange(i + 1, numToRemove);
                            l.Add(newList);
                            ++numLists;
                            numPoints -= numToRemove;
                            j = i + 1;
                        }
                    }
                }
                l[listIdx].Simplify();
                ++listIdx;
            }

            return l;
        }
    
	}


    public class EdgeIntersectInfo
    {
        public EdgeIntersectInfo(Edge edgeOne, Edge edgeTwo, Point2D intersectionPoint)
        {
            EdgeOne = edgeOne;
            EdgeTwo = edgeTwo;
            IntersectionPoint = intersectionPoint;
        }

        public Edge EdgeOne { get; private set; }
        public Edge EdgeTwo { get; private set; }
        public Point2D IntersectionPoint { get; private set; }
    }


    public class SplitComplexPolygonNode
    {
        /*
         * Given sines and cosines, tells if A's angle is less than B's on -Pi, Pi
         * (in other words, is A "righter" than B)
         */
        private List<SplitComplexPolygonNode> mConnected = new List<SplitComplexPolygonNode>();
        private Point2D mPosition = null;

        public int NumConnected { get { return mConnected.Count; } }
        public Point2D Position { get { return mPosition; } set { mPosition = value; } }
        public SplitComplexPolygonNode this[int index]
        {
            get { return mConnected[index]; }
        }


        public SplitComplexPolygonNode()
        {
        }

        public SplitComplexPolygonNode(Point2D pos)
        {
            mPosition = pos;
        }


        public override bool Equals(Object obj)
        {
            SplitComplexPolygonNode pn = obj as SplitComplexPolygonNode;
            if (pn == null)
            {
                return base.Equals(obj);
            }

            return Equals(pn);
        }


        public bool Equals(SplitComplexPolygonNode pn)
        {
            if ((Object)pn == null)
            {
                return false;
            }
            if (mPosition == null || pn.Position == null)
            {
                return false;
            }

            return mPosition.Equals(pn.Position);
        }


        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(SplitComplexPolygonNode lhs, SplitComplexPolygonNode rhs) { if ((object)lhs != null) { return lhs.Equals(rhs); } if ((Object)rhs == null) { return true; } else { return false; } }
        public static bool operator !=(SplitComplexPolygonNode lhs, SplitComplexPolygonNode rhs) { if ((object)lhs != null) { return !lhs.Equals(rhs); } if ((Object)rhs == null) { return false; } else { return true; } }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append(mPosition.ToString());
            sb.Append(" -> ");
            for (int i = 0; i < NumConnected; ++i)
            {
                if (i != 0)
                {
                    sb.Append(", ");
                }
                sb.Append(mConnected[i].Position.ToString());
            }

            return sb.ToString();
        }


        private bool IsRighter(double sinA, double cosA, double sinB, double cosB)
        {
            if (sinA < 0)
            {
                if (sinB > 0 || cosA <= cosB)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (sinB < 0 || cosA <= cosB)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        //Fix for obnoxious behavior for the % operator for negative numbers...
        private int remainder(int x, int modulus)
        {
            int rem = x % modulus;
            while (rem < 0)
            {
                rem += modulus;
            }
            return rem;
        }

        public void AddConnection(SplitComplexPolygonNode toMe)
        {
            // Ignore duplicate additions
            if (!mConnected.Contains(toMe) && toMe != this)
            {
                mConnected.Add(toMe);
            }
        }

        public void RemoveConnection(SplitComplexPolygonNode fromMe)
        {
            mConnected.Remove(fromMe);
        }

        private void RemoveConnectionByIndex(int index)
        {
            if (index < 0 || index >= mConnected.Count)
            {
                return;
            }
            mConnected.RemoveAt(index);
        }

        public void ClearConnections()
        {
            mConnected.Clear();
        }

        private bool IsConnectedTo(SplitComplexPolygonNode me)
        {
            return mConnected.Contains(me);
        }

        public SplitComplexPolygonNode GetRightestConnection(SplitComplexPolygonNode incoming)
        {
            if (NumConnected == 0)
            {
                throw new Exception("the connection graph is inconsistent");
            }
            if (NumConnected == 1)
            {
                //b2Assert(false);
                // Because of the possibility of collapsing nearby points,
                // we may end up with "spider legs" dangling off of a region.
                // The correct behavior here is to turn around.
                return incoming;
            }
            Point2D inDir = mPosition - incoming.mPosition;

            double inLength = inDir.Magnitude();
            inDir.Normalize();

            if (inLength <= MathUtil.EPSILON)
            {
                throw new Exception("Length too small");
            }

            SplitComplexPolygonNode result = null;
            for (int i = 0; i < NumConnected; ++i)
            {
                if (mConnected[i] == incoming)
                {
                    continue;
                }
                Point2D testDir = mConnected[i].mPosition - mPosition;
                double testLengthSqr = testDir.MagnitudeSquared();
                testDir.Normalize();
                /*
                if (testLengthSqr < COLLAPSE_DIST_SQR) {
                    printf("Problem with connection %d\n",i);
                    printf("This node has %d connections\n",nConnected);
                    printf("That one has %d\n",connected[i].nConnected);
                    if (this == connected[i]) printf("This points at itself.\n");
                }*/
                if (testLengthSqr <= (MathUtil.EPSILON * MathUtil.EPSILON))
                {
                    throw new Exception("Length too small");
                }

                double myCos = Point2D.Dot(inDir, testDir);
                double mySin = Point2D.Cross(inDir, testDir);
                if (result != null)
                {
                    Point2D resultDir = result.mPosition - mPosition;
                    resultDir.Normalize();
                    double resCos = Point2D.Dot(inDir, resultDir);
                    double resSin = Point2D.Cross(inDir, resultDir);
                    if (IsRighter(mySin, myCos, resSin, resCos))
                    {
                        result = mConnected[i];
                    }
                }
                else
                {
                    result = mConnected[i];
                }
            }

            //if (B2_POLYGON_REPORT_ERRORS && result != null)
            //{
            //    printf("nConnected = %d\n", nConnected);
            //    for (int i = 0; i < nConnected; ++i)
            //    {
            //        printf("connected[%d] @ %d\n", i, (int)connected[i]);
            //    }
            //}
            //Debug.Assert(result != null);

            return result;
        }

        public SplitComplexPolygonNode GetRightestConnection(Point2D incomingDir)
        {
            Point2D diff = mPosition - incomingDir;
            SplitComplexPolygonNode temp = new SplitComplexPolygonNode(diff);
            SplitComplexPolygonNode res = GetRightestConnection(temp);
            //Debug.Assert(res != null);
            return res;
        }
    }


    public class PolygonOperationContext
    {
        public PolygonUtil.PolyOperation mOperations;
        public Point2DList mOriginalPolygon1;
        public Point2DList mOriginalPolygon2;
        public Point2DList mPoly1;
        public Point2DList mPoly2;
        public List<EdgeIntersectInfo> mIntersections;
        public int mStartingIndex;
        public PolygonUtil.PolyUnionError mError;
        public List<int> mPoly1VectorAngles;
        public List<int> mPoly2VectorAngles;
        public Dictionary<uint, Point2DList> mOutput = new Dictionary<uint, Point2DList>();

        public Point2DList Union
        {
            get
            {
                Point2DList l = null;
                if (!mOutput.TryGetValue((uint)PolygonUtil.PolyOperation.Union, out l))
                {
                    l = new Point2DList();
                    mOutput.Add((uint)PolygonUtil.PolyOperation.Union, l);
                }

                return l;
            }
        }
        public Point2DList Intersect
        {
            get
            {
                Point2DList l = null;
                if (!mOutput.TryGetValue((uint)PolygonUtil.PolyOperation.Intersect, out l))
                {
                    l = new Point2DList();
                    mOutput.Add((uint)PolygonUtil.PolyOperation.Intersect, l);
                }

                return l;
            }
        }
        public Point2DList Subtract
        {
            get
            {
                Point2DList l = null;
                if (!mOutput.TryGetValue((uint)PolygonUtil.PolyOperation.Subtract, out l))
                {
                    l = new Point2DList();
                    mOutput.Add((uint)PolygonUtil.PolyOperation.Subtract, l);
                }

                return l;
            }
        }

        public PolygonOperationContext() { }


        public void Clear()
        {
            mOperations = PolygonUtil.PolyOperation.None;
            mOriginalPolygon1 = null;
            mOriginalPolygon2 = null;
            mPoly1 = null;
            mPoly2 = null;
            mIntersections = null;
            mStartingIndex = -1;
            mError = PolygonUtil.PolyUnionError.None;
            mPoly1VectorAngles = null;
            mPoly2VectorAngles = null;
            mOutput = new Dictionary<uint, Point2DList>();
        }


        public bool Init(PolygonUtil.PolyOperation operations, Point2DList polygon1, Point2DList polygon2)
        {
            Clear();

            mOperations = operations;
            mOriginalPolygon1 = polygon1;
            mOriginalPolygon2 = polygon2;

            // Make a copy of the polygons so that we dont modify the originals, and
            // force vertices to integer (pixel) values.
            mPoly1 = new Point2DList(polygon1);
            mPoly1.WindingOrder = Point2DList.WindingOrderType.Default;
            mPoly2 = new Point2DList(polygon2);
            mPoly2.WindingOrder = Point2DList.WindingOrderType.Default;

            // Find intersection points
            if (!VerticesIntersect(mPoly1, mPoly2, out mIntersections))
            {
                // No intersections found - polygons do not overlap.
                mError = PolygonUtil.PolyUnionError.NoIntersections;
                return false;
            }

            // make sure edges that intersect more than once are updated to have correct start points
            int numIntersections = mIntersections.Count;
            for (int i = 0; i < numIntersections; ++i)
            {
                for (int j = i + 1; j < numIntersections; ++j)
                {
                    if (mIntersections[i].EdgeOne.EdgeStart.Equals(mIntersections[j].EdgeOne.EdgeStart) &&
                        mIntersections[i].EdgeOne.EdgeEnd.Equals(mIntersections[j].EdgeOne.EdgeEnd))
                    {
                        mIntersections[j].EdgeOne.EdgeStart = mIntersections[i].IntersectionPoint;
                    }
                    if (mIntersections[i].EdgeTwo.EdgeStart.Equals(mIntersections[j].EdgeTwo.EdgeStart) &&
                        mIntersections[i].EdgeTwo.EdgeEnd.Equals(mIntersections[j].EdgeTwo.EdgeEnd))
                    {
                        mIntersections[j].EdgeTwo.EdgeStart = mIntersections[i].IntersectionPoint;
                    }
                }
            }

            // Add intersection points to original polygons, ignoring existing points.
            foreach (EdgeIntersectInfo intersect in mIntersections)
            {
                if (!mPoly1.Contains(intersect.IntersectionPoint))
                {
                    mPoly1.Insert(mPoly1.IndexOf(intersect.EdgeOne.EdgeStart) + 1, intersect.IntersectionPoint);
                }

                if (!mPoly2.Contains(intersect.IntersectionPoint))
                {
                    mPoly2.Insert(mPoly2.IndexOf(intersect.EdgeTwo.EdgeStart) + 1, intersect.IntersectionPoint);
                }
            }

            mPoly1VectorAngles = new List<int>();
            for (int i = 0; i < mPoly2.Count; ++i)
            {
                mPoly1VectorAngles.Add(-1);
            }
            mPoly2VectorAngles = new List<int>();
            for (int i = 0; i < mPoly1.Count; ++i)
            {
                mPoly2VectorAngles.Add(-1);
            }

            // Find starting point on the edge of polygon1 that is outside of
            // the intersected area to begin polygon trace.
            int currentIndex = 0;
            do
            {
                bool bPointInPolygonAngle = PointInPolygonAngle(mPoly1[currentIndex], mPoly2);
                mPoly2VectorAngles[currentIndex] = bPointInPolygonAngle ? 1 : 0;
                if (bPointInPolygonAngle)
                {
                    mStartingIndex = currentIndex;
                    break;
                }
                currentIndex = mPoly1.NextIndex(currentIndex);
            } while (currentIndex != 0);

            // If we don't find a point on polygon1 thats outside of the
            // intersect area, the polygon1 must be inside of polygon2,
            // in which case, polygon2 IS the union of the two.
            if (mStartingIndex == -1)
            {
                mError = PolygonUtil.PolyUnionError.Poly1InsidePoly2;
                return false;
            }

            return true;
        }


        /// <summary>
        /// Check and return polygon intersections
        /// </summary>
        /// <param name="polygon1"></param>
        /// <param name="polygon2"></param>
        /// <param name="intersections"></param>
        /// <returns></returns>
        private bool VerticesIntersect(Point2DList polygon1, Point2DList polygon2, out List<EdgeIntersectInfo> intersections)
        {
            intersections = new List<EdgeIntersectInfo>();
            double epsilon = Math.Min(polygon1.Epsilon, polygon2.Epsilon);

            // Iterate through polygon1's edges
            for (int i = 0; i < polygon1.Count; i++)
            {
                // Get edge vertices
                Point2D p1 = polygon1[i];
                Point2D p2 = polygon1[polygon1.NextIndex(i)];

                // Get intersections between this edge and polygon2
                for (int j = 0; j < polygon2.Count; j++)
                {
                    Point2D point = new Point2D();

                    Point2D p3 = polygon2[j];
                    Point2D p4 = polygon2[polygon2.NextIndex(j)];

                    // Check if the edges intersect
                    if (TriangulationUtil.LinesIntersect2D(p1, p2, p3, p4, ref point, epsilon))
                    {
                        // Rounding is not needed since we compare using an epsilon.
                        //// Here, we round the returned intersection point to its nearest whole number.
                        //// This prevents floating point anomolies where 99.9999-> is returned instead of 100.
                        //point = new Point2D((float)Math.Round(point.X, 0), (float)Math.Round(point.Y, 0));
                        // Record the intersection
                        intersections.Add(new EdgeIntersectInfo(new Edge(p1, p2), new Edge(p3, p4), point));
                    }
                }
            }

            // true if any intersections were found.
            return (intersections.Count > 0);
        }


        /// <summary>
        /// * ref: http://ozviz.wasp.uwa.edu.au/~pbourke/geometry/insidepoly/  - Solution 2 
        /// * Compute the sum of the angles made between the test point and each pair of points making up the polygon. 
        /// * If this sum is 2pi then the point is an interior point, if 0 then the point is an exterior point. 
        /// </summary>
        public bool PointInPolygonAngle(Point2D point, Point2DList polygon)
        {
            double angle = 0;

            // Iterate through polygon's edges
            for (int i = 0; i < polygon.Count; i++)
            {
                // Get points
                Point2D p1 = polygon[i] - point;
                Point2D p2 = polygon[polygon.NextIndex(i)] - point;

                angle += VectorAngle(p1, p2);
            }

            if (Math.Abs(angle) < Math.PI)
            {
                return false;
            }

            return true;
        }


        /// <summary>
        /// Return the angle between two vectors on a plane
        /// The angle is from vector 1 to vector 2, positive anticlockwise
        /// The result is between -pi -> pi
        /// </summary>
        public double VectorAngle(Point2D p1, Point2D p2)
        {
            double theta1 = Math.Atan2(p1.Y, p1.X);
            double theta2 = Math.Atan2(p2.Y, p2.X);
            double dtheta = theta2 - theta1;
            while (dtheta > Math.PI)
            {
                dtheta -= (2.0 * Math.PI);
            }
            while (dtheta < -Math.PI)
            {
                dtheta += (2.0 * Math.PI);
            }

            return (dtheta);
        }

    }

}

// ----------------------------------------------------------------------
// ConstrainedPointSet.cs

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

// using System;
// using System.Collections.Generic;
// using System.Text;


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

// ----------------------------------------------------------------------
// PointSet.cs

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

// using System;
// using System.Collections.Generic;
// using System.Text;


namespace Poly2Tri
{
    public class PointSet : Point2DList, ITriangulatable, IEnumerable<TriangulationPoint>, IList<TriangulationPoint>
    {
        protected Dictionary<uint, TriangulationPoint> mPointMap = new Dictionary<uint, TriangulationPoint>();
        public IList<TriangulationPoint> Points { get { return this; } private set { } }
        public IList<DelaunayTriangle> Triangles { get; private set; }

        public string FileName { get; set; }
        public bool DisplayFlipX { get; set; }
        public bool DisplayFlipY { get; set; }
        public float DisplayRotate { get; set; }

        protected double mPrecision = TriangulationPoint.kVertexCodeDefaultPrecision;
        public double Precision { get { return mPrecision; } set { mPrecision = value; } }

        public double MinX { get { return mBoundingBox.MinX; } }
        public double MaxX { get { return mBoundingBox.MaxX; } }
        public double MinY { get { return mBoundingBox.MinY; } }
        public double MaxY { get { return mBoundingBox.MaxY; } }
        public Rect2D Bounds { get { return mBoundingBox; } }

        public virtual TriangulationMode TriangulationMode { get { return TriangulationMode.Unconstrained; } }

        public new TriangulationPoint this[int index]
        {
            get { return mPoints[index] as TriangulationPoint; }
            set { mPoints[index] = value; }
        }


        public PointSet(List<TriangulationPoint> bounds)
        {
            //Points = new List<TriangulationPoint>();
            foreach (TriangulationPoint p in bounds)
            {
                Add(p, -1, false);

                // Only the initial points are counted toward min/max x/y as they 
                // are considered to be the boundaries of the point-set
                mBoundingBox.AddPoint(p);
            }
            mEpsilon = CalculateEpsilon();
            mWindingOrder = WindingOrderType.Unknown;   // not valid for a point-set
        }


        IEnumerator<TriangulationPoint> IEnumerable<TriangulationPoint>.GetEnumerator()
        {
            return new TriangulationPointEnumerator(mPoints);
        }


        public int IndexOf(TriangulationPoint p)
        {
            return mPoints.IndexOf(p);
        }


        public override void Add(Point2D p)
        {
            Add(p as TriangulationPoint, -1, false);
        }

        public virtual void Add(TriangulationPoint p)
        {
            Add(p, -1, false);
        }


        protected override void Add(Point2D p, int idx, bool constrainToBounds)
        {
            Add(p as TriangulationPoint, idx, constrainToBounds);
        }


        protected bool Add(TriangulationPoint p, int idx, bool constrainToBounds)
        {
            if (p == null)
            {
                return false;
            }

            if (constrainToBounds)
            {
                ConstrainPointToBounds(p);
            }

            // if we already have an instance of the point, then don't bother inserting it again as duplicate points
            // will actually cause some real problems later on.   Still return true though to indicate that the point
            // is successfully "added"
            if (mPointMap.ContainsKey(p.VertexCode))
            {
                return true;
            }
            mPointMap.Add(p.VertexCode, p);

            if (idx < 0)
            {
                mPoints.Add(p);
            }
            else
            {
                mPoints.Insert(idx, p);
            }

            return true;
        }


        public override void AddRange(IEnumerator<Point2D> iter, WindingOrderType windingOrder)
        {
            if (iter == null)
            {
                return;
            }

            iter.Reset();
            while (iter.MoveNext())
            {
                Add(iter.Current);
            }
        }

        
        public virtual bool AddRange(List<TriangulationPoint> points)
        {
            bool bOK = true;
            foreach (TriangulationPoint p in points)
            {
                bOK = Add(p, -1, false) && bOK;
            }

            return bOK;
        }


        public bool TryGetPoint(double x, double y, out TriangulationPoint p)
        {
            uint vc = TriangulationPoint.CreateVertexCode(x, y, Precision);
            if (mPointMap.TryGetValue(vc, out p))
            {
                return true;
            }

            return false;
        }


        //public override void Insert(int idx, Point2D item)
        //{
        //    Add(item, idx, true);
        //}


        public void Insert(int idx, TriangulationPoint item)
        {
            mPoints.Insert(idx, item);
        }


        public override bool Remove(Point2D p)
        {
            return mPoints.Remove(p);
        }


        public bool Remove(TriangulationPoint p)
        {
            return mPoints.Remove(p);
        }


        public override void RemoveAt(int idx)
        {
            if (idx < 0 || idx >= Count)
            {
                return;
            }
            mPoints.RemoveAt(idx);
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


        // returns true if the point is changed, false if the point is unchanged
        protected bool ConstrainPointToBounds(Point2D p)
        {
            double oldX = p.X;
            double oldY = p.Y;
            p.X = Math.Max(MinX, p.X);
            p.X = Math.Min(MaxX, p.X);
            p.Y = Math.Max(MinY, p.Y);
            p.Y = Math.Min(MaxY, p.Y);

            return (p.X != oldX) || (p.Y != oldY);
        }


        protected bool ConstrainPointToBounds(TriangulationPoint p)
        {
            double oldX = p.X;
            double oldY = p.Y;
            p.X = Math.Max(MinX, p.X);
            p.X = Math.Min(MaxX, p.X);
            p.Y = Math.Max(MinY, p.Y);
            p.Y = Math.Min(MaxY, p.Y);

            return (p.X != oldX) || (p.Y != oldY);
        }

        
        public virtual void AddTriangle(DelaunayTriangle t)
        {
            Triangles.Add(t);
        }


        public void AddTriangles(IEnumerable<DelaunayTriangle> list)
        {
            foreach (var tri in list)
            {
                AddTriangle(tri);
            }
        }


        public void ClearTriangles()
        {
            Triangles.Clear();
        }


        public virtual bool Initialize()
        {
            return true;
        }


        public virtual void Prepare(TriangulationContext tcx)
        {
            if (Triangles == null)
            {
                Triangles = new List<DelaunayTriangle>(Points.Count);
            }
            else
            {
                Triangles.Clear();
            }
            tcx.Points.AddRange(Points);
        }
    }
}

// ----------------------------------------------------------------------
// PointGenerator.cs

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

// using System;
// using System.Collections.Generic;

namespace Poly2Tri
{
    public class PointGenerator
    {
        static readonly Random RNG = new Random();


        public static List<TriangulationPoint> UniformDistribution(int n, double scale)
        {
            List<TriangulationPoint> points = new List<TriangulationPoint>();
            for (int i = 0; i < n; i++)
            {
                points.Add(new TriangulationPoint(scale * (0.5 - RNG.NextDouble()), scale * (0.5 - RNG.NextDouble())));
            }

            return points;
        }


        public static List<TriangulationPoint> UniformGrid(int n, double scale)
        {
            double x = 0;
            double size = scale / n;
            double halfScale = 0.5 * scale;

            List<TriangulationPoint> points = new List<TriangulationPoint>();
            for (int i = 0; i < n + 1; i++)
            {
                x = halfScale - i * size;
                for (int j = 0; j < n + 1; j++)
                {
                    points.Add(new TriangulationPoint(x, halfScale - j * size));
                }
            }

            return points;
        }
    }
}

// ----------------------------------------------------------------------
// PolygonGenerator.cs

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

// using System;

namespace Poly2Tri
{
    public class PolygonGenerator
    {
        static readonly Random RNG = new Random();

        private static double PI_2 = 2.0 * Math.PI;

        public static Polygon RandomCircleSweep(double scale, int vertexCount)
        {
            PolygonPoint point;
            PolygonPoint[] points;
            double radius = scale / 4;

            points = new PolygonPoint[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                do
                {
                    if (i % 250 == 0)
                    {
                        radius += scale / 2 * (0.5 - RNG.NextDouble());
                    }
                    else if (i % 50 == 0)
                    {
                        radius += scale / 5 * (0.5 - RNG.NextDouble());
                    }
                    else
                    {
                        radius += 25 * scale / vertexCount * (0.5 - RNG.NextDouble());
                    }
                    radius = radius > scale / 2 ? scale / 2 : radius;
                    radius = radius < scale / 10 ? scale / 10 : radius;
                } while (radius < scale / 10 || radius > scale / 2);
                point = new PolygonPoint(radius * Math.Cos((PI_2 * i) / vertexCount), radius * Math.Sin((PI_2 * i) / vertexCount));
                points[i] = point;
            }
            return new Polygon(points);
        }

        public static Polygon RandomCircleSweep2(double scale, int vertexCount)
        {
            PolygonPoint point;
            PolygonPoint[] points;
            double radius = scale / 4;

            points = new PolygonPoint[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                do
                {
                    radius += scale / 5 * (0.5 - RNG.NextDouble());
                    radius = radius > scale / 2 ? scale / 2 : radius;
                    radius = radius < scale / 10 ? scale / 10 : radius;
                } while (radius < scale / 10 || radius > scale / 2);
                point = new PolygonPoint(radius * Math.Cos((PI_2 * i) / vertexCount), radius * Math.Sin((PI_2 * i) / vertexCount));
                points[i] = point;
            }
            return new Polygon(points);
        }
    }
}

// ----------------------------------------------------------------------
// TriangulationUtil.cs

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

// using System;
// using System.Collections.Generic;
// using System.Text;


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

// ----------------------------------------------------------------------
// FixedArray3.cs

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

// using System;
// using System.Collections;
// using System.Collections.Generic;

namespace Poly2Tri
{
    public struct FixedArray3<T> : IEnumerable<T> where T : class
    {
        public T _0, _1, _2;
        public T this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return _0;
                    case 1:
                        return _1;
                    case 2:
                        return _2;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        _0 = value;
                        break;
                    case 1:
                        _1 = value;
                        break;
                    case 2:
                        _2 = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }


        public bool Contains(T value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] != null && this[i].Equals(value))
                {
                    return true;
                }
            }

            return false;
        }


        public int IndexOf(T value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] != null && this[i].Equals(value))
                {
                    return i;
                }
            }

            return -1;
        }

        
        public void Clear()
        {
            _0 = _1 = _2 = null;
        }

        
        public void Clear(T value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] != null && this[i].Equals(value))
                {
                    this[i] = null;
                }
            }
        }


        private IEnumerable<T> Enumerate()
        {
            for (int i = 0; i < 3; ++i)
            {
                yield return this[i];
            }
        }

        
        public IEnumerator<T> GetEnumerator() { return Enumerate().GetEnumerator(); }

        
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}

// ----------------------------------------------------------------------
// FixedBitArray3.cs

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

// using System;
// using System.Collections;
// using System.Collections.Generic;

namespace Poly2Tri
{
    public struct FixedBitArray3 : IEnumerable<bool>
    {
        public bool _0, _1, _2;
        public bool this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return _0;
                    case 1:
                        return _1;
                    case 2:
                        return _2;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        _0 = value;
                        break;
                    case 1:
                        _1 = value;
                        break;
                    case 2:
                        _2 = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }


        public bool Contains(bool value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
        
        
        public int IndexOf(bool value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        
        public void Clear()
        {
            _0 = _1 = _2 = false;
        }

        
        public void Clear(bool value)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (this[i] == value)
                {
                    this[i] = false;
                }
            }
        }


        private IEnumerable<bool> Enumerate()
        {
            for (int i = 0; i < 3; ++i)
            {
                yield return this[i];
            }
        }

        
        public IEnumerator<bool> GetEnumerator() { return Enumerate().GetEnumerator(); }

        
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}

// ----------------------------------------------------------------------
// MathUtil.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Poly2Tri
{
    public class MathUtil
    {
        public static double EPSILON = 1e-12;


        public static bool AreValuesEqual(double val1, double val2)
        {
            return AreValuesEqual(val1, val2, EPSILON);
        }


        public static bool AreValuesEqual(double val1, double val2, double tolerance)
        {
            if (val1 >= (val2 - tolerance) && val1 <= (val2 + tolerance))
            {
                return true;
            }

            return false;
        }


        public static bool IsValueBetween(double val, double min, double max, double tolerance)
        {
            if (min > max)
            {
                double tmp = min;
                min = max;
                max = tmp;
            }
            if ((val + tolerance) >= min && (val - tolerance) <= max)
            {
                return true;
            }

            return false;
        }


        public static double RoundWithPrecision(double f, double precision)
        {
            if (precision < 0.0)
            {
                return f;
            }

            double mul = Math.Pow(10.0, precision);
            double fTemp = Math.Floor(f * mul) / mul;

            return fTemp;
        }


        public static double Clamp(double a, double low, double high)
        {
            return Math.Max(low, Math.Min(a, high));
        }


        public static void Swap<T>(ref T a, ref T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }


        public static uint Jenkins32Hash(byte[] data, uint nInitialValue)
        {
            foreach (byte b in data)
            {
                nInitialValue += (uint)b;
                nInitialValue += (nInitialValue << 10);
                nInitialValue += (nInitialValue >> 6);
            }

            nInitialValue += (nInitialValue << 3);
            nInitialValue ^= (nInitialValue >> 11);
            nInitialValue += (nInitialValue << 15);

            return nInitialValue;
        }
    }
}

// ----------------------------------------------------------------------
// Point2D.cs

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

// using System;
// using System.Collections;
// using System.Collections.Generic;


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

// ----------------------------------------------------------------------
// Point2DList.cs

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

/*
 * The Following notice applies to the Methods CheckPolygon and 
 * MergeParallelEdges.   Both are altered only enough to convert to C#
 * and take advantage of some of C#'s language features.   Any errors
 * are thus mine from the conversion and not Eric's.
 * 
 * Copyright (c) 2007 Eric Jordan
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty.  In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 * */


// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Text;


namespace Poly2Tri
{
    public class Point2DList : IEnumerable<Point2D>, IList<Point2D> // : List<Point2D>
    {
        public static readonly int kMaxPolygonVertices = 100000; // adjust to suit...

        /// A small length used as a collision and constraint tolerance. Usually it is
        /// chosen to be numerically significant, but visually insignificant.
        public static readonly double kLinearSlop = 0.005;

        /// A small angle used as a collision and constraint tolerance. Usually it is
        /// chosen to be numerically significant, but visually insignificant.
        public static readonly double kAngularSlop = (2.0 / (180.0 * Math.PI));

        public enum WindingOrderType
        {
            CW,
            CCW,
            Unknown,

            Default = CCW,
        }

        [Flags]
        public enum PolygonError : uint
        {
            None = 0,
            NotEnoughVertices           = 1 << 0,
            NotConvex                   = 1 << 1,
            NotSimple                   = 1 << 2,
            AreaTooSmall                = 1 << 3,
            SidesTooCloseToParallel     = 1 << 4,
            TooThin                     = 1 << 5,
            Degenerate                  = 1 << 6,
            Unknown                     = 1 << 30,
        }


        protected List<Point2D> mPoints = new List<Point2D>();
        protected Rect2D mBoundingBox = new Rect2D();
        protected WindingOrderType mWindingOrder = WindingOrderType.Unknown;
        protected double mEpsilon = MathUtil.EPSILON;    // Epsilon is a function of the size of the bounds of the polygon

        public Rect2D BoundingBox { get { return mBoundingBox; } }
        public WindingOrderType WindingOrder
        {
            get { return mWindingOrder; }
            set
            {
                if (mWindingOrder == WindingOrderType.Unknown)
                {
                    mWindingOrder = CalculateWindingOrder();
                }
                if (value != mWindingOrder)
                {
                    mPoints.Reverse();
                    mWindingOrder = value;
                }
            }
        }
        public double Epsilon { get { return mEpsilon; } }
        public Point2D this[int index]
        {
            get { return mPoints[index]; }
            set { mPoints[index] = value; }
        }
        public int Count { get { return mPoints.Count; } }
        public virtual bool IsReadOnly { get { return false; } }


        public Point2DList()
        {
        }


        public Point2DList(int capacity)
        {
            mPoints.Capacity = capacity;
        }


        public Point2DList(IList<Point2D> l)
        {
            AddRange(l.GetEnumerator(), WindingOrderType.Unknown);
        }


        public Point2DList(Point2DList l)
        {
            int numPoints = l.Count;
            for (int i = 0; i < numPoints; ++i)
            {
                mPoints.Add(l[i]);
            }
            mBoundingBox.Set(l.BoundingBox);
            mEpsilon = l.Epsilon;
            mWindingOrder = l.WindingOrder;
        }


        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                builder.Append(this[i].ToString());
                if (i < Count - 1)
                {
                    builder.Append(" ");
                }
            }
            return builder.ToString();
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return mPoints.GetEnumerator();
        }

        
        IEnumerator<Point2D> IEnumerable<Point2D>.GetEnumerator()
        {
            return new Point2DEnumerator(mPoints);
        }
        

        public void Clear()
        {
            mPoints.Clear();
            mBoundingBox.Clear();
            mEpsilon = MathUtil.EPSILON;
            mWindingOrder = WindingOrderType.Unknown;
        }


        public int IndexOf(Point2D p)
        {
            return mPoints.IndexOf(p);
        }


        public virtual void Add(Point2D p)
        {
            Add(p, -1, true);
        }


        protected virtual void Add(Point2D p, int idx, bool bCalcWindingOrderAndEpsilon)
        {
            if (idx < 0)
            {
                mPoints.Add(p);
            }
            else
            {
                mPoints.Insert(idx, p);
            }
            mBoundingBox.AddPoint(p);
            if (bCalcWindingOrderAndEpsilon)
            {
                if (mWindingOrder == WindingOrderType.Unknown)
                {
                    mWindingOrder = CalculateWindingOrder();
                }
                mEpsilon = CalculateEpsilon();
            }
        }


        public virtual void AddRange(Point2DList l)
        {
            AddRange(l.mPoints.GetEnumerator(), l.WindingOrder);
        }


        public virtual void AddRange(IEnumerator<Point2D> iter, WindingOrderType windingOrder)
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
                if (!bAddedFirst)
                {
                    bAddedFirst = true;
                    mPoints.Add(iter.Current);
                }
                else if (bReverseReadOrder)
                {
                    mPoints.Insert(startCount, iter.Current);
                }
                else
                {
                    mPoints.Add(iter.Current);
                }
                mBoundingBox.AddPoint(iter.Current);
            }
            if (mWindingOrder == WindingOrderType.Unknown && windingOrder == WindingOrderType.Unknown)
            {
                mWindingOrder = CalculateWindingOrder();
            }
            mEpsilon = CalculateEpsilon();
        }


        public virtual void Insert(int idx, Point2D item)
        {
            Add(item, idx, true);
        }


        public virtual bool Remove(Point2D p)
        {
            if (mPoints.Remove(p))
            {
                CalculateBounds();
                mEpsilon = CalculateEpsilon();
                return true;
            }

            return false;
        }


        public virtual void RemoveAt(int idx)
        {
            if (idx < 0 || idx >= Count)
            {
                return;
            }
            mPoints.RemoveAt(idx);
            CalculateBounds();
            mEpsilon = CalculateEpsilon();
        }


        public virtual void RemoveRange(int idxStart, int count)
        {
            if (idxStart < 0 || idxStart >= Count)
            {
                return;
            }
            if (count == 0)
            {
                return;
            }

            mPoints.RemoveRange(idxStart, count);
            CalculateBounds();
            mEpsilon = CalculateEpsilon();
        }


        public bool Contains(Point2D p)
        {
            return mPoints.Contains(p);
        }


        public void CopyTo(Point2D[] array, int arrayIndex)
        {
            int numElementsToCopy = Math.Min(Count, array.Length - arrayIndex);
            for (int i = 0; i < numElementsToCopy; ++i)
            {
                array[arrayIndex + i] = mPoints[i];
            }
        }


        public void CalculateBounds()
        {
            mBoundingBox.Clear();
            foreach (Point2D pt in mPoints)
            {
                mBoundingBox.AddPoint(pt);
            }
        }


        public double CalculateEpsilon()
        {
            return Math.Max(Math.Min(mBoundingBox.Width, mBoundingBox.Height) * 0.001f, MathUtil.EPSILON);
        }


        public WindingOrderType CalculateWindingOrder()
        {
            // the sign of the 'area' of the polygon is all we are interested in.
            double area = GetSignedArea();
            if (area < 0.0)
            {
                return WindingOrderType.CW;
            }
            else if (area > 0.0)
            {
                return WindingOrderType.CCW;
            }

            // error condition - not even verts to calculate, non-simple poly, etc.
            return WindingOrderType.Unknown;
        }


        public int NextIndex(int index)
        {
            if (index == Count - 1)
            {
                return 0;
            }
            return index + 1;
        }


        /// <summary>
        /// Gets the previous index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public int PreviousIndex(int index)
        {
            if (index == 0)
            {
                return Count - 1;
            }
            return index - 1;
        }


        /// <summary>
        /// Gets the signed area.
        /// </summary>
        /// <returns></returns>
        public double GetSignedArea()
        {
            double area = 0.0;
            for (int i = 0; i < Count; i++)
            {
                int j = (i + 1) % Count;
                area += this[i].X * this[j].Y;
                area -= this[i].Y * this[j].X;
            }
            area /= 2.0f;

            return area;
        }


        /// <summary>
        /// Gets the area.
        /// </summary>
        /// <returns></returns>
        public double GetArea()
        {
            int i;
            double area = 0;

            for (i = 0; i < Count; i++)
            {
                int j = (i + 1) % Count;
                area += this[i].X * this[j].Y;
                area -= this[i].Y * this[j].X;
            }
            area /= 2.0f;
            return (area < 0 ? -area : area);
        }


        /// <summary>
        /// Gets the centroid.
        /// </summary>
        /// <returns></returns>
        public Point2D GetCentroid()
        {
            // Same algorithm is used by Box2D

            Point2D c = new Point2D();
            double area = 0.0f;

            const double inv3 = 1.0 / 3.0;
            Point2D pRef = new Point2D();
            for (int i = 0; i < Count; ++i)
            {
                // Triangle vertices.
                Point2D p1 = pRef;
                Point2D p2 = this[i];
                Point2D p3 = i + 1 < Count ? this[i + 1] : this[0];

                Point2D e1 = p2 - p1;
                Point2D e2 = p3 - p1;

                double D = Point2D.Cross(e1, e2);

                double triangleArea = 0.5f * D;
                area += triangleArea;

                // Area weighted centroid
                c += triangleArea * inv3 * (p1 + p2 + p3);
            }

            // Centroid
            c *= 1.0f / area;
            return c;
        }


        //    /// <summary>
        /// Translates the vertices with the specified vector.
        /// </summary>
        /// <param name="vector">The vector.</param>
        public void Translate(Point2D vector)
        {
            for (int i = 0; i < Count; i++)
            {
                this[i] += vector;
            }
        }


        /// <summary>
        /// Scales the vertices with the specified vector.
        /// </summary>
        /// <param name="value">The Value.</param>
        public void Scale(Point2D value)
        {
            for (int i = 0; i < Count; i++)
            {
                this[i] *= value;
            }
        }


        /// <summary>
        /// Rotate the vertices with the defined value in radians.
        /// </summary>
        /// <param name="value">The amount to rotate by in radians.</param>
        public void Rotate(double radians)
        {
            // kickin' it old-skool since I don't want to create a Matrix class for now.
            double cosr = Math.Cos(radians);
            double sinr = Math.Sin(radians);
            foreach (Point2D p in mPoints)
            {
                double xold = p.X;
                p.X = xold * cosr - p.Y * sinr;
                p.Y = xold * sinr + p.Y * cosr;
            }
        }

        // A degenerate polygon is one in which some vertex lies on an edge joining two other vertices. 
        // This can happen in one of two ways: either the vertices V(i-1), V(i), and V(i+1) can be collinear or
        // the vertices V(i) and V(i+1) can overlap (fail to be distinct). In either of these cases, our polygon of
        // n vertices will appear to have n - 1 or fewer -- it will have "degenerated" from an n-gon to an (n-1)-gon.
        // (In the case of triangles, this will result in either a line segment or a point.) 
        public bool IsDegenerate()
        {
            if (Count < 3)
            {
                return false;
            }
            if (Count < 3)
            {
                return false;
            }
            for (int k = 0; k < Count; ++k)
            {
                int j = PreviousIndex(k);
                if (mPoints[j].Equals(mPoints[k], Epsilon))
                {
                    return true;
                }
                int i = PreviousIndex(j);
                Orientation orientation = TriangulationUtil.Orient2d(mPoints[i], mPoints[j], mPoints[k]);
                if (orientation == Orientation.Collinear)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Assuming the polygon is simple; determines whether the polygon is convex.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is convex; otherwise, <c>false</c>.
        /// </returns>
        public bool IsConvex()
        {
            bool isPositive = false;

            for (int i = 0; i < Count; ++i)
            {
                int lower = (i == 0) ? (Count - 1) : (i - 1);
                int middle = i;
                int upper = (i == Count - 1) ? (0) : (i + 1);

                double dx0 = this[middle].X - this[lower].X;
                double dy0 = this[middle].Y - this[lower].Y;
                double dx1 = this[upper].X - this[middle].X;
                double dy1 = this[upper].Y - this[middle].Y;

                double cross = dx0 * dy1 - dx1 * dy0;

                // Cross product should have same sign
                // for each vertex if poly is convex.
                bool newIsP = (cross >= 0) ? true : false;
                if (i == 0)
                {
                    isPositive = newIsP;
                }
                else if (isPositive != newIsP)
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Check for edge crossings
        /// </summary>
        /// <returns></returns>
        public bool IsSimple()
        {
            for (int i = 0; i < Count; ++i)
            {
                int iplus = NextIndex(i);
                for (int j = i + 1; j < Count; ++j)
                {
                    int jplus = NextIndex(j);
                    Point2D temp = null;
                    if (TriangulationUtil.LinesIntersect2D(mPoints[i], mPoints[iplus], mPoints[j], mPoints[jplus], ref temp, mEpsilon))
                    {
                        return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Checks if polygon is valid for use in Box2d engine.
        /// Last ditch effort to ensure no invalid polygons are
        /// added to world geometry.
        ///
        /// Performs a full check, for simplicity, convexity,
        /// orientation, minimum angle, and volume.  This won't
        /// be very efficient, and a lot of it is redundant when
        /// other tools in this section are used.
        ///
        /// From Eric Jordan's convex decomposition library
        /// </summary>
        /// <param name="printErrors"></param>
        /// <returns></returns>
        public PolygonError CheckPolygon()
        {
            PolygonError error = PolygonError.None;
            if (Count < 3 || Count > Point2DList.kMaxPolygonVertices)
            {
                error |= PolygonError.NotEnoughVertices;
                // no other tests will be valid at this point, so just return
                return error;
            }
            if (IsDegenerate())
            {
                error |= PolygonError.Degenerate;
            }
            //bool bIsConvex = IsConvex();
            //if (!IsConvex())
            //{
            //    error |= PolygonError.NotConvex;
            //}
            if (!IsSimple())
            {
                error |= PolygonError.NotSimple;
            }
            if (GetArea() < MathUtil.EPSILON)
            {
                error |= PolygonError.AreaTooSmall;
            }

            // the following tests don't make sense if the polygon is not simple
            if ((error & PolygonError.NotSimple) != PolygonError.NotSimple)
            {
                bool bReversed = false;
                WindingOrderType expectedWindingOrder = WindingOrderType.CCW;
                WindingOrderType reverseWindingOrder = WindingOrderType.CW;
                if (WindingOrder == reverseWindingOrder)
                {
                    WindingOrder = expectedWindingOrder;
                    bReversed = true;
                }

                //Compute normals
                Point2D[] normals = new Point2D[Count];
                Point2DList vertices = new Point2DList(Count);
                for (int i = 0; i < Count; ++i)
                {
                    vertices.Add(new Point2D(this[i].X, this[i].Y));
                    int i1 = i;
                    int i2 = NextIndex(i);
                    Point2D edge = new Point2D(this[i2].X - this[i1].X, this[i2].Y - this[i1].Y);
                    normals[i] = Point2D.Perpendicular(edge, 1.0);
                    normals[i].Normalize();
                }

                //Required side checks
                for (int i = 0; i < Count; ++i)
                {
                    int iminus = PreviousIndex(i);

                    //Parallel sides check
                    double cross = Point2D.Cross(normals[iminus], normals[i]);
                    cross = MathUtil.Clamp(cross, -1.0f, 1.0f);
                    float angle = (float)Math.Asin(cross);
                    if (Math.Abs(angle) <= Point2DList.kAngularSlop)
                    {
                        error |= PolygonError.SidesTooCloseToParallel;
                        break;
                    }

                    // For some reason, the following checks do not seem to work
                    // correctly in all cases - they return false positives.
                //    //Too skinny check - only valid for convex polygons
                //    if (bIsConvex)
                //    {
                //        for (int j = 0; j < Count; ++j)
                //        {
                //            if (j == i || j == NextIndex(i))
                //            {
                //                continue;
                //            }
                //            Point2D testVector = vertices[j] - vertices[i];
                //            testVector.Normalize();
                //            double s = Point2D.Dot(testVector, normals[i]);
                //            if (s >= -Point2DList.kLinearSlop)
                //            {
                //                error |= PolygonError.TooThin;
                //            }
                //        }

                //        Point2D centroid = vertices.GetCentroid();
                //        Point2D n1 = normals[iminus];
                //        Point2D n2 = normals[i];
                //        Point2D v = vertices[i] - centroid;

                //        Point2D d = new Point2D();
                //        d.X = Point2D.Dot(n1, v); // - toiSlop;
                //        d.Y = Point2D.Dot(n2, v); // - toiSlop;

                //        // Shifting the edge inward by toiSlop should
                //        // not cause the plane to pass the centroid.
                //        if ((d.X < 0.0f) || (d.Y < 0.0f))
                //        {
                //            error |= PolygonError.TooThin;
                //        }
                //    }
                }

                if (bReversed)
                {
                    WindingOrder = reverseWindingOrder;
                }
            }

            //if (error != PolygonError.None)
            //{
            //    Console.WriteLine("Found invalid polygon: {0} {1}\n", Point2DList.GetErrorString(error), this.ToString());
            //}

            return error;
        }


        public static string GetErrorString(PolygonError error)
        {
            StringBuilder sb = new StringBuilder(256);
            if (error == PolygonError.None)
            {
                sb.AppendFormat("No errors.\n");
            }
            else
            {
                if ((error & PolygonError.NotEnoughVertices) == PolygonError.NotEnoughVertices)
                {
                    sb.AppendFormat("NotEnoughVertices: must have between 3 and {0} vertices.\n", kMaxPolygonVertices);
                }
                if ((error & PolygonError.NotConvex) == PolygonError.NotConvex)
                {
                    sb.AppendFormat("NotConvex: Polygon is not convex.\n");
                }
                if ((error & PolygonError.NotSimple) == PolygonError.NotSimple)
                {
                    sb.AppendFormat("NotSimple: Polygon is not simple (i.e. it intersects itself).\n");
                }
                if ((error & PolygonError.AreaTooSmall) == PolygonError.AreaTooSmall)
                {
                    sb.AppendFormat("AreaTooSmall: Polygon's area is too small.\n");
                }
                if ((error & PolygonError.SidesTooCloseToParallel) == PolygonError.SidesTooCloseToParallel)
                {
                    sb.AppendFormat("SidesTooCloseToParallel: Polygon's sides are too close to parallel.\n");
                }
                if ((error & PolygonError.TooThin) == PolygonError.TooThin)
                {
                    sb.AppendFormat("TooThin: Polygon is too thin or core shape generation would move edge past centroid.\n");
                }
                if ((error & PolygonError.Degenerate) == PolygonError.Degenerate)
                {
                    sb.AppendFormat("Degenerate: Polygon is degenerate (contains collinear points or duplicate coincident points).\n");
                }
                if ((error & PolygonError.Unknown) == PolygonError.Unknown)
                {
                    sb.AppendFormat("Unknown: Unknown Polygon error!.\n");
                }
            }

            return sb.ToString();
        }


        /// <summary>
        /// Removes duplicate points that lie next to each other in the list
        /// </summary>
        public void RemoveDuplicateNeighborPoints()
        {
            int numPoints = Count;
            int i = numPoints - 1;
            int j = 0;
            while (numPoints > 1 && j < numPoints)
            {
                if(mPoints[i].Equals(mPoints[j]))
                {
                    int idxToRemove = Math.Max(i, j);
                    mPoints.RemoveAt(idxToRemove);
                    --numPoints;
                    if (i >= numPoints)
                    {
                        // can happen if first element in list is deleted...
                        i = numPoints - 1;
                    }
                    // don't increment i, j in this case because we want to check i against the new value at j
                }
                else
                {
                    i = NextIndex(i);
                    ++j;  // intentionally not wrapping value of j so we have a valid end-point for the loop
                }
            }
        }


        /// <summary>
        /// Removes all collinear points on the polygon.
        /// Has a default bias of 0
        /// </summary>
        /// <param name="polygon">The polygon that needs simplification.</param>
        /// <returns>A simplified polygon.</returns>
        public void Simplify()
        {
            Simplify(0.0);
        }


        /// <summary>
        /// Removes all collinear points on the polygon.   Note that this is NOT safe to run on a complex
        /// polygon as it will remove points that it should not.   For example, consider this polygon:
        /// 
        ///           2
        ///           +
        ///          / \
        ///         /   \
        ///        /     \
        /// 0 +---+-------+
        ///       3       1
        /// 
        /// This algorithm would delete point 3, leaving you with the polygon 0,1,2 - definitely NOT the correct
        /// polygon.  Caveat Emptor!
        /// 
        /// </summary>
        /// <param name="polygon">The polygon that needs simplification.</param>
        /// <param name="bias">The distance bias between points. Points closer than this will be 'joined'.</param>
        /// <returns>A simplified polygon.</returns>
        public void Simplify(double bias)
        {
            //We can't simplify polygons under 3 vertices
            if (Count < 3)
            {
                return;
            }

//#if DEBUG
//            if (!IsSimple())
//            {
//                throw new Exception("Do not run Simplify on a non-simple polygon!");
//            }
//#endif

            int curr = 0;
            int numVerts = Count;
            double biasSquared = bias * bias;
            while (curr < numVerts && numVerts >= 3)
            {
                int prevId = PreviousIndex(curr);
                int nextId = NextIndex(curr);

                Point2D prev = this[prevId];
                Point2D current = this[curr];
                Point2D next = this[nextId];

                //If they are closer than the bias, continue
                if ((prev - current).MagnitudeSquared() <= biasSquared)
                {
                    RemoveAt(curr);
                    --numVerts;
                    continue;
                }

                //If they collinear, continue
                Orientation orientation = TriangulationUtil.Orient2d(prev, current, next);
                if (orientation == Orientation.Collinear)
                {
                    RemoveAt(curr);
                    --numVerts;
                    continue;
                }

                ++curr;
            }
        }


        // From Eric Jordan's convex decomposition library
        /// <summary>
        /// Merges all parallel edges in the list of vertices
        /// </summary>
        /// <param name="tolerance"></param>
        public void MergeParallelEdges(double tolerance)
        {
            if (Count <= 3)
            {
                // Can't do anything useful here to a triangle
                return;
            }

            bool[] mergeMe = new bool[Count];
            int newNVertices = Count;

            //Gather points to process
            for (int i = 0; i < Count; ++i)
            {
                int lower = (i == 0) ? (Count - 1) : (i - 1);
                int middle = i;
                int upper = (i == Count - 1) ? (0) : (i + 1);

                double dx0 = this[middle].X - this[lower].X;
                double dy0 = this[middle].Y - this[lower].Y;
                double dx1 = this[upper].Y - this[middle].X;
                double dy1 = this[upper].Y - this[middle].Y;
                double norm0 = Math.Sqrt(dx0 * dx0 + dy0 * dy0);
                double norm1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);

                if (!(norm0 > 0.0 && norm1 > 0.0) && newNVertices > 3)
                {
                    //Merge identical points
                    mergeMe[i] = true;
                    --newNVertices;
                }

                dx0 /= norm0;
                dy0 /= norm0;
                dx1 /= norm1;
                dy1 /= norm1;
                double cross = dx0 * dy1 - dx1 * dy0;
                double dot = dx0 * dx1 + dy0 * dy1;

                if (Math.Abs(cross) < tolerance && dot > 0 && newNVertices > 3)
                {
                    mergeMe[i] = true;
                    --newNVertices;
                }
                else
                {
                    mergeMe[i] = false;
                }
            }

            if (newNVertices == Count || newNVertices == 0)
            {
                return;
            }

            int currIndex = 0;

            // Copy the vertices to a new list and clear the old
            Point2DList oldVertices = new Point2DList(this);
            Clear();

            for (int i = 0; i < oldVertices.Count; ++i)
            {
                if (mergeMe[i] || newNVertices == 0 || currIndex == newNVertices)
                {
                    continue;
                }

                if (currIndex >= newNVertices)
                {
                    throw new Exception("Point2DList::MergeParallelEdges - currIndex[ " + currIndex.ToString() + "] >= newNVertices[" + newNVertices + "]");
                }

                mPoints.Add(oldVertices[i]);
                mBoundingBox.AddPoint(oldVertices[i]);
                ++currIndex;
            }
            mWindingOrder = CalculateWindingOrder();
            mEpsilon = CalculateEpsilon();
        }


        /// <summary>
        /// Projects to axis.
        /// </summary>
        /// <param name="axis">The axis.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        public void ProjectToAxis(Point2D axis, out double min, out double max)
        {
            // To project a point on an axis use the dot product
            double dotProduct = Point2D.Dot(axis, this[0]);
            min = dotProduct;
            max = dotProduct;

            for (int i = 0; i < Count; i++)
            {
                dotProduct = Point2D.Dot(this[i], axis);
                if (dotProduct < min)
                {
                    min = dotProduct;
                }
                else
                {
                    if (dotProduct > max)
                    {
                        max = dotProduct;
                    }
                }
            }
        }
    }
}

// ----------------------------------------------------------------------
// Rect2D.cs

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

// using System;


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

