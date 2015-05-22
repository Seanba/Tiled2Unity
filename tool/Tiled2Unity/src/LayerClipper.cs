using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

using ClipperLib;

// Given a TmxMap and TmxLayer, crank out a Clipper polytree solution
namespace Tiled2Unity
{
    using ClipperPolygon = List<IntPoint>;
    using ClipperPolygons = List<List<IntPoint>>;

    class LayerClipper
    {
        // Break the map into smaller pieces to feed to Clipper
        private static readonly int GroupBySize = 10;

        // Note: Will need to work with this. We need Even Odd fill rules right now because winding order on polygons is not deterministic
        private static PolyFillType SubjectFillRule = PolyFillType.pftNonZero;
        private static PolyFillType ClipFillRule = PolyFillType.pftEvenOdd;

        // Need a method to transform points into our coordinate system (different between Windows and Unity)
        public delegate ClipperLib.IntPoint TransformPointFunc(float x, float y);
        public delegate void ProgressFunc(string progress);

        public static PolyTree ExecuteClipper(TmxMap tmxMap, TmxLayer tmxLayer, TransformPointFunc xfFunc, ProgressFunc progFunc)
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
                    progFunc(String.Format("Clipping '{0}' polygons: {1}%", tmxLayer.UniqueName, (groupIndex / (float)groupCount) * 100));
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
                    // Before we transform then we put all the points into local space relative to the tile
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
                fullClipper.AddPaths(Clipper.ClosedPathsFromPolyTree(solution), PolyType.ptSubject, true);
                fullClipper.AddPaths(Clipper.OpenPathsFromPolyTree(solution), PolyType.ptSubject, false);
            }
            progFunc(String.Format("Clipping '{0}' polygons: 100%", tmxLayer.UniqueName));

            ClipperLib.PolyTree fullSolution = new ClipperLib.PolyTree();
            fullClipper.Execute(ClipperLib.ClipType.ctUnion, fullSolution, LayerClipper.SubjectFillRule, LayerClipper.ClipFillRule);

            return fullSolution;
        }
    }
}
