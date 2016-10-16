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

        /// <summary>
        /// Execute the MeshWriter.
        /// </summary>
        /// <returns>0 for success, some other number for failure.</returns>
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
            var map = layer.Map;
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

        /// <summary>
        /// For the given coordinate (x,y) in the layer, attempts to find the largest
        /// possible valid quad.
        /// </summary>
        /// <param name="x">The x-coordinate of the tile in the layer.</param>
        /// <param name="y">The y-coordinate of the tile in the layer.</param>
        /// <param name="positions">The positions array of the quad.</param>
        /// <param name="texcoords">The texture coordinates array of the quad.</param>
        /// <returns>True if a quad is found and should be written out, false if no
        /// quad should be written out.</returns>
        private bool DetermineQuad(int x, int y, out FaceVertices positions, out PointF[] texcoords)
        {
            // Is this tile already part of the mesh?
            if (mTileUsed.Get(x, y))
            {
                positions = new FaceVertices { };
                texcoords = null;
                return false;
            }
            
            bool flipDiagonal, flipHorizontal, flipVertical;
            var tile = GetTile(x, y, out flipDiagonal, out flipHorizontal, out flipVertical);
            if (tile == null)
            {
                // This means we have a blank tile. Skip this one.
                positions = new FaceVertices { };
                texcoords = null;
                return false;
            }
            else if (flipDiagonal || flipHorizontal || flipVertical)
            {
                // The tile has a non-trivial rotation matrix, so we're not
                // gonna do our optimizations. Export this as a single tile.

                var position = Mesh.Layer.Map.GetMapPositionAt(x, y);
                // If we're using depth shaders then we'll need to set a depth value of this face
                float depth_z = 0.0f;
                if (Tiled2Unity.Settings.DepthBufferEnabled)
                {
                    depth_z = position.Y / Mesh.Layer.Map.MapSizeInPixels().Height * -1.0f;
                }


                if (tile.IsSingleColor)
                {
                    Logger.WriteLine("Found single-color tile with ID {0}", tile.LocalId);
                }

                var pos2 = CalculateFaceVertices(position, tile.TileSize, Mesh.Layer.Map.TileHeight, tile.Offset);

                positions = new FaceVertices { Vertices = pos2, Depth_z = depth_z };
                texcoords = CalculateFaceTextureCoordinates(tile, flipDiagonal, flipHorizontal, flipVertical);

                return true;
            }
            else
            {
                var position = Mesh.Layer.Map.GetMapPositionAt(x, y);
                // If we're using depth shaders then we'll need to set a depth value of this face
                float depth_z = 0.0f;
                if (Tiled2Unity.Settings.DepthBufferEnabled)
                {
                    depth_z = position.Y / Mesh.Layer.Map.MapSizeInPixels().Height * -1.0f;
                }

                // No null tile, unflipped in any possible direction.
                // Here's the point where we do the optimization.
                var singleColorRect = DoSingleColorHeuristic(x, y);
                mTileUsed.Set(singleColorRect, true);
                singleColorRect.Location = position;
                var pos2 = CalculateFaceVertices(singleColorRect, tile.TileSize, Mesh.Layer.Map.TileHeight, tile.Offset);
                positions = new FaceVertices { Vertices = pos2, Depth_z = depth_z };
                if (singleColorRect.Size == Size.Empty)
                {
                    // We didn't find any larger quad.
                    texcoords = CalculateFaceTextureCoordinates(tile, false, false, false);
                }
                else
                {
                    // We found a larger quad.
                    texcoords = CalculateFaceTextureCoordinatesForSingleColorHeuristic(singleColorRect, tile);
                }
                
                return true;
            }
        }

        private void CommitRectangle(Rectangle rect, out FaceVertices positions, out PointF[] texcoords)
        {
            // We have a computed rect and we want to build positions and texcoords out of it.
            // We also write the rect to the mTileUsed BitPlane so that we're not gonna
            // write the same quad multiple times.
            throw new NotImplementedException();
        }

        private Rectangle DoStackingHeuristic(int x, int y)
        {
            throw new NotImplementedException();
        }

        private Rectangle DoSingleColorHeuristic(int x, int y)
        {
            var rect = new Rectangle(x, y, 0, 0);
            DoSingleColorHeuristic(ref rect);
            return rect;
        }

        private void DoSingleColorHeuristic(ref Rectangle rect)
        {
            bool flip0, flip1, flip2;
            bool continueInHorDir = true;
            bool continueInVertDir = true;
            int horDir = Mesh.Layer.Map.DrawOrderHorizontal;
            int verDir = Mesh.Layer.Map.DrawOrderVertical;
            var startTile = GetTile(rect.Location, out flip0, out flip1, out flip2);
            if (startTile == null || flip0 || flip1 || flip2)
            {
                // This should be impossible because we
                // check for it in a method up in the call stack.
                throw new InvalidOperationException("MeshWriter error");
            }
            if (startTile.IsSingleColor == false)
            {
                return;
            }
            var startColor = startTile.TopLeftColor;

            // Here's the big loop that attempts to enlarge the rectangle.
            while (continueInHorDir || continueInVertDir)
            {
                if (continueInHorDir)
                {
                    continueInHorDir = CheckFrontierForSameColor(startColor, horDir == 1 ? rect.GetRightFrontier() : rect.GetLeftFrontier());
                }
                if (continueInVertDir)
                {
                    continueInVertDir = CheckFrontierForSameColor(startColor, verDir == 1 ? rect.GetBottomFrontier() : rect.GetTopFrontier());
                }
                if (continueInHorDir && continueInVertDir)
                {
                    int x = horDir == 1 ? rect.Right + 1 : rect.Left - 1;
                    int y = verDir == 1 ? rect.Bottom + 1 : rect.Top - 1;
                    var diagTile = GetTile(x, y, out flip0, out flip1, out flip2);
                    if (x >= mTileUsed.Width ||
                        y >= mTileUsed.Height ||
                        mTileUsed.Get(x, y) || 
                        diagTile == null ||
                        flip0 ||
                        flip1 ||
                        flip2 ||
                        diagTile.IsSingleColor == false ||
                        diagTile.TopLeftColor != startColor)
                    {
                        // Here we're making a choice. Do we continue in the bottom
                        // or continue to the right? What is wise?
                        continueInVertDir = true;
                        continueInHorDir = false;
                        if (verDir == 1)
                        {
                            RectangleUtils.EnlargeBottom(ref rect);
                        }
                        else // verDir == -1
                        {
                            RectangleUtils.EnlargeTop(ref rect);
                        }
                    }
                    else
                    {
                        if (horDir == 1)
                        {
                            RectangleUtils.EnlargeRight(ref rect);
                        }
                        else // horDir == -1
                        {
                            RectangleUtils.EnlargeLeft(ref rect);
                        }
                        if (verDir == 1)
                        {
                            RectangleUtils.EnlargeBottom(ref rect);
                        }
                        else // verDir == -1
                        {
                            RectangleUtils.EnlargeTop(ref rect);
                        }
                    }
                }
                else
                {
                    if (continueInHorDir)
                    {
                        if (horDir == 1) RectangleUtils.EnlargeRight(ref rect);
                        else RectangleUtils.EnlargeLeft(ref rect);
                    }
                    if (continueInVertDir)
                    {
                        if (verDir == 1) RectangleUtils.EnlargeBottom(ref rect);
                        else RectangleUtils.EnlargeTop(ref rect);
                    }
                }

                if ((horDir == 1 && rect.Right == Mesh.Layer.Width - 1) ||
                    (horDir == -1 && rect.Left == 0))
                {
                    // Don't continue or we'll go out-of-bounds.
                    continueInHorDir = false;
                }
                if ((verDir == 1 && rect.Bottom == Mesh.Layer.Height - 1) ||
                    (verDir == -1 && rect.Top == 0))
                {
                    // Don't continue or we'll go out-of-bounds.
                    continueInVertDir = false;
                }
                
            }
        }

        private bool CheckFrontierForSameColor(Color startColor, RectangleFrontierProxy frontier)
        {
            bool flip0, flip1, flip2;
            foreach (var pt in frontier)
            {
                if (pt.X >= mTileUsed.Width || pt.Y >= mTileUsed.Height) return false;
                if (mTileUsed.Get(pt.X, pt.Y)) return false;
                var tile = GetTile(pt, out flip0, out flip1, out flip2);
                if (tile == null ||
                    flip0 ||
                    flip1 ||
                    flip2 ||
                    tile.IsSingleColor == false ||
                    tile.TopLeftColor != startColor)
                {
                    return false;
                }
            }
            return true;
        }

        private TmxTile GetTile(int x, int y, out bool flipDiagonal, out bool flipHorizontal, out bool flipVertical)
        {
            int tileIndex = Mesh.Layer.GetTileIndex(x, y);
            uint tileId = Mesh.GetTileIdAt(tileIndex);

            // Skip blank tiles
            if (tileId == 0)
            {
                flipDiagonal = flipHorizontal = flipVertical = false;
                return null;
            }
            var tile = Mesh.Layer.Map.Tiles[TmxMath.GetTileIdWithoutFlags(tileId)];
            flipDiagonal = TmxMath.IsTileFlippedDiagonally(tileId);
            flipHorizontal = TmxMath.IsTileFlippedHorizontally(tileId);
            flipVertical = TmxMath.IsTileFlippedVertically(tileId);

            return tile;
        }

        private TmxTile GetTile(Point point, out bool flipDiagonal, out bool flipHorizontal, out bool flipVertical)
        {
            return GetTile(point.X, point.Y, out flipDiagonal, out flipHorizontal, out flipVertical);
        }

        private PointF[] CalculateFaceVertices(Rectangle rect, Size tileSize, int mapTileHeight, PointF offset)
        {
            var mapLocation = rect.Location;

            // Location on map is complicated by tiles that are 'higher' than the tile size given for the overall map
            mapLocation.Offset(0, -tileSize.Height + mapTileHeight);

            PointF pt0 = mapLocation;
            PointF pt1 = PointF.Add(mapLocation, new Size((rect.Width + 1) * tileSize.Width, 0));
            PointF pt2 = PointF.Add(mapLocation, new Size((rect.Width + 1) * tileSize.Width, (rect.Height + 1) * tileSize.Height));
            PointF pt3 = PointF.Add(mapLocation, new Size(0, (rect.Height + 1) * tileSize.Height));

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

        private PointF[] CalculateFaceVertices(Point mapLocation, Size tileSize, int mapTileHeight, PointF offset)
        {
            return CalculateFaceVertices(new Rectangle(mapLocation, Size.Empty), tileSize, mapTileHeight, offset);
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

        private PointF[] CalculateFaceTextureCoordinatesForSingleColorHeuristic(Rectangle rect, TmxTile tile)
        {
            var tileSize = tile.TileSize;
            var imageLocation = tile.LocationOnSource;

            // Why do we offset the image location like that?
            // Because of floating point rounding errors. If we don't
            // offset the image location, we might end up sampling the wrong
            // color off of the image. So just to be on the safe side, we sample
            // the center of the tile.
            imageLocation.Offset(tileSize.Width / 2, tileSize.Height / 2);

            var imageSize = tile.TmxImage.Size;

            var points = new PointF[4];
            points[3] = TiledMapExporter.PointToTextureCoordinate(imageLocation, imageSize);
            points[2] = TiledMapExporter.PointToTextureCoordinate(imageLocation, imageSize);
            points[1] = TiledMapExporter.PointToTextureCoordinate(imageLocation, imageSize);
            points[0] = TiledMapExporter.PointToTextureCoordinate(imageLocation, imageSize);

            //var coordinates = new PointF[4];
            //coordinates[3] = TiledMapExporter.PointToTextureCoordinate(points[0], imageSize);
            //coordinates[2] = TiledMapExporter.PointToTextureCoordinate(points[1], imageSize);
            //coordinates[1] = TiledMapExporter.PointToTextureCoordinate(points[2], imageSize);
            //coordinates[0] = TiledMapExporter.PointToTextureCoordinate(points[3], imageSize);

            return points;
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
