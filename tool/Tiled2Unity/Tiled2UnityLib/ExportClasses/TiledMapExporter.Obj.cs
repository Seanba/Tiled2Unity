using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

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

        // Enumerate all our meshes and bake them into OBJ Wavefront format
        private IEnumerable<Tuple<string, StringWriter>> EnumerateWavefrontData()
        {
            Logger.WriteLine("Enumerate map layers for mesh-build.");
            foreach (var layer in this.tmxMap.EnumerateTileLayers())
            {
                if (layer.Visible != true)
                    continue;

                if (layer.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                // Enumerate over the tiles in the direction given by the draw order of the map
                var verticalRange = (this.tmxMap.DrawOrderVertical == 1) ? Enumerable.Range(0, layer.Height) : Enumerable.Range(0, layer.Height).Reverse();
                var horizontalRange = (this.tmxMap.DrawOrderHorizontal == 1) ? Enumerable.Range(0, layer.Width) : Enumerable.Range(0, layer.Width).Reverse();

                foreach (TmxMesh mesh in layer.Meshes)
                {
                    yield return Tuple.Create(mesh.UniqueMeshName, BuildWavefrontStringForLayerMesh(layer, mesh, horizontalRange, verticalRange));
                }
            }
            Logger.WriteLine("Finished enumeration.");

            Logger.WriteLine("Enumerate tile objects for mesh-build.");
            foreach (var mesh in this.tmxMap.GetUniqueListOfVisibleObjectTileMeshes())
            {
                yield return Tuple.Create(mesh.UniqueMeshName, BuildWavefrontStringForTileObjectMesh(mesh));
            }
            Logger.WriteLine("Finished enumeration.");
        }

        private StringWriter BuildWavefrontStringForLayerMesh(TmxLayer layer, TmxMesh mesh, IEnumerable<int> horizontalRange, IEnumerable<int> verticalRange)
        {
            Logger.WriteLine("Building mesh obj file for '{0}'", mesh.UniqueMeshName);
            GenericListDatabase<Vertex3> vertexDatabase = new GenericListDatabase<Vertex3>();
            HashIndexOf<PointF> uvDatabase = new HashIndexOf<PointF>();
            StringBuilder faces = new StringBuilder();

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
                    var position = this.tmxMap.GetMapPositionAt(x, y, tile);
                    var vertices = CalculateFaceVertices(position, tile.TileSize);

                    // If we're using depth shaders then we'll need to set a depth value of this face
                    float depth_z = 0.0f;
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        depth_z = CalculateFaceDepth(position.Y + tmxMap.TileHeight, tmxMap.MapSizeInPixels.Height);
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
                    faces.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);
                }
            }

            // We have all the data we need to build the wavefront file format
            return CreateWavefrontWriter(mesh, vertexDatabase, uvDatabase, faces);
        }

        private StringWriter BuildWavefrontStringForTileObjectMesh(TmxMesh mesh)
        {
            Logger.WriteLine("Building mesh obj file for tile: '{0}.obj'", mesh.UniqueMeshName);
            GenericListDatabase<Vertex3> vertexDatabase = new GenericListDatabase<Vertex3>();
            HashIndexOf<PointF> uvDatabase = new HashIndexOf<PointF>();
            StringBuilder faces = new StringBuilder();


            // Get the single tile associated with this mesh
            TmxTile tmxTile = this.tmxMap.Tiles[mesh.TileIds[0]];

            var vertices = CalculateFaceVertices_TileObject(tmxTile.TileSize, tmxTile.Offset);
            var uvs = CalculateFaceTextureCoordinates(tmxTile, false, false, false);

            // TileObjects have zero depth on their vertices. Their GameObject parent will set depth.
            FaceVertices faceVertices = new FaceVertices { Vertices = vertices, Depth_z = 0.0f };

            // Adds vertices and uvs to the database as we build the face strings
            string v0 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V0) + 1, uvDatabase.Add(uvs[0]) + 1);
            string v1 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V1) + 1, uvDatabase.Add(uvs[1]) + 1);
            string v2 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V2) + 1, uvDatabase.Add(uvs[2]) + 1);
            string v3 = String.Format("{0}/{1}/1", vertexDatabase.AddToDatabase(faceVertices.V3) + 1, uvDatabase.Add(uvs[3]) + 1);
            faces.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);

            // We have all the data we need to build the wavefront file format
            return CreateWavefrontWriter(mesh, vertexDatabase, uvDatabase, faces);
        }


        private StringWriter CreateWavefrontWriter(TmxMesh mesh, GenericListDatabase<Vertex3> vertexDatabase, HashIndexOf<PointF> uvDatabase, StringBuilder faces)
        {
            StringWriter wavefront = new StringWriter();
            wavefront.WriteLine("# Tiled2Unity generated file. Do not modify by hand.");
            wavefront.WriteLine("# Wavefront file for '{0}.obj'", mesh.UniqueMeshName);
            wavefront.WriteLine();

            wavefront.WriteLine("# Vertices (Count = {0})", vertexDatabase.List.Count());
            foreach (var v in vertexDatabase.List)
            {
                wavefront.WriteLine("v {0} {1} {2}", v.X, v.Y, v.Z);
            }
            wavefront.WriteLine();

            wavefront.WriteLine("# Texture cooridinates (Count = {0})", uvDatabase.List.Count());
            foreach (var uv in uvDatabase.List)
            {
                wavefront.WriteLine("vt {0} {1}", uv.X, uv.Y);
            }
            wavefront.WriteLine();

            // Write the one indexed normal
            wavefront.WriteLine("# Normal");
            wavefront.WriteLine("vn 0 0 -1");
            wavefront.WriteLine();

            // Now we can copy over the string used to build the databases
            wavefront.WriteLine("# Mesh description");
            wavefront.WriteLine("g {0}", mesh.UniqueMeshName);
            wavefront.WriteLine();
            wavefront.WriteLine("# Faces");
            wavefront.WriteLine(faces.ToString());

            return wavefront;
        }

        private PointF[] CalculateFaceVertices(Point mapLocation, Size tileSize)
        {
            PointF pt0 = mapLocation;
            PointF pt1 = PointF.Add(mapLocation, new Size(tileSize.Width, 0));
            PointF pt2 = PointF.Add(mapLocation, tileSize);
            PointF pt3 = PointF.Add(mapLocation, new Size(0, tileSize.Height));

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

            // Transform the points with our flip flags
            PointF center = new PointF(tileSize.Width * 0.5f, tileSize.Height * 0.5f);
            center.X += imageLocation.X;
            center.Y += imageLocation.Y;
            TmxMath.TransformPoints_DiagFirst(points, center, flipDiagonal, flipHorizontal, flipVertical);

            // "Tuck in" the points a tiny bit to help avoid seams
            // This can be turned off by setting Texel Bias to zero
            // Note that selecting a texel bias that is too small or a texture that is too big may affect pixel-perfect rendering (pixel snapping in shader will help)
            float bias = 0.0f;
            PointF[] tucks = new PointF[4];
            if (Tiled2Unity.Settings.TexelBias > 0)
            {
                bias = 1.0f / Tiled2Unity.Settings.TexelBias;
                tucks[0].X += 1.0f;
                tucks[0].Y += 1.0f;

                tucks[1].X -= 1.0f;
                tucks[1].Y += 1.0f;

                tucks[2].X -= 1.0f;
                tucks[2].Y -= 1.0f;

                tucks[3].X += 1.0f;
                tucks[3].Y -= 1.0f;
            }
            TmxMath.TransformPoints_DiagFirst(tucks, PointF.Empty, flipDiagonal, flipHorizontal, flipVertical);

            PointF[] coordinates = new PointF[4];
            coordinates[3] = TmxMath.AddPoints(PointToTextureCoordinate(points[0], imageSize), TmxMath.ScalePoint(tucks[0].X, -tucks[0].Y, bias));
            coordinates[2] = TmxMath.AddPoints(PointToTextureCoordinate(points[1], imageSize), TmxMath.ScalePoint(tucks[1].X, -tucks[1].Y, bias));
            coordinates[1] = TmxMath.AddPoints(PointToTextureCoordinate(points[2], imageSize), TmxMath.ScalePoint(tucks[2].X, -tucks[2].Y, bias));
            coordinates[0] = TmxMath.AddPoints(PointToTextureCoordinate(points[3], imageSize), TmxMath.ScalePoint(tucks[3].X, -tucks[3].Y, bias));

            return coordinates;
        }
    }
}
