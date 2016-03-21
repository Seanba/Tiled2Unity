using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;

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
