//#define T2U_TRIANGLES

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

// Given a TmxMap and TmxLayer, crank out a Clipper polytree solution
namespace Tiled2Unity
{
    using ClipperPolygon = List<ClipperLib.IntPoint>;
    using ClipperPolygons = List<List<ClipperLib.IntPoint>>;

    public class LayerClipper
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

            // Limit to polygon "type" that matches the collision layer name (unless we are overriding the whole layer to a specific Unity Layer Name)
            bool usingUnityLayerOverride = !String.IsNullOrEmpty(tmxLayer.UnityLayerOverrideName);

            // From the perspective of Clipper lines are polygons too
            // Closed paths == polygons
            // Open paths == lines
            var polygonGroups = from y in Enumerable.Range(0, tmxLayer.Height)
                                from x in Enumerable.Range(0, tmxLayer.Width)
                                let rawTileId = tmxLayer.GetRawTileIdAt(x, y)
                                where rawTileId != 0
                                let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                let tile = tmxMap.Tiles[tileId]
                                from polygon in tile.ObjectGroup.Objects
                                where (polygon as TmxHasPoints) != null
                                where  usingUnityLayerOverride || String.Compare(polygon.Type, tmxLayer.Name, true) == 0
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
            // Triangulate the solution polygon
            Geometry.TriangulateClipperSolution triangulation = new Geometry.TriangulateClipperSolution();
            List<PointF[]> triangles = triangulation.Triangulate(solution);
#if T2U_TRIANGLES
            // Force triangle output
            foreach (var tri in triangles)
            {
                yield return tri;
            }
#else
            // Group the triangles into convex polygons
            Geometry.ComposeConvexPolygons composition = new Geometry.ComposeConvexPolygons();
            List<PointF[]> polygons = composition.Compose(triangles);
            foreach (var poly in polygons)
            {
                yield return poly;
            }
#endif
        }

    }
}
