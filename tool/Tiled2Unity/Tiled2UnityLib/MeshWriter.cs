using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tiled2Unity
{
    public class MeshWriter
    {
        private BitPlane mTileUsed = null;

        public StringBuilder Builder = null;

        public HashIndexOf<Vertex3> PositionDatabase = null;

        public HashIndexOf<PointF> TexcoordDatabase = null;

        public TmxMesh Mesh = null;

        public MeshWriter() { }

        public MeshWriter(
            StringBuilder builder, 
            HashIndexOf<Vertex3> positionDatabase, 
            HashIndexOf<PointF> texcoordDatabase, 
            TmxMesh mesh)
        {
            Builder = builder;
            PositionDatabase = positionDatabase;
            TexcoordDatabase = texcoordDatabase;
            Mesh = mesh;
        }

        public int Execute()
        {
            if (Mesh == null || 
                Builder == null ||
                PositionDatabase == null ||
                TexcoordDatabase == null)
            {
                return -1;
            }
            var layer = Mesh.Layer;
            var map = layer.TmxMap;
            //var vertexDatabase = new HashIndexOf<TiledMapExporter.Vertex3>();
            //var uvDatabase = new HashIndexOf<PointF>();
            float mapLogicalHeight = map.MapSizeInPixels().Height;
            mTileUsed = new BitPlane(layer.Width, layer.Height);
            
            var verticalRange = (map.DrawOrderVertical == 1) ? Enumerable.Range(0, layer.Height) : Enumerable.Range(0, layer.Height).Reverse();
            var horizontalRange = (map.DrawOrderHorizontal == 1) ? Enumerable.Range(0, layer.Width) : Enumerable.Range(0, layer.Width).Reverse();

            Logger.WriteLine("Writing '{0}' mesh group", Mesh.UniqueMeshName);
            Builder.AppendFormat("\ng {0}\n", Mesh.UniqueMeshName);

            FaceVertices faceVertices;
            PointF[] uvs;

            foreach (int y in verticalRange)
            {
                foreach (int x in horizontalRange)
                {
                    if (!DetermineQuad(x, y, out faceVertices, out uvs))
                    {
                        continue;
                    }

                    // Adds vertices and uvs to the database as we build the face strings
                    string v0 = String.Format("{0}/{1}/1", PositionDatabase.Add(faceVertices.V0) + 1, TexcoordDatabase.Add(uvs[0]) + 1);
                    string v1 = String.Format("{0}/{1}/1", PositionDatabase.Add(faceVertices.V1) + 1, TexcoordDatabase.Add(uvs[1]) + 1);
                    string v2 = String.Format("{0}/{1}/1", PositionDatabase.Add(faceVertices.V2) + 1, TexcoordDatabase.Add(uvs[2]) + 1);
                    string v3 = String.Format("{0}/{1}/1", PositionDatabase.Add(faceVertices.V3) + 1, TexcoordDatabase.Add(uvs[3]) + 1);
                    Builder.AppendFormat("f {0} {1} {2} {3}\n", v0, v1, v2, v3);
                }
            }
            return 0;
        }

        private bool DetermineQuad(int x, int y, out FaceVertices positions, out PointF[] texcoords)
        {
            // Is this tile already part of the mesh?
            if (mTileUsed.Get(x, y))
            {
                positions = new FaceVertices { };
                texcoords = null;
                return false;
            }

            var layer = Mesh.Layer;
            var map = layer.TmxMap;
            int tileIndex = layer.GetTileIndex(x, y);
            uint tileId = Mesh.GetTileIdAt(tileIndex);

            // Skip blank tiles
            if (tileId == 0)
            {
                positions = new FaceVertices { };
                texcoords = null;
                return false;
            }
            var tile = map.Tiles[TmxMath.GetTileIdWithoutFlags(tileId)];
            bool flipDiagonal = TmxMath.IsTileFlippedDiagonally(tileId);
            bool flipHorizontal = TmxMath.IsTileFlippedHorizontally(tileId);
            bool flipVertical = TmxMath.IsTileFlippedVertically(tileId);

            var position = map.GetMapPositionAt(x, y);

            // If we're using depth shaders then we'll need to set a depth value of this face
            float depth_z = 0.0f;
            if (Tiled2Unity.Settings.DepthBufferEnabled)
            {
                depth_z = position.Y / map.MapSizeInPixels().Height * -1.0f;
            }

            if (tile.IsSingleColor)
            {
                Logger.WriteLine("Found single-color tile with ID {0}", tileId);
            }

            var pos2 = CalculateFaceVertices(position, tile.TileSize, map.TileHeight, tile.Offset);

            positions = new FaceVertices { Vertices = pos2, Depth_z = depth_z };
            texcoords = CalculateFaceTextureCoordinates(tile, flipDiagonal, flipHorizontal, flipVertical);

            return true;
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
            PointF[] vertices = new PointF[4];
            vertices[3] = TiledMapExporter.PointFToObjVertex(pt0);
            vertices[2] = TiledMapExporter.PointFToObjVertex(pt1);
            vertices[1] = TiledMapExporter.PointFToObjVertex(pt2);
            vertices[0] = TiledMapExporter.PointFToObjVertex(pt3);
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
            vertices[3] = TiledMapExporter.PointFToObjVertex(pt0);
            vertices[2] = TiledMapExporter.PointFToObjVertex(pt1);
            vertices[1] = TiledMapExporter.PointFToObjVertex(pt2);
            vertices[0] = TiledMapExporter.PointFToObjVertex(pt3);
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
            coordinates[3] = TiledMapExporter.PointToTextureCoordinate(points[0], imageSize);
            coordinates[2] = TiledMapExporter.PointToTextureCoordinate(points[1], imageSize);
            coordinates[1] = TiledMapExporter.PointToTextureCoordinate(points[2], imageSize);
            coordinates[0] = TiledMapExporter.PointToTextureCoordinate(points[3], imageSize);

            // Apply a small bias to the "inner" edges of the texels
            // This keeps us from seeing seams
            //const float bias = 1.0f / 8192.0f;
            //const float bias = 1.0f / 4096.0f;
            //const float bias = 1.0f / 2048.0f;
            if (Tiled2Unity.Settings.TexelBias > 0)
            {
                float bias = 1.0f / Tiled2Unity.Settings.TexelBias;

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
