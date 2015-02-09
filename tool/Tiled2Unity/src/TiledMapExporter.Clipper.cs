using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

using ClipperLib;

namespace Tiled2Unity
{
    using ClipperPolygon = List<IntPoint>;
    using ClipperPolygons = List<List<IntPoint>>;

    partial class TiledMapExporter
    {
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
                    Vector3D pointUnity3d = PointFToUnityVector_NoScale(new PointF(x, y));
                    IntPoint point = new IntPoint(pointUnity3d.X, pointUnity3d.Y);
                    return point;
                };

            LayerClipper.ProgressFunc progFunc =
                delegate(string prog)
                {
                    Program.WriteLine(prog);
                };

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(this.tmxMap, layer, xfFunc, progFunc);

            // Add our polygon and edge colliders
            List<XElement> polyColliderElements = new List<XElement>();
            AddPolygonCollider2DElements(Clipper.ClosedPathsFromPolyTree(solution), polyColliderElements);
            AddEdgeCollider2DElements(Clipper.OpenPathsFromPolyTree(solution), polyColliderElements);

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

        private void AddPolygonCollider2DElements(ClipperPolygons polygons, List<XElement> xmlList)
        {
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
