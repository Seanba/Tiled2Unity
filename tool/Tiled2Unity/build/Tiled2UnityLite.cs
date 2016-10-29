// Tiled2UnityLite is automatically generated. Do not modify by hand.
// version 1.0.8.0

//css_reference System;
//css_reference System.Core;
//css_reference System.Drawing;
//css_reference System.Xml.Linq;
//css_reference System.Data.DataSetExtensions;
//css_reference System.Data;
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
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;


#if DOUBLE
using Real = System.Double;
#else
using Real = System.Single;
#endif


namespace Tiled2Unity
{
    static class Program
    {
        public static int Main(string[] args)
        {
            return Tiled2Unity.Tiled2UnityLite.Run(args);
        }
    }

    static class Info
    {
        public static string GetLibraryName()
        {
            return "Tiled2UnityLite";
        }

        public static string GetVersion()
        {
            return "1.0.8.0";
        }

        public static string GetPlatform()
        {
            return "CSScript";
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
        private string directoryOld = "";
        private string directoryNow = "";

        public ChDir(string path)
        {
            this.directoryOld = Directory.GetCurrentDirectory();
            if (Directory.Exists(path))
            {
                this.directoryNow = path;
            }
            else if (File.Exists(path))
            {
                this.directoryNow = Path.GetDirectoryName(path);
            }
            else
            {
                throw new DirectoryNotFoundException(String.Format("Cannot set current directory. Does not exist: {0}", path));
            }

            Directory.SetCurrentDirectory(this.directoryNow);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(this.directoryOld);
        }
    }
}

// ----------------------------------------------------------------------
// GenericListDatabase.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;

namespace Tiled2Unity
{
    // A simple list acting as our "database"
    // Similar items are found multiple times in this collection (as opposed to HashIndexOf)
    class GenericListDatabase<T> : IGenericDatabase<T>
    {
        public List<T> List { get; private set; }

        public GenericListDatabase()
        {
            this.List = new List<T>();
        }

        public int AddToDatabase(T value)
        {
            this.List.Add(value);
            return this.List.Count - 1;
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
    public class HashIndexOf<T> : IGenericDatabase<T>
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

        public int AddToDatabase(T value)
        {
            return Add(value);
        }
    }
}

// ----------------------------------------------------------------------
// IGenericDatabase.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;

namespace Tiled2Unity
{
    // This is really just a cheap interface that adds "stuff" to a container, returning an index
    // You can access the items (that may be unique or there may be repeats) through the List property
    // (We just want to be able to have unique or repeated collection of items polymorphically)
    interface IGenericDatabase<T>
    {
        List<T> List { get; }
        int AddToDatabase(T value);
    }
}

// ----------------------------------------------------------------------
// LayerClipper.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

//#define T2U_TRIANGLES

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

// ----------------------------------------------------------------------
// Logger.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public class Logger
    {
        public delegate void WriteLineDelegate(string line);
        public static event WriteLineDelegate OnWriteLine;

        public delegate void WriteSuccessDelegate(string line);
        public static event WriteSuccessDelegate OnWriteSuccess;

        public delegate void WriteWarningDelegate(string line);
        public static event WriteWarningDelegate OnWriteWarning;

        public delegate void WriteErrorDelegate(string line);
        public static event WriteErrorDelegate OnWriteError;

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
        }

        public static void WriteLine(string fmt, params object[] args)
        {
            WriteLine(String.Format(fmt, args));
        }

        public static void WriteSuccess(string success)
        {
            success += "\n";
            if (OnWriteSuccess != null)
                OnWriteSuccess(success);
            Console.Write(success);
        }

        public static void WriteSuccess(string fmt, params object[] args)
        {
            WriteSuccess(String.Format(fmt, args));
        }

        public static void WriteWarning(string warning)
        {
            warning += "\n";
            if (OnWriteWarning != null)
                OnWriteWarning(warning);
            Console.Write(warning);
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
        }

        public static void WriteError(string fmt, params object[] args)
        {
            WriteError(String.Format(fmt, args));
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
// Session.Args.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Text.RegularExpressions;

namespace Tiled2Unity
{
    partial class Session
    {
        private void ParseEnvironmentVariables()
        {
            string tmxPath = Environment.GetEnvironmentVariable("TILED2UNITY_TMXPATH");
            if (!String.IsNullOrEmpty(tmxPath))
            {
                Logger.WriteLine("Found TILED2UNITY_TMXPATH environment variable: {0}", tmxPath);
                this.TmxFilePath = tmxPath;
            }

            string unityDir = Environment.GetEnvironmentVariable("TILED2UNITY_UNITYDIR");
            if (!String.IsNullOrEmpty(unityDir))
            {
                Logger.WriteLine("Found TILED2UNITY_UNITYDIR environment variable: {0}", unityDir);
                this.TmxFilePath = tmxPath;
            }
        }

        // Returns true if the program is to keep going. Will return false if there is an error parsing options or if we were only interested in help/version information
        private bool ParseOptions(string[] args)
        {
            args = CleanseArgs(args);

            bool displayVersion = false;
            bool displayHelp = false;
            bool isAuto = false;

            NDesk.Options.OptionSet options = new NDesk.Options.OptionSet()
            {
                { "o|object-type-xml=", "Supply an Object Type XML file for types and their properties", o => Tiled2Unity.Settings.ObjectTypeXml = !String.IsNullOrEmpty(o) ? Path.GetFullPath(o) : "" },
                { "s|scale=", "Scale the output vertices by a value.\nA value of 0.01 is popular for many Unity projects that use 'Pixels Per Unit' of 100 for sprites.\nDefault is 1 (no scaling).", s => Tiled2Unity.Settings.Scale = ParseFloatDefault(s, 1.0f) },
                { "c|convex", "Limit polygon colliders to be convex with no holes. Increases the number of polygon colliders in export. Can be overriden on map or layer basis with unity:convex property.", c => Tiled2Unity.Settings.PreferConvexPolygons = true },
                { "t|texel-bias=", "Bias for texel sampling.\nTexels are offset by 1 / value.\nDefault value is 8192.\n A value of 0 means no bias.", t => Tiled2Unity.Settings.TexelBias = ParseFloatDefault(t, Tiled2Unity.Settings.DefaultTexelBias) },
                { "d|depth-buffer", "Uses a depth buffer to render the layers of the map in order. Useful for sprites that may be drawn below or above map layers depending on location.", d => Tiled2Unity.Settings.DepthBufferEnabled = true },
                { "a|auto-export", "Automatically run exporter and exit. TMXPATH and UNITYDIR are not optional in this case.", a => isAuto = true },
                { "w|writeable-vertices", "Exported meshes will have writable vertices. This increases the memory used by meshes significantly. Only use if you will mutate the vertices through scripting.", w => Tiled2Unity.Settings.WriteableVertices = true },
                { "v|version", "Display version information.", v => displayVersion = true },
                { "h|help", "Display this help message.", h => displayHelp = true },
            };

            // Parse the options
            List<string> extra = options.Parse(args);

            // Are we displaying the version?
            if (displayVersion)
            {
                Logger.WriteLine("{0} ({1}) version {2}", Tiled2Unity.Info.GetLibraryName(), Tiled2Unity.Info.GetPlatform(), Tiled2Unity.Info.GetVersion());
                return false;
            }

            // Are we displaying help?
            if (displayHelp)
            {
                PrintHelp(options);
                return false;
            }

            if (isAuto)
            {
                Logger.WriteLine("Running automatic export.");
            }

            bool success = true;

            // If we're here then we're 'running' the program
            // First left over option is the TMX file we are exporting
            if (extra.Count() == 0)
            {
                Logger.WriteLine("Missing TMXPATH argument.");
                Logger.WriteLine("  If using the GUI, try opening a TMX file now");
                Logger.WriteLine("  If using the command line, provide a path to a TMX file");
                Logger.WriteLine("  If using from Tiled Map Editor, try adding %mapfile to the command");
            }
            else
            {
                this.TmxFilePath = Path.GetFullPath(extra[0]);

                if (!File.Exists(this.TmxFilePath))
                {
                    Logger.WriteError("TMXPATH file '{0}' does not exist.", this.TmxFilePath);
                    this.TmxFilePath = null;
                    success = false;
                }

                extra.RemoveAt(0);
            }

            // The next 'left over' option is the Tiled2Unity folder of the Unity project that we are exporting to
            if (extra.Count() > 0)
            {
                this.UnityExportFolderPath = Path.GetFullPath(extra[0]);

                if (String.IsNullOrEmpty(this.UnityExportFolderPath))
                {
                    Logger.WriteError("UNITYDIR argument is not a valid path '{0}'", extra[0]);
                    this.UnityExportFolderPath = null;
                    success = false;
                }
                else if (!Directory.Exists(this.UnityExportFolderPath))
                {
                    Logger.WriteError("UNITYDIR Unity Tiled2Unity Project Directory '{0}' does not exist", this.UnityExportFolderPath);
                    this.UnityExportFolderPath = null;
                    success = false;
                }
                else if (!File.Exists(Path.Combine(this.UnityExportFolderPath, "Tiled2Unity.export.txt")))
                {
                    Logger.WriteError("UNITYDIR '{0}' is not a Tiled2Unity Unity Project folder", this.UnityExportFolderPath);
                    this.UnityExportFolderPath = null;
                    success = false;
                }

                extra.RemoveAt(0);
            }

            // Do we have any other options left over? We shouldn't.
            if (extra.Count() > 0)
            {
                Logger.WriteError("Too many arguments. Can't parse '{0}'", extra[0]);
                success = false;
            }

            if (!success)
            {
                Logger.WriteError("Command line arguments: {0}", String.Join(" ", args));
                PrintHelp(options);
                return false;
            }

            return true;
        }

        private static void PrintHelp(NDesk.Options.OptionSet options)
        {
            Logger.WriteLine("{0} Utility, Version: {1}", Tiled2Unity.Info.GetLibraryName(), Tiled2Unity.Info.GetVersion());
            Logger.WriteLine("Usage: {0} [OPTIONS]+ TMXPATH [UNITYDIR]", Tiled2Unity.Info.GetLibraryName());
            Logger.WriteLine("Example: {0} -s=0.01 MyTiledMap.tmx ../../MyUnityProjectFolder/Assets/Tiled2Unity", Tiled2Unity.Info.GetLibraryName());
            Logger.WriteLine("");
            Logger.WriteLine("Options:");

            TextWriter writer = new StringWriter();
            options.WriteOptionDescriptions(writer);
            Logger.WriteLine(writer.ToString());

            Logger.WriteLine("Prefab object properties (set in TMX file for map or Tile/Object layer properties)");
            Logger.WriteLine("  unity:sortingLayerName");
            Logger.WriteLine("  unity:sortingOrder");
            Logger.WriteLine("  unity:layer");
            Logger.WriteLine("  unity:tag");
            Logger.WriteLine("  unity:scale");
            Logger.WriteLine("  unity:isTrigger");
            Logger.WriteLine("  unity:convex");
            Logger.WriteLine("  unity:ignore (value = [false|true|collision|visual])");
            Logger.WriteLine("  unity:resource (value = [false|true])");
            Logger.WriteLine("  unity:resourcePath");
            Logger.WriteLine("  (Other properties are exported for custom scripting in your Unity project)");
            Logger.WriteLine("Support Tiled Map Editor on Patreon: https://www.patreon.com/bjorn");
            Logger.WriteLine("Make a donation for Tiled2Unity: http://www.seanba.com/donate");
        }

        // Removes unwanted cruft from arguments
        private static string[] CleanseArgs(string[] args)
        {
            List<string> arguments = new List<string>(args);

#if TILED2UNITY_MAC
            // MacOSX adds "-psn_number_number" (process number) argument. Get rid of it.
            var regex = new Regex(@"-psn_\d+_\d+");
            arguments.RemoveAll(s => regex.IsMatch(s));
#endif

            return arguments.ToArray();

        }


        private static float ParseFloatDefault(string str, float defaultValue)
        {
            float resultValue = 0;
            if (float.TryParse(str, out resultValue))
            {
                return resultValue;
            }
            return defaultValue;
        }

    }

}

// ----------------------------------------------------------------------
// Session.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // A Tiled2Unity Session is made up of
    // a) A TMX file (which results in a TMX object)
    // b) An export directory
    public partial class Session
    {
        public string TmxFilePath { get; private set; }
        public string UnityExportFolderPath { get; set; }
        public TmxMap TmxMap { get; private set; }

        private SummaryReport summaryReport = new SummaryReport();

        public void SetCulture()
        {
            // Force decimal numbers to use '.' as the decimal separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
        }

        public bool InitializeWithArgs(string[] args, bool summary)
        {
            Logger.WriteLine("Command: {0}", String.Join(" ", args));

            // Create our map instance (which is empty/unloaded at first)
            this.TmxMap = new TmxMap();

            // Parse the arguments and let our listeners know in case any settings changed
            if (summary)
            {
                this.summaryReport.Capture("Arguments");
            }

            ParseEnvironmentVariables();
            bool success = ParseOptions(args);

            if (summary)
            {
                this.summaryReport.Report();
            }

            return success;
        }

        public void LoadInitialTmxFile()
        {
            // Only load the inital TMX file if it is set
            if (!String.IsNullOrEmpty(this.TmxFilePath))
            {
                LoadTmxFile(this.TmxFilePath);
            }
        }

        public void LoadTmxFile(string tmxFilePath)
        {
            this.TmxFilePath = tmxFilePath;

            this.summaryReport.Capture("Loading");

            // Load the TMX map
            try
            {
                this.TmxMap = TmxMap.LoadFromFile(this.TmxFilePath);

                // Load the Object Type Xml file if it exists
                if (File.Exists(Tiled2Unity.Settings.ObjectTypeXml))
                {
                    this.TmxMap.LoadObjectTypeXml(Tiled2Unity.Settings.ObjectTypeXml);
                }
            }
            catch (TmxException tmx)
            {
                this.TmxMap = new TmxMap();
                Logger.WriteError(tmx.Message);
            }
            catch (Exception e)
            {
                this.TmxMap = new TmxMap();
                Logger.WriteError(e.Message);
            }

            this.summaryReport.Report();
        }

        public void ExportTmxMap()
        {
            this.summaryReport.Capture("Exporting");
            {
                if (this.TmxMap.IsLoaded == false)
                {
                    Logger.WriteError("Tiled map file not loaded!");
                }
                else
                {
                    try
                    {
                        Logger.WriteLine("Exporting '{0}' to '{1}'", this.TmxFilePath, this.UnityExportFolderPath);
                        TiledMapExporter exporter = new TiledMapExporter(this.TmxMap);
                        exporter.Export(this.UnityExportFolderPath);
                    }
                    catch (TmxException tmx)
                    {
                        Logger.WriteError(tmx.Message);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteError(e.Message);
                    }
                }
            }
            this.summaryReport.Report();
        }

        public void DisplayHelp()
        {
            List<string> args = new List<string>() { "-h" };
            ParseOptions(args.ToArray());
        }

    }
}

// ----------------------------------------------------------------------
// Settings.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // Setttings for compiling and export Tiled maps into Unity
    public class Settings
    {
        static public string ObjectTypeXml = "";

        public static float Scale = 1.0f;
        public static bool PreferConvexPolygons = false;
        public static bool DepthBufferEnabled = false;
        public static bool WriteableVertices = false;

        public static readonly float DefaultTexelBias = 8192.0f;
        public static float TexelBias = DefaultTexelBias;
    }
}

// ----------------------------------------------------------------------
// SummaryReport.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // Helper class that gathers success, warning and error messages and blasts them back out through logging when requested
    class SummaryReport
    {
        private string name = "";
        private List<string> successes = new List<string>();
        private List<string> warnings = new List<string>();
        private List<string> errors = new List<string>();

        public void Capture(string name)
        {
            Listen();

            this.name = name;
            this.successes.Clear();
            this.warnings.Clear();
            this.errors.Clear();
        }

        private delegate void LoggingDelegate(string message, params object[] args);
        public void Report()
        {
            // Stop listening because we're going to blast back out to the logging system
            Ignore();

            // Are we going to log as success, warnings, or errors?
            LoggingDelegate func = Logger.WriteSuccess;
            if (this.warnings.Count > 0)
            {
                func = Logger.WriteWarning;
            }
            if (this.errors.Count > 0)
            {
                func = Logger.WriteError;
            }

            // Write out the summary report
            string separator = new string('-', 80);
            Logger.WriteLine(separator);
            func("{0} summary", this.name);

            // Add successes
            Logger.WriteLine("Succeeded: {0}", this.successes.Count);
            foreach (var success in this.successes)
            {
                Logger.WriteSuccess("  {0}", success);
            }

            // Add warnings
            Logger.WriteLine("Warnings: {0}", this.warnings.Count);
            foreach (var warn in this.warnings)
            {
                Logger.WriteWarning("  {0}", warn);
            }

            // Add errors
            Logger.WriteLine("Errors: {0}", this.errors.Count);
            foreach (var error in this.errors)
            {
                Logger.WriteError("  {0}", error);
            }

            Logger.WriteLine(separator);
        }

        private void Listen()
        {
            Logger.OnWriteSuccess += Logger_OnWriteSuccess;
            Logger.OnWriteWarning += Logger_OnWriteWarning;
            Logger.OnWriteError += Logger_OnWriteError;
        }

        private void Ignore()
        {
            Logger.OnWriteSuccess -= Logger_OnWriteSuccess;
            Logger.OnWriteWarning -= Logger_OnWriteWarning;
            Logger.OnWriteError -= Logger_OnWriteError;
        }

        private void Logger_OnWriteError(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.errors.Add(line);
        }

        private void Logger_OnWriteWarning(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.warnings.Add(line);
        }

        private void Logger_OnWriteSuccess(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.successes.Add(line);
        }
    }
}

// ----------------------------------------------------------------------
// Tiled2UnityLite.Main.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

// Tiled2UnityLite is the 'automated export' version of Tiled2Unity
// It is expected to open a TMX file, export, and close on its own as a command-line utility
namespace Tiled2Unity
{
    public class Tiled2UnityLite
    {
        public static int Run(string[] args)
        {
            int error = 0;

            // If we get an error then that changes our error code
            Tiled2Unity.Logger.OnWriteError += delegate (string line)
            {
                error = 1;
            };

            // Run the session
            Tiled2Unity.Session tmxSession = new Session();
            tmxSession.SetCulture();

            if (tmxSession.InitializeWithArgs(args, false))
            {
                // Load the Tiled file (TMX)
                if (error == 0)
                {
                    tmxSession.LoadInitialTmxFile();
                }

                // Export the Tiled file to Unity
                if (error == 0)
                {
                    tmxSession.ExportTmxMap();
                }
            }

            return error;
        }
    }
}

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
                    Logger.WriteLine(prog);
                };

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(this.tmxMap, layer, xfFunc, progFunc);

            var paths = ClipperLib.Clipper.ClosedPathsFromPolyTree(solution);
            if (paths.Count >= MaxNumberOfSafePaths)
            {
                StringBuilder warning = new StringBuilder();
                warning.AppendFormat("Layer '{0}' has a large number of polygon paths ({1}).", layer.Name, paths.Count);
                warning.AppendLine("  Importing this layer may be slow in Unity. (Can take an hour or more for +1000 paths.)");
                warning.AppendLine("  Check polygon/rectangle objects in Tile Collision Editor in Tiled and use 'Snap to Grid' or 'Snap to Fine Grid'.");
                warning.AppendLine("  You want colliders to be set up so they can be merged with colliders on neighboring tiles, reducing path count considerably.");
                warning.AppendLine("  In some cases the size of the map may need to be reduced.");
                Logger.WriteWarning(warning.ToString());
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

            // Collision layer may have a name and "unity physics layer" to go with it
            // (But not if we're using unity:layer override)
            if (String.IsNullOrEmpty(layer.UnityLayerOverrideName) && !String.IsNullOrEmpty(layer.Name))
            {
                gameObjectCollision.SetAttributeValue("name", "Collision_" + layer.Name);
                gameObjectCollision.SetAttributeValue("layer", layer.Name);
            }

            return gameObjectCollision;
        }

        private void AddPolygonCollider2DElements_Convex(ClipperLib.PolyTree solution, List<XElement> xmlList)
        {
            // This may generate many convex polygons as opposed to one "complicated" one
            var polygons = LayerClipper.SolutionPolygons_Simple(solution);

            // Each PointF array is a polygon with a single path
            foreach (var pointfArray in polygons)
            {
                string data = String.Join(" ", pointfArray.Select(pt => String.Format("{0},{1}", pt.X * Tiled2Unity.Settings.Scale, pt.Y * Tiled2Unity.Settings.Scale)));
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
                string data = String.Join(" ", path.Select(pt => String.Format("{0},{1}", pt.X * Tiled2Unity.Settings.Scale, pt.Y * Tiled2Unity.Settings.Scale)));
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
                string data = String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X * Tiled2Unity.Settings.Scale, pt.Y * Tiled2Unity.Settings.Scale)));
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
    public partial class TiledMapExporter
    {
        private TmxMap tmxMap = null;

        public TiledMapExporter(TmxMap tmxMap)
        {
            this.tmxMap = tmxMap;
        }

        public void Export(string exportToTiled2UnityPath)
        {
            if (String.IsNullOrEmpty(exportToTiled2UnityPath))
            {
                throw new TmxException("Unity project export path is invalid or not set.");
            }

            // Create an Xml file to be imported by a Unity project
            // The unity project will have code that turns the Xml into Unity objects and prefabs
            string fileToSave = this.tmxMap.GetExportedFilename();
            Logger.WriteLine("Compiling tiled2unity file: {0}", fileToSave);

            // Need an element for embedded file data that will be imported into Unity
            // These are models and textures
            List<XElement> importFiles = CreateImportFilesElements(exportToTiled2UnityPath);
            List<XElement> assignMaterials = CreateAssignMaterialsElements();

            Logger.WriteLine("Gathering prefab data ...");
            XElement prefab = CreatePrefabElement();

            // Create the Xml root and populate it
            Logger.WriteLine("Writing as Xml ...");

            string version = Tiled2Unity.Info.GetVersion();
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
                Logger.WriteError(builder.ToString());
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
                Logger.WriteError(builder.ToString());
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
                    if (Tiled2Unity.Info.GetVersion() != group.ToString())
                    {
                        StringBuilder builder = new StringBuilder();
                        builder.AppendFormat("Export/Import Version mismatch\n");
                        builder.AppendFormat("  Tiled2Unity version   : {0}\n", Tiled2Unity.Info.GetVersion());
                        builder.AppendFormat("  Unity Project version : {0}\n", group.ToString());
                        builder.AppendFormat("  (Did you forget to update Tiled2Unity scipts in your Unity project?)");
                        Logger.WriteWarning(builder.ToString());
                    }
                }
            }

            // Save the file (which is importing it into Unity)
            string pathToSave = Path.Combine(exportDir, fileToSave);
            Logger.WriteLine("Exporting to: {0}", pathToSave);
            doc.Save(pathToSave);
            Logger.WriteSuccess("Succesfully exported: {0}\n  Vertex Scale = {1}\n  Object Type Xml = {2}",
                pathToSave,
                Tiled2Unity.Settings.Scale,
                String.IsNullOrEmpty(Tiled2Unity.Settings.ObjectTypeXml) ? "<none>" : Tiled2Unity.Settings.ObjectTypeXml);
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
            scaled.X *= Tiled2Unity.Settings.Scale;
            scaled.Y *= Tiled2Unity.Settings.Scale;

            // Have to watch for negative zero, ffs
            return new PointF(scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointFToObjVertex(PointF pt)
        {
            // Note, we negate the x and y due to Wavefront's coordinate system
            // Applying scaling
            PointF scaled = pt;
            scaled.X *= Tiled2Unity.Settings.Scale;
            scaled.Y *= Tiled2Unity.Settings.Scale;

            // Watch for negative zero, ffs
            return new PointF(scaled.X == 0 ? 0 : -scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointToTextureCoordinate(PointF pt, Size imageSize)
        {
            float tx = pt.X / (float)imageSize.Width;
            float ty = pt.Y / (float)imageSize.Height;
            return new PointF(tx, 1.0f - ty);
        }

        public static float CalculateFaceDepth(float position_y, float mapHeight)
        {
            float z = position_y / mapHeight * -1.0f;
            return (z == -0.0f) ? 0 : z;
        }

        public static float CalculateLayerDepth(int layerOrder, float tileHeight, float mapHeight)
        {
            // Note: I don't think a layer depth of this complexity is helpful and seems to be leading to z-fighting anyhow
            //float z = layerOrder * tileHeight / mapHeight * -1.0f;
            float z = layerOrder * -1.0f;
            return (z == -0.0f) ? 0 : z;
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
        private class TmxImageComparer : IEqualityComparer<TmxImage>
        {
            public bool Equals(TmxImage lhs, TmxImage rhs)
            {
                return lhs.AbsolutePath.ToLower() == rhs.AbsolutePath.ToLower();
            }

            public int GetHashCode(TmxImage tmxImage)
            {
                return tmxImage.AbsolutePath.GetHashCode();
            }
        }

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
                var layerImages = from layer in this.tmxMap.Layers
                                  where layer.Visible == true
                                  from rawTileId in layer.TileIds
                                  where rawTileId != 0
                                  let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                  let tile = this.tmxMap.Tiles[tileId]
                                  select tile.TmxImage;

                // Find the images from the frames as well
                var frameImages = from layer in this.tmxMap.Layers
                                  where layer.Visible == true
                                  from rawTileId in layer.TileIds
                                  where rawTileId != 0
                                  let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                  let tile = this.tmxMap.Tiles[tileId]
                                  from rawFrame in tile.Animation.Frames
                                  let frameId = TmxMath.GetTileIdWithoutFlags(rawFrame.GlobalTileId)
                                  let frame = this.tmxMap.Tiles[frameId]
                                  select frame.TmxImage;


                // Tile Objects may have images not yet references by a layer
                var objectImages = from objectGroup in this.tmxMap.ObjectGroups
                                   where objectGroup.Visible == true
                                   from tmxObject in objectGroup.Objects
                                   where tmxObject.Visible == true
                                   where tmxObject is TmxObjectTile
                                   let tmxTileObject = tmxObject as TmxObjectTile
                                   from mesh in tmxTileObject.Tile.Meshes
                                   select mesh.TmxImage;

                // Combine image paths from tile layers and object layers
                List<TmxImage> images = new List<TmxImage>();
                images.AddRange(layerImages);
                images.AddRange(frameImages);
                images.AddRange(objectImages);

                // Get rid of duplicate images
                TmxImageComparer imageComparer = new TmxImageComparer();
                images = images.Distinct(imageComparer).ToList();

                foreach (TmxImage image in images)
                {
                    // The source texture is internal if it has a sibling *.meta file
                    // We don't want to copy internal textures into Unity because they are already there.
                    bool isInternal = File.Exists(image.AbsolutePath + ".meta");
                    if (isInternal)
                    {
                        // The texture is already in the Unity project so don't import
                        XElement xmlInternalTexture = new XElement("InternalTexture");

                        // The path to the texture will be WRT to the Unity project root
                        string assetsFolder = GetUnityAssetsPath(image.AbsolutePath);
                        string assetPath = image.AbsolutePath.Remove(0, assetsFolder.Length);
                        assetPath = "Assets" + assetPath;
                        assetPath = assetPath.Replace("\\", "/");

                        Logger.WriteLine("InternalTexture : {0}", assetPath);

                        // Path to texture in the asset directory
                        xmlInternalTexture.SetAttributeValue("assetPath", assetPath);

                        // Transparent color key?
                        if (!String.IsNullOrEmpty(image.TransparentColor))
                        {
                            xmlInternalTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                        }

                        // Are we using depth shaders on our materials?
                        if (Tiled2Unity.Settings.DepthBufferEnabled)
                        {
                            xmlInternalTexture.SetAttributeValue("usesDepthShaders", true);
                        }

                        elements.Add(xmlInternalTexture);
                    }
                    else
                    {
                        // The texture needs to be imported into the Unity project (under Tiled2Unity's care)
                        XElement xmlImportTexture = new XElement("ImportTexture");

                        // Note that compression is not available in Unity. Go with Base64 string. Blerg.
                        Logger.WriteLine("ImportTexture : will import '{0}' to {1}", image.AbsolutePath, Path.Combine(exportToUnityProjectPath, "Textures"));

                        // Is there a color key for transparency?
                        if (!String.IsNullOrEmpty(image.TransparentColor))
                        {
                            xmlImportTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                        }

                        // Are we using depth shaders on our materials?
                        if (Tiled2Unity.Settings.DepthBufferEnabled)
                        {
                            xmlImportTexture.SetAttributeValue("usesDepthShaders", true);
                        }

                        // Bake the image file into the xml
                        xmlImportTexture.Add(new XAttribute("filename", Path.GetFileName(image.AbsolutePath)), FileToBase64String(image.AbsolutePath));

                        elements.Add(xmlImportTexture);
                    }
                }
            }

            return elements;
        }

        // Assumes the path passed in is within the "Assets" directory of a Unity project
        private string GetUnityAssetsPath(string path)
        {
            string folderPath = Path.GetDirectoryName(path);
            while (!String.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                string folderName = Path.GetFileName(folderPath);
                if (String.Compare(folderName, "Assets", true) == 0)
                {
                    return folderPath;
                }
                folderPath = Path.GetDirectoryName(folderPath);
            }

            return Path.GetDirectoryName(path);
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
        // Working man's vertex
        public struct Vertex3
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public static Vertex3 FromPointF(PointF point, float depth)
            {
                return new Vertex3 { X = point.X, Y = point.Y, Z = depth };
            }
        }

        public struct FaceVertices
        {
            public PointF[] Vertices { get; set; }
            public float Depth_z { get; set; }

            public Vertex3 V0
            {
                get { return Vertex3.FromPointF(Vertices[0], this.Depth_z); }
            }

            public Vertex3 V1
            {
                get { return Vertex3.FromPointF(Vertices[1], this.Depth_z); }
            }

            public Vertex3 V2
            {
                get { return Vertex3.FromPointF(Vertices[2], this.Depth_z); }
            }

            public Vertex3 V3
            {
                get { return Vertex3.FromPointF(Vertices[3], this.Depth_z); }
            }
        }

        // Creates the text for a Wavefront OBJ file for the TmxMap
        private StringWriter BuildObjString()
        {
            IGenericDatabase<Vertex3> vertexDatabase = new HashIndexOf<Vertex3>();
            HashIndexOf<PointF> uvDatabase = new HashIndexOf<PointF>();

            // Are we allowing vertices to be written too (advanced option)
            if (Tiled2Unity.Settings.WriteableVertices)
            {
                // Replace vertex database with class that ensure each vertex (even ones with similar values) are unique
                Logger.WriteLine("Using writeable-vertices. This will increase the size of the mesh but will allow you mutate vertices through scripting. This is an advanced feature.");
                vertexDatabase = new GenericListDatabase<Vertex3>();
            }

            float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;

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
                    Logger.WriteLine("Writing '{0}' mesh group", mesh.UniqueMeshName);
                    faceBuilder.AppendFormat("\ng {0}\n", mesh.UniqueMeshName);

                    foreach (int y in verticalRange)
                    {
                        foreach (int x in horizontalRange)
                        {
                            int tileIndex = layer.GetTileIndex(x, y);
                            uint tileId = mesh.GetTileIdAt(tileIndex);

                            // Skip blank tiles
                            if (tileId == 0)
                                continue;

                            TmxTile tile = this.tmxMap.Tiles[TmxMath.GetTileIdWithoutFlags(tileId)];
                            
                            // What are the vertex and texture coorindates of this face on the mesh?
                            var position = this.tmxMap.GetMapPositionAt(x, y);
                            var vertices = CalculateFaceVertices(position, tile.TileSize, this.tmxMap.TileHeight, tile.Offset);

                            // If we're using depth shaders then we'll need to set a depth value of this face
                            float depth_z = 0.0f;
                            if (Tiled2Unity.Settings.DepthBufferEnabled)
                            {
                                depth_z = CalculateFaceDepth(position.Y, mapLogicalHeight);
                            }

                            FaceVertices faceVertices = new FaceVertices { Vertices = vertices, Depth_z = depth_z };

                            // Is the tile being flipped or rotated (needed for texture cooridinates)
                            bool flipDiagonal = TmxMath.IsTileFlippedDiagonally(tileId);
                            bool flipHorizontal = TmxMath.IsTileFlippedHorizontally(tileId);
                            bool flipVertical = TmxMath.IsTileFlippedVertically(tileId);
                            var uvs = CalculateFaceTextureCoordinates(tile, flipDiagonal, flipHorizontal, flipVertical);

                            // Adds vertices and uvs to the database as we build the face strings
                            string v0 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V0) + 1, uvDatabase.Add(uvs[0]) + 1);
                            string v1 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V1) + 1, uvDatabase.Add(uvs[1]) + 1);
                            string v2 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V2) + 1, uvDatabase.Add(uvs[2]) + 1);
                            string v3 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V3) + 1, uvDatabase.Add(uvs[3]) + 1);
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

                Logger.WriteLine("Writing '{0}' tile group", tmxMesh.UniqueMeshName);
                faceBuilder.AppendFormat("\ng {0}\n", tmxMesh.UniqueMeshName);

                // Get the single tile associated with this mesh
                TmxTile tmxTile = this.tmxMap.Tiles[tmxMesh.TileIds[0]];

                var vertices = CalculateFaceVertices_TileObject(tmxTile.TileSize, tmxTile.Offset);
                var uvs = CalculateFaceTextureCoordinates(tmxTile, false, false, false);

                // TileObjects have zero depth on their vertices. Their GameObject parent will set depth.
                FaceVertices faceVertices = new FaceVertices { Vertices = vertices, Depth_z = 0.0f };

                // Adds vertices and uvs to the database as we build the face strings
                string v0 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V0) + 1, uvDatabase.Add(uvs[0]) + 1);
                string v1 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V1) + 1, uvDatabase.Add(uvs[1]) + 1);
                string v2 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V2) + 1, uvDatabase.Add(uvs[2]) + 1);
                string v3 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V3) + 1, uvDatabase.Add(uvs[3]) + 1);
                faceBuilder.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);
            }

            // All of our faces have been built and vertex and uv databases have been filled.
            // Start building out the obj file
            StringWriter objWriter = new StringWriter();
            objWriter.WriteLine("# Wavefront OBJ file automatically generated by Tiled2Unity");
            objWriter.WriteLine();

            Logger.WriteLine("Writing face vertices");
            objWriter.WriteLine("# Vertices (Count = {0})", vertexDatabase.List.Count());
            foreach (var v in vertexDatabase.List)
            {
                objWriter.WriteLine("v {0} {1} {2}", v.X, v.Y, v.Z);
            }
            objWriter.WriteLine();

            Logger.WriteLine("Writing face uv coordinates");
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

            // "Tuck in" the points a tiny bit to help avoid seams
            // This can be turned off by setting Texel Bias to zero
            // Note that selecting a texel bias that is too small or a texture that is too big may affect pixel-perfect rendering (pixel snapping in shader will help)
            if (Tiled2Unity.Settings.TexelBias > 0)
            {
                float bias = 1.0f / Tiled2Unity.Settings.TexelBias;
                float bias_w = bias * tileSize.Width;
                float bias_h = bias * tileSize.Height;

                points[0].X += bias_w;
                points[0].Y += bias_h;

                points[1].X -= bias_w;
                points[1].Y += bias_h;

                points[2].X -= bias_w;
                points[2].Y -= bias_h;

                points[3].X += bias_w;
                points[3].Y -= bias_h;
            }

            PointF center = new PointF(tileSize.Width * 0.5f, tileSize.Height * 0.5f);
            center.X += imageLocation.X;
            center.Y += imageLocation.Y;
            TmxMath.TransformPoints_DiagFirst(points, center, flipDiagonal, flipHorizontal, flipVertical);

            PointF[] coordinates = new PointF[4];
            coordinates[3] = PointToTextureCoordinate(points[0], imageSize);
            coordinates[2] = PointToTextureCoordinate(points[1], imageSize);
            coordinates[1] = PointToTextureCoordinate(points[2], imageSize);
            coordinates[0] = PointToTextureCoordinate(points[3], imageSize);

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

            prefab.SetAttributeValue("orientation", this.tmxMap.Orientation.ToString());
            prefab.SetAttributeValue("staggerAxis", this.tmxMap.StaggerAxis.ToString());
            prefab.SetAttributeValue("staggerIndex", this.tmxMap.StaggerIndex.ToString());
            prefab.SetAttributeValue("hexSideLength", this.tmxMap.HexSideLength);

            prefab.SetAttributeValue("numLayers", this.tmxMap.Layers.Count);
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);

            prefab.SetAttributeValue("exportScale", Tiled2Unity.Settings.Scale);
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

                    // Is we're using depth shaders for our materials then each layer needs depth assigned to it
                    // The "depth" of the layer is negative because the Unity camera is along the negative z axis
                    float depth_z = 0;
                    if (Tiled2Unity.Settings.DepthBufferEnabled && layer.SortingOrder != 0)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileHeight = this.tmxMap.TileHeight;
                        depth_z = CalculateLayerDepth(layer.SortingOrder, tileHeight, mapLogicalHeight);
                    }

                    XElement layerElement =
                        new XElement("GameObject",
                            new XAttribute("name", layer.Name),
                            new XAttribute("x", offset.X),
                            new XAttribute("y", offset.Y),
                            new XAttribute("z", depth_z));

                    if (layer.Ignore != TmxLayer.IgnoreSettings.Visual)
                    {
                        // Submeshes for the layer (layer+material)
                        var meshElements = CreateMeshElementsForLayer(layer);
                        layerElement.Add(meshElements);
                    }

                    // Collision data for the layer
                    if (layer.Ignore != TmxLayer.IgnoreSettings.Collision)
                    {
                        foreach (var collisionLayer in layer.CollisionLayers)
                        {
                            var collisionElements = CreateCollisionElementForLayer(collisionLayer);
                            layerElement.Add(collisionElements);
                        }
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

                    // Is we're using depth shaders for our materials then each object group needs depth assigned to it
                    // The "depth" of the layer is negative because the Unity camera is along the negative z axis
                    float depth_z = 0;
                    if (Tiled2Unity.Settings.DepthBufferEnabled && objGroup.SortingOrder != 0)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileHeight = this.tmxMap.TileHeight;

                        depth_z = CalculateLayerDepth(objGroup.SortingOrder, tileHeight, mapLogicalHeight);
                    }

                    gameObject.SetAttributeValue("z", depth_z);

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

                // If we're not using a unity:layer override and there is an Object Type to go with this object then use it
                if (String.IsNullOrEmpty(objectGroup.UnityLayerOverrideName))
                {
                    xmlObject.SetAttributeValue("layer", tmxObject.Type);
                }

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
                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;

                    // Apply z-cooridnate for use with the depth buffer
                    // (Again, this is complicated by the fact that object tiles position is WRT the bottom edge of a tile)
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileLogicalHeight = this.tmxMap.TileHeight;
                        float logicalPos_y = (-pos.Y / Tiled2Unity.Settings.Scale) - tileLogicalHeight;

                        float depth_z = CalculateFaceDepth(logicalPos_y, mapLogicalHeight);
                        xmlObject.SetAttributeValue("z", depth_z);
                    }

                    AddTileObjectElements(tmxObject as TmxObjectTile, xmlObject);
                }
                else
                {
                    Logger.WriteLine("Object '{0}' has been added for use with custom importers", tmxObject);
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

        private void AssignUnityProperties(TmxHasProperties tmxHasProperties, XElement xml, PrefabContext context)
        {
            var properties = TmxHelper.GetPropertiesWithTypeDefaults(tmxHasProperties, this.tmxMap.ObjectTypes);

            // Only the root of the prefab can have a scale
            {
                string unityScale = properties.GetPropertyValueAsString("unity:scale", "");
                if (!String.IsNullOrEmpty(unityScale))
                {
                    float scale = 1.0f;
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:scale only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Single.TryParse(unityScale, out scale))
                    {
                        Logger.WriteError("unity:scale property value '{0}' could not be converted to a float", unityScale);
                    }
                    else
                    {
                        xml.SetAttributeValue("scale", unityScale);
                    }
                }
            }

            // Only the root of the prefab can be marked a resource
            {
                string unityResource = properties.GetPropertyValueAsString("unity:resource", "");
                if (!String.IsNullOrEmpty(unityResource))
                {
                    bool resource = false;
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:resource only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Boolean.TryParse(unityResource, out resource))
                    {
                        Logger.WriteError("unity:resource property value '{0}' could not be converted to a boolean", unityResource);
                    }
                    else
                    {
                        xml.SetAttributeValue("resource", unityResource);
                    }
                }
            }

            // Some users may want resource prefabs to be saved to a particular path
            {
                string unityResourcePath = properties.GetPropertyValueAsString("unity:resourcePath", "");
                if (!String.IsNullOrEmpty(unityResourcePath))
                {
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:resourcePath only applies to map properties\n{0}", xml.ToString());
                    }
                    else
                    {
                        bool isInvalid = Path.GetInvalidPathChars().Any(c => unityResourcePath.Contains(c));
                        if (isInvalid)
                        {
                            Logger.WriteError("unity:resourcePath has invalid path characters: {0}", unityResourcePath);
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
                string unityIsTrigger = properties.GetPropertyValueAsString("unity:isTrigger", "");
                if (!String.IsNullOrEmpty(unityIsTrigger))
                {
                    bool isTrigger = false;
                    if (!Boolean.TryParse(unityIsTrigger, out isTrigger))
                    {
                        Logger.WriteError("unity:isTrigger property value '{0}' cound not be converted to a boolean", unityIsTrigger);
                    }
                    else
                    {
                        xml.SetAttributeValue("isTrigger", unityIsTrigger);
                    }
                }
            }

            // Any part of the prefab can be assigned a 'layer'
            {
                string unityLayer = properties.GetPropertyValueAsString("unity:layer", "");
                if (!String.IsNullOrEmpty(unityLayer))
                {
                    xml.SetAttributeValue("layer", unityLayer);
                }
            }

            // Any part of the prefab can be assigned a 'tag'
            {
                string unityTag = properties.GetPropertyValueAsString("unity:tag", "");
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

            var unknown = from p in properties.PropertyMap
                          where p.Key.StartsWith("unity:")
                          where knownProperties.Contains(p.Key) == false
                          select p.Key;
            foreach (var p in unknown)
            {
                Logger.WriteWarning("Unknown unity property '{0}' in GameObject '{1}'", p, tmxHasProperties.ToString());
            }
        }

        private void AssignTiledProperties(TmxHasProperties tmxHasProperties, XElement xml)
        {
            var properties = TmxHelper.GetPropertiesWithTypeDefaults(tmxHasProperties, this.tmxMap.ObjectTypes);

            List<XElement> xmlProperties = new List<XElement>();

            foreach (var prop in properties.PropertyMap)
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


                XElement xmlProp = new XElement("Property", new XAttribute("name", prop.Key), new XAttribute("value", prop.Value.Value));
                xmlProperties.Add(xmlProp);
            }

            xml.Add(xmlProperties);
        }

        private XElement CreateBoxColliderElement(TmxObjectRectangle tmxRectangle)
        {
            XElement xmlCollider =
                new XElement("BoxCollider2D",
                    new XAttribute("width", tmxRectangle.Size.Width * Tiled2Unity.Settings.Scale),
                    new XAttribute("height", tmxRectangle.Size.Height * Tiled2Unity.Settings.Scale));

            return xmlCollider;
        }

        private XElement CreateCircleColliderElement(TmxObjectEllipse tmxEllipse, string objGroupName)
        {
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Logger.WriteError("Collision ellipse in Object Layer '{0}' is not supported in Isometric maps: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else if (!tmxEllipse.IsCircle())
            {
                Logger.WriteError("Collision ellipse in Object Layer '{0}' is not a circle: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else
            {
                XElement circleCollider =
                    new XElement("CircleCollider2D",
                        new XAttribute("radius", tmxEllipse.Radius * Tiled2Unity.Settings.Scale));

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
            // TileObjects can be scaled (this is separate from vertex scaling)
            SizeF scale = tmxObjectTile.GetTileObjectScale();

            // Flipping is done through negative-scaling on the child object
            float flip_w = tmxObjectTile.FlippedHorizontal ? -1.0f : 1.0f;
            float flip_h = tmxObjectTile.FlippedVertical ? -1.0f : 1.0f;

            // Helper values for moving tile about local origin
            float full_w = tmxObjectTile.Tile.TileSize.Width;
            float full_h = tmxObjectTile.Tile.TileSize.Height;
            float half_w = full_w * 0.5f;
            float half_h = full_h * 0.5f;

            // Scale goes onto root node
            xmlTileObjectRoot.SetAttributeValue("scaleX", scale.Width);
            xmlTileObjectRoot.SetAttributeValue("scaleY", scale.Height);

            // We combine the properties of the tile that is referenced and add it to our own properties
            AssignTiledProperties(tmxObjectTile.Tile, xmlTileObjectRoot);

            // Add a TileObject component for scripting purposes
            {
                XElement xmlTileObjectComponent = new XElement("TileObjectComponent");
                xmlTileObjectComponent.SetAttributeValue("width", tmxObjectTile.Tile.TileSize.Width * scale.Width * Tiled2Unity.Settings.Scale);
                xmlTileObjectComponent.SetAttributeValue("height", tmxObjectTile.Tile.TileSize.Height * scale.Height * Tiled2Unity.Settings.Scale);
                xmlTileObjectRoot.Add(xmlTileObjectComponent);
            }

            // Child node positions game object to match center of tile so can flip along x and y axes
            XElement xmlTileObject = new XElement("GameObject");
            xmlTileObject.SetAttributeValue("name", "TileObject");
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                // In isometric mode the local origin of the tile is at the bottom middle
                xmlTileObject.SetAttributeValue("x", 0);
                xmlTileObject.SetAttributeValue("y", half_h);
            }
            else
            {
                // For non-isometric maps the local origin of the tile is the bottom left
                xmlTileObject.SetAttributeValue("x", half_w);
                xmlTileObject.SetAttributeValue("y", half_h);
            }
            xmlTileObject.SetAttributeValue("scaleX", flip_w);
            xmlTileObject.SetAttributeValue("scaleY", flip_h);

            // Add any colliders that might be on the tile
            // Note: Colliders on a tile object are always treated as if they are in Orthogonal space
            TmxMap.MapOrientation restoreOrientation = tmxMap.Orientation;
            this.tmxMap.Orientation = TmxMap.MapOrientation.Orthogonal;
            {
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
                        // This object is currently in the center of the Tile Object we are constructing
                        // The collision geometry is wrt the top-left corner
                        // The "Offset" of the collider translation to get to lop-left corner and the collider's position into account
                        float offset_x = (-half_w + tmxObject.Position.X) * Tiled2Unity.Settings.Scale;
                        float offset_y = (half_h - tmxObject.Position.Y) * Tiled2Unity.Settings.Scale;
                        objElement.SetAttributeValue("offsetX", offset_x);
                        objElement.SetAttributeValue("offsetY", offset_y);

                        xmlTileObject.Add(objElement);
                    }
                }
            }
            this.tmxMap.Orientation = restoreOrientation;

            // Add a child for each mesh
            // (The child node is needed due to animation)
            foreach (var mesh in tmxObjectTile.Tile.Meshes)
            {
                XElement xmlMeshObject = new XElement("GameObject");

                xmlMeshObject.SetAttributeValue("name", mesh.ObjectName);
                xmlMeshObject.SetAttributeValue("copy", mesh.UniqueMeshName);

                xmlMeshObject.SetAttributeValue("sortingLayerName", tmxObjectTile.SortingLayerName ?? tmxObjectTile.ParentObjectGroup.SortingLayerName);
                xmlMeshObject.SetAttributeValue("sortingOrder", tmxObjectTile.SortingOrder ?? tmxObjectTile.ParentObjectGroup.SortingOrder);

                // Game object that contains mesh moves position to that local origin of Tile Object (from Tiled's point of view) matches the root position of the Tile game object
                // Put another way: This translation moves away from center to local origin
                xmlMeshObject.SetAttributeValue("x", -half_w);
                xmlMeshObject.SetAttributeValue("y", half_h);

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
// ComposeConvexPolygons.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;


namespace Tiled2Unity.Geometry
{
    // Input is a collection of triangles and output is a collection of convex polygons
    // We remove shared edges along triangles were we can using the Hertel-Mehlhorn Algorithm
    public class ComposeConvexPolygons
    {
        public PolygonEdgeGroup PolygonEdgeGroup { get; private set; }
        public List<PointF[]> ConvexPolygons { get; private set; }

        public ComposeConvexPolygons()
        {
            this.PolygonEdgeGroup = new PolygonEdgeGroup();
        }

        public List<PointF[]> Compose(List<PointF[]> triangles)
        {
            this.PolygonEdgeGroup.Initialize(triangles);
            CombinePolygons();
            return this.ConvexPolygons;
        }

        private void CombinePolygons()
        {
            // Before we start merging polygons keep a list of all the ones we have
            List<CompositionPolygon> convexPolygons = new List<CompositionPolygon>();
            foreach (var edge in this.PolygonEdgeGroup.PolygonEdges)
            {
                if (edge.MajorPartner != null)
                {
                    convexPolygons.Add(edge.MajorPartner);
                }

                if (edge.MinorPartner != null)
                {
                    convexPolygons.Add(edge.MinorPartner);
                }
            }
            convexPolygons = convexPolygons.Distinct().ToList();

            // Remove edges that don't have both partners since we can't possibly merge on them
            this.PolygonEdgeGroup.PolygonEdges.RemoveAll(e => e.MinorPartner == null || e.MajorPartner == null);

            // Now try to remove edges by merging the polygons on both sides
            // We try to remove the longest edges first as, in general, it gives us solutions that avoid long splinters
            var edgesByLength = this.PolygonEdgeGroup.PolygonEdges.OrderByDescending(edge => edge.Length2);

            foreach (var edge in edgesByLength)
            {
                if (edge.CanMergePolygons())
                {
                    // Remove the minor polygon from our list of convex polygons and merge
                    convexPolygons.Remove(edge.MinorPartner);

                    edge.MergePolygons();
                }
            }

            this.ConvexPolygons = convexPolygons.Select(cp => cp.Points.ToArray()).ToList();
        }


    }
}

// ----------------------------------------------------------------------
// CompositionPolygon.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;

namespace Tiled2Unity.Geometry
{
    // For compositional considerations, a Polygon is a group of points and edges
    // This allows us to merge polygons along edges
    public class CompositionPolygon
    {
        public List<PointF> Points { get; private set; }
        public List<PolygonEdge> Edges { get; private set; }

        // A polygon starts off as a triangle with one edge
        // Other points and edges are added to the polygon during merge
        public CompositionPolygon(IEnumerable<PointF> points)
        {
            this.Points = new List<PointF>();
            this.Edges = new List<PolygonEdge>();

            this.Points.AddRange(points);
        }

        public void AddEdge(PolygonEdge edge)
        {
            this.Edges.Add(edge);
        }

        public int NextIndex(int index)
        {
            Debug.Assert(index >= 0);

            return (index + 1) % this.Points.Count;
        }

        public int PrevIndex(int index)
        {
            Debug.Assert(index >= 0);

            if (index == 0)
            {
                return this.Points.Count - 1;
            }

            return (index - 1) % this.Points.Count;
        }

        public PointF NextPoint(int index)
        {
            index = NextIndex(index);
            return this.Points[index];
        }

        public PointF PrevPoint(int index)
        {
            index = PrevIndex(index);
            return this.Points[index];
        }

        public void AbsorbPolygon(int q, CompositionPolygon minor, int pMinor)
        {
            // Insert Minor points Minor[P+1] ... Minor[Q-1] into Major, inserted at Major[Q]
            // Same as inserting numPoints-2 starting at index qMinor+1
            int numMinorPoints = minor.Points.Count - 2;

            List<PointF> pointsToInsert = new List<PointF>();
            for (int i = 0; i < numMinorPoints; ++i)
            {
                int qInsert = (pMinor + 1 + i) % minor.Points.Count;
                pointsToInsert.Add(minor.Points[qInsert]);
            }

            this.Points.InsertRange(q, pointsToInsert);
        }

        public void ReplaceEdgesWithPolygon(CompositionPolygon replacement, PolygonEdge ignoreEdge)
        {
            // This polygon is going away as it was merged with another
            // All edges this polygon referenced will need to reference the replacement instead
            foreach (var edge in this.Edges)
            {
                if (edge == ignoreEdge)
                    continue;

                Debug.Assert(!(edge.MajorPartner == this && edge.MinorPartner == this));

                if (edge.MajorPartner == this)
                {
                    edge.ReplaceMajor(replacement);
                }
                else if (edge.MinorPartner == this)
                {
                    edge.ReplaceMinor(replacement);
                }
            }
        }

        public void UpdateEdgeIndices(PolygonEdge ignoreEdge)
        {
            // All of our edges need to update their indices to us
            foreach (var edge in this.Edges)
            {
                if (edge == ignoreEdge)
                    continue;

                edge.UpdateIndices(this);
            }
        }


    }
}

// ----------------------------------------------------------------------
// Math.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;

namespace Tiled2Unity.Geometry
{
    class Math
    {
        // Points are ordered CCW with B as the junction
        public static float Cross(PointF A, PointF B, PointF C)
        {
            PointF lhs = new PointF(B.X - A.X, B.Y - A.Y);
            PointF rhs = new PointF(C.X - B.X, C.Y - B.Y);
            return (lhs.X * rhs.Y) - (lhs.Y * rhs.X);
        }

    }
}

// ----------------------------------------------------------------------
// PolygonEdge.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;



namespace Tiled2Unity.Geometry
{
    // A polygon edge that may be shared with another polygon
    public class PolygonEdge
    {
        public bool HasBeenMerged { get; private set; }

        public PointF P { get; private set; }
        public PointF Q { get; private set; }
        public float Length2 { get; private set; }

        // Our Major partner (the edge PQ will be counter-clockwise on this polygon)
        // When we merge polygons it is always the Major partner that absorbs
        public CompositionPolygon MajorPartner { get; private set; }
        public int MajorPartner_pIndex { get; private set; }
        public int MajorPartner_qIndex { get; private set; }

        // Our Minor partner (the edge PQ will clockwise on this polygon)
        public CompositionPolygon MinorPartner { get; private set; }
        public int MinorPartner_pIndex { get; private set; }
        public int MinorPartner_qIndex { get; private set; }

        public PolygonEdge(CompositionPolygon compPolygon, int p)
        {
            Debug.Assert(compPolygon.Points.Count >= 3);

            this.HasBeenMerged = false;

            // P and Q make up our edge
            int q = (p + 1) % compPolygon.Points.Count;
            this.P = compPolygon.Points[p];
            this.Q = compPolygon.Points[q];

            // Create a compositional polygon with our edge
            this.MajorPartner = compPolygon;
            this.MajorPartner_pIndex = p;
            this.MajorPartner_qIndex = q;

            // Calculate the squared length
            float x = (this.P.X - this.Q.X);
            float y = (this.P.Y - this.Q.Y);
            this.Length2 = (x * x) + (y * y);
        }

        public void AssignMinorPartner(CompositionPolygon polygon)
        {
            Debug.Assert(this.MinorPartner == null);
            Debug.Assert(this.MajorPartner != null);

            ReplaceMinor(polygon);
        }

        public void ReplaceMajor(CompositionPolygon polygon)
        {
            this.MajorPartner = polygon;
            this.MajorPartner_pIndex = this.MajorPartner.Points.IndexOf(this.P);
            this.MajorPartner_qIndex = this.MajorPartner.Points.IndexOf(this.Q);
        }

        public void ReplaceMinor(CompositionPolygon polygon)
        {
            this.MinorPartner = polygon;
            this.MinorPartner_pIndex = this.MinorPartner.Points.IndexOf(this.P);
            this.MinorPartner_qIndex = this.MinorPartner.Points.IndexOf(this.Q);
        }

        public bool CanMergePolygons()
        {
            // The two polygon partners can be merged if the two vectors on each point where they would merge don't create a concave polygon
            // Concave testing is done through a cross product and assumes CCW winding of the polyon points

            // Can merge point P of the Major/CCW partner?
            {
                // A = CWW[P - 1]
                // B = CWW[P]
                // C = CW[P + 1]
                PointF A = this.MajorPartner.PrevPoint(this.MajorPartner_pIndex);
                PointF B = this.MajorPartner.Points[this.MajorPartner_pIndex];
                PointF C = this.MinorPartner.NextPoint(this.MinorPartner_pIndex);
                float cross = Geometry.Math.Cross(A, B, C);
                if (cross  > 0)
                    return false;
            }

            // Can merge point Q of Major/CCW partner?
            {
                // A = CWW[Q + 1]
                // B = CWW[Q]
                // C = CW[Q-1]
                PointF A = this.MajorPartner.NextPoint(this.MajorPartner_qIndex);
                PointF B = this.MajorPartner.Points[this.MajorPartner_qIndex];
                PointF C = this.MinorPartner.PrevPoint(this.MinorPartner_qIndex);
                float cross = Geometry.Math.Cross(A, B, C);
                if (cross < 0)
                    return false;
            }

            return true;
        }

        public void MergePolygons()
        {
            Debug.Assert(this.HasBeenMerged == false);

            // The major polygon will absorb the minor
            this.MajorPartner.AbsorbPolygon(this.MajorPartner_qIndex, this.MinorPartner, this.MinorPartner_pIndex);

            // All edges that referened the minor will need to reference the major
            this.MinorPartner.ReplaceEdgesWithPolygon(this.MajorPartner, this);

            // All edges that reference the major will need their P/Q indices updated
            this.MajorPartner.UpdateEdgeIndices(this);

            // This edge has now been processed and we shouldn't merge on it again
            this.HasBeenMerged = true;
        }

        public void UpdateIndices(CompositionPolygon polygon)
        {
            if (polygon == this.MajorPartner)
            {
                this.MajorPartner_pIndex = polygon.Points.IndexOf(this.P);
                this.MajorPartner_qIndex = polygon.Points.IndexOf(this.Q);
            }
            else if (polygon == MinorPartner)
            {
                this.MinorPartner_pIndex = polygon.Points.IndexOf(this.P);
                this.MinorPartner_qIndex = polygon.Points.IndexOf(this.Q);
            }
        }

    }
}

// ----------------------------------------------------------------------
// PolygonEdgeGroup.cs

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Drawing;
// using System.Linq;
// using System.Text;


namespace Tiled2Unity.Geometry
{
    // Keeps a collection of polygon edges that are shared bewteen two polygons
    // Assumes that all polygons have CCW winding to them
    public class PolygonEdgeGroup
    {
        public List<PolygonEdge> PolygonEdges { get; set; }


        public void Initialize(List<PointF[]> polygons)
        {
            this.PolygonEdges = new List<PolygonEdge>();

            foreach (var polygon in polygons)
            {
                // Our polygon will be added to each edge
                CompositionPolygon compPolygon = new CompositionPolygon(polygon);

                // Process all edges of the polygon
                for (int p = polygon.Length - 1, q = 0; q < polygon.Length; p = q++)
                {
                    PointF P = polygon[p];
                    PointF Q = polygon[q];

                    // The clockwise edge may already exist if it was added by an earlier polygon as the counter-clockwise edge
                    // If so, add this polygon as the CW partner of that edge
                    PolygonEdge edge = this.PolygonEdges.FirstOrDefault(e => e.P == Q && e.Q == P);
                    if (edge != null)
                    {
                        // Add ourselves as the Minor/CW partner
                        edge.AssignMinorPartner(compPolygon);
                        compPolygon.AddEdge(edge);
                    }
                    else
                    {
                        // If this edge is new to the collection then add it with this polygon being the CCW partner
                        PolygonEdge newEdge = new PolygonEdge(compPolygon, p);
                        compPolygon.AddEdge(newEdge);
                        this.PolygonEdges.Add(newEdge);
                    }
                }
            }
        }

    }
}

// ----------------------------------------------------------------------
// TriangulateClipperSolution.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;


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

                // Assre each triangle needs to be CCW
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

// NOTE: Clipper library was put into Tiled2Unity namespace to avoid name collision with Mac drawing libraries (which also used Clipper)
// NOTE: Version 6.4.0 of Clipper is a beta release but fixes bugs with open path clipping

/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.4.0                                                           *
* Date      :  2 July 2015                                                     *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2015                                         *
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


// using System;
// using System.Collections.Generic;
//using System.Text;          //for Int128.AsString() & StringBuilder
//using System.IO;            //debugging with streamReader & StreamWriter
//using System.Windows.Forms; //debugging to clipboard

namespace Tiled2Unity.ClipperLib
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

      //The GC probably handles this cleanup more efficiently ...
      //~PolyTree(){Clear();}
        
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
      return a.X != b.X  || a.Y != b.Y; 
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

  internal enum EdgeSide {esLeft, esRight};
  internal enum Direction {dRightToLeft, dLeftToRight};
    
  internal class TEdge {
    internal IntPoint Bot;
    internal IntPoint Curr; //current (updated for every new scanbeam)
    internal IntPoint Top;
    internal IntPoint Delta;
    internal double Dx;
    internal PolyType PolyTyp;
    internal EdgeSide Side; //side only refers to current side of solution poly
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

  internal class Maxima
  {
      internal cInt X;
      internal Maxima Next;
      internal Maxima Prev;
  };

  //OutRec: contains a path in the clipping solution. Edges in the AEL will
  //carry a pointer to an OutRec when they are part of the clipping solution.
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
    internal const double horizontal = -3.4E+38;
    internal const int Skip = -2;
    internal const int Unassigned = -1;
    internal const double tolerance = 1.0E-20;
    internal static bool near_zero(double val){return (val > -tolerance) && (val < tolerance);}

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
    internal Scanbeam m_Scanbeam;
    internal List<OutRec> m_PolyOuts;
    internal TEdge m_ActiveEdges;
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

    internal static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
        IntPoint pt3, bool UseFullRange)
    {
        if (UseFullRange)
            return Int128.Int128Mul(pt1.Y - pt2.Y, pt2.X - pt3.X) ==
              Int128.Int128Mul(pt1.X - pt2.X, pt2.Y - pt3.Y);
        else return
          (cInt)(pt1.Y - pt2.Y) * (pt2.X - pt3.X) - (cInt)(pt1.X - pt2.X) * (pt2.Y - pt3.Y) == 0;
    }
    //------------------------------------------------------------------------------

    internal static bool SlopesEqual(IntPoint pt1, IntPoint pt2,
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
        while( m_MinimaList != null )
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
      for (;;)
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
        if (EStart.Dx == horizontal) //ie an adjoining horizontal skip edge
        {
        if (EStart.Bot.X != E.Bot.X && EStart.Top.X != E.Bot.X)
            ReverseHorizontal(E);
        }
        else if (EStart.Bot.X != E.Bot.X)
        ReverseHorizontal(E);
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
          if (Horz.Prev.Top.X > Result.Next.Top.X) Result = Horz.Prev;
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
          if (Horz.Next.Top.X == Result.Prev.Top.X || 
              Horz.Next.Top.X > Result.Prev.Top.X) Result = Horz.Next;
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
      List<TEdge> edges = new List<TEdge>(highI+1);
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
      for (;;)
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
        LocalMinima locMin = new LocalMinima();
        locMin.Next = null;
        locMin.Y = E.Bot.Y;
        locMin.LeftBound = null;
        locMin.RightBound = E;
        locMin.RightBound.Side = EdgeSide.esRight;
        locMin.RightBound.WindDelta = 0;
        for ( ; ; )
        {
          if (E.Bot.X != E.Prev.Top.X) ReverseHorizontal(E);
          if (E.Next.OutIdx == Skip) break;
          E.NextInLML = E.Next;
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

      for (;;)
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
        } else
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
      if( m_MinimaList == null )
      {
        m_MinimaList = newLm;
      }
      else if( newLm.Y >= m_MinimaList.Y )
      {
        newLm.Next = m_MinimaList;
        m_MinimaList = newLm;
      } else
      {
        LocalMinima tmpLm = m_MinimaList;
        while( tmpLm.Next != null  && ( newLm.Y < tmpLm.Next.Y ) )
          tmpLm = tmpLm.Next;
        newLm.Next = tmpLm.Next;
        tmpLm.Next = newLm;
      }
    }
    //------------------------------------------------------------------------------

    internal Boolean PopLocalMinima(cInt Y, out LocalMinima current)
    {
        current = m_CurrentLM;
        if (m_CurrentLM != null && m_CurrentLM.Y == Y)
        {
            m_CurrentLM = m_CurrentLM.Next;
            return true;
        }
        return false;
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

    internal virtual void Reset()
    {
      m_CurrentLM = m_MinimaList;
      if (m_CurrentLM == null) return; //ie nothing to process

      //reset all edges ...
      m_Scanbeam = null;
      LocalMinima lm = m_MinimaList;
      while (lm != null)
      {
        InsertScanbeam(lm.Y);
        TEdge e = lm.LeftBound;
        if (e != null)
        {
          e.Curr = e.Bot;
          e.OutIdx = Unassigned;
        }
        e = lm.RightBound;
        if (e != null)
        {
          e.Curr = e.Bot;
          e.OutIdx = Unassigned;
        }
        lm = lm.Next;
      }
      m_ActiveEdges = null;
    }
    //------------------------------------------------------------------------------

    public static IntRect GetBounds(Paths paths)
    {
      int i = 0, cnt = paths.Count;
      while (i < cnt && paths[i].Count == 0) i++;
      if (i == cnt) return new IntRect(0,0,0,0);
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
    //------------------------------------------------------------------------------

    internal void InsertScanbeam(cInt Y)
    {
        //single-linked list: sorted descending, ignoring dups.
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

    internal Boolean PopScanbeam(out cInt Y)
    {
        if (m_Scanbeam == null)
        {
            Y = 0;
            return false;
        }
        Y = m_Scanbeam.Y;
        m_Scanbeam = m_Scanbeam.Next;
        return true;
    }
    //------------------------------------------------------------------------------

    internal Boolean LocalMinimaPending()
    {
        return (m_CurrentLM != null);
    }
    //------------------------------------------------------------------------------

    internal OutRec CreateOutRec()
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

    internal void DisposeOutRec(int index)
    {
        OutRec outRec = m_PolyOuts[index];
        outRec.Pts = null;
        outRec = null;
        m_PolyOuts[index] = null;
    }
    //------------------------------------------------------------------------------

    internal void UpdateEdgeIntoAEL(ref TEdge e)
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

    internal void SwapPositionsInAEL(TEdge edge1, TEdge edge2)
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

    internal void DeleteFromAEL(TEdge e)
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

  } //end ClipperBase

  public class Clipper : ClipperBase
  {
      //InitOptions that can be passed to the constructor ...
      public const int ioReverseSolution = 1;
      public const int ioStrictlySimple = 2;
      public const int ioPreserveCollinear = 4;

      private ClipType m_ClipType;
      private Maxima m_Maxima;
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
      public Clipper(int InitOptions = 0): base() //constructor
      {
          m_Scanbeam = null;
          m_Maxima = null;
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

      private void InsertMaxima(cInt X)
      {
          //double-linked list: sorted ascending, ignoring dups.
          Maxima newMax = new Maxima();
          newMax.X = X;
          if (m_Maxima == null)
          {
              m_Maxima = newMax;
              m_Maxima.Next = null;
              m_Maxima.Prev = null;
          }
          else if (X < m_Maxima.X)
          {
              newMax.Next = m_Maxima;
              newMax.Prev = null;
              m_Maxima = newMax;
          }
          else
          {
              Maxima m = m_Maxima;
              while (m.Next != null && (X >= m.Next.X)) m = m.Next;
              if (X == m.X) return; //ie ignores duplicates (& CG to clean up newMax)
              //insert newMax between m and m.Next ...
              newMax.Next = m.Next;
              newMax.Prev = m;
              if (m.Next != null) m.Next.Prev = newMax;
              m.Next = newMax;
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
       
      public bool Execute(ClipType clipType, Paths solution, 
          PolyFillType FillType = PolyFillType.pftEvenOdd)
      {
          return Execute(clipType, solution, FillType, FillType);
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, PolyTree polytree,
          PolyFillType FillType = PolyFillType.pftEvenOdd)
      {
          return Execute(clipType, polytree, FillType, FillType);
      }
      //------------------------------------------------------------------------------

      public bool Execute(ClipType clipType, Paths solution,
          PolyFillType subjFillType, PolyFillType clipFillType)
      {
          if (m_ExecuteLocked) return false;
          if (m_HasOpenPaths) throw 
            new ClipperException("Error: PolyTree struct is needed for open path clipping.");

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
          m_SortedEdges = null;
          m_Maxima = null;

          cInt botY, topY;
          if (!PopScanbeam(out botY)) return false;
          InsertLocalMinimaIntoAEL(botY);
          while (PopScanbeam(out topY) || LocalMinimaPending())
          {
            ProcessHorizontals();
            m_GhostJoins.Clear();
            if (!ProcessIntersections(topY)) return false;
            ProcessEdgesAtTopOfScanbeam(topY);
            botY = topY;
            InsertLocalMinimaIntoAEL(botY);
          } 

          //fix orientations ...
          foreach (OutRec outRec in m_PolyOuts)
          {
            if (outRec.Pts == null || outRec.IsOpen) continue;
            if ((outRec.IsHole ^ ReverseSolution) == (Area(outRec) > 0))
              ReversePolyPtLinks(outRec.Pts);
          }

          JoinCommonEdges();

          foreach (OutRec outRec in m_PolyOuts)
          {
            if (outRec.Pts == null) 
                continue;
            else if (outRec.IsOpen)
                FixupOutPolyline(outRec);
            else
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

      private void DisposeAllPolyPts(){
        for (int i = 0; i < m_PolyOuts.Count; ++i) DisposeOutRec(i);
        m_PolyOuts.Clear();
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
        LocalMinima lm;
        while (PopLocalMinima(botY, out lm))
        {
          TEdge lb = lm.LeftBound;
          TEdge rb = lm.RightBound;

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
            {
              if (rb.NextInLML != null)
                InsertScanbeam(rb.NextInLML.Top.Y);
              AddEdgeToSEL(rb);
            }
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
            SlopesEqual(lb.PrevInAEL.Curr, lb.PrevInAEL.Top, lb.Curr, lb.Top, m_UseFullRange) &&
            lb.WindDelta != 0 && lb.PrevInAEL.WindDelta != 0)
          {
            OutPt Op2 = AddOutPt(lb.PrevInAEL, lb.Bot);
            AddJoin(Op1, Op2, lb.Top);
          }

          if( lb.NextInAEL != rb )
          {

            if (rb.OutIdx >= 0 && rb.PrevInAEL.OutIdx >= 0 &&
              SlopesEqual(rb.PrevInAEL.Curr, rb.PrevInAEL.Top, rb.Curr, rb.Top, m_UseFullRange) &&
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
          PolyFillType pft;
          pft = (edge.PolyTyp == PolyType.ptSubject ? m_SubjFillType : m_ClipFillType);
          if (edge.WindDelta == 0) edge.WindCnt = (pft == PolyFillType.pftNegative ? -1 : 1);
          else edge.WindCnt = edge.WindDelta;
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
        //SEL pointers in PEdge are use to build transient lists of horizontal edges.
        //However, since we don't need to worry about processing order, all additions
        //are made to the front of the list ...
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

      internal Boolean PopEdgeFromSEL(out TEdge e)
      {
        //Pop edge from front of SEL (ie SEL is a FILO list)
        e = m_SortedEdges;
        if (e == null) return false;
        TEdge oldE = e;
        m_SortedEdges = e.NextInSEL;
        if (m_SortedEdges != null) m_SortedEdges.PrevInSEL = null;
        oldE.NextInSEL = null;
        oldE.PrevInSEL = null;
        return true;
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

        if (prevE != null && prevE.OutIdx >= 0)
        {
          cInt xPrev = TopX(prevE, pt.Y);
          cInt xE = TopX(e, pt.Y);
          if ((xPrev == xE) && (e.WindDelta != 0) && (prevE.WindDelta != 0) &&
            SlopesEqual(new IntPoint(xPrev, pt.Y), prevE.Top, new IntPoint(xE, pt.Y), e.Top, m_UseFullRange))
          {
            OutPt outPt = AddOutPt(prevE, pt);
            AddJoin(result, outPt, e.Top);
          }
        }
        return result;
      }
      //------------------------------------------------------------------------------

      private OutPt AddOutPt(TEdge e, IntPoint pt)
      {
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
              bool ToFront = (e.Side == EdgeSide.esLeft);
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

      private OutPt GetLastOutPt(TEdge e)
      {
        OutRec outRec = m_PolyOuts[e.OutIdx];
        if (e.Side == EdgeSide.esLeft) 
            return outRec.Pts;
        else
            return outRec.Pts.Prev;
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
        TEdge e2 = e.PrevInAEL;
        TEdge eTmp = null;  
        while (e2 != null)
          {
            if (e2.OutIdx >= 0 && e2.WindDelta != 0) 
            {
              if (eTmp == null)
                eTmp = e2;
              else if (eTmp.OutIdx == e2.OutIdx)
                eTmp = null; //paired               
            }
            e2 = e2.PrevInAEL;
          }

        if (eTmp == null)
        {
          outRec.FirstLeft = null;
          outRec.IsHole = false;
        }
        else
        {
          outRec.FirstLeft = m_PolyOuts[eTmp.OutIdx];
          outRec.IsHole = !outRec.FirstLeft.IsHole;
        }
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

        if (Math.Max(dx1p, dx1n) == Math.Max(dx2p, dx2n) &&
          Math.Min(dx1p, dx1n) == Math.Min(dx2p, dx2n))
          return Area(btmPt1) > 0; //if otherwise identical use orientation
        else
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
            } else
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

      bool OutRec1RightOfOutRec2(OutRec outRec1, OutRec outRec2)
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
        OutRec outRec1 = m_PolyOuts[e1.OutIdx];
        OutRec outRec2 = m_PolyOuts[e2.OutIdx];

        OutRec holeStateRec;
        if (OutRec1RightOfOutRec2(outRec1, outRec2)) 
            holeStateRec = outRec2;
        else if (OutRec1RightOfOutRec2(outRec2, outRec1))
            holeStateRec = outRec1;
        else
            holeStateRec = GetLowermostRec(outRec1, outRec2);

        //get the start and ends of both output polygons and
        //join E2 poly onto E1 poly and delete pointers to E2 ...
        OutPt p1_lft = outRec1.Pts;
        OutPt p1_rt = p1_lft.Prev;
        OutPt p2_lft = outRec2.Pts;
        OutPt p2_rt = p2_lft.Prev;

        //join e2 poly onto e1 poly and delete pointers to e2 ...
        if(  e1.Side == EdgeSide.esLeft )
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
          } else
          {
            //x y z a b c
            p2_rt.Next = p1_lft;
            p1_lft.Prev = p2_rt;
            p2_lft.Prev = p1_rt;
            p1_rt.Next = p2_lft;
            outRec1.Pts = p2_lft;
          }
        } else
        {
          if (e2.Side == EdgeSide.esRight)
          {
            //a b c z y x
            ReversePolyPtLinks( p2_lft );
            p1_rt.Next = p2_rt;
            p2_rt.Prev = p1_rt;
            p2_lft.Next = p1_lft;
            p1_lft.Prev = p2_lft;
          } else
          {
            //a b c x y z
            p1_rt.Next = p2_lft;
            p2_lft.Prev = p1_rt;
            p1_lft.Prev = p2_rt;
            p2_rt.Next = p1_lft;
          }
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
        while( e != null )
        {
          if( e.OutIdx == ObsoleteIdx )
          {
            e.OutIdx = OKIdx;
            e.Side = e1.Side;
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
          else if ( (e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
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

      private void ProcessHorizontals()
      {
          TEdge horzEdge; //m_SortedEdges;
          while (PopEdgeFromSEL(out horzEdge))
            ProcessHorizontal(horzEdge);
      }
      //------------------------------------------------------------------------------

      void GetHorzDirection(TEdge HorzEdge, out Direction Dir, out cInt Left, out cInt Right)
      {
        if (HorzEdge.Bot.X < HorzEdge.Top.X)
        {
          Left = HorzEdge.Bot.X;
          Right = HorzEdge.Top.X;
          Dir = Direction.dLeftToRight;
        } else
        {
          Left = HorzEdge.Top.X;
          Right = HorzEdge.Bot.X;
          Dir = Direction.dRightToLeft;
        }
      }
      //------------------------------------------------------------------------

      private void ProcessHorizontal(TEdge horzEdge)
      {
        Direction dir;
        cInt horzLeft, horzRight;
        bool IsOpen = horzEdge.WindDelta == 0;

        GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

        TEdge eLastHorz = horzEdge, eMaxPair = null;
        while (eLastHorz.NextInLML != null && IsHorizontal(eLastHorz.NextInLML)) 
          eLastHorz = eLastHorz.NextInLML;
        if (eLastHorz.NextInLML == null)
          eMaxPair = GetMaximaPair(eLastHorz);

        Maxima currMax = m_Maxima;
        if (currMax != null)
        {
            //get the first maxima in range (X) ...
            if (dir == Direction.dLeftToRight)
            {
              while (currMax != null && currMax.X <= horzEdge.Bot.X)
                  currMax = currMax.Next;
              if (currMax != null && currMax.X >= eLastHorz.Top.X) 
                  currMax = null;
            }
            else
            {
              while (currMax.Next != null && currMax.Next.X < horzEdge.Bot.X) 
                  currMax = currMax.Next;
              if (currMax.X <= eLastHorz.Top.X) currMax = null;
            }
        }

        OutPt op1 = null;
        for (;;) //loop through consec. horizontal edges
        {
          bool IsLastHorz = (horzEdge == eLastHorz);
          TEdge e = GetNextInAEL(horzEdge, dir);
          while(e != null)
          {

              //this code block inserts extra coords into horizontal edges (in output
              //polygons) whereever maxima touch these horizontal edges. This helps
              //'simplifying' polygons (ie if the Simplify property is set).
              if (currMax != null)
              {
                  if (dir == Direction.dLeftToRight)
                  {
                      while (currMax != null && currMax.X < e.Curr.X) 
                      {
                        if (horzEdge.OutIdx >= 0 && !IsOpen) 
                          AddOutPt(horzEdge, new IntPoint(currMax.X, horzEdge.Bot.Y));
                        currMax = currMax.Next;                  
                      }
                  }
                  else
                  {
                      while (currMax != null && currMax.X > e.Curr.X)
                      {
                          if (horzEdge.OutIdx >= 0 && !IsOpen)
                            AddOutPt(horzEdge, new IntPoint(currMax.X, horzEdge.Bot.Y));
                        currMax = currMax.Prev;
                      }
                  }
              };

              if ((dir == Direction.dLeftToRight && e.Curr.X > horzRight) ||
                (dir == Direction.dRightToLeft && e.Curr.X < horzLeft)) break;
                                
              //Also break if we've got to the end of an intermediate horizontal edge ...
              //nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
              if (e.Curr.X == horzEdge.Top.X && horzEdge.NextInLML != null && 
                e.Dx < horzEdge.NextInLML.Dx) break;

              if (horzEdge.OutIdx >= 0 && !IsOpen)  //note: may be done multiple times
              {
                  op1 = AddOutPt(horzEdge, e.Curr);
                  TEdge eNextHorz = m_SortedEdges;
                  while (eNextHorz != null)
                  {
                      if (eNextHorz.OutIdx >= 0 &&
                        HorzSegmentsOverlap(horzEdge.Bot.X,
                        horzEdge.Top.X, eNextHorz.Bot.X, eNextHorz.Top.X))
                      {
                          OutPt op2 = GetLastOutPt(eNextHorz);
                          AddJoin(op2, op1, eNextHorz.Top);
                      }
                      eNextHorz = eNextHorz.NextInSEL;
                  }
                  AddGhostJoin(op1, horzEdge.Bot);
              }
            
              //OK, so far we're still in range of the horizontal Edge  but make sure
              //we're at the last of consec. horizontals when matching with eMaxPair
              if(e == eMaxPair && IsLastHorz)
              {
                if (horzEdge.OutIdx >= 0)
                  AddLocalMaxPoly(horzEdge, eMaxPair, horzEdge.Top);
                DeleteFromAEL(horzEdge);
                DeleteFromAEL(eMaxPair);
                return;
              }
              
              if(dir == Direction.dLeftToRight)
              {
                IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
                IntersectEdges(horzEdge, e, Pt);
              }
              else
              {
                IntPoint Pt = new IntPoint(e.Curr.X, horzEdge.Curr.Y);
                IntersectEdges(e, horzEdge, Pt);
              }
              TEdge eNext = GetNextInAEL(e, dir);
              SwapPositionsInAEL(horzEdge, e);
              e = eNext;
          } //end while(e != null)

          //Break out of loop if HorzEdge.NextInLML is not also horizontal ...
          if (horzEdge.NextInLML == null || !IsHorizontal(horzEdge.NextInLML)) break;

          UpdateEdgeIntoAEL(ref horzEdge);
          if (horzEdge.OutIdx >= 0) AddOutPt(horzEdge, horzEdge.Bot);
          GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

        } //end for (;;)

        if (horzEdge.OutIdx >= 0 && op1 == null)
        {
            op1 = GetLastOutPt(horzEdge);
            TEdge eNextHorz = m_SortedEdges;
            while (eNextHorz != null)
            {
                if (eNextHorz.OutIdx >= 0 &&
                  HorzSegmentsOverlap(horzEdge.Bot.X,
                  horzEdge.Top.X, eNextHorz.Bot.X, eNextHorz.Top.X))
                {
                    OutPt op2 = GetLastOutPt(eNextHorz);
                    AddJoin(op2, op1, eNextHorz.Top);
                }
                eNextHorz = eNextHorz.NextInSEL;
            }
            AddGhostJoin(op1, horzEdge.Top);
        }

        if (horzEdge.NextInLML != null)
        {
          if(horzEdge.OutIdx >= 0)
          {
            op1 = AddOutPt( horzEdge, horzEdge.Top);

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
          return Direction == Direction.dLeftToRight ? e.NextInAEL: e.PrevInAEL;
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

      internal TEdge GetMaximaPair(TEdge e)
      {
        if ((e.Next.Top == e.Top) && e.Next.NextInLML == null)
          return e.Next;
        else if ((e.Prev.Top == e.Top) && e.Prev.NextInLML == null)
          return e.Prev;
        else 
					return null;
      }
      //------------------------------------------------------------------------------

      internal TEdge GetMaximaPairEx(TEdge e)
      {
        //as above but returns null if MaxPair isn't in AEL (unless it's horizontal)
        TEdge result = GetMaximaPair(e);
        if (result == null || result.OutIdx == Skip ||
          ((result.NextInAEL == result.PrevInAEL) && !IsHorizontal(result))) return null;
        return result;
      }
      //------------------------------------------------------------------------------

      private bool ProcessIntersections(cInt topY)
      {
        if( m_ActiveEdges == null ) return true;
        try {
          BuildIntersectList(topY);
          if ( m_IntersectList.Count == 0) return true;
          if (m_IntersectList.Count == 1 || FixupIntersectionOrder()) 
              ProcessIntersectList();
          else 
              return false;
        }
        catch {
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
        if ( m_ActiveEdges == null ) return;

        //prepare for sorting ...
        TEdge e = m_ActiveEdges;
        m_SortedEdges = e;
        while( e != null )
        {
          e.PrevInSEL = e.PrevInAEL;
          e.NextInSEL = e.NextInAEL;
          e.Curr.X = TopX( e, topY );
          e = e.NextInAEL;
        }

        //bubblesort ...
        bool isModified = true;
        while( isModified && m_SortedEdges != null )
        {
          isModified = false;
          e = m_SortedEdges;
          while( e.NextInSEL != null )
          {
            TEdge eNext = e.NextInSEL;
            IntPoint pt;
            if (e.Curr.X > eNext.Curr.X)
            {
                IntersectPoint(e, eNext, out pt);
                if (pt.Y < topY)
                  pt = new IntPoint(TopX(e, topY), topY);
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
          if( e.PrevInSEL != null ) e.PrevInSEL.NextInSEL = null;
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
          return edge.Bot.X + Round(edge.Dx *(currentY - edge.Bot.Y));
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
        while(e != null)
        {
          //1. process maxima, treating them as if they're 'bent' horizontal edges,
          //   but exclude maxima with horizontal edges. nb: e can't be a horizontal.
          bool IsMaximaEdge = IsMaxima(e, topY);

          if(IsMaximaEdge)
          {
            TEdge eMaxPair = GetMaximaPairEx(e);
            IsMaximaEdge = (eMaxPair == null || !IsHorizontal(eMaxPair));
          }

          if(IsMaximaEdge)
          {
            if (StrictlySimple) InsertMaxima(e.Top.X);
            TEdge ePrev = e.PrevInAEL;
            DoMaxima(e);
            if( ePrev == null) e = m_ActiveEdges;
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
              e.Curr.X = TopX( e, topY );
              e.Curr.Y = topY;
            }

            //When StrictlySimple and 'e' is being touched by another edge, then
            //make sure both edges have a vertex here ...
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
        ProcessHorizontals();
        m_Maxima = null;

        //4. Promote intermediate vertices ...
        e = m_ActiveEdges;
        while (e != null)
        {
          if(IsIntermediate(e, topY))
          {
            OutPt op = null;
            if( e.OutIdx >= 0 ) 
              op = AddOutPt(e, e.Top);
            UpdateEdgeIntoAEL(ref e);

            //if output polygons share an edge, they'll need joining later ...
            TEdge ePrev = e.PrevInAEL;
            TEdge eNext = e.NextInAEL;
            if (ePrev != null && ePrev.Curr.X == e.Bot.X &&
              ePrev.Curr.Y == e.Bot.Y && op != null &&
              ePrev.OutIdx >= 0 && ePrev.Curr.Y > ePrev.Top.Y &&
              SlopesEqual(e.Curr, e.Top, ePrev.Curr, ePrev.Top, m_UseFullRange) &&
              (e.WindDelta != 0) && (ePrev.WindDelta != 0))
            {
              OutPt op2 = AddOutPt(ePrev, e.Bot);
              AddJoin(op, op2, e.Top);
            }
            else if (eNext != null && eNext.Curr.X == e.Bot.X &&
              eNext.Curr.Y == e.Bot.Y && op != null &&
              eNext.OutIdx >= 0 && eNext.Curr.Y > eNext.Top.Y &&
              SlopesEqual(e.Curr, e.Top, eNext.Curr, eNext.Top, m_UseFullRange) &&
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
        TEdge eMaxPair = GetMaximaPairEx(e);
        if (eMaxPair == null)
        {
          if (e.OutIdx >= 0)
            AddOutPt(e, e.Top);
          DeleteFromAEL(e);
          return;
        }

        TEdge eNext = e.NextInAEL;
        while(eNext != null && eNext != eMaxPair)
        {
          IntersectEdges(e, eNext, e.Top);
          SwapPositionsInAEL(e, eNext);
          eNext = e.NextInAEL;
        }

        if(e.OutIdx == Unassigned && eMaxPair.OutIdx == Unassigned)
        {
          DeleteFromAEL(e);
          DeleteFromAEL(eMaxPair);
        }
        else if( e.OutIdx >= 0 && eMaxPair.OutIdx >= 0 )
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

      private void FixupOutPolyline(OutRec outrec)
      {
        OutPt pp = outrec.Pts;
        OutPt lastPP = pp.Prev;
        while (pp != lastPP)
        {
            pp = pp.Next;
            if (pp.Pt == pp.Prev.Pt)
            {
                if (pp == lastPP) lastPP = pp.Prev;
                OutPt tmpPP = pp.Prev;
                tmpPP.Next = pp.Next;
                pp.Next.Prev = tmpPP;
                pp = tmpPP;
            }
        }
        if (pp == pp.Prev) outrec.Pts = null;
      }
      //------------------------------------------------------------------------------

      private void FixupOutPolygon(OutRec outRec)
      {
          //FixupOutPolygon() - removes duplicate points and simplifies consecutive
          //parallel edges by removing the middle vertex.
          OutPt lastOK = null;
          outRec.BottomPt = null;
          OutPt pp = outRec.Pts;
          bool preserveCol = PreserveCollinear || StrictlySimple;
          for (;;)
          {
              if (pp.Prev == pp || pp.Prev == pp.Next)
              {
                  outRec.Pts = null;
                  return;
              }
              //test for duplicate points and collinear edges ...
              if ((pp.Pt == pp.Next.Pt) || (pp.Pt == pp.Prev.Pt) ||
                (SlopesEqual(pp.Prev.Pt, pp.Pt, pp.Next.Pt, m_UseFullRange) &&
                (!preserveCol || !Pt2IsBetweenPt1AndPt3(pp.Prev.Pt, pp.Pt, pp.Next.Pt))))
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
          if (b1 < b2) {Left = Math.Max(a1,b1); Right = Math.Min(a2,b2);}
          else {Left = Math.Max(a1,b2); Right = Math.Min(a2,b1);}
        } 
        else
        {
          if (b1 < b2) {Left = Math.Max(a2,b1); Right = Math.Min(a1,b2);}
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
        } else
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
        //1. Horizontal joins where Join.OutPt1 & Join.OutPt2 are vertices anywhere
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
          } else
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
          else if (op2.Pt.X >= Left&& op2.Pt.X <= Right) 
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
        } else
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
          } else
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

      //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
      //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
      private static int PointInPolygon(IntPoint pt, OutPt op)
      {
        //returns 0 if false, +1 if true, -1 if pt ON polygon boundary
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
        foreach (OutRec outRec in m_PolyOuts)
        {
          OutRec firstLeft = ParseFirstLeft(outRec.FirstLeft);
          if (outRec.Pts != null && firstLeft == OldOutRec)
          {
            if (Poly2ContainsPoly1(outRec.Pts, NewOutRec.Pts))
                outRec.FirstLeft = NewOutRec;
          }
        }
      }
      //----------------------------------------------------------------------

      private void FixupFirstLefts2(OutRec innerOutRec, OutRec outerOutRec)
      {
        //A polygon has split into two such that one is now the inner of the other.
        //It's possible that these polygons now wrap around other polygons, so check
        //every polygon that's also contained by OuterOutRec's FirstLeft container
        //(including nil) to see if they've become inner to the new inner polygon ...
        OutRec orfl = outerOutRec.FirstLeft;
        foreach (OutRec outRec in m_PolyOuts)
        {
          if (outRec.Pts == null || outRec == outerOutRec || outRec == innerOutRec) 
            continue;
          OutRec firstLeft = ParseFirstLeft(outRec.FirstLeft);
          if (firstLeft != orfl && firstLeft != innerOutRec && firstLeft != outerOutRec) 
            continue;
          if (Poly2ContainsPoly1(outRec.Pts, innerOutRec.Pts))
            outRec.FirstLeft = innerOutRec;
          else if (Poly2ContainsPoly1(outRec.Pts, outerOutRec.Pts))
            outRec.FirstLeft = outerOutRec;
          else if (outRec.FirstLeft == innerOutRec || outRec.FirstLeft == outerOutRec) 
            outRec.FirstLeft = orfl;
        }
      }
      //----------------------------------------------------------------------

      private void FixupFirstLefts3(OutRec OldOutRec, OutRec NewOutRec)
      {
        //same as FixupFirstLefts1 but doesn't call Poly2ContainsPoly1()
        foreach (OutRec outRec in m_PolyOuts)
        {
          OutRec firstLeft = ParseFirstLeft(outRec.FirstLeft);
          if (outRec.Pts != null && outRec.FirstLeft == OldOutRec) 
            outRec.FirstLeft = NewOutRec;
        }
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
          if (outRec1.IsOpen || outRec2.IsOpen) continue;

          //get the polygon fragment with the correct hole state (FirstLeft)
          //before calling JoinPoints() ...
          OutRec holeStateRec;
          if (outRec1 == outRec2) holeStateRec = outRec1;
          else if (OutRec1RightOfOutRec2(outRec1, outRec2)) holeStateRec = outRec2;
          else if (OutRec1RightOfOutRec2(outRec2, outRec1)) holeStateRec = outRec1;
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

            if (Poly2ContainsPoly1(outRec2.Pts, outRec1.Pts))
            {
              //outRec1 contains outRec2 ...
              outRec2.IsHole = !outRec1.IsHole;
              outRec2.FirstLeft = outRec1;

              if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);

              if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                ReversePolyPtLinks(outRec2.Pts);

            }
            else if (Poly2ContainsPoly1(outRec1.Pts, outRec2.Pts))
            {
              //outRec2 contains outRec1 ...
              outRec2.IsHole = outRec1.IsHole;
              outRec1.IsHole = !outRec2.IsHole;
              outRec2.FirstLeft = outRec1.FirstLeft;
              outRec1.FirstLeft = outRec2;

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
     
          } else
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
            if (m_UsingPolyTree) FixupFirstLefts3(outRec2, outRec1);
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
        while(op != outrec.Pts);
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

      internal double Area(OutRec outRec)
      {
        return Area(outRec.Pts);
      }
      //------------------------------------------------------------------------------

      internal double Area(OutPt op)
      {
        OutPt opFirst = op;
        if (op == null) return 0;
        double a = 0;
        do {
          a = a + (double)(op.Prev.Pt.X + op.Pt.X) * (double)(op.Prev.Pt.Y - op.Pt.Y);
          op = op.Next;
        } while (op != opFirst);
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
        return (dx*dx + dy*dy);
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
        double C = A * ln1.X  + B * ln1.Y;
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

        OutPt [] outPts = new OutPt[cnt];
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
      int steps = Math.Max((int)Round(m_StepsPerRad * Math.Abs(a)),1);

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
      public ClipperException(string description) : base(description){}
  }
  //------------------------------------------------------------------------------

} //end ClipperLib namespace

// ----------------------------------------------------------------------
// Dict.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

#if DOUBLE
namespace LibTessDotNet.Double
#else
namespace LibTessDotNet
#endif
{
    internal class Dict<TValue> where TValue : class
    {
        public class Node
        {
            internal TValue _key;
            internal Node _prev, _next;

            public TValue Key { get { return _key; } }
            public Node Prev { get { return _prev; } }
            public Node Next { get { return _next; } }
        }

        public delegate bool LessOrEqual(TValue lhs, TValue rhs);

        private LessOrEqual _leq;
        Node _head;

        public Dict(LessOrEqual leq)
        {
            _leq = leq;

            _head = new Node { _key = null };
            _head._prev = _head;
            _head._next = _head;
        }

        public Node Insert(TValue key)
        {
            return InsertBefore(_head, key);
        }

        public Node InsertBefore(Node node, TValue key)
        {
            do {
                node = node._prev;
            } while (node._key != null && !_leq(node._key, key));

            var newNode = new Node { _key = key };
            newNode._next = node._next;
            node._next._prev = newNode;
            newNode._prev = node;
            node._next = newNode;

            return newNode;
        }

        public Node Find(TValue key)
        {
            var node = _head;
            do {
                node = node._next;
            } while (node._key != null && !_leq(key, node._key));
            return node;
        }

        public Node Min()
        {
            return _head._next;
        }

        public void Remove(Node node)
        {
            node._next._prev = node._prev;
            node._prev._next = node._next;
        }
    }
}

// ----------------------------------------------------------------------
// Geom.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Diagnostics;

#if DOUBLE
// using Real = System.Double;
namespace LibTessDotNet.Double
#else
// using Real = System.Single;
namespace LibTessDotNet
#endif
{
    internal static class Geom
    {
        public static bool IsWindingInside(WindingRule rule, int n)
        {
            switch (rule)
            {
                case WindingRule.EvenOdd:
                    return (n & 1) == 1;
                case WindingRule.NonZero:
                    return n != 0;
                case WindingRule.Positive:
                    return n > 0;
                case WindingRule.Negative:
                    return n < 0;
                case WindingRule.AbsGeqTwo:
                    return n >= 2 || n <= -2;
            }
            throw new Exception("Wrong winding rule");
        }

        public static bool VertCCW(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            return (u._s * (v._t - w._t) + v._s * (w._t - u._t) + w._s * (u._t - v._t)) >= 0.0f;
        }
        public static bool VertEq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return lhs._s == rhs._s && lhs._t == rhs._t;
        }
        public static bool VertLeq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return (lhs._s < rhs._s) || (lhs._s == rhs._s && lhs._t <= rhs._t);
        }

        /// <summary>
        /// Given three vertices u,v,w such that VertLeq(u,v) && VertLeq(v,w),
        /// evaluates the t-coord of the edge uw at the s-coord of the vertex v.
        /// Returns v->t - (uw)(v->s), ie. the signed distance from uw to v.
        /// If uw is vertical (and thus passes thru v), the result is zero.
        /// 
        /// The calculation is extremely accurate and stable, even when v
        /// is very close to u or w.  In particular if we set v->t = 0 and
        /// let r be the negated result (this evaluates (uw)(v->s)), then
        /// r is guaranteed to satisfy MIN(u->t,w->t) <= r <= MAX(u->t,w->t).
        /// </summary>
        public static Real EdgeEval(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(VertLeq(u, v) && VertLeq(v, w));

            var gapL = v._s - u._s;
            var gapR = w._s - v._s;

            if (gapL + gapR > 0.0f)
            {
                if (gapL < gapR)
                {
                    return (v._t - u._t) + (u._t - w._t) * (gapL / (gapL + gapR));
                }
                else
                {
                    return (v._t - w._t) + (w._t - u._t) * (gapR / (gapL + gapR));
                }
            }
            /* vertical line */
            return 0;
        }

        /// <summary>
        /// Returns a number whose sign matches EdgeEval(u,v,w) but which
        /// is cheaper to evaluate. Returns > 0, == 0 , or < 0
        /// as v is above, on, or below the edge uw.
        /// </summary>
        public static Real EdgeSign(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(VertLeq(u, v) && VertLeq(v, w));

            var gapL = v._s - u._s;
            var gapR = w._s - v._s;

            if (gapL + gapR > 0.0f)
            {
                return (v._t - w._t) * gapL + (v._t - u._t) * gapR;
            }
            /* vertical line */
            return 0;
        }

        public static bool TransLeq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return (lhs._t < rhs._t) || (lhs._t == rhs._t && lhs._s <= rhs._s);
        }

        public static Real TransEval(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(TransLeq(u, v) && TransLeq(v, w));

            var gapL = v._t - u._t;
            var gapR = w._t - v._t;

            if (gapL + gapR > 0.0f)
            {
                if (gapL < gapR)
                {
                    return (v._s - u._s) + (u._s - w._s) * (gapL / (gapL + gapR));
                }
                else
                {
                    return (v._s - w._s) + (w._s - u._s) * (gapR / (gapL + gapR));
                }
            }
            /* vertical line */
            return 0;
        }

        public static Real TransSign(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(TransLeq(u, v) && TransLeq(v, w));

            var gapL = v._t - u._t;
            var gapR = w._t - v._t;

            if (gapL + gapR > 0.0f)
            {
                return (v._s - w._s) * gapL + (v._s - u._s) * gapR;
            }
            /* vertical line */
            return 0;
        }

        public static bool EdgeGoesLeft(MeshUtils.Edge e)
        {
            return VertLeq(e._Dst, e._Org);
        }

        public static bool EdgeGoesRight(MeshUtils.Edge e)
        {
            return VertLeq(e._Org, e._Dst);
        }

        public static Real VertL1dist(MeshUtils.Vertex u, MeshUtils.Vertex v)
        {
            return Math.Abs(u._s - v._s) + Math.Abs(u._t - v._t);
        }

        public static void AddWinding(MeshUtils.Edge eDst, MeshUtils.Edge eSrc)
        {
            eDst._winding += eSrc._winding;
            eDst._Sym._winding += eSrc._Sym._winding;
        }

        public static Real Interpolate(Real a, Real x, Real b, Real y)
        {
            if (a < 0.0f)
            {
                a = 0.0f;
            }
            if (b < 0.0f)
            {
                b = 0.0f;
            }
            return ((a <= b) ? ((b == 0.0f) ? ((x+y) / 2.0f)
                    : (x + (y-x) * (a/(a+b))))
                    : (y + (x-y) * (b/(a+b))));
        }

        static void Swap(ref MeshUtils.Vertex a, ref MeshUtils.Vertex b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        /// <summary>
        /// Given edges (o1,d1) and (o2,d2), compute their point of intersection.
        /// The computed point is guaranteed to lie in the intersection of the
        /// bounding rectangles defined by each edge.
        /// </summary>
        public static void EdgeIntersect(MeshUtils.Vertex o1, MeshUtils.Vertex d1, MeshUtils.Vertex o2, MeshUtils.Vertex d2, MeshUtils.Vertex v)
        {
            // This is certainly not the most efficient way to find the intersection
            // of two line segments, but it is very numerically stable.
            // 
            // Strategy: find the two middle vertices in the VertLeq ordering,
            // and interpolate the intersection s-value from these.  Then repeat
            // using the TransLeq ordering to find the intersection t-value.

            if (!VertLeq(o1, d1)) { Swap(ref o1, ref d1); }
            if (!VertLeq(o2, d2)) { Swap(ref o2, ref d2); }
            if (!VertLeq(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!VertLeq(o2, d1))
            {
                // Technically, no intersection -- do our best
                v._s = (o2._s + d1._s) / 2.0f;
            }
            else if (VertLeq(d1, d2))
            {
                // Interpolate between o2 and d1
                var z1 = EdgeEval(o1, o2, d1);
                var z2 = EdgeEval(o2, d1, d2);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._s = Interpolate(z1, o2._s, z2, d1._s);
            }
            else
            {
                // Interpolate between o2 and d2
                var z1 = EdgeSign(o1, o2, d1);
                var z2 = -EdgeSign(o1, d2, d1);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._s = Interpolate(z1, o2._s, z2, d2._s);
            }

            // Now repeat the process for t

            if (!TransLeq(o1, d1)) { Swap(ref o1, ref d1); }
            if (!TransLeq(o2, d2)) { Swap(ref o2, ref d2); }
            if (!TransLeq(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!TransLeq(o2, d1))
            {
                // Technically, no intersection -- do our best
                v._t = (o2._t + d1._t) / 2.0f;
            }
            else if (TransLeq(d1, d2))
            {
                // Interpolate between o2 and d1
                var z1 = TransEval(o1, o2, d1);
                var z2 = TransEval(o2, d1, d2);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._t = Interpolate(z1, o2._t, z2, d1._t);
            }
            else
            {
                // Interpolate between o2 and d2
                var z1 = TransSign(o1, o2, d1);
                var z2 = -TransSign(o1, d2, d1);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._t = Interpolate(z1, o2._t, z2, d2._t);
            }
        }
    }
}

// ----------------------------------------------------------------------
// Mesh.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Diagnostics;

#if DOUBLE
namespace LibTessDotNet.Double
#else
namespace LibTessDotNet
#endif
{
    internal class Mesh : MeshUtils.Pooled<Mesh>
    {
        internal MeshUtils.Vertex _vHead;
        internal MeshUtils.Face _fHead;
        internal MeshUtils.Edge _eHead, _eHeadSym;

        public Mesh()
        {
            var v = _vHead = MeshUtils.Vertex.Create();
            var f = _fHead = MeshUtils.Face.Create();

            var pair = MeshUtils.EdgePair.Create();
            var e = _eHead = pair._e;
            var eSym = _eHeadSym = pair._eSym;

            v._next = v._prev = v;
            v._anEdge = null;

            f._next = f._prev = f;
            f._anEdge = null;
            f._trail = null;
            f._marked = false;
            f._inside = false;

            e._next = e;
            e._Sym = eSym;
            e._Onext = null;
            e._Lnext = null;
            e._Org = null;
            e._Lface = null;
            e._winding = 0;
            e._activeRegion = null;

            eSym._next = eSym;
            eSym._Sym = e;
            eSym._Onext = null;
            eSym._Lnext = null;
            eSym._Org = null;
            eSym._Lface = null;
            eSym._winding = 0;
            eSym._activeRegion = null;
        }

        public override void Reset()
        {
            _vHead = null;
            _fHead = null;
            _eHead = _eHeadSym = null;
        }

        public override void OnFree()
        {
            for (MeshUtils.Face f = _fHead._next, fNext = _fHead; f != _fHead; f = fNext)
            {
                fNext = f._next;
                f.Free();
            }
            for (MeshUtils.Vertex v = _vHead._next, vNext = _vHead; v != _vHead; v = vNext)
            {
                vNext = v._next;
                v.Free();
            }
            for (MeshUtils.Edge e = _eHead._next, eNext = _eHead; e != _eHead; e = eNext)
            {
                eNext = e._next;
                e.Free();
            }
        }

        /// <summary>
        /// Creates one edge, two vertices and a loop (face).
        /// The loop consists of the two new half-edges.
        /// </summary>
        public MeshUtils.Edge MakeEdge()
        {
            var e = MeshUtils.MakeEdge(_eHead);

            MeshUtils.MakeVertex(e, _vHead);
            MeshUtils.MakeVertex(e._Sym, _vHead);
            MeshUtils.MakeFace(e, _fHead);

            return e;
        }

        /// <summary>
        /// Splice is the basic operation for changing the
        /// mesh connectivity and topology.  It changes the mesh so that
        ///     eOrg->Onext = OLD( eDst->Onext )
        ///     eDst->Onext = OLD( eOrg->Onext )
        /// where OLD(...) means the value before the meshSplice operation.
        /// 
        /// This can have two effects on the vertex structure:
        ///  - if eOrg->Org != eDst->Org, the two vertices are merged together
        ///  - if eOrg->Org == eDst->Org, the origin is split into two vertices
        /// In both cases, eDst->Org is changed and eOrg->Org is untouched.
        /// 
        /// Similarly (and independently) for the face structure,
        ///  - if eOrg->Lface == eDst->Lface, one loop is split into two
        ///  - if eOrg->Lface != eDst->Lface, two distinct loops are joined into one
        /// In both cases, eDst->Lface is changed and eOrg->Lface is unaffected.
        /// 
        /// Some special cases:
        /// If eDst == eOrg, the operation has no effect.
        /// If eDst == eOrg->Lnext, the new face will have a single edge.
        /// If eDst == eOrg->Lprev, the old face will have a single edge.
        /// If eDst == eOrg->Onext, the new vertex will have a single edge.
        /// If eDst == eOrg->Oprev, the old vertex will have a single edge.
        /// </summary>
        public void Splice(MeshUtils.Edge eOrg, MeshUtils.Edge eDst)
        {
            if (eOrg == eDst)
            {
                return;
            }

            bool joiningVertices = false;
            if (eDst._Org != eOrg._Org)
            {
                // We are merging two disjoint vertices -- destroy eDst->Org
                joiningVertices = true;
                MeshUtils.KillVertex(eDst._Org, eOrg._Org);
            }
            bool joiningLoops = false;
            if (eDst._Lface != eOrg._Lface)
            {
                // We are connecting two disjoint loops -- destroy eDst->Lface
                joiningLoops = true;
                MeshUtils.KillFace(eDst._Lface, eOrg._Lface);
            }

            // Change the edge structure
            MeshUtils.Splice(eDst, eOrg);

            if (!joiningVertices)
            {
                // We split one vertex into two -- the new vertex is eDst->Org.
                // Make sure the old vertex points to a valid half-edge.
                MeshUtils.MakeVertex(eDst, eOrg._Org);
                eOrg._Org._anEdge = eOrg;
            }
            if (!joiningLoops)
            {
                // We split one loop into two -- the new loop is eDst->Lface.
                // Make sure the old face points to a valid half-edge.
                MeshUtils.MakeFace(eDst, eOrg._Lface);
                eOrg._Lface._anEdge = eOrg;
            }
        }

        /// <summary>
        /// Removes the edge eDel. There are several cases:
        /// if (eDel->Lface != eDel->Rface), we join two loops into one; the loop
        /// eDel->Lface is deleted. Otherwise, we are splitting one loop into two;
        /// the newly created loop will contain eDel->Dst. If the deletion of eDel
        /// would create isolated vertices, those are deleted as well.
        /// </summary>
        public void Delete(MeshUtils.Edge eDel)
        {
            var eDelSym = eDel._Sym;

            // First step: disconnect the origin vertex eDel->Org.  We make all
            // changes to get a consistent mesh in this "intermediate" state.

            bool joiningLoops = false;
            if (eDel._Lface != eDel._Rface)
            {
                // We are joining two loops into one -- remove the left face
                joiningLoops = true;
                MeshUtils.KillFace(eDel._Lface, eDel._Rface);
            }

            if (eDel._Onext == eDel)
            {
                MeshUtils.KillVertex(eDel._Org, null);
            }
            else
            {
                // Make sure that eDel->Org and eDel->Rface point to valid half-edges
                eDel._Rface._anEdge = eDel._Oprev;
                eDel._Org._anEdge = eDel._Onext;

                MeshUtils.Splice(eDel, eDel._Oprev);

                if (!joiningLoops)
                {
                    // We are splitting one loop into two -- create a new loop for eDel.
                    MeshUtils.MakeFace(eDel, eDel._Lface);
                }
            }

            // Claim: the mesh is now in a consistent state, except that eDel->Org
            // may have been deleted.  Now we disconnect eDel->Dst.

            if (eDelSym._Onext == eDelSym)
            {
                MeshUtils.KillVertex(eDelSym._Org, null);
                MeshUtils.KillFace(eDelSym._Lface, null);
            }
            else
            {
                // Make sure that eDel->Dst and eDel->Lface point to valid half-edges
                eDel._Lface._anEdge = eDelSym._Oprev;
                eDelSym._Org._anEdge = eDelSym._Onext;
                MeshUtils.Splice(eDelSym, eDelSym._Oprev);
            }

            // Any isolated vertices or faces have already been freed.
            MeshUtils.KillEdge(eDel);
        }

        /// <summary>
        /// Creates a new edge such that eNew == eOrg.Lnext and eNew.Dst is a newly created vertex.
        /// eOrg and eNew will have the same left face.
        /// </summary>
        public MeshUtils.Edge AddEdgeVertex(MeshUtils.Edge eOrg)
        {
            var eNew = MeshUtils.MakeEdge(eOrg);
            var eNewSym = eNew._Sym;

            // Connect the new edge appropriately
            MeshUtils.Splice(eNew, eOrg._Lnext);

            // Set vertex and face information
            eNew._Org = eOrg._Dst;
            MeshUtils.MakeVertex(eNewSym, eNew._Org);
            eNew._Lface = eNewSym._Lface = eOrg._Lface;

            return eNew;
        }

        /// <summary>
        /// Splits eOrg into two edges eOrg and eNew such that eNew == eOrg.Lnext.
        /// The new vertex is eOrg.Dst == eNew.Org.
        /// eOrg and eNew will have the same left face.
        /// </summary>
        public MeshUtils.Edge SplitEdge(MeshUtils.Edge eOrg)
        {
            var eTmp = AddEdgeVertex(eOrg);
            var eNew = eTmp._Sym;

            // Disconnect eOrg from eOrg->Dst and connect it to eNew->Org
            MeshUtils.Splice(eOrg._Sym, eOrg._Sym._Oprev);
            MeshUtils.Splice(eOrg._Sym, eNew);

            // Set the vertex and face information
            eOrg._Dst = eNew._Org;
            eNew._Dst._anEdge = eNew._Sym; // may have pointed to eOrg->Sym
            eNew._Rface = eOrg._Rface;
            eNew._winding = eOrg._winding; // copy old winding information
            eNew._Sym._winding = eOrg._Sym._winding;

            return eNew;
        }

        /// <summary>
        /// Creates a new edge from eOrg->Dst to eDst->Org, and returns the corresponding half-edge eNew.
        /// If eOrg->Lface == eDst->Lface, this splits one loop into two,
        /// and the newly created loop is eNew->Lface.  Otherwise, two disjoint
        /// loops are merged into one, and the loop eDst->Lface is destroyed.
        /// 
        /// If (eOrg == eDst), the new face will have only two edges.
        /// If (eOrg->Lnext == eDst), the old face is reduced to a single edge.
        /// If (eOrg->Lnext->Lnext == eDst), the old face is reduced to two edges.
        /// </summary>
        public MeshUtils.Edge Connect(MeshUtils.Edge eOrg, MeshUtils.Edge eDst)
        {
            var eNew = MeshUtils.MakeEdge(eOrg);
            var eNewSym = eNew._Sym;

            bool joiningLoops = false;
            if (eDst._Lface != eOrg._Lface)
            {
                // We are connecting two disjoint loops -- destroy eDst->Lface
                joiningLoops = true;
                MeshUtils.KillFace(eDst._Lface, eOrg._Lface);
            }

            // Connect the new edge appropriately
            MeshUtils.Splice(eNew, eOrg._Lnext);
            MeshUtils.Splice(eNewSym, eDst);

            // Set the vertex and face information
            eNew._Org = eOrg._Dst;
            eNewSym._Org = eDst._Org;
            eNew._Lface = eNewSym._Lface = eOrg._Lface;

            // Make sure the old face points to a valid half-edge
            eOrg._Lface._anEdge = eNewSym;

            if (!joiningLoops)
            {
                MeshUtils.MakeFace(eNew, eOrg._Lface);
            }

            return eNew;
        }

        /// <summary>
        /// Destroys a face and removes it from the global face list. All edges of
        /// fZap will have a NULL pointer as their left face. Any edges which
        /// also have a NULL pointer as their right face are deleted entirely
        /// (along with any isolated vertices this produces).
        /// An entire mesh can be deleted by zapping its faces, one at a time,
        /// in any order. Zapped faces cannot be used in further mesh operations!
        /// </summary>
        public void ZapFace(MeshUtils.Face fZap)
        {
            var eStart = fZap._anEdge;

            // walk around face, deleting edges whose right face is also NULL
            var eNext = eStart._Lnext;
            MeshUtils.Edge e, eSym;
            do {
                e = eNext;
                eNext = e._Lnext;

                e._Lface = null;
                if (e._Rface == null)
                {
                    // delete the edge -- see TESSmeshDelete above

                    if (e._Onext == e)
                    {
                        MeshUtils.KillVertex(e._Org, null);
                    }
                    else
                    {
                        // Make sure that e._Org points to a valid half-edge
                        e._Org._anEdge = e._Onext;
                        MeshUtils.Splice(e, e._Oprev);
                    }
                    eSym = e._Sym;
                    if (eSym._Onext == eSym)
                    {
                        MeshUtils.KillVertex(eSym._Org, null);
                    }
                    else
                    {
                        // Make sure that eSym._Org points to a valid half-edge
                        eSym._Org._anEdge = eSym._Onext;
                        MeshUtils.Splice(eSym, eSym._Oprev);
                    }
                    MeshUtils.KillEdge(e);
                }
            } while (e != eStart);

            /* delete from circular doubly-linked list */
            var fPrev = fZap._prev;
            var fNext = fZap._next;
            fNext._prev = fPrev;
            fPrev._next = fNext;

            fZap.Free();
        }

        public void MergeConvexFaces(int maxVertsPerFace)
        {
            for (var f = _fHead._next; f != _fHead; f = f._next)
            {
                // Skip faces which are outside the result
                if (!f._inside)
                {
                    continue;
                }

                var eCur = f._anEdge;
                var vStart = eCur._Org;

                while (true)
                {
                    var eNext = eCur._Lnext;
                    var eSym = eCur._Sym;

                    if (eSym != null && eSym._Lface != null && eSym._Lface._inside)
                    {
                        // Try to merge the neighbour faces if the resulting polygons
                        // does not exceed maximum number of vertices.
                        int curNv = f.VertsCount;
                        int symNv = eSym._Lface.VertsCount;
                        if ((curNv + symNv - 2) <= maxVertsPerFace)
                        {
                            // Merge if the resulting poly is convex.
                            if (Geom.VertCCW(eCur._Lprev._Org, eCur._Org, eSym._Lnext._Lnext._Org) &&
                                Geom.VertCCW(eSym._Lprev._Org, eSym._Org, eCur._Lnext._Lnext._Org))
                            {
                                eNext = eSym._Lnext;
                                Delete(eSym);
                                eCur = null;
                            }
                        }
                    }

                    if (eCur != null && eCur._Lnext._Org == vStart)
                        break;

                    // Continue to next edge.
                    eCur = eNext;
                }
            }
        }

        [Conditional("DEBUG")]
        public void Check()
        {
            MeshUtils.Edge e;

            MeshUtils.Face fPrev = _fHead, f;
            for (fPrev = _fHead; (f = fPrev._next) != _fHead; fPrev = f)
            {
                e = f._anEdge;
                do {
                    Debug.Assert(e._Sym != e);
                    Debug.Assert(e._Sym._Sym == e);
                    Debug.Assert(e._Lnext._Onext._Sym == e);
                    Debug.Assert(e._Onext._Sym._Lnext == e);
                    Debug.Assert(e._Lface == f);
                    e = e._Lnext;
                } while (e != f._anEdge);
            }
            Debug.Assert(f._prev == fPrev && f._anEdge == null);

            MeshUtils.Vertex vPrev = _vHead, v;
            for (vPrev = _vHead; (v = vPrev._next) != _vHead; vPrev = v)
            {
                Debug.Assert(v._prev == vPrev);
                e = v._anEdge;
                do
                {
                    Debug.Assert(e._Sym != e);
                    Debug.Assert(e._Sym._Sym == e);
                    Debug.Assert(e._Lnext._Onext._Sym == e);
                    Debug.Assert(e._Onext._Sym._Lnext == e);
                    Debug.Assert(e._Org == v);
                    e = e._Onext;
                } while (e != v._anEdge);
            }
            Debug.Assert(v._prev == vPrev && v._anEdge == null);

            MeshUtils.Edge ePrev = _eHead;
            for (ePrev = _eHead; (e = ePrev._next) != _eHead; ePrev = e)
            {
                Debug.Assert(e._Sym._next == ePrev._Sym);
                Debug.Assert(e._Sym != e);
                Debug.Assert(e._Sym._Sym == e);
                Debug.Assert(e._Org != null);
                Debug.Assert(e._Dst != null);
                Debug.Assert(e._Lnext._Onext._Sym == e);
                Debug.Assert(e._Onext._Sym._Lnext == e);
            }
            Debug.Assert(e._Sym._next == ePrev._Sym
                && e._Sym == _eHeadSym
                && e._Sym._Sym == e
                && e._Org == null && e._Dst == null
                && e._Lface == null && e._Rface == null);
        }
    }
}

// ----------------------------------------------------------------------
// MeshUtils.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;

#if DOUBLE
// using Real = System.Double;
namespace LibTessDotNet.Double
#else
// using Real = System.Single;
namespace LibTessDotNet
#endif
{
    public struct Vec3
    {
        public readonly static Vec3 Zero = new Vec3();

        public Real X, Y, Z;

        public Real this[int index]
        {
            get
            {
                if (index == 0) return X;
                if (index == 1) return Y;
                if (index == 2) return Z;
                throw new IndexOutOfRangeException();
            }
            set
            {
                if (index == 0) X = value;
                else if (index == 1) Y = value;
                else if (index == 2) Z = value;
                else throw new IndexOutOfRangeException();
            }
        }

        public static void Sub(ref Vec3 lhs, ref Vec3 rhs, out Vec3 result)
        {
            result.X = lhs.X - rhs.X;
            result.Y = lhs.Y - rhs.Y;
            result.Z = lhs.Z - rhs.Z;
        }

        public static void Neg(ref Vec3 v)
        {
            v.X = -v.X;
            v.Y = -v.Y;
            v.Z = -v.Z;
        }

        public static void Dot(ref Vec3 u, ref Vec3 v, out Real dot)
        {
            dot = u.X * v.X + u.Y * v.Y + u.Z * v.Z;
        }

        public static void Normalize(ref Vec3 v)
        {
            var len = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            Debug.Assert(len >= 0.0f);
            len = 1.0f / (Real)Math.Sqrt(len);
            v.X *= len;
            v.Y *= len;
            v.Z *= len;
        }

        public static int LongAxis(ref Vec3 v)
        {
            int i = 0;
            if (Math.Abs(v.Y) > Math.Abs(v.X)) i = 1;
            if (Math.Abs(v.Z) > Math.Abs(i == 0 ? v.X : v.Y)) i = 2;
            return i;
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}", X, Y, Z);
        }
    }

    internal static class MeshUtils
    {
        public const int Undef = ~0;

        public abstract class Pooled<T> where T : Pooled<T>, new()
        {
            private static Stack<T> _stack;

            public abstract void Reset();
            public virtual void OnFree() {}

            public static T Create()
            {
                if (_stack != null && _stack.Count > 0)
                {
                    return _stack.Pop();
                }
                return new T();
            }

            public void Free()
            {
                OnFree();
                Reset();
                if (_stack == null)
                {
                    _stack = new Stack<T>();
                }
                _stack.Push((T)this);
            }
        }

        public class Vertex : Pooled<Vertex>
        {
            internal Vertex _prev, _next;
            internal Edge _anEdge;

            internal Vec3 _coords;
            internal Real _s, _t;
            internal PQHandle _pqHandle;
            internal int _n;
            internal object _data;

            public override void Reset()
            {
                _prev = _next = null;
                _anEdge = null;
                _coords = Vec3.Zero;
                _s = 0;
                _t = 0;
                _pqHandle = new PQHandle();
                _n = 0;
                _data = null;
            }
        }

        public class Face : Pooled<Face>
        {
            internal Face _prev, _next;
            internal Edge _anEdge;

            internal Face _trail;
            internal int _n;
            internal bool _marked, _inside;

            internal int VertsCount
            {
                get
                {
                    int n = 0;
                    var eCur = _anEdge;
                    do {
                        n++;
                        eCur = eCur._Lnext;
                    } while (eCur != _anEdge);
                    return n;
                }
            }

            public override void Reset()
            {
                _prev = _next = null;
                _anEdge = null;
                _trail = null;
                _n = 0;
                _marked = false;
                _inside = false;
            }
        }

        public struct EdgePair
        {
            internal Edge _e, _eSym;

            public static EdgePair Create()
            {
                var pair = new MeshUtils.EdgePair();
                pair._e = MeshUtils.Edge.Create();
                pair._e._pair = pair;
                pair._eSym = MeshUtils.Edge.Create();
                pair._eSym._pair = pair;
                return pair;
            }

            public void Reset()
            {
                _e = _eSym = null;
            }
        }

        public class Edge : Pooled<Edge>
        {
            internal EdgePair _pair;
            internal Edge _next, _Sym, _Onext, _Lnext;
            internal Vertex _Org;
            internal Face _Lface;
            internal Tess.ActiveRegion _activeRegion;
            internal int _winding;

            internal Face _Rface { get { return _Sym._Lface; } set { _Sym._Lface = value; } }
            internal Vertex _Dst { get { return _Sym._Org; }  set { _Sym._Org = value; } }

            internal Edge _Oprev { get { return _Sym._Lnext; } set { _Sym._Lnext = value; } }
            internal Edge _Lprev { get { return _Onext._Sym; } set { _Onext._Sym = value; } }
            internal Edge _Dprev { get { return _Lnext._Sym; } set { _Lnext._Sym = value; } }
            internal Edge _Rprev { get { return _Sym._Onext; } set { _Sym._Onext = value; } }
            internal Edge _Dnext { get { return _Rprev._Sym; } set { _Rprev._Sym = value; } }
            internal Edge _Rnext { get { return _Oprev._Sym; } set { _Oprev._Sym = value; } }

            internal static void EnsureFirst(ref Edge e)
            {
                if (e == e._pair._eSym)
                {
                    e = e._Sym;
                }
            }

            public override void Reset()
            {
                _pair.Reset();
                _next = _Sym = _Onext = _Lnext = null;
                _Org = null;
                _Lface = null;
                _activeRegion = null;
                _winding = 0;
            }
        }

        /// <summary>
        /// MakeEdge creates a new pair of half-edges which form their own loop.
        /// No vertex or face structures are allocated, but these must be assigned
        /// before the current edge operation is completed.
        /// </summary>
        public static Edge MakeEdge(Edge eNext)
        {
            Debug.Assert(eNext != null);

            var pair = EdgePair.Create();
            var e = pair._e;
            var eSym = pair._eSym;

            // Make sure eNext points to the first edge of the edge pair
            Edge.EnsureFirst(ref eNext);

            // Insert in circular doubly-linked list before eNext.
            // Note that the prev pointer is stored in Sym->next.
            var ePrev = eNext._Sym._next;
            eSym._next = ePrev;
            ePrev._Sym._next = e;
            e._next = eNext;
            eNext._Sym._next = eSym;

            e._Sym = eSym;
            e._Onext = e;
            e._Lnext = eSym;
            e._Org = null;
            e._Lface = null;
            e._winding = 0;
            e._activeRegion = null;

            eSym._Sym = e;
            eSym._Onext = eSym;
            eSym._Lnext = e;
            eSym._Org = null;
            eSym._Lface = null;
            eSym._winding = 0;
            eSym._activeRegion = null;

            return e;
        }

        /// <summary>
        /// Splice( a, b ) is best described by the Guibas/Stolfi paper or the
        /// CS348a notes (see Mesh.cs). Basically it modifies the mesh so that
        /// a->Onext and b->Onext are exchanged. This can have various effects
        /// depending on whether a and b belong to different face or vertex rings.
        /// For more explanation see Mesh.Splice().
        /// </summary>
        public static void Splice(Edge a, Edge b)
        {
            var aOnext = a._Onext;
            var bOnext = b._Onext;

            aOnext._Sym._Lnext = b;
            bOnext._Sym._Lnext = a;
            a._Onext = bOnext;
            b._Onext = aOnext;
        }

        /// <summary>
        /// MakeVertex( eOrig, vNext ) attaches a new vertex and makes it the
        /// origin of all edges in the vertex loop to which eOrig belongs. "vNext" gives
        /// a place to insert the new vertex in the global vertex list. We insert
        /// the new vertex *before* vNext so that algorithms which walk the vertex
        /// list will not see the newly created vertices.
        /// </summary>
        public static void MakeVertex(Edge eOrig, Vertex vNext)
        {
            var vNew = MeshUtils.Vertex.Create();

            // insert in circular doubly-linked list before vNext
            var vPrev = vNext._prev;
            vNew._prev = vPrev;
            vPrev._next = vNew;
            vNew._next = vNext;
            vNext._prev = vNew;

            vNew._anEdge = eOrig;
            // leave coords, s, t undefined

            // fix other edges on this vertex loop
            var e = eOrig;
            do {
                e._Org = vNew;
                e = e._Onext;
            } while (e != eOrig);
        }

        /// <summary>
        /// MakeFace( eOrig, fNext ) attaches a new face and makes it the left
        /// face of all edges in the face loop to which eOrig belongs. "fNext" gives
        /// a place to insert the new face in the global face list. We insert
        /// the new face *before* fNext so that algorithms which walk the face
        /// list will not see the newly created faces.
        /// </summary>
        public static void MakeFace(Edge eOrig, Face fNext)
        {
            var fNew = MeshUtils.Face.Create();

            // insert in circular doubly-linked list before fNext
            var fPrev = fNext._prev;
            fNew._prev = fPrev;
            fPrev._next = fNew;
            fNew._next = fNext;
            fNext._prev = fNew;

            fNew._anEdge = eOrig;
            fNew._trail = null;
            fNew._marked = false;

            // The new face is marked "inside" if the old one was. This is a
            // convenience for the common case where a face has been split in two.
            fNew._inside = fNext._inside;

            // fix other edges on this face loop
            var e = eOrig;
            do {
                e._Lface = fNew;
                e = e._Lnext;
            } while (e != eOrig);
        }

        /// <summary>
        /// KillEdge( eDel ) destroys an edge (the half-edges eDel and eDel->Sym),
        /// and removes from the global edge list.
        /// </summary>
        public static void KillEdge(Edge eDel)
        {
            // Half-edges are allocated in pairs, see EdgePair above
            Edge.EnsureFirst(ref eDel);

            // delete from circular doubly-linked list
            var eNext = eDel._next;
            var ePrev = eDel._Sym._next;
            eNext._Sym._next = ePrev;
            ePrev._Sym._next = eNext;

            eDel.Free();
        }

        /// <summary>
        /// KillVertex( vDel ) destroys a vertex and removes it from the global
        /// vertex list. It updates the vertex loop to point to a given new vertex.
        /// </summary>
        public static void KillVertex(Vertex vDel, Vertex newOrg)
        {
            var eStart = vDel._anEdge;

            // change the origin of all affected edges
            var e = eStart;
            do {
                e._Org = newOrg;
                e = e._Onext;
            } while (e != eStart);

            // delete from circular doubly-linked list
            var vPrev = vDel._prev;
            var vNext = vDel._next;
            vNext._prev = vPrev;
            vPrev._next = vNext;

            vDel.Free();
        }

        /// <summary>
        /// KillFace( fDel ) destroys a face and removes it from the global face
        /// list. It updates the face loop to point to a given new face.
        /// </summary>
        public static void KillFace(Face fDel, Face newLFace)
        {
            var eStart = fDel._anEdge;

            // change the left face of all affected edges
            var e = eStart;
            do {
                e._Lface = newLFace;
                e = e._Lnext;
            } while (e != eStart);

            // delete from circular doubly-linked list
            var fPrev = fDel._prev;
            var fNext = fDel._next;
            fNext._prev = fPrev;
            fPrev._next = fNext;

            fDel.Free();
        }

        /// <summary>
        /// Return signed area of face.
        /// </summary>
        public static Real FaceArea(Face f)
        {
            Real area = 0;
            var e = f._anEdge;
            do
            {
                area += (e._Org._s - e._Dst._s) * (e._Org._t + e._Dst._t);
                e = e._Lnext;
            } while (e != f._anEdge);
            return area;
        }
    }
}

// ----------------------------------------------------------------------
// PriorityHeap.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Diagnostics;

#if DOUBLE
namespace LibTessDotNet.Double
#else
namespace LibTessDotNet
#endif
{
    internal struct PQHandle
    {
        public static readonly int Invalid = 0x0fffffff;
        internal int _handle;
    }

    internal class PriorityHeap<TValue> where TValue : class
    {
        public delegate bool LessOrEqual(TValue lhs, TValue rhs);

        protected class HandleElem
        {
            internal TValue _key;
            internal int _node;
        }

        private LessOrEqual _leq;
        private int[] _nodes;
        private HandleElem[] _handles;
        private int _size, _max;
        private int _freeList;
        private bool _initialized;

        public bool Empty { get { return _size == 0; } }

        public PriorityHeap(int initialSize, LessOrEqual leq)
        {
            _leq = leq;

            _nodes = new int[initialSize + 1];
            _handles = new HandleElem[initialSize + 1];

            _size = 0;
            _max = initialSize;
            _freeList = 0;
            _initialized = false;

            _nodes[1] = 1;
            _handles[1] = new HandleElem { _key = null };
        }

        private void FloatDown(int curr)
        {
            int child;
            int hCurr, hChild;

            hCurr = _nodes[curr];
            while (true)
            {
                child = curr << 1;
                if (child < _size && _leq(_handles[_nodes[child + 1]]._key, _handles[_nodes[child]]._key))
                {
                    ++child;
                }

                Debug.Assert(child <= _max);

                hChild = _nodes[child];
                if (child > _size || _leq(_handles[hCurr]._key, _handles[hChild]._key))
                {
                    _nodes[curr] = hCurr;
                    _handles[hCurr]._node = curr;
                    break;
                }

                _nodes[curr] = hChild;
                _handles[hChild]._node = curr;
                curr = child;
            }
        }

        private void FloatUp(int curr)
        {
            int parent;
            int hCurr, hParent;

            hCurr = _nodes[curr];
            while (true)
            {
                parent = curr >> 1;
                hParent = _nodes[parent];
                if (parent == 0 || _leq(_handles[hParent]._key, _handles[hCurr]._key))
                {
                    _nodes[curr] = hCurr;
                    _handles[hCurr]._node = curr;
                    break;
                }
                _nodes[curr] = hParent;
                _handles[hParent]._node = curr;
                curr = parent;
            }
        }

        public void Init()
        {
            for (int i = _size; i >= 1; --i)
            {
                FloatDown(i);
            }
            _initialized = true;
        }

        public PQHandle Insert(TValue value)
        {
            int curr = ++_size;
            if ((curr * 2) > _max)
            {
                _max <<= 1;
                Array.Resize(ref _nodes, _max + 1);
                Array.Resize(ref _handles, _max + 1);
            }

            int free;
            if (_freeList == 0)
            {
                free = curr;
            }
            else
            {
                free = _freeList;
                _freeList = _handles[free]._node;
            }

            _nodes[curr] = free;
            if (_handles[free] == null)
            {
                _handles[free] = new HandleElem { _key = value, _node = curr };
            }
            else
            {
                _handles[free]._node = curr;
                _handles[free]._key = value;
            }

            if (_initialized)
            {
                FloatUp(curr);
            }

            Debug.Assert(free != PQHandle.Invalid);
            return new PQHandle { _handle = free };
        }

        public TValue ExtractMin()
        {
            Debug.Assert(_initialized);

            int hMin = _nodes[1];
            TValue min = _handles[hMin]._key;

            if (_size > 0)
            {
                _nodes[1] = _nodes[_size];
                _handles[_nodes[1]]._node = 1;

                _handles[hMin]._key = null;
                _handles[hMin]._node = _freeList;
                _freeList = hMin;

                if (--_size > 0)
                {
                    FloatDown(1);
                }
            }

            return min;
        }

        public TValue Minimum()
        {
            Debug.Assert(_initialized);
            return _handles[_nodes[1]]._key;
        }

        public void Remove(PQHandle handle)
        {
            Debug.Assert(_initialized);

            int hCurr = handle._handle;
            Debug.Assert(hCurr >= 1 && hCurr <= _max && _handles[hCurr]._key != null);

            int curr = _handles[hCurr]._node;
            _nodes[curr] = _nodes[_size];
            _handles[_nodes[curr]]._node = curr;

            if (curr <= --_size)
            {
                if (curr <= 1 || _leq(_handles[_nodes[curr >> 1]]._key, _handles[_nodes[curr]]._key))
                {
                    FloatDown(curr);
                }
                else
                {
                    FloatUp(curr);
                }
            }

            _handles[hCurr]._key = null;
            _handles[hCurr]._node = _freeList;
            _freeList = hCurr;
        }
    }
}

// ----------------------------------------------------------------------
// PriorityQueue.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;

#if DOUBLE
namespace LibTessDotNet.Double
#else
namespace LibTessDotNet
#endif
{
    internal class PriorityQueue<TValue> where TValue : class
    {
        private PriorityHeap<TValue>.LessOrEqual _leq;
        private PriorityHeap<TValue> _heap;
        private TValue[] _keys;
        private int[] _order;

        private int _size, _max;
        private bool _initialized;

        public bool Empty { get { return _size == 0 && _heap.Empty; } }

        public PriorityQueue(int initialSize, PriorityHeap<TValue>.LessOrEqual leq)
        {
            _leq = leq;
            _heap = new PriorityHeap<TValue>(initialSize, leq);

            _keys = new TValue[initialSize];

            _size = 0;
            _max = initialSize;
            _initialized = false;
        }

        class StackItem
        {
            internal int p, r;
        };

        static void Swap(ref int a, ref int b)
        {
            int tmp = a;
            a = b;
            b = tmp;
        }

        public void Init()
        {
            var stack = new Stack<StackItem>();
            int p, r, i, j, piv;
            uint seed = 2016473283;

            p = 0;
            r = _size - 1;
            _order = new int[_size + 1];
            for (piv = 0, i = p; i <= r; ++piv, ++i)
            {
                _order[i] = piv;
            }

            stack.Push(new StackItem { p = p, r = r });
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                p = top.p;
                r = top.r;

                while (r > p + 10)
                {
                    seed = seed * 1539415821 + 1;
                    i = p + (int)(seed % (r - p + 1));
                    piv = _order[i];
                    _order[i] = _order[p];
                    _order[p] = piv;
                    i = p - 1;
                    j = r + 1;
                    do {
                        do { ++i; } while (!_leq(_keys[_order[i]], _keys[piv]));
                        do { --j; } while (!_leq(_keys[piv], _keys[_order[j]]));
                        Swap(ref _order[i], ref _order[j]);
                    } while (i < j);
                    Swap(ref _order[i], ref _order[j]);
                    if (i - p < r - j)
                    {
                        stack.Push(new StackItem { p = j + 1, r = r });
                        r = i - 1;
                    }
                    else
                    {
                        stack.Push(new StackItem { p = p, r = i - 1 });
                        p = j + 1;
                    }
                }
                for (i = p + 1; i <= r; ++i)
                {
                    piv = _order[i];
                    for (j = i; j > p && !_leq(_keys[piv], _keys[_order[j - 1]]); --j)
                    {
                        _order[j] = _order[j - 1];
                    }
                    _order[j] = piv;
                }
            }

#if DEBUG
            p = 0;
            r = _size - 1;
            for (i = p; i < r; ++i)
            {
                Debug.Assert(_leq(_keys[_order[i + 1]], _keys[_order[i]]), "Wrong sort");
            }
#endif

            _max = _size;
            _initialized = true;
            _heap.Init();
        }

        public PQHandle Insert(TValue value)
        {
            if (_initialized)
            {
                return _heap.Insert(value);
            }

            int curr = _size;
            if (++_size >= _max)
            {
                _max <<= 1;
                Array.Resize(ref _keys, _max);
            }

            _keys[curr] = value;
            return new PQHandle { _handle = -(curr + 1) };
        }

        public TValue ExtractMin()
        {
            Debug.Assert(_initialized);

            if (_size == 0)
            {
                return _heap.ExtractMin();
            }
            TValue sortMin = _keys[_order[_size - 1]];
            if (!_heap.Empty)
            {
                TValue heapMin = _heap.Minimum();
                if (_leq(heapMin, sortMin))
                    return _heap.ExtractMin();
            }
            do {
                --_size;
            } while (_size > 0 && _keys[_order[_size - 1]] == null);

            return sortMin;
        }

        public TValue Minimum()
        {
            Debug.Assert(_initialized);

            if (_size == 0)
            {
                return _heap.Minimum();
            }
            TValue sortMin = _keys[_order[_size - 1]];
            if (!_heap.Empty)
            {
                TValue heapMin = _heap.Minimum();
                if (_leq(heapMin, sortMin))
                    return heapMin;
            }
            return sortMin;
        }

        public void Remove(PQHandle handle)
        {
            Debug.Assert(_initialized);

            int curr = handle._handle;
            if (curr >= 0)
            {
                _heap.Remove(handle);
                return;
            }
            curr = -(curr + 1);
            Debug.Assert(curr < _max && _keys[curr] != null);

            _keys[curr] = null;
            while (_size > 0 && _keys[_order[_size - 1]] == null)
            {
                --_size;
            }
        }
    }
}

// ----------------------------------------------------------------------
// Sweep.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Diagnostics;

#if DOUBLE
// using Real = System.Double;
namespace LibTessDotNet.Double
#else
// using Real = System.Single;
namespace LibTessDotNet
#endif
{
    public partial class Tess
    {
        internal class ActiveRegion
        {
            internal MeshUtils.Edge _eUp;
            internal Dict<ActiveRegion>.Node _nodeUp;
            internal int _windingNumber;
            internal bool _inside, _sentinel, _dirty, _fixUpperEdge;
        }

        private ActiveRegion RegionBelow(ActiveRegion reg)
        {
            return reg._nodeUp._prev._key;
        }

        private ActiveRegion RegionAbove(ActiveRegion reg)
        {
            return reg._nodeUp._next._key;
        }

        /// <summary>
        /// Both edges must be directed from right to left (this is the canonical
        /// direction for the upper edge of each region).
        /// 
        /// The strategy is to evaluate a "t" value for each edge at the
        /// current sweep line position, given by tess->event. The calculations
        /// are designed to be very stable, but of course they are not perfect.
        /// 
        /// Special case: if both edge destinations are at the sweep event,
        /// we sort the edges by slope (they would otherwise compare equally).
        /// </summary>
        private bool EdgeLeq(ActiveRegion reg1, ActiveRegion reg2)
        {
            var e1 = reg1._eUp;
            var e2 = reg2._eUp;

            if (e1._Dst == _event)
            {
                if (e2._Dst == _event)
                {
                    // Two edges right of the sweep line which meet at the sweep event.
                    // Sort them by slope.
                    if (Geom.VertLeq(e1._Org, e2._Org))
                    {
                        return Geom.EdgeSign(e2._Dst, e1._Org, e2._Org) <= 0.0f;
                    }
                    return Geom.EdgeSign(e1._Dst, e2._Org, e1._Org) >= 0.0f;
                }
                return Geom.EdgeSign(e2._Dst, _event, e2._Org) <= 0.0f;
            }
            if (e2._Dst == _event)
            {
                return Geom.EdgeSign(e1._Dst, _event, e1._Org) >= 0.0f;
            }

            // General case - compute signed distance *from* e1, e2 to event
            var t1 = Geom.EdgeEval(e1._Dst, _event, e1._Org);
            var t2 = Geom.EdgeEval(e2._Dst, _event, e2._Org);
            return (t1 >= t2);
        }

        private void DeleteRegion(ActiveRegion reg)
        {
            if (reg._fixUpperEdge)
            {
                // It was created with zero winding number, so it better be
                // deleted with zero winding number (ie. it better not get merged
                // with a real edge).
                Debug.Assert(reg._eUp._winding == 0);
            }
            reg._eUp._activeRegion = null;
            _dict.Remove(reg._nodeUp);
        }

        /// <summary>
        /// Replace an upper edge which needs fixing (see ConnectRightVertex).
        /// </summary>
        private void FixUpperEdge(ActiveRegion reg, MeshUtils.Edge newEdge)
        {
            Debug.Assert(reg._fixUpperEdge);
            _mesh.Delete(reg._eUp);
            reg._fixUpperEdge = false;
            reg._eUp = newEdge;
            newEdge._activeRegion = reg;
        }

        private ActiveRegion TopLeftRegion(ActiveRegion reg)
        {
            var org = reg._eUp._Org;

            // Find the region above the uppermost edge with the same origin
            do {
                reg = RegionAbove(reg);
            } while (reg._eUp._Org == org);

            // If the edge above was a temporary edge introduced by ConnectRightVertex,
            // now is the time to fix it.
            if (reg._fixUpperEdge)
            {
                var e = _mesh.Connect(RegionBelow(reg)._eUp._Sym, reg._eUp._Lnext);
                FixUpperEdge(reg, e);
                reg = RegionAbove(reg);
            }

            return reg;
        }

        private ActiveRegion TopRightRegion(ActiveRegion reg)
        {
            var dst = reg._eUp._Dst;

            // Find the region above the uppermost edge with the same destination
            do {
                reg = RegionAbove(reg);
            } while (reg._eUp._Dst == dst);

            return reg;
        }

        /// <summary>
        /// Add a new active region to the sweep line, *somewhere* below "regAbove"
        /// (according to where the new edge belongs in the sweep-line dictionary).
        /// The upper edge of the new region will be "eNewUp".
        /// Winding number and "inside" flag are not updated.
        /// </summary>
        private ActiveRegion AddRegionBelow(ActiveRegion regAbove, MeshUtils.Edge eNewUp)
        {
            var regNew = new ActiveRegion();

            regNew._eUp = eNewUp;
            regNew._nodeUp = _dict.InsertBefore(regAbove._nodeUp, regNew);
            regNew._fixUpperEdge = false;
            regNew._sentinel = false;
            regNew._dirty = false;

            eNewUp._activeRegion = regNew;

            return regNew;
        }

        private void ComputeWinding(ActiveRegion reg)
        {
            reg._windingNumber = RegionAbove(reg)._windingNumber + reg._eUp._winding;
            reg._inside = Geom.IsWindingInside(_windingRule, reg._windingNumber);
        }

        /// <summary>
        /// Delete a region from the sweep line. This happens when the upper
        /// and lower chains of a region meet (at a vertex on the sweep line).
        /// The "inside" flag is copied to the appropriate mesh face (we could
        /// not do this before -- since the structure of the mesh is always
        /// changing, this face may not have even existed until now).
        /// </summary>
        private void FinishRegion(ActiveRegion reg)
        {
            var e = reg._eUp;
            var f = e._Lface;

            f._inside = reg._inside;
            f._anEdge = e;
            DeleteRegion(reg);
        }

        /// <summary>
        /// We are given a vertex with one or more left-going edges.  All affected
        /// edges should be in the edge dictionary.  Starting at regFirst->eUp,
        /// we walk down deleting all regions where both edges have the same
        /// origin vOrg.  At the same time we copy the "inside" flag from the
        /// active region to the face, since at this point each face will belong
        /// to at most one region (this was not necessarily true until this point
        /// in the sweep).  The walk stops at the region above regLast; if regLast
        /// is null we walk as far as possible.  At the same time we relink the
        /// mesh if necessary, so that the ordering of edges around vOrg is the
        /// same as in the dictionary.
        /// </summary>
        private MeshUtils.Edge FinishLeftRegions(ActiveRegion regFirst, ActiveRegion regLast)
        {
            var regPrev = regFirst;
            var ePrev = regFirst._eUp;

            while (regPrev != regLast)
            {
                regPrev._fixUpperEdge = false;	// placement was OK
                var reg = RegionBelow(regPrev);
                var e = reg._eUp;
                if (e._Org != ePrev._Org)
                {
                    if (!reg._fixUpperEdge)
                    {
                        // Remove the last left-going edge.  Even though there are no further
                        // edges in the dictionary with this origin, there may be further
                        // such edges in the mesh (if we are adding left edges to a vertex
                        // that has already been processed).  Thus it is important to call
                        // FinishRegion rather than just DeleteRegion.
                        FinishRegion(regPrev);
                        break;
                    }
                    // If the edge below was a temporary edge introduced by
                    // ConnectRightVertex, now is the time to fix it.
                    e = _mesh.Connect(ePrev._Lprev, e._Sym);
                    FixUpperEdge(reg, e);
                }

                // Relink edges so that ePrev.Onext == e
                if (ePrev._Onext != e)
                {
                    _mesh.Splice(e._Oprev, e);
                    _mesh.Splice(ePrev, e);
                }
                FinishRegion(regPrev); // may change reg.eUp
                ePrev = reg._eUp;
                regPrev = reg;
            }

            return ePrev;
        }

        /// <summary>
        /// Purpose: insert right-going edges into the edge dictionary, and update
        /// winding numbers and mesh connectivity appropriately.  All right-going
        /// edges share a common origin vOrg.  Edges are inserted CCW starting at
        /// eFirst; the last edge inserted is eLast.Oprev.  If vOrg has any
        /// left-going edges already processed, then eTopLeft must be the edge
        /// such that an imaginary upward vertical segment from vOrg would be
        /// contained between eTopLeft.Oprev and eTopLeft; otherwise eTopLeft
        /// should be null.
        /// </summary>
        private void AddRightEdges(ActiveRegion regUp, MeshUtils.Edge eFirst, MeshUtils.Edge eLast, MeshUtils.Edge eTopLeft, bool cleanUp)
        {
            bool firstTime = true;

            var e = eFirst; do
            {
                Debug.Assert(Geom.VertLeq(e._Org, e._Dst));
                AddRegionBelow(regUp, e._Sym);
                e = e._Onext;
            } while (e != eLast);

            // Walk *all* right-going edges from e.Org, in the dictionary order,
            // updating the winding numbers of each region, and re-linking the mesh
            // edges to match the dictionary ordering (if necessary).
            if (eTopLeft == null)
            {
                eTopLeft = RegionBelow(regUp)._eUp._Rprev;
            }

            ActiveRegion regPrev = regUp, reg;
            var ePrev = eTopLeft;
            while (true)
            {
                reg = RegionBelow(regPrev);
                e = reg._eUp._Sym;
                if (e._Org != ePrev._Org) break;

                if (e._Onext != ePrev)
                {
                    // Unlink e from its current position, and relink below ePrev
                    _mesh.Splice(e._Oprev, e);
                    _mesh.Splice(ePrev._Oprev, e);
                }
                // Compute the winding number and "inside" flag for the new regions
                reg._windingNumber = regPrev._windingNumber - e._winding;
                reg._inside = Geom.IsWindingInside(_windingRule, reg._windingNumber);

                // Check for two outgoing edges with same slope -- process these
                // before any intersection tests (see example in tessComputeInterior).
                regPrev._dirty = true;
                if (!firstTime && CheckForRightSplice(regPrev))
                {
                    Geom.AddWinding(e, ePrev);
                    DeleteRegion(regPrev);
                    _mesh.Delete(ePrev);
                }
                firstTime = false;
                regPrev = reg;
                ePrev = e;
            }
            regPrev._dirty = true;
            Debug.Assert(regPrev._windingNumber - e._winding == reg._windingNumber);

            if (cleanUp)
            {
                // Check for intersections between newly adjacent edges.
                WalkDirtyRegions(regPrev);
            }
        }

        /// <summary>
        /// Two vertices with idential coordinates are combined into one.
        /// e1.Org is kept, while e2.Org is discarded.
        /// </summary>
        private void SpliceMergeVertices(MeshUtils.Edge e1, MeshUtils.Edge e2)
        {
            _mesh.Splice(e1, e2);
        }

        /// <summary>
        /// Find some weights which describe how the intersection vertex is
        /// a linear combination of "org" and "dest".  Each of the two edges
        /// which generated "isect" is allocated 50% of the weight; each edge
        /// splits the weight between its org and dst according to the
        /// relative distance to "isect".
        /// </summary>
        private void VertexWeights(MeshUtils.Vertex isect, MeshUtils.Vertex org, MeshUtils.Vertex dst, out Real w0, out Real w1)
        {
            var t1 = Geom.VertL1dist(org, isect);
            var t2 = Geom.VertL1dist(dst, isect);

            w0 = (t2 / (t1 + t2)) / 2.0f;
            w1 = (t1 / (t1 + t2)) / 2.0f;

            isect._coords.X += w0 * org._coords.X + w1 * dst._coords.X;
            isect._coords.Y += w0 * org._coords.Y + w1 * dst._coords.Y;
            isect._coords.Z += w0 * org._coords.Z + w1 * dst._coords.Z;
        }

        /// <summary>
        /// We've computed a new intersection point, now we need a "data" pointer
        /// from the user so that we can refer to this new vertex in the
        /// rendering callbacks.
        /// </summary>
        private void GetIntersectData(MeshUtils.Vertex isect, MeshUtils.Vertex orgUp, MeshUtils.Vertex dstUp, MeshUtils.Vertex orgLo, MeshUtils.Vertex dstLo)
        {
            isect._coords = Vec3.Zero;
            Real w0, w1, w2, w3;
            VertexWeights(isect, orgUp, dstUp, out w0, out w1);
            VertexWeights(isect, orgLo, dstLo, out w2, out w3);

            if (_combineCallback != null)
            {
                isect._data = _combineCallback(
                    isect._coords,
                    new object[] { orgUp._data, dstUp._data, orgLo._data, dstLo._data },
                    new Real[] { w0, w1, w2, w3 }
                );
            }
        }

        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp->Org is above eLo, or eLo->Org is below eUp (depending on which
        /// origin is leftmost).
        /// 
        /// The main purpose is to splice right-going edges with the same
        /// dest vertex and nearly identical slopes (ie. we can't distinguish
        /// the slopes numerically).  However the splicing can also help us
        /// to recover from numerical errors.  For example, suppose at one
        /// point we checked eUp and eLo, and decided that eUp->Org is barely
        /// above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// our test so that now eUp->Org is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants.
        /// 
        /// One possibility is to check these edges for intersection again
        /// (ie. CheckForIntersect).  This is what we do if possible.  However
        /// CheckForIntersect requires that tess->event lies between eUp and eLo,
        /// so that it has something to fall back on when the intersection
        /// calculation gives us an unusable answer.  So, for those cases where
        /// we can't check for intersection, this routine fixes the problem
        /// by just splicing the offending vertex into the other edge.
        /// This is a guaranteed solution, no matter how degenerate things get.
        /// Basically this is a combinatorial solution to a numerical problem.
        /// </summary>
        private bool CheckForRightSplice(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            if (Geom.VertLeq(eUp._Org, eLo._Org))
            {
                if (Geom.EdgeSign(eLo._Dst, eUp._Org, eLo._Org) > 0.0f)
                {
                    return false;
                }

                // eUp.Org appears to be below eLo
                if (!Geom.VertEq(eUp._Org, eLo._Org))
                {
                    // Splice eUp._Org into eLo
                    _mesh.SplitEdge(eLo._Sym);
                    _mesh.Splice(eUp, eLo._Oprev);
                    regUp._dirty = regLo._dirty = true;
                }
                else if (eUp._Org != eLo._Org)
                {
                    // merge the two vertices, discarding eUp.Org
                    _pq.Remove(eUp._Org._pqHandle);
                    SpliceMergeVertices(eLo._Oprev, eUp);
                }
            }
            else
            {
                if (Geom.EdgeSign(eUp._Dst, eLo._Org, eUp._Org) < 0.0f)
                {
                    return false;
                }

                // eLo.Org appears to be above eUp, so splice eLo.Org into eUp
                RegionAbove(regUp)._dirty = regUp._dirty = true;
                _mesh.SplitEdge(eUp._Sym);
                _mesh.Splice(eLo._Oprev, eUp);
            }
            return true;
        }
        
        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp->Dst is above eLo, or eLo->Dst is below eUp (depending on which
        /// destination is rightmost).
        /// 
        /// Theoretically, this should always be true.  However, splitting an edge
        /// into two pieces can change the results of previous tests.  For example,
        /// suppose at one point we checked eUp and eLo, and decided that eUp->Dst
        /// is barely above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// the test so that now eUp->Dst is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants
        /// (otherwise new edges might get inserted in the wrong place in the
        /// dictionary, and bad stuff will happen).
        /// 
        /// We fix the problem by just splicing the offending vertex into the
        /// other edge.
        /// </summary>
        private bool CheckForLeftSplice(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            Debug.Assert(!Geom.VertEq(eUp._Dst, eLo._Dst));

            if (Geom.VertLeq(eUp._Dst, eLo._Dst))
            {
                if (Geom.EdgeSign(eUp._Dst, eLo._Dst, eUp._Org) < 0.0f)
                {
                    return false;
                }

                // eLo.Dst is above eUp, so splice eLo.Dst into eUp
                RegionAbove(regUp)._dirty = regUp._dirty = true;
                var e = _mesh.SplitEdge(eUp);
                _mesh.Splice(eLo._Sym, e);
                e._Lface._inside = regUp._inside;
            }
            else
            {
                if (Geom.EdgeSign(eLo._Dst, eUp._Dst, eLo._Org) > 0.0f)
                {
                    return false;
                }

                // eUp.Dst is below eLo, so splice eUp.Dst into eLo
                regUp._dirty = regLo._dirty = true;
                var e = _mesh.SplitEdge(eLo);
                _mesh.Splice(eUp._Lnext, eLo._Sym);
                e._Rface._inside = regUp._inside;
            }
            return true;
        }

        /// <summary>
        /// Check the upper and lower edges of the given region to see if
        /// they intersect.  If so, create the intersection and add it
        /// to the data structures.
        /// 
        /// Returns TRUE if adding the new intersection resulted in a recursive
        /// call to AddRightEdges(); in this case all "dirty" regions have been
        /// checked for intersections, and possibly regUp has been deleted.
        /// </summary>
        private bool CheckForIntersect(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;
            var orgUp = eUp._Org;
            var orgLo = eLo._Org;
            var dstUp = eUp._Dst;
            var dstLo = eLo._Dst;

            Debug.Assert(!Geom.VertEq(dstLo, dstUp));
            Debug.Assert(Geom.EdgeSign(dstUp, _event, orgUp) <= 0.0f);
            Debug.Assert(Geom.EdgeSign(dstLo, _event, orgLo) >= 0.0f);
            Debug.Assert(orgUp != _event && orgLo != _event);
            Debug.Assert(!regUp._fixUpperEdge && !regLo._fixUpperEdge);

            if( orgUp == orgLo )
            {
                // right endpoints are the same
                return false;
            }

            var tMinUp = Math.Min(orgUp._t, dstUp._t);
            var tMaxLo = Math.Max(orgLo._t, dstLo._t);
            if( tMinUp > tMaxLo )
            {
                // t ranges do not overlap
                return false;
            }

            if (Geom.VertLeq(orgUp, orgLo))
            {
                if (Geom.EdgeSign( dstLo, orgUp, orgLo ) > 0.0f)
                {
                    return false;
                }
            }
            else
            {
                if (Geom.EdgeSign( dstUp, orgLo, orgUp ) < 0.0f)
                {
                    return false;
                }
            }

            // At this point the edges intersect, at least marginally

            var isect = MeshUtils.Vertex.Create();
            Geom.EdgeIntersect(dstUp, orgUp, dstLo, orgLo, isect);
            // The following properties are guaranteed:
            Debug.Assert(Math.Min(orgUp._t, dstUp._t) <= isect._t);
            Debug.Assert(isect._t <= Math.Max(orgLo._t, dstLo._t));
            Debug.Assert(Math.Min(dstLo._s, dstUp._s) <= isect._s);
            Debug.Assert(isect._s <= Math.Max(orgLo._s, orgUp._s));

            if (Geom.VertLeq(isect, _event))
            {
                // The intersection point lies slightly to the left of the sweep line,
                // so move it until it''s slightly to the right of the sweep line.
                // (If we had perfect numerical precision, this would never happen
                // in the first place). The easiest and safest thing to do is
                // replace the intersection by tess._event.
                isect._s = _event._s;
                isect._t = _event._t;
            }
            // Similarly, if the computed intersection lies to the right of the
            // rightmost origin (which should rarely happen), it can cause
            // unbelievable inefficiency on sufficiently degenerate inputs.
            // (If you have the test program, try running test54.d with the
            // "X zoom" option turned on).
            var orgMin = Geom.VertLeq(orgUp, orgLo) ? orgUp : orgLo;
            if (Geom.VertLeq(orgMin, isect))
            {
                isect._s = orgMin._s;
                isect._t = orgMin._t;
            }

            if (Geom.VertEq(isect, orgUp) || Geom.VertEq(isect, orgLo))
            {
                // Easy case -- intersection at one of the right endpoints
                CheckForRightSplice(regUp);
                return false;
            }

            if (   (! Geom.VertEq(dstUp, _event)
                && Geom.EdgeSign(dstUp, _event, isect) >= 0.0f)
                || (! Geom.VertEq(dstLo, _event)
                && Geom.EdgeSign(dstLo, _event, isect) <= 0.0f))
            {
                // Very unusual -- the new upper or lower edge would pass on the
                // wrong side of the sweep event, or through it. This can happen
                // due to very small numerical errors in the intersection calculation.
                if (dstLo == _event)
                {
                    // Splice dstLo into eUp, and process the new region(s)
                    _mesh.SplitEdge(eUp._Sym);
                    _mesh.Splice(eLo._Sym, eUp);
                    regUp = TopLeftRegion(regUp);
                    eUp = RegionBelow(regUp)._eUp;
                    FinishLeftRegions(RegionBelow(regUp), regLo);
                    AddRightEdges(regUp, eUp._Oprev, eUp, eUp, true);
                    return true;
                }
                if( dstUp == _event ) {
                    /* Splice dstUp into eLo, and process the new region(s) */
                    _mesh.SplitEdge(eLo._Sym);
                    _mesh.Splice(eUp._Lnext, eLo._Oprev);
                    regLo = regUp;
                    regUp = TopRightRegion(regUp);
                    var e = RegionBelow(regUp)._eUp._Rprev;
                    regLo._eUp = eLo._Oprev;
                    eLo = FinishLeftRegions(regLo, null);
                    AddRightEdges(regUp, eLo._Onext, eUp._Rprev, e, true);
                    return true;
                }
                // Special case: called from ConnectRightVertex. If either
                // edge passes on the wrong side of tess._event, split it
                // (and wait for ConnectRightVertex to splice it appropriately).
                if (Geom.EdgeSign( dstUp, _event, isect ) >= 0.0f)
                {
                    RegionAbove(regUp)._dirty = regUp._dirty = true;
                    _mesh.SplitEdge(eUp._Sym);
                    eUp._Org._s = _event._s;
                    eUp._Org._t = _event._t;
                }
                if (Geom.EdgeSign(dstLo, _event, isect) <= 0.0f)
                {
                    regUp._dirty = regLo._dirty = true;
                    _mesh.SplitEdge(eLo._Sym);
                    eLo._Org._s = _event._s;
                    eLo._Org._t = _event._t;
                }
                // leave the rest for ConnectRightVertex
                return false;
            }

            // General case -- split both edges, splice into new vertex.
            // When we do the splice operation, the order of the arguments is
            // arbitrary as far as correctness goes. However, when the operation
            // creates a new face, the work done is proportional to the size of
            // the new face.  We expect the faces in the processed part of
            // the mesh (ie. eUp._Lface) to be smaller than the faces in the
            // unprocessed original contours (which will be eLo._Oprev._Lface).
            _mesh.SplitEdge(eUp._Sym);
            _mesh.SplitEdge(eLo._Sym);
            _mesh.Splice(eLo._Oprev, eUp);
            eUp._Org._s = isect._s;
            eUp._Org._t = isect._t;
            eUp._Org._pqHandle = _pq.Insert(eUp._Org);
            if (eUp._Org._pqHandle._handle == PQHandle.Invalid)
            {
                throw new InvalidOperationException("PQHandle should not be invalid");
            }
            GetIntersectData(eUp._Org, orgUp, dstUp, orgLo, dstLo);
            RegionAbove(regUp)._dirty = regUp._dirty = regLo._dirty = true;
            return false;
        }

        /// <summary>
        /// When the upper or lower edge of any region changes, the region is
        /// marked "dirty".  This routine walks through all the dirty regions
        /// and makes sure that the dictionary invariants are satisfied
        /// (see the comments at the beginning of this file).  Of course
        /// new dirty regions can be created as we make changes to restore
        /// the invariants.
        /// </summary>
        private void WalkDirtyRegions(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            MeshUtils.Edge eUp, eLo;

            while (true)
            {
                // Find the lowest dirty region (we walk from the bottom up).
                while (regLo._dirty)
                {
                    regUp = regLo;
                    regLo = RegionBelow(regLo);
                }
                if (!regUp._dirty)
                {
                    regLo = regUp;
                    regUp = RegionAbove( regUp );
                    if(regUp == null || !regUp._dirty)
                    {
                        // We've walked all the dirty regions
                        return;
                    }
                }
                regUp._dirty = false;
                eUp = regUp._eUp;
                eLo = regLo._eUp;

                if (eUp._Dst != eLo._Dst)
                {
                    // Check that the edge ordering is obeyed at the Dst vertices.
                    if (CheckForLeftSplice(regUp))
                    {

                        // If the upper or lower edge was marked fixUpperEdge, then
                        // we no longer need it (since these edges are needed only for
                        // vertices which otherwise have no right-going edges).
                        if (regLo._fixUpperEdge)
                        {
                            DeleteRegion(regLo);
                            _mesh.Delete(eLo);
                            regLo = RegionBelow(regUp);
                            eLo = regLo._eUp;
                        }
                        else if( regUp._fixUpperEdge )
                        {
                            DeleteRegion(regUp);
                            _mesh.Delete(eUp);
                            regUp = RegionAbove(regLo);
                            eUp = regUp._eUp;
                        }
                    }
                }
                if (eUp._Org != eLo._Org)
                {
                    if(    eUp._Dst != eLo._Dst
                        && ! regUp._fixUpperEdge && ! regLo._fixUpperEdge
                        && (eUp._Dst == _event || eLo._Dst == _event) )
                    {
                        // When all else fails in CheckForIntersect(), it uses tess._event
                        // as the intersection location. To make this possible, it requires
                        // that tess._event lie between the upper and lower edges, and also
                        // that neither of these is marked fixUpperEdge (since in the worst
                        // case it might splice one of these edges into tess.event, and
                        // violate the invariant that fixable edges are the only right-going
                        // edge from their associated vertex).
                        if (CheckForIntersect(regUp))
                        {
                            // WalkDirtyRegions() was called recursively; we're done
                            return;
                        }
                    }
                    else
                    {
                        // Even though we can't use CheckForIntersect(), the Org vertices
                        // may violate the dictionary edge ordering. Check and correct this.
                        CheckForRightSplice(regUp);
                    }
                }
                if (eUp._Org == eLo._Org && eUp._Dst == eLo._Dst)
                {
                    // A degenerate loop consisting of only two edges -- delete it.
                    Geom.AddWinding(eLo, eUp);
                    DeleteRegion(regUp);
                    _mesh.Delete(eUp);
                    regUp = RegionAbove(regLo);
                }
            }
        }

        /// <summary>
        /// Purpose: connect a "right" vertex vEvent (one where all edges go left)
        /// to the unprocessed portion of the mesh.  Since there are no right-going
        /// edges, two regions (one above vEvent and one below) are being merged
        /// into one.  "regUp" is the upper of these two regions.
        /// 
        /// There are two reasons for doing this (adding a right-going edge):
        ///  - if the two regions being merged are "inside", we must add an edge
        ///    to keep them separated (the combined region would not be monotone).
        ///  - in any case, we must leave some record of vEvent in the dictionary,
        ///    so that we can merge vEvent with features that we have not seen yet.
        ///    For example, maybe there is a vertical edge which passes just to
        ///    the right of vEvent; we would like to splice vEvent into this edge.
        /// 
        /// However, we don't want to connect vEvent to just any vertex.  We don''t
        /// want the new edge to cross any other edges; otherwise we will create
        /// intersection vertices even when the input data had no self-intersections.
        /// (This is a bad thing; if the user's input data has no intersections,
        /// we don't want to generate any false intersections ourselves.)
        /// 
        /// Our eventual goal is to connect vEvent to the leftmost unprocessed
        /// vertex of the combined region (the union of regUp and regLo).
        /// But because of unseen vertices with all right-going edges, and also
        /// new vertices which may be created by edge intersections, we don''t
        /// know where that leftmost unprocessed vertex is.  In the meantime, we
        /// connect vEvent to the closest vertex of either chain, and mark the region
        /// as "fixUpperEdge".  This flag says to delete and reconnect this edge
        /// to the next processed vertex on the boundary of the combined region.
        /// Quite possibly the vertex we connected to will turn out to be the
        /// closest one, in which case we won''t need to make any changes.
        /// </summary>
        private void ConnectRightVertex(ActiveRegion regUp, MeshUtils.Edge eBottomLeft)
        {
            var eTopLeft = eBottomLeft._Onext;
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;
            bool degenerate = false;

            if (eUp._Dst != eLo._Dst)
            {
                CheckForIntersect(regUp);
            }

            // Possible new degeneracies: upper or lower edge of regUp may pass
            // through vEvent, or may coincide with new intersection vertex
            if (Geom.VertEq(eUp._Org, _event))
            {
                _mesh.Splice(eTopLeft._Oprev, eUp);
                regUp = TopLeftRegion(regUp);
                eTopLeft = RegionBelow(regUp)._eUp;
                FinishLeftRegions(RegionBelow(regUp), regLo);
                degenerate = true;
            }
            if (Geom.VertEq(eLo._Org, _event))
            {
                _mesh.Splice(eBottomLeft, eLo._Oprev);
                eBottomLeft = FinishLeftRegions(regLo, null);
                degenerate = true;
            }
            if (degenerate)
            {
                AddRightEdges(regUp, eBottomLeft._Onext, eTopLeft, eTopLeft, true);
                return;
            }

            // Non-degenerate situation -- need to add a temporary, fixable edge.
            // Connect to the closer of eLo.Org, eUp.Org.
            MeshUtils.Edge eNew;
            if (Geom.VertLeq(eLo._Org, eUp._Org))
            {
                eNew = eLo._Oprev;
            }
            else
            {
                eNew = eUp;
            }
            eNew = _mesh.Connect(eBottomLeft._Lprev, eNew);

            // Prevent cleanup, otherwise eNew might disappear before we've even
            // had a chance to mark it as a temporary edge.
            AddRightEdges(regUp, eNew, eNew._Onext, eNew._Onext, false);
            eNew._Sym._activeRegion._fixUpperEdge = true;
            WalkDirtyRegions(regUp);
        }

        /// <summary>
        /// The event vertex lies exacty on an already-processed edge or vertex.
        /// Adding the new vertex involves splicing it into the already-processed
        /// part of the mesh.
        /// </summary>
        private void ConnectLeftDegenerate(ActiveRegion regUp, MeshUtils.Vertex vEvent)
        {
            var e = regUp._eUp;
            if (Geom.VertEq(e._Org, vEvent))
            {
                // e.Org is an unprocessed vertex - just combine them, and wait
                // for e.Org to be pulled from the queue
                // C# : in the C version, there is a flag but it was never implemented
                // the vertices are before beginning the tesselation
                throw new InvalidOperationException("Vertices should have been merged before");
            }

            if (!Geom.VertEq(e._Dst, vEvent))
            {
                // General case -- splice vEvent into edge e which passes through it
                _mesh.SplitEdge(e._Sym);
                if (regUp._fixUpperEdge)
                {
                    // This edge was fixable -- delete unused portion of original edge
                    _mesh.Delete(e._Onext);
                    regUp._fixUpperEdge = false;
                }
                _mesh.Splice(vEvent._anEdge, e);
                SweepEvent(vEvent);	// recurse
                return;
            }

            // See above
            throw new InvalidOperationException("Vertices should have been merged before");
        }

        /// <summary>
        /// Purpose: connect a "left" vertex (one where both edges go right)
        /// to the processed portion of the mesh.  Let R be the active region
        /// containing vEvent, and let U and L be the upper and lower edge
        /// chains of R.  There are two possibilities:
        /// 
        /// - the normal case: split R into two regions, by connecting vEvent to
        ///   the rightmost vertex of U or L lying to the left of the sweep line
        /// 
        /// - the degenerate case: if vEvent is close enough to U or L, we
        ///   merge vEvent into that edge chain.  The subcases are:
        ///     - merging with the rightmost vertex of U or L
        ///     - merging with the active edge of U or L
        ///     - merging with an already-processed portion of U or L
        /// </summary>
        private void ConnectLeftVertex(MeshUtils.Vertex vEvent)
        {
            var tmp = new ActiveRegion();

            // Get a pointer to the active region containing vEvent
            tmp._eUp = vEvent._anEdge._Sym;
            var regUp = _dict.Find(tmp).Key;
            var regLo = RegionBelow(regUp);
            if (regLo == null)
            {
                // This may happen if the input polygon is coplanar.
                return;
            }
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            // Try merging with U or L first
            if (Geom.EdgeSign(eUp._Dst, vEvent, eUp._Org) == 0.0f)
            {
                ConnectLeftDegenerate(regUp, vEvent);
                return;
            }

            // Connect vEvent to rightmost processed vertex of either chain.
            // e._Dst is the vertex that we will connect to vEvent.
            var reg = Geom.VertLeq(eLo._Dst, eUp._Dst) ? regUp : regLo;

            if (regUp._inside || reg._fixUpperEdge)
            {
                MeshUtils.Edge eNew;
                if (reg == regUp)
                {
                    eNew = _mesh.Connect(vEvent._anEdge._Sym, eUp._Lnext);
                }
                else
                {
                    eNew = _mesh.Connect(eLo._Dnext, vEvent._anEdge)._Sym;
                }
                if (reg._fixUpperEdge)
                {
                    FixUpperEdge(reg, eNew);
                }
                else
                {
                    ComputeWinding(AddRegionBelow(regUp, eNew));
                }
                SweepEvent(vEvent);
            }
            else
            {
                // The new vertex is in a region which does not belong to the polygon.
                // We don't need to connect this vertex to the rest of the mesh.
                AddRightEdges(regUp, vEvent._anEdge, vEvent._anEdge, null, true);
            }
        }

        /// <summary>
        /// Does everything necessary when the sweep line crosses a vertex.
        /// Updates the mesh and the edge dictionary.
        /// </summary>
        private void SweepEvent(MeshUtils.Vertex vEvent)
        {
            _event = vEvent;

            // Check if this vertex is the right endpoint of an edge that is
            // already in the dictionary. In this case we don't need to waste
            // time searching for the location to insert new edges.
            var e = vEvent._anEdge;
            while (e._activeRegion == null)
            {
                e = e._Onext;
                if (e == vEvent._anEdge)
                {
                    // All edges go right -- not incident to any processed edges
                    ConnectLeftVertex(vEvent);
                    return;
                }
            }

            // Processing consists of two phases: first we "finish" all the
            // active regions where both the upper and lower edges terminate
            // at vEvent (ie. vEvent is closing off these regions).
            // We mark these faces "inside" or "outside" the polygon according
            // to their winding number, and delete the edges from the dictionary.
            // This takes care of all the left-going edges from vEvent.
            var regUp = TopLeftRegion(e._activeRegion);
            var reg = RegionBelow(regUp);
            var eTopLeft = reg._eUp;
            var eBottomLeft = FinishLeftRegions(reg, null);

            // Next we process all the right-going edges from vEvent. This
            // involves adding the edges to the dictionary, and creating the
            // associated "active regions" which record information about the
            // regions between adjacent dictionary edges.
            if (eBottomLeft._Onext == eTopLeft)
            {
                // No right-going edges -- add a temporary "fixable" edge
                ConnectRightVertex(regUp, eBottomLeft);
            }
            else
            {
                AddRightEdges(regUp, eBottomLeft._Onext, eTopLeft, eTopLeft, true);
            }
        }

        /// <summary>
        /// Make the sentinel coordinates big enough that they will never be
        /// merged with real input features.
        /// 
        /// We add two sentinel edges above and below all other edges,
        /// to avoid special cases at the top and bottom.
        /// </summary>
        private void AddSentinel(Real smin, Real smax, Real t)
        {
            var e = _mesh.MakeEdge();
            e._Org._s = smax;
            e._Org._t = t;
            e._Dst._s = smin;
            e._Dst._t = t;
            _event = e._Dst; // initialize it

            var reg = new ActiveRegion();
            reg._eUp = e;
            reg._windingNumber = 0;
            reg._inside = false;
            reg._fixUpperEdge = false;
            reg._sentinel = true;
            reg._dirty = false;
            reg._nodeUp = _dict.Insert(reg);
        }

        /// <summary>
        /// We maintain an ordering of edge intersections with the sweep line.
        /// This order is maintained in a dynamic dictionary.
        /// </summary>
        private void InitEdgeDict()
        {
            _dict = new Dict<ActiveRegion>(EdgeLeq);

            AddSentinel(-SentinelCoord, SentinelCoord, -SentinelCoord);
            AddSentinel(-SentinelCoord, SentinelCoord, +SentinelCoord);
        }

        private void DoneEdgeDict()
        {
            int fixedEdges = 0;

            ActiveRegion reg;
            while ((reg = _dict.Min().Key) != null)
            {
                // At the end of all processing, the dictionary should contain
                // only the two sentinel edges, plus at most one "fixable" edge
                // created by ConnectRightVertex().
                if (!reg._sentinel)
                {
                    Debug.Assert(reg._fixUpperEdge);
                    Debug.Assert(++fixedEdges == 1);
                }
                Debug.Assert(reg._windingNumber == 0);
                DeleteRegion(reg);
            }

            _dict = null;
        }

        /// <summary>
        /// Remove zero-length edges, and contours with fewer than 3 vertices.
        /// </summary>
        private void RemoveDegenerateEdges()
        {
            MeshUtils.Edge eHead = _mesh._eHead, e, eNext, eLnext;

            for (e = eHead._next; e != eHead; e = eNext)
            {
                eNext = e._next;
                eLnext = e._Lnext;

                if (Geom.VertEq(e._Org, e._Dst) && e._Lnext._Lnext != e)
                {
                    // Zero-length edge, contour has at least 3 edges

                    SpliceMergeVertices(eLnext, e);	// deletes e.Org
                    _mesh.Delete(e); // e is a self-loop
                    e = eLnext;
                    eLnext = e._Lnext;
                }
                if (eLnext._Lnext == e)
                {
                    // Degenerate contour (one or two edges)

                    if (eLnext != e)
                    {
                        if (eLnext == eNext || eLnext == eNext._Sym)
                        {
                            eNext = eNext._next;
                        }
                        _mesh.Delete(eLnext);
                    }
                    if (e == eNext || e == eNext._Sym)
                    {
                        eNext = eNext._next;
                    }
                    _mesh.Delete(e);
                }
            }
        }

        /// <summary>
        /// Insert all vertices into the priority queue which determines the
        /// order in which vertices cross the sweep line.
        /// </summary>
        private void InitPriorityQ()
        {
            MeshUtils.Vertex vHead = _mesh._vHead, v;
            int vertexCount = 0;

            for (v = vHead._next; v != vHead; v = v._next)
            {
                vertexCount++;
            }
            // Make sure there is enough space for sentinels.
            vertexCount += 8;
    
            _pq = new PriorityQueue<MeshUtils.Vertex>(vertexCount, Geom.VertLeq);

            vHead = _mesh._vHead;
            for( v = vHead._next; v != vHead; v = v._next ) {
                v._pqHandle = _pq.Insert(v);
                if (v._pqHandle._handle == PQHandle.Invalid)
                {
                    throw new InvalidOperationException("PQHandle should not be invalid");
                }
            }
            _pq.Init();
        }

        private void DonePriorityQ()
        {
            _pq = null;
        }

        /// <summary>
        /// Delete any degenerate faces with only two edges.  WalkDirtyRegions()
        /// will catch almost all of these, but it won't catch degenerate faces
        /// produced by splice operations on already-processed edges.
        /// The two places this can happen are in FinishLeftRegions(), when
        /// we splice in a "temporary" edge produced by ConnectRightVertex(),
        /// and in CheckForLeftSplice(), where we splice already-processed
        /// edges to ensure that our dictionary invariants are not violated
        /// by numerical errors.
        /// 
        /// In both these cases it is *very* dangerous to delete the offending
        /// edge at the time, since one of the routines further up the stack
        /// will sometimes be keeping a pointer to that edge.
        /// </summary>
        private void RemoveDegenerateFaces()
        {
            MeshUtils.Face f, fNext;
            MeshUtils.Edge e;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = fNext)
            {
                fNext = f._next;
                e = f._anEdge;
                Debug.Assert(e._Lnext != e);

                if (e._Lnext._Lnext == e)
                {
                    // A face with only two edges
                    Geom.AddWinding(e._Onext, e);
                    _mesh.Delete(e);
                }
            }
        }

        /// <summary>
        /// ComputeInterior computes the planar arrangement specified
        /// by the given contours, and further subdivides this arrangement
        /// into regions.  Each region is marked "inside" if it belongs
        /// to the polygon, according to the rule given by windingRule.
        /// Each interior region is guaranteed to be monotone.
        /// </summary>
        protected void ComputeInterior()
        {
            // Each vertex defines an event for our sweep line. Start by inserting
            // all the vertices in a priority queue. Events are processed in
            // lexicographic order, ie.
            // 
            // e1 < e2  iff  e1.x < e2.x || (e1.x == e2.x && e1.y < e2.y)
            RemoveDegenerateEdges();
            InitPriorityQ();
            RemoveDegenerateFaces();
            InitEdgeDict();

            MeshUtils.Vertex v, vNext;
            while ((v = _pq.ExtractMin()) != null)
            {
                 while (true)
                 {
                    vNext = _pq.Minimum();
                    if (vNext == null || !Geom.VertEq(vNext, v))
                    {
                        break;
                    }

                    // Merge together all vertices at exactly the same location.
                    // This is more efficient than processing them one at a time,
                    // simplifies the code (see ConnectLeftDegenerate), and is also
                    // important for correct handling of certain degenerate cases.
                    // For example, suppose there are two identical edges A and B
                    // that belong to different contours (so without this code they would
                    // be processed by separate sweep events). Suppose another edge C
                    // crosses A and B from above. When A is processed, we split it
                    // at its intersection point with C. However this also splits C,
                    // so when we insert B we may compute a slightly different
                    // intersection point. This might leave two edges with a small
                    // gap between them. This kind of error is especially obvious
                    // when using boundary extraction (BoundaryOnly).
                    vNext = _pq.ExtractMin();
                    SpliceMergeVertices(v._anEdge, vNext._anEdge);
                }
                SweepEvent(v);
            }

            DoneEdgeDict();
            DonePriorityQ();

            RemoveDegenerateFaces();
            _mesh.Check();
        }
    }
}

// ----------------------------------------------------------------------
// Tess.cs

/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

// using System;
// using System.Diagnostics;

#if DOUBLE
// using Real = System.Double;
namespace LibTessDotNet.Double
#else
// using Real = System.Single;
namespace LibTessDotNet
#endif
{
    public enum WindingRule
    {
        EvenOdd,
        NonZero,
        Positive,
        Negative,
        AbsGeqTwo
    }

    public enum ElementType
    {
        Polygons,
        ConnectedPolygons,
        BoundaryContours
    }

    public enum ContourOrientation
    {
        Original,
        Clockwise,
        CounterClockwise
    }

    public struct ContourVertex
    {
        public Vec3 Position;
        public object Data;

        public override string ToString()
        {
            return string.Format("{0}, {1}", Position, Data);
        }
    }

    public delegate object CombineCallback(Vec3 position, object[] data, Real[] weights);

    public partial class Tess
    {
        private Mesh _mesh;
        private Vec3 _normal;
        private Vec3 _sUnit;
        private Vec3 _tUnit;

        private Real _bminX, _bminY, _bmaxX, _bmaxY;

        private WindingRule _windingRule;

        private Dict<ActiveRegion> _dict;
        private PriorityQueue<MeshUtils.Vertex> _pq;
        private MeshUtils.Vertex _event;

        private CombineCallback _combineCallback;

        private ContourVertex[] _vertices;
        private int _vertexCount;
        private int[] _elements;
        private int _elementCount;

        public Vec3 Normal { get { return _normal; } set { _normal = value; } }

        public Real SUnitX = 1;
        public Real SUnitY = 0;
#if DOUBLE
        public Real SentinelCoord = 4e150;
#else
        public Real SentinelCoord = 4e30f;
#endif

        /// <summary>
        /// If true, will remove empty (zero area) polygons.
        /// </summary>
        public bool NoEmptyPolygons = false;

        /// <summary>
        /// If true, will use pooling to reduce GC (compare performance with/without, can vary wildly).
        /// </summary>
        public bool UsePooling = false;

        public ContourVertex[] Vertices { get { return _vertices; } }
        public int VertexCount { get { return _vertexCount; } }

        public int[] Elements { get { return _elements; } }
        public int ElementCount { get { return _elementCount; } }

        public Tess()
        {
            _normal = Vec3.Zero;
            _bminX = _bminY = _bmaxX = _bmaxY = 0;

            _windingRule = WindingRule.EvenOdd;
            _mesh = null;

            _vertices = null;
            _vertexCount = 0;
            _elements = null;
            _elementCount = 0;
        }

        private void ComputeNormal(ref Vec3 norm)
        {
            var v = _mesh._vHead._next;

            var minVal = new Real[3] { v._coords.X, v._coords.Y, v._coords.Z };
            var minVert = new MeshUtils.Vertex[3] { v, v, v };
            var maxVal = new Real[3] { v._coords.X, v._coords.Y, v._coords.Z };
            var maxVert = new MeshUtils.Vertex[3] { v, v, v };

            for (; v != _mesh._vHead; v = v._next)
            {
                if (v._coords.X < minVal[0]) { minVal[0] = v._coords.X; minVert[0] = v; }
                if (v._coords.Y < minVal[1]) { minVal[1] = v._coords.Y; minVert[1] = v; }
                if (v._coords.Z < minVal[2]) { minVal[2] = v._coords.Z; minVert[2] = v; }
                if (v._coords.X > maxVal[0]) { maxVal[0] = v._coords.X; maxVert[0] = v; }
                if (v._coords.Y > maxVal[1]) { maxVal[1] = v._coords.Y; maxVert[1] = v; }
                if (v._coords.Z > maxVal[2]) { maxVal[2] = v._coords.Z; maxVert[2] = v; }
            }

            // Find two vertices separated by at least 1/sqrt(3) of the maximum
            // distance between any two vertices
            int i = 0;
            if (maxVal[1] - minVal[1] > maxVal[0] - minVal[0]) { i = 1; }
            if (maxVal[2] - minVal[2] > maxVal[i] - minVal[i]) { i = 2; }
            if (minVal[i] >= maxVal[i])
            {
                // All vertices are the same -- normal doesn't matter
                norm = new Vec3 { X = 0, Y = 0, Z = 1 };
                return;
            }

            // Look for a third vertex which forms the triangle with maximum area
            // (Length of normal == twice the triangle area)
            Real maxLen2 = 0, tLen2;
            var v1 = minVert[i];
            var v2 = maxVert[i];
            Vec3 d1, d2, tNorm;
            Vec3.Sub(ref v1._coords, ref v2._coords, out d1);
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                Vec3.Sub(ref v._coords, ref v2._coords, out d2);
                tNorm.X = d1.Y * d2.Z - d1.Z * d2.Y;
                tNorm.Y = d1.Z * d2.X - d1.X * d2.Z;
                tNorm.Z = d1.X * d2.Y - d1.Y * d2.X;
                tLen2 = tNorm.X*tNorm.X + tNorm.Y*tNorm.Y + tNorm.Z*tNorm.Z;
                if (tLen2 > maxLen2)
                {
                    maxLen2 = tLen2;
                    norm = tNorm;
                }
            }

            if (maxLen2 <= 0.0f)
            {
                // All points lie on a single line -- any decent normal will do
                norm = Vec3.Zero;
                i = Vec3.LongAxis(ref d1);
                norm[i] = 1;
            }
        }

        private void CheckOrientation()
        {
            // When we compute the normal automatically, we choose the orientation
            // so that the the sum of the signed areas of all contours is non-negative.
            Real area = 0.0f;
            for (var f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (f._anEdge._winding <= 0)
                {
                    continue;
                }
                area += MeshUtils.FaceArea(f);
            }
            if (area < 0.0f)
            {
                // Reverse the orientation by flipping all the t-coordinates
                for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
                {
                    v._t = -v._t;
                }
                Vec3.Neg(ref _tUnit);
            }
        }

        private void ProjectPolygon()
        {
            var norm = _normal;

            bool computedNormal = false;
            if (norm.X == 0.0f && norm.Y == 0.0f && norm.Z == 0.0f)
            {
                ComputeNormal(ref norm);
                _normal = norm;
                computedNormal = true;
            }

            int i = Vec3.LongAxis(ref norm);

            _sUnit[i] = 0;
            _sUnit[(i + 1) % 3] = SUnitX;
            _sUnit[(i + 2) % 3] = SUnitY;

            _tUnit[i] = 0;
            _tUnit[(i + 1) % 3] = norm[i] > 0.0f ? -SUnitY : SUnitY;
            _tUnit[(i + 2) % 3] = norm[i] > 0.0f ? SUnitX : -SUnitX;

            // Project the vertices onto the sweep plane
            for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                Vec3.Dot(ref v._coords, ref _sUnit, out v._s);
                Vec3.Dot(ref v._coords, ref _tUnit, out v._t);
            }
            if (computedNormal)
            {
                CheckOrientation();
            }

            // Compute ST bounds.
            bool first = true;
            for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                if (first)
                {
                    _bminX = _bmaxX = v._s;
                    _bminY = _bmaxY = v._t;
                    first = false;
                }
                else
                {
                    if (v._s < _bminX) _bminX = v._s;
                    if (v._s > _bmaxX) _bmaxX = v._s;
                    if (v._t < _bminY) _bminY = v._t;
                    if (v._t > _bmaxY) _bmaxY = v._t;
                }
            }
        }

        /// <summary>
        /// TessellateMonoRegion( face ) tessellates a monotone region
        /// (what else would it do??)  The region must consist of a single
        /// loop of half-edges (see mesh.h) oriented CCW.  "Monotone" in this
        /// case means that any vertical line intersects the interior of the
        /// region in a single interval.  
        /// 
        /// Tessellation consists of adding interior edges (actually pairs of
        /// half-edges), to split the region into non-overlapping triangles.
        /// 
        /// The basic idea is explained in Preparata and Shamos (which I don't
        /// have handy right now), although their implementation is more
        /// complicated than this one.  The are two edge chains, an upper chain
        /// and a lower chain.  We process all vertices from both chains in order,
        /// from right to left.
        /// 
        /// The algorithm ensures that the following invariant holds after each
        /// vertex is processed: the untessellated region consists of two
        /// chains, where one chain (say the upper) is a single edge, and
        /// the other chain is concave.  The left vertex of the single edge
        /// is always to the left of all vertices in the concave chain.
        /// 
        /// Each step consists of adding the rightmost unprocessed vertex to one
        /// of the two chains, and forming a fan of triangles from the rightmost
        /// of two chain endpoints.  Determining whether we can add each triangle
        /// to the fan is a simple orientation test.  By making the fan as large
        /// as possible, we restore the invariant (check it yourself).
        /// </summary>
        private void TessellateMonoRegion(MeshUtils.Face face)
        {
            // All edges are oriented CCW around the boundary of the region.
            // First, find the half-edge whose origin vertex is rightmost.
            // Since the sweep goes from left to right, face->anEdge should
            // be close to the edge we want.
            var up = face._anEdge;
            Debug.Assert(up._Lnext != up && up._Lnext._Lnext != up);

            while (Geom.VertLeq(up._Dst, up._Org)) up = up._Lprev;
            while (Geom.VertLeq(up._Org, up._Dst)) up = up._Lnext;

            var lo = up._Lprev;

            while (up._Lnext != lo)
            {
                if (Geom.VertLeq(up._Dst, lo._Org))
                {
                    // up.Dst is on the left. It is safe to form triangles from lo.Org.
                    // The EdgeGoesLeft test guarantees progress even when some triangles
                    // are CW, given that the upper and lower chains are truly monotone.
                    while (lo._Lnext != up && (Geom.EdgeGoesLeft(lo._Lnext)
                        || Geom.EdgeSign(lo._Org, lo._Dst, lo._Lnext._Dst) <= 0.0f))
                    {
                        lo = _mesh.Connect(lo._Lnext, lo)._Sym;
                    }
                    lo = lo._Lprev;
                }
                else
                {
                    // lo.Org is on the left.  We can make CCW triangles from up.Dst.
                    while (lo._Lnext != up && (Geom.EdgeGoesRight(up._Lprev)
                        || Geom.EdgeSign(up._Dst, up._Org, up._Lprev._Org) >= 0.0f))
                    {
                        up = _mesh.Connect(up, up._Lprev)._Sym;
                    }
                    up = up._Lnext;
                }
            }

            // Now lo.Org == up.Dst == the leftmost vertex.  The remaining region
            // can be tessellated in a fan from this leftmost vertex.
            Debug.Assert(lo._Lnext != up);
            while (lo._Lnext._Lnext != up)
            {
                lo = _mesh.Connect(lo._Lnext, lo)._Sym;
            }
        }

        /// <summary>
        /// TessellateInterior( mesh ) tessellates each region of
        /// the mesh which is marked "inside" the polygon. Each such region
        /// must be monotone.
        /// </summary>
        private void TessellateInterior()
        {
            MeshUtils.Face f, next;
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = next)
            {
                // Make sure we don't try to tessellate the new triangles.
                next = f._next;
                if (f._inside)
                {
                    TessellateMonoRegion(f);
                }
            }
        }

        /// <summary>
        /// DiscardExterior zaps (ie. sets to null) all faces
        /// which are not marked "inside" the polygon.  Since further mesh operations
        /// on NULL faces are not allowed, the main purpose is to clean up the
        /// mesh so that exterior loops are not represented in the data structure.
        /// </summary>
        private void DiscardExterior()
        {
            MeshUtils.Face f, next;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = next)
            {
                // Since f will be destroyed, save its next pointer.
                next = f._next;
                if( ! f._inside ) {
                    _mesh.ZapFace(f);
                }
            }
        }

        /// <summary>
        /// SetWindingNumber( value, keepOnlyBoundary ) resets the
        /// winding numbers on all edges so that regions marked "inside" the
        /// polygon have a winding number of "value", and regions outside
        /// have a winding number of 0.
        /// 
        /// If keepOnlyBoundary is TRUE, it also deletes all edges which do not
        /// separate an interior region from an exterior one.
        /// </summary>
        private void SetWindingNumber(int value, bool keepOnlyBoundary)
        {
            MeshUtils.Edge e, eNext;

            for (e = _mesh._eHead._next; e != _mesh._eHead; e = eNext)
            {
                eNext = e._next;
                if (e._Rface._inside != e._Lface._inside)
                {

                    /* This is a boundary edge (one side is interior, one is exterior). */
                    e._winding = (e._Lface._inside) ? value : -value;
                }
                else
                {

                    /* Both regions are interior, or both are exterior. */
                    if (!keepOnlyBoundary)
                    {
                        e._winding = 0;
                    }
                    else
                    {
                        _mesh.Delete(e);
                    }
                }
            }

        }

        private int GetNeighbourFace(MeshUtils.Edge edge)
        {
            if (edge._Rface == null)
                return MeshUtils.Undef;
            if (!edge._Rface._inside)
                return MeshUtils.Undef;
            return edge._Rface._n;
        }

        private void OutputPolymesh(ElementType elementType, int polySize)
        {
            MeshUtils.Vertex v;
            MeshUtils.Face f;
            MeshUtils.Edge edge;
            int maxFaceCount = 0;
            int maxVertexCount = 0;
            int faceVerts, i;

            if (polySize < 3)
            {
                polySize = 3;
            }
            // Assume that the input data is triangles now.
            // Try to merge as many polygons as possible
            if (polySize > 3)
            {
                _mesh.MergeConvexFaces(polySize);
            }

            // Mark unused
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
                v._n = MeshUtils.Undef;

            // Create unique IDs for all vertices and faces.
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                f._n = MeshUtils.Undef;
                if (!f._inside) continue;

                if (NoEmptyPolygons)
                {
                    var area = MeshUtils.FaceArea(f);
                    if (Math.Abs(area) < Real.Epsilon)
                    {
                        continue;
                    }
                }

                edge = f._anEdge;
                faceVerts = 0;
                do {
                    v = edge._Org;
                    if (v._n == MeshUtils.Undef)
                    {
                        v._n = maxVertexCount;
                        maxVertexCount++;
                    }
                    faceVerts++;
                    edge = edge._Lnext;
                }
                while (edge != f._anEdge);

                Debug.Assert(faceVerts <= polySize);

                f._n = maxFaceCount;
                ++maxFaceCount;
            }

            _elementCount = maxFaceCount;
            if (elementType == ElementType.ConnectedPolygons)
                maxFaceCount *= 2;
            _elements = new int[maxFaceCount * polySize];

            _vertexCount = maxVertexCount;
            _vertices = new ContourVertex[_vertexCount];

            // Output vertices.
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                if (v._n != MeshUtils.Undef)
                {
                    // Store coordinate
                    _vertices[v._n].Position = v._coords;
                    _vertices[v._n].Data = v._data;
                }
            }

            // Output indices.
            int elementIndex = 0;
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                if (NoEmptyPolygons)
                {
                    var area = MeshUtils.FaceArea(f);
                    if (Math.Abs(area) < Real.Epsilon)
                    {
                        continue;
                    }
                }

                // Store polygon
                edge = f._anEdge;
                faceVerts = 0;
                do {
                    v = edge._Org;
                    _elements[elementIndex++] = v._n;
                    faceVerts++;
                    edge = edge._Lnext;
                } while (edge != f._anEdge);
                // Fill unused.
                for (i = faceVerts; i < polySize; ++i)
                {
                    _elements[elementIndex++] = MeshUtils.Undef;
                }

                // Store polygon connectivity
                if (elementType == ElementType.ConnectedPolygons)
                {
                    edge = f._anEdge;
                    do
                    {
                        _elements[elementIndex++] = GetNeighbourFace(edge);
                        edge = edge._Lnext;
                    } while (edge != f._anEdge);
                    // Fill unused.
                    for (i = faceVerts; i < polySize; ++i)
                    {
                        _elements[elementIndex++] = MeshUtils.Undef;
                    }
                }
            }
        }

        private void OutputContours()
        {
            MeshUtils.Face f;
            MeshUtils.Edge edge, start;
            int startVert = 0;
            int vertCount = 0;

            _vertexCount = 0;
            _elementCount = 0;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                start = edge = f._anEdge;
                do
                {
                    ++_vertexCount;
                    edge = edge._Lnext;
                }
                while (edge != start);

                ++_elementCount;
            }

            _elements = new int[_elementCount * 2];
            _vertices = new ContourVertex[_vertexCount];

            int vertIndex = 0;
            int elementIndex = 0;

            startVert = 0;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                vertCount = 0;
                start = edge = f._anEdge;
                do {
                    _vertices[vertIndex].Position = edge._Org._coords;
                    _vertices[vertIndex].Data = edge._Org._data;
                    ++vertIndex;
                    ++vertCount;
                    edge = edge._Lnext;
                } while (edge != start);

                _elements[elementIndex++] = startVert;
                _elements[elementIndex++] = vertCount;

                startVert += vertCount;
            }
        }

        private Real SignedArea(ContourVertex[] vertices)
        {
            Real area = 0.0f;

            for (int i = 0; i < vertices.Length; i++)
            {
                var v0 = vertices[i];
                var v1 = vertices[(i + 1) % vertices.Length];

                area += v0.Position.X * v1.Position.Y;
                area -= v0.Position.Y * v1.Position.X;
            }

            return 0.5f * area;
        }

        public void AddContour(ContourVertex[] vertices)
        {
            AddContour(vertices, ContourOrientation.Original);
        }

        public void AddContour(ContourVertex[] vertices, ContourOrientation forceOrientation)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh();
            }

            bool reverse = false;
            if (forceOrientation != ContourOrientation.Original)
            {
                var area = SignedArea(vertices);
                reverse = (forceOrientation == ContourOrientation.Clockwise && area < 0.0f) || (forceOrientation == ContourOrientation.CounterClockwise && area > 0.0f);
            }

            MeshUtils.Edge e = null;
            for (int i = 0; i < vertices.Length; ++i)
            {
                if (e == null)
                {
                    e = _mesh.MakeEdge();
                    _mesh.Splice(e, e._Sym);
                }
                else
                {
                    // Create a new vertex and edge which immediately follow e
                    // in the ordering around the left face.
                    _mesh.SplitEdge(e);
                    e = e._Lnext;
                }

                int index = reverse ? vertices.Length - 1 - i : i;
                // The new vertex is now e._Org.
                e._Org._coords = vertices[index].Position;
                e._Org._data = vertices[index].Data;

                // The winding of an edge says how the winding number changes as we
                // cross from the edge's right face to its left face.  We add the
                // vertices in such an order that a CCW contour will add +1 to
                // the winding number of the region inside the contour.
                e._winding = 1;
                e._Sym._winding = -1;
            }
        }

        public void Tessellate(WindingRule windingRule, ElementType elementType, int polySize)
        {
            Tessellate(windingRule, elementType, polySize, null);
        }

        public void Tessellate(WindingRule windingRule, ElementType elementType, int polySize, CombineCallback combineCallback)
        {
            _normal = Vec3.Zero;
            _vertices = null;
            _elements = null;

            _windingRule = windingRule;
            _combineCallback = combineCallback;

            if (_mesh == null)
            {
                return;
            }

            // Determine the polygon normal and project vertices onto the plane
            // of the polygon.
            ProjectPolygon();

            // ComputeInterior computes the planar arrangement specified
            // by the given contours, and further subdivides this arrangement
            // into regions.  Each region is marked "inside" if it belongs
            // to the polygon, according to the rule given by windingRule.
            // Each interior region is guaranteed be monotone.
            ComputeInterior();

            // If the user wants only the boundary contours, we throw away all edges
            // except those which separate the interior from the exterior.
            // Otherwise we tessellate all the regions marked "inside".
            if (elementType == ElementType.BoundaryContours)
            {
                SetWindingNumber(1, true);
            }
            else
            {
                TessellateInterior();
            }

            _mesh.Check();

            if (elementType == ElementType.BoundaryContours)
            {
                OutputContours();
            }
            else
            {
                OutputPolymesh(elementType, polySize);
            }

            if (UsePooling)
            {
                _mesh.Free();
            }
            _mesh = null;
        }
    }
}

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
    public class TmxException : Exception
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
    public interface TmxHasPoints
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
    public interface TmxHasProperties
    {
        Tiled2Unity.TmxProperties Properties { get; }
    }
}

// ----------------------------------------------------------------------
// TmxHelper.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Drawing.Drawing2D;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml;
// using System.Xml.Linq;


namespace Tiled2Unity
{
    public class TmxHelper
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

        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName)
        {
            string colorString = elem.Attribute(attrName).Value;
            System.Drawing.Color color = TmxHelper.ColorFromHtml(colorString);
            return color;
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

        public static TmxProperties GetPropertiesWithTypeDefaults(TmxHasProperties hasProperties, TmxObjectTypes objectTypes)
        {
            TmxProperties tmxProperties = new TmxProperties();

            // Fill in all the default properties first
            // (Note: At the moment, only TmxObject has default properties it inherits from TmxObjectType)
            string objectTypeName = null;
            if (hasProperties is TmxObject)
            {
                TmxObject tmxObject = hasProperties as TmxObject;
                objectTypeName = tmxObject.Type;
            }

            // If an object type has been found then copy over all the default values for properties
            TmxObjectType tmxObjectType = objectTypes.GetValueOrNull(objectTypeName);
            if (tmxObjectType != null)
            {
                foreach (TmxObjectTypeProperty tmxTypeProp in tmxObjectType.Properties.Values)
                {
                    tmxProperties.PropertyMap[tmxTypeProp.Name] = new TmxProperty() { Name = tmxTypeProp.Name, Type = tmxTypeProp.Type, Value = tmxTypeProp.Default };
                }
            }

            // Now add all the object properties (which may override some of the default properties)
            foreach (TmxProperty tmxProp in hasProperties.Properties.PropertyMap.Values)
            {
                tmxProperties.PropertyMap[tmxProp.Name] = tmxProp;
            }

            return tmxProperties;
        }

        public static Color ColorFromHtml(string html)
        {
            // Trim any leading hash from the string
            html = html.TrimStart('#');

            // Put leading zeros into anything less than 6 characters
            html = html.PadLeft(6, '0');

            // Put leading F into anthing less than 8 characters to cover alpha
            html = html.PadLeft(8, 'F');

            // Convert the hex string into a number
            try
            {
                int argb = Convert.ToInt32(html, 16);
                return Color.FromArgb(argb);
            }
            catch
            {
                return Color.HotPink;
            }
        }

        // Prefer 32bpp bitmaps as they are at least 2x faster at Graphics.DrawImage functions
        // Note that 32bppPArgb is not properly supported on Mac builds.
        public static Bitmap CreateBitmap32bpp(int width, int height)
        {
#if TILED2UNITY_MAC
            return new Bitmap(width, height);
#else
            return new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
#endif
        }

        public static Bitmap FromFileBitmap32bpp(string file)
        {
            Bitmap bitmapRaw = (Bitmap)Bitmap.FromFile(file);

#if TILED2UNITY_MAC
            return bitmapRaw;
#else
            // Need to copy the bitmap into our 32bpp surface
            Bitmap bitmapPArgb = TmxHelper.CreateBitmap32bpp(bitmapRaw.Width, bitmapRaw.Height);

            using (Graphics g = Graphics.FromImage(bitmapPArgb))
            {
                g.DrawImage(bitmapRaw, 0, 0, bitmapPArgb.Width, bitmapPArgb.Height);
            }

            return bitmapPArgb;
#endif
        }

#if !TILED_2_UNITY_LITE
        // Helper function to create Layer collider brush. Note that Mac does not support Hatch brushes.
        public static Brush CreateLayerColliderBrush(Color color)
        {
#if TILED2UNITY_MAC
            // On Mac we can use a solid brush with some alpha
            return new SolidBrush(Color.FromArgb(100, color));
#else
            return new HatchBrush(HatchStyle.Percent60, color, Color.Transparent);
#endif
        }
#endif

#if !TILED_2_UNITY_LITE
        // Helper function to create Object collider brush. Note that Mac does not support Hatch brushes.
        public static Brush CreateObjectColliderBrush(Color color)
        {
            Color secondary = Color.FromArgb(100, color);
#if TILED2UNITY_MAC
            // On Mac we can use a solid brush with some alpha
            return new SolidBrush(secondary);
#else
            return new HatchBrush(HatchStyle.BackwardDiagonal, color, secondary);
#endif
        }
#endif

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
                tmxImage.ImageBitmap = TmxHelper.FromFileBitmap32bpp(tmxImage.AbsolutePath);
            }
            catch (FileNotFoundException fnf)
            {
                string msg = String.Format("Image file not found: {0}", tmxImage.AbsolutePath);
                throw new TmxException(msg, fnf);

                // Testing for when image files are missing. Just make up an image.
                //int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
                //int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
                //tmxImage.ImageBitmap = new TmxHelper.CreateBitmap32bpp(width, height);
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
#if !TILED_2_UNITY_LITE
                System.Drawing.Color transColor = TmxHelper.ColorFromHtml(tmxImage.TransparentColor);
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
        public int Width { get; private set; }
        public int Height { get; private set; }
        public uint[] TileIds { get; private set; }
        public List<TmxMesh> Meshes { get; private set; }
        public List<TmxLayer> CollisionLayers { get; private set; }

        public TmxLayer(TmxMap map) : base(map)
        {
            this.Visible = true;
            this.Opacity = 1.0f;
            this.CollisionLayers = new List<TmxLayer>();
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
            return Tiled2Unity.Settings.PreferConvexPolygons;
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
                    Logger.WriteWarning("Image Layer '{0}' is being ignored since it has no image.", tmxLayer.Name);
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

            // Sometimes TMX files have "dead" tiles in them (tiles that were removed but are still referenced)
            // Remove these tiles from the layer by replacing them with zero
            for (int t = 0; t < tmxLayer.TileIds.Length; ++t)
            {
                uint tileId = tmxLayer.TileIds[t];
                tileId = TmxMath.GetTileIdWithoutFlags(tileId);
                if (!tmxMap.Tiles.ContainsKey(tileId))
                {
                    tmxLayer.TileIds[t] = 0;
                }
            }

            // Each layer will be broken down into "meshes" which are collections of tiles matching the same texture or animation
            tmxLayer.Meshes = TmxMesh.ListFromTmxLayer(tmxLayer);

            // Each layer may contain different collision types which are themselves put into "Collison Layers" to be processed later
            tmxLayer.UnityLayerOverrideName = tmxLayer.Properties.GetPropertyValueAsString("unity:layer", "");
            tmxLayer.BuildCollisionLayers();

            return tmxLayer;
        }

        private void ParseData(XElement elem)
        {
            Logger.WriteLine("Parse {0} layer data ...", this.Name);

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
            Logger.WriteLine("Parsing layer data as Xml elements ...");
            var tiles = from t in elemData.Elements("tile")
                        select TmxHelper.GetAttributeAsUInt(t, "gid");
            this.TileIds = tiles.ToArray();
        }

        private void ParseTileDataAsCsv(XElement elem)
        {
            Logger.WriteLine("Parsing layer data as CSV ...");
            List<uint> tileIds = new List<uint>();

            // Splitting line-by-line reducues out-of-memory exceptions in x86 builds
            string value = elem.Value;
            StringReader reader = new StringReader(value);
            string line = string.Empty;
            do
            {
                line = reader.ReadLine();
                if (!String.IsNullOrEmpty(line))
                {
                    var datum = from val in line.Split(',')
                                where !String.IsNullOrEmpty(val)
                                select Convert.ToUInt32(val);
                    tileIds.AddRange(datum);
                }

            } while (line != null);

            this.TileIds = tileIds.ToArray();
        }

        private void ParseTileDataAsBase64(XElement elem)
        {
            Logger.WriteLine("Parsing layer data as base64 string ...");
            byte[] bytes = Convert.FromBase64String(elem.Value);
            BytesToTiles(bytes);
        }

        private void ParseTileDataAsBase64GZip(XElement elem)
        {
            Logger.WriteLine("Parsing layer data as base64 gzip-compressed string ...");
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
            Logger.WriteLine("Parsing layer data as base64 zlib-compressed string ...");
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

        private void BuildCollisionLayers()
        {
            this.CollisionLayers.Clear();

            // Don't build collision layers if we're invisible
            if (this.Visible == false)
                return;

            // Don't build collision layers if we're ignored
            if (this.Ignore == IgnoreSettings.True)
                return;

            // Don't build collision layers if collision is ignored
            if (this.Ignore == IgnoreSettings.Collision)
                return;

            // Are we using a unity-layer override? If so we have to put everything from this layer into it.
            if (String.IsNullOrEmpty(this.UnityLayerOverrideName))
            {
                BuildBuildCollisionLayers_ByObjectType();
            }
            else
            {
                BuildBuildCollisionLayers_Override();
            }
        }

        private void BuildBuildCollisionLayers_Override()
        {
            // Just make the layer the collision layer
            this.CollisionLayers.Clear();
            this.CollisionLayers.Add(this);
        }

        private void BuildBuildCollisionLayers_ByObjectType()
        {
            // Find all tiles with collisions on them and put them into a "Collision Layer" of the same type
            for (int t = 0; t < this.TileIds.Length; ++t)
            {
                uint rawTileId = this.TileIds[t];
                if (rawTileId == 0)
                    continue;

                uint tileId = TmxMath.GetTileIdWithoutFlags(rawTileId);
                TmxTile tmxTile = this.TmxMap.Tiles[tileId];

                foreach (TmxObject colliderObject in tmxTile.ObjectGroup.Objects)
                {
                    if ((colliderObject is TmxHasPoints) == false)
                        continue;

                    // We have a collider object on the tile
                    // Add the tile to the Collision Layer of the matching type
                    // Or, create a new Collision Layer of this type to add this tile to
                    TmxLayer collisionLayer = this.CollisionLayers.Find(l => String.Compare(l.Name, colliderObject.Type, true) == 0);
                    if (collisionLayer == null)
                    {
                        // Create a new Collision Layer
                        collisionLayer = new TmxLayer(this.TmxMap);
                        this.CollisionLayers.Add(collisionLayer);

                        // The new Collision Layer has the name of the collider object and empty tiles (they will be filled with tiles that have matching collider objects)
                        collisionLayer.Name = colliderObject.Type;
                        collisionLayer.TileIds = new uint[this.TileIds.Length];

                        // Copy over some stuff from parent layer that we need for creating collisions
                        collisionLayer.Offset = this.Offset;
                        collisionLayer.Width = this.Width;
                        collisionLayer.Height = this.Height;
                        collisionLayer.Ignore = this.Ignore;
                        collisionLayer.Properties = this.Properties;
                    }

                    // Add the tile to this collision layer
                    collisionLayer.TileIds[t] = rawTileId;
                }
            }
        }

    }
}

// ----------------------------------------------------------------------
// TmxLayerBase.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    // There are several different "layer" types in Tiled that share some behaviour (tile layer, object layer, image layer)
    // (In Tiled2Unity we treat image layers as a special case of tile layer)
    public class TmxLayerBase : TmxHasProperties
    {
        public enum IgnoreSettings
        {
            False,      // Ingore nothing (layer fully-enabled)
            True,       // Ignore everything (like layer doesn't exist)
            Collision,  // Ignore collision on layer
            Visual,     // Ignore visual on layer
        };

        public TmxMap TmxMap { get; private set; }

        public string Name { get; protected set; }
        public bool Visible { get; protected set; }
        public float Opacity { get; protected set; }
        public PointF Offset { get; protected set; }
        public IgnoreSettings Ignore { get; protected set; }

        public TmxProperties Properties { get; protected set; }

        public int XmlElementIndex { get; protected set; }

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }

        public string UnityLayerOverrideName { get; protected set; }

        public TmxLayerBase(TmxMap tmxMap)
        {
            this.TmxMap = tmxMap;
        }
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

        public bool IsLoaded { get; private set; }

        public string Name { get; private set; }
        public MapOrientation Orientation { get; set; }
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

        // The map may load object type data from another file
        public TmxObjectTypes ObjectTypes = new TmxObjectTypes();

        private uint nextUniqueId = 0;

        public TmxMap()
        {
            this.IsLoaded = false;
            this.Properties = new TmxProperties();
        }

        public string GetExportedFilename()
        {
            return String.Format("{0}.tiled2unity.xml", this.Name);
        }

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

        // Load an Object Type Xml file for this map's objects to reference
        public void LoadObjectTypeXml(string xmlPath)
        {
            Logger.WriteLine("Loading Object Type Xml file: '{0}'", xmlPath);

            try
            {
                this.ObjectTypes = TmxObjectTypes.FromXmlFile(xmlPath);
            }
            catch (FileNotFoundException)
            {
                Logger.WriteError("Object Type Xml file was not found: {0}", xmlPath);
                this.ObjectTypes = new TmxObjectTypes();
            }
            catch (Exception e)
            {
                Logger.WriteError("Error parsing Object Type Xml file: {0}\n{1}", xmlPath, e.Message);
                this.ObjectTypes = new TmxObjectTypes();
            }

            Logger.WriteLine("Tiled Object Type count = {0}", this.ObjectTypes.TmxObjectTypeMapping.Count());
        }

        public void ClearObjectTypeXml()
        {
            Logger.WriteLine("Removing Object Types from map.");
            this.ObjectTypes = new TmxObjectTypes();
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
                Logger.WriteLine("Map details: {0}", tmxMap.ToString());
                Logger.WriteSuccess("Parsed: {0} ", fullTmxPath);

                tmxMap.IsLoaded = true;
                return tmxMap;
            }
        }

        private XDocument LoadDocument(string xmlPath)
        {
            XDocument doc = null;
            Logger.WriteLine("Opening {0} ...", xmlPath);
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
            Logger.WriteLine("Parsing map root ...");

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
            Logger.WriteLine("Parsing tileset elements ...");
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
                ParseExternalTileset(attrSource.Value, firstId);
            }
        }

        // This method is called eventually for external tilesets too
        // Only the gid attribute has been consumed at this point for the tileset
        private void ParseInternalTileset(XElement elemTileset, uint firstId)
        {
            string tilesetName = TmxHelper.GetAttributeAsString(elemTileset, "name");

            Logger.WriteLine("Parse internal tileset '{0}' (gid = {1}) ...", tilesetName, firstId);

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
                        TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
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
                    TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
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
            Logger.WriteLine("Added {0} tiles", tilesToAdd.Count);

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
                    Logger.WriteWarning("Tile '{0}' in tileset '{1}' does not exist but there is tile data for it.\n{2}", localTileId, tilesetName, elemTile.ToString());
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
                Logger.WriteWarning("Image Layer '{0}' has no image assigned.", tilesetName);
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

            TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
            tile.SetTileSize(tmxImage.Size.Width, tmxImage.Size.Height);
            tile.SetLocationOnSource(0, 0);
            this.Tiles[tile.GlobalId] = tile;
        }

        private void ParseAllLayers(XDocument doc)
        {
            Logger.WriteLine("Parsing layer elements ...");

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
                    Logger.WriteLine("Ignoring layer due to unity:ignore = True property: {0}", tmxLayer.Name);
                    continue;
                }

                this.Layers.Add(tmxLayer);
            }
        }

        private void ParseAllObjectGroups(XDocument doc)
        {
            Logger.WriteLine("Parsing objectgroup elements ...");
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
    public class TmxMath
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

        public static PointF MidPoint(PointF a, PointF b)
        {
            float mx = (a.X + b.X) * 0.5f;
            float my = (a.Y + b.Y) * 0.5f;
            return new PointF(mx, my);
        }

        public static PointF ScalePoint(PointF p, float s)
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

        public int StartingTileIndex { get; private set; }
        public int NumberOfTiles { get; private set; }

        // Animation properties
        public int StartTimeMs { get; private set; }
        public int DurationMs { get; private set; }
        public int FullAnimationDurationMs { get; private set; }

        public bool IsMeshFull()
        {
            return this.NumberOfTiles >= TmxMesh.MaxNumberOfTiles;
        }

        public uint GetTileIdAt(int tileIndex)
        {
            int fauxIndex = tileIndex - this.StartingTileIndex;
            if (fauxIndex < 0 || fauxIndex >= this.TileIds.Length)
            {
                return 0;
            }

            return this.TileIds[fauxIndex];
        }

        private void AddTile(int index, uint tileId)
        {
            // Assumes non-zero tileIds
            this.TileIds[index] = tileId;
            this.NumberOfTiles++;

            // Is the mesh "full" now
            if (IsMeshFull())
            {
                List<uint> tiles = this.TileIds.ToList();

                // Remove leading batch of zero tiles
                int firstNonZero = tiles.FindIndex(t => t != 0);
                if (firstNonZero > 0)
                {
                    this.StartingTileIndex = firstNonZero;
                    tiles.RemoveRange(0, firstNonZero);
                }
                
                // Remove the trailing batch of zero tiles
                tiles.Reverse();
                firstNonZero = tiles.FindIndex(t => t != 0);
                if (firstNonZero > 0)
                {
                    tiles.RemoveRange(0, firstNonZero);
                }

                // Reverse the tiles back
                tiles.Reverse();

                this.TileIds = tiles.ToArray();
            }
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
                    TmxMesh mesh = meshes.Find(m => m.CanAddFrame(tile, timeMs, frame.DurationMs, tile.Animation.TotalTimeMs));
                    if (mesh == null)
                    {
                        var frameTile = layer.TmxMap.GetTileFromTileId(frameTileId);

                        // Create a new mesh and add it to our list
                        mesh = new TmxMesh();
                        mesh.TileIds = new uint[layer.TileIds.Count()];
                        mesh.UniqueMeshName = String.Format("mesh_{0}", layer.TmxMap.GetUniqueId().ToString("D4"));
                        mesh.TmxImage = frameTile.TmxImage;

                        // Keep track of the timing for this mesh (non-animating meshes will have a start time and duration of 0)
                        mesh.StartTimeMs = timeMs;
                        mesh.DurationMs = frame.DurationMs;
                        mesh.FullAnimationDurationMs = tile.Animation.TotalTimeMs;
                        mesh.ObjectName = Path.GetFileNameWithoutExtension(frameTile.TmxImage.AbsolutePath);

                        if (mesh.DurationMs != 0)
                        {
                            // Decorate the name a bit with some animation details for the frame
                            mesh.ObjectName += string.Format("[{0}-{1}][{2}]", timeMs, timeMs + mesh.DurationMs, mesh.FullAnimationDurationMs);
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
                    mesh.ObjectName += string.Format("[{0}-{1}][{2}]", timeMs, timeMs + mesh.DurationMs, mesh.FullAnimationDurationMs);
                }

                // Advance time
                timeMs += frame.DurationMs;

                // Add the animation frame to our list of meshes
                meshes.Add(mesh);
            }

            return meshes;
        }

        private bool CanAddFrame(TmxTile tile, int startMs, int durationMs, int totalTimeMs)
        {
            if (IsMeshFull())
                return false;

            if (this.TmxImage != tile.TmxImage)
                return false;

            if (this.StartTimeMs != startMs)
                return false;

            if (this.DurationMs != durationMs)
                return false;

            if (this.FullAnimationDurationMs != totalTimeMs)
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
            Logger.WriteLine("Parsing object ...");

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
                    Logger.WriteWarning("Tile Id {0} not found in tilesets. Using a rectangle instead.\n{1}", gid, xml.ToString());
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
    public class TmxObjectEllipse : TmxObject
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
        public List<TmxObject> Objects { get; private set; }
        public Color Color { get; private set; }

        public TmxObjectGroup(TmxMap tmxMap) : base(tmxMap)
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

            TmxObjectGroup tmxObjectGroup = new TmxObjectGroup(tmxMap);

            // Order within Xml file is import for layer types
            tmxObjectGroup.XmlElementIndex = xml.NodesBeforeSelf().Count();

            tmxObjectGroup.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObjectGroup.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;
            tmxObjectGroup.Opacity = TmxHelper.GetAttributeAsFloat(xml, "opacity", 1);
            tmxObjectGroup.Color = TmxHelper.GetAttributeAsColor(xml, "color", Color.FromArgb(128, 128, 128));
            tmxObjectGroup.Properties = TmxProperties.FromXml(xml);

            // Set the "ignore" setting on this object group
            tmxObjectGroup.Ignore = tmxObjectGroup.Properties.GetPropertyValueAsEnum<IgnoreSettings>("unity:ignore", IgnoreSettings.False);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(xml, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(xml, "offsety", 0);
            tmxObjectGroup.Offset = offset;

            // Get all the objects
            Logger.WriteLine("Parsing objects in object group '{0}'", tmxObjectGroup.Name);
            var objects = from obj in xml.Elements("object")
                          select TmxObject.FromXml(obj, tmxObjectGroup, tmxMap);

            // The objects are ordered "visually" by Y position
            tmxObjectGroup.Objects = objects.OrderBy(o => TmxMath.ObjectPointFToMapSpace(tmxMap, o.Position).Y).ToList();

            // Are we using a unity:layer override?
            tmxObjectGroup.UnityLayerOverrideName = tmxObjectGroup.Properties.GetPropertyValueAsString("unity:layer", "");

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
    public class TmxObjectPolygon : TmxObject, TmxHasPoints
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
    public class TmxObjectPolyline : TmxObject, TmxHasPoints
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

            // If there are only 2 points in the polyline then we force a midpoint between them
            // This is because the clipper library is rejecting polylines unless there is 3 or more points
            if (points.Count() == 2)
            {
                var A = points.First();
                var B = points.Last();
                var M = TmxMath.MidPoint(A, B);
                this.Points = new List<PointF>() { A, M, B };
            }
            else
            {
                this.Points = points.ToList();
            }
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
    public class TmxObjectRectangle : TmxObjectPolygon
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
                Logger.WriteWarning("Warning: Rectangle has zero width or height in object group\n{0}", xml.Parent.ToString());
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
    public class TmxObjectTile : TmxObject
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
// TmxObjectType.cs

// using System;
// using System.Collections.Generic;
// using System.Drawing;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    // Has data for a single object type
    public class TmxObjectType
    {
        public string Name { get; private set; }
        public Color Color { get; private set; }
        public Dictionary<string, TmxObjectTypeProperty> Properties { get; private set; }

        public TmxObjectType()
        {
            this.Name = "";
            this.Color = Color.Gray;
            this.Properties = new Dictionary<string, TmxObjectTypeProperty>();
        }

        public static TmxObjectType FromXml(XElement xml)
        {
            TmxObjectType tmxObjectType = new TmxObjectType();

            tmxObjectType.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObjectType.Color = TmxHelper.GetAttributeAsColor(xml, "color", Color.Gray);
            tmxObjectType.Properties = TmxObjectTypeProperty.FromObjectTypeXml(xml);

            return tmxObjectType;
        }
    }
}

// ----------------------------------------------------------------------
// TmxObjectTypeProperty.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    public class TmxObjectTypeProperty
    {
        public string Name { get; private set; }
        public TmxPropertyType Type { get; private set; }
        public string Default { get; set; }

        // Create a dictionary collection of Object Type Property instances from the parent xml element
        public static Dictionary<string, TmxObjectTypeProperty> FromObjectTypeXml(XElement xmlObjectType)
        {
            Dictionary<string, TmxObjectTypeProperty> tmxObjectTypeProperties = new Dictionary<string, TmxObjectTypeProperty>();

            foreach (var xmlProperty in xmlObjectType.Elements("property"))
            {
                TmxObjectTypeProperty tmxObjectTypeProperty = new TmxObjectTypeProperty();

                tmxObjectTypeProperty.Name = TmxHelper.GetAttributeAsString(xmlProperty, "name", "");
                tmxObjectTypeProperty.Type = TmxHelper.GetAttributeAsEnum(xmlProperty, "type", TmxPropertyType.String);
                tmxObjectTypeProperty.Default = TmxHelper.GetAttributeAsString(xmlProperty, "default", "");

                tmxObjectTypeProperties.Add(tmxObjectTypeProperty.Name, tmxObjectTypeProperty);
            }

            return tmxObjectTypeProperties;
        }
    }
}

// ----------------------------------------------------------------------
// TmxObjectTypes.cs

// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Text;
// using System.Xml.Linq;

namespace Tiled2Unity
{
    // The "objecttypes.xml" file has project-specific data to be used with the TmxObject instances
    public class TmxObjectTypes
    {
        public Dictionary<string, TmxObjectType> TmxObjectTypeMapping { get; private set; }

        public TmxObjectTypes()
        {
            this.TmxObjectTypeMapping = new Dictionary<string, TmxObjectType>(StringComparer.InvariantCultureIgnoreCase);
        }

        public TmxObjectType GetValueOrDefault(string key)
        {
            if (this.TmxObjectTypeMapping.ContainsKey(key))
            {
                return this.TmxObjectTypeMapping[key];
            }

            return new TmxObjectType();
        }

        public TmxObjectType GetValueOrNull(string key)
        {
            if (key != null && this.TmxObjectTypeMapping.ContainsKey(key))
            {
                return this.TmxObjectTypeMapping[key];
            }

            return null;
        }


        public static TmxObjectTypes FromXmlFile(string xmlPath)
        {
            TmxObjectTypes xmlObjectTypes = new TmxObjectTypes();

            XDocument doc = XDocument.Load(xmlPath);

            foreach (var xml in doc.Element("objecttypes").Elements("objecttype"))
            {
                TmxObjectType tmxObjectType = TmxObjectType.FromXml(xml);
                xmlObjectTypes.TmxObjectTypeMapping[tmxObjectType.Name] = tmxObjectType;
            }

            return xmlObjectTypes;
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
        public IDictionary<string, TmxProperty> PropertyMap { get; private set; }

        public TmxProperties()
        {
            this.PropertyMap = new Dictionary<string, TmxProperty>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string GetPropertyValueAsString(string name)
        {
            return this.PropertyMap[name].Value;
        }

        public string GetPropertyValueAsString(string name, string defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return this.PropertyMap[name].Value;
            return defaultValue;
        }

        public int GetPropertyValueAsInt(string name)
        {
            try
            {
                return Convert.ToInt32(this.PropertyMap[name].Value);
            }
            catch (System.FormatException inner)
            {
                string message = String.Format("Error evaulating property '{0}={1}'\n  '{1}' is not an integer", name, this.PropertyMap[name].Value);
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
                asBoolean = Convert.ToBoolean(this.PropertyMap[name].Value);
            }
            catch (FormatException)
            {
                Logger.WriteWarning("Property '{0}' value '{1}' cannot be converted to a boolean.", name, this.PropertyMap[name].Value);
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
            return TmxHelper.GetStringAsEnum<T>(this.PropertyMap[name].Value);
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
                            Type = TmxHelper.GetAttributeAsEnum(elem2, "type", TmxPropertyType.String),

                            // Value may be attribute or inner text
                            Value = TmxHelper.GetAttributeAsString(elem2, "value", null) ?? elem2.Value,
                        };

            if (props.Count() > 0)
            {
                Logger.WriteLine("Parse properites ...");
            }

            foreach (var p in props)
            {
                tmxProps.PropertyMap[p.Name] = new TmxProperty { Name = p.Name, Type = p.Type, Value = p.Value };
            }

            return tmxProps;
        }

    }
}

// ----------------------------------------------------------------------
// TmxProperty.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public class TmxProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public TmxPropertyType Type { get; set; }
    }
}

// ----------------------------------------------------------------------
// TmxPropertyType.cs

// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;

namespace Tiled2Unity
{
    public enum TmxPropertyType
    {
        String,
        Int,
        Float,
        Bool,
        Color,
        File,
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
        public TmxMap TmxMap { get; private set; }
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


        public TmxTile(TmxMap tmxMap, uint globalId, uint localId, string tilesetName, TmxImage tmxImage)
        {
            this.TmxMap = TmxMap;
            this.GlobalId = globalId;
            this.LocalId = localId;
            this.TmxImage = tmxImage;
            this.Properties = new TmxProperties();
            this.ObjectGroup = new TmxObjectGroup(this.TmxMap);
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
            Logger.WriteLine("Parse tile data (gid = {0}, id {1}) ...", this.GlobalId, this.LocalId);

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


