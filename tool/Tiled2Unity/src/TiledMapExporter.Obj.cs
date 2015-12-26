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
