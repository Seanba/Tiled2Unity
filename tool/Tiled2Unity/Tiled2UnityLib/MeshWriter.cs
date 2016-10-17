using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public class MeshWriter
    {
        #region Datamembers

        /// <summary>
        /// A reference to the string builder.
        /// </summary>
        public StringBuilder Builder = null;

        /// <summary>
        /// A reference to the position database.
        /// </summary>
        public HashIndexOf<Vertex3> PositionDatabase = null;

        /// <summary>
        /// A reference to the texture coordinate database.
        /// </summary>
        public HashIndexOf<PointF> TexcoordDatabase = null;

        /// <summary>
        /// A reference to the mesh that will be written to the builder.
        /// </summary>
        public TmxMesh Mesh = null;

        /// <summary>
        /// Subscribe to this event to be notified of progress.
        /// This event will be triggered on every new row of the layer
        /// that is being worked on.
        /// </summary>
        public event ProgressChangedEventHandler ProgressChanged;

        private BitPlane mTileUsed = null;

        #endregion // Datamembers

        #region Public methods

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
        /// Execute the MeshWriter. This method may throw various exceptions so be sure to catch them.
        /// </summary>
        public void Execute()
        {
            if (Mesh == null || 
                Builder == null ||
                PositionDatabase == null ||
                TexcoordDatabase == null)
            {
                // Nothing to do.
                return;
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
                OnProgressChanged(y / layer.Height);
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
        }

        #endregion // Public methods

        #region Helper methods

        protected void OnProgressChanged(int percentage)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(this, new ProgressChangedEventArgs(percentage, null));
            }
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
            else if (flipDiagonal || flipHorizontal || flipVertical || 
                Mesh.Layer.Map.Orientation != TmxMap.MapOrientation.Orthogonal)
            {
                // The tile has a non-trivial rotation matrix, or the map is not orthogonal,
                // so we're not gonna do our optimizations. Export this as a single tile.
                var position = Mesh.Layer.Map.GetMapPositionAt(x, y);
                var pos2 = CalculateFaceVertices(position, tile.TileSize, Mesh.Layer.Map.TileHeight, tile.Offset);

                positions = new FaceVertices { Vertices = pos2, Depth_z = GetDepthValueAt(position) };
                texcoords = CalculateFaceTextureCoordinates(tile, flipDiagonal, flipHorizontal, flipVertical);

                return true;
            }
            else
            {
                // No null tile, unflipped in any possible direction.
                // Here's the point where we do the optimization.
                var singleColorRect = DoSingleColorHeuristic(x, y);
                var stackingRect = DoStackingHeuristic(x, y);
                if (singleColorRect.GetClosedArea() < stackingRect.GetClosedArea())
                {
                    // Take the stacking rect route
                    CommitStackingHeuristic(x, y, tile, stackingRect, out positions, out texcoords);
                }
                else // singleColorRect.GetArea() >= stackingRect.GetArea() 
                {
                    // Take the single color rect route
                    // NOTE: If both rects have area 0 (i.e. represent a single tile),
                    // then both optimizations failed. In that case the method below just
                    // emits the usual single tile quad.
                    CommitSingleColorHeuristic(x, y, tile, singleColorRect, out positions, out texcoords);
                }
                return true;
            }
        }

        private float GetDepthValueAt(Point mapPosition)
        {
            // If we're using depth shaders then we'll need to set a depth value of this face
            if (Tiled2Unity.Settings.DepthBufferEnabled)
            {
                return mapPosition.Y / Mesh.Layer.Map.MapSizeInPixels().Height * -1.0f;
            }
            else
            {
                return 0.0f;
            }
        }

        private void CommitSingleColorHeuristic(int x, int y, TmxTile tile, Rectangle singleColorRect, out FaceVertices positions, out PointF[] texcoords)
        {
            var position = Mesh.Layer.Map.GetMapPositionAt(x, y);
            mTileUsed.Set(singleColorRect, true);
            singleColorRect.Location = position;
            var pos2 = CalculateFaceVertices(singleColorRect, tile.TileSize, Mesh.Layer.Map.TileHeight, tile.Offset);
            positions = new FaceVertices { Vertices = pos2, Depth_z = GetDepthValueAt(position) };
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
        }

        private void CommitStackingHeuristic(int x, int y, TmxTile tile, Rectangle stackingRect, out FaceVertices positions, out PointF[] texcoords)
        {
            var position = Mesh.Layer.Map.GetMapPositionAt(x, y);
            mTileUsed.Set(stackingRect, true);
            var oldPos = stackingRect.Location;
            stackingRect.Location = position;
            var pos2 = CalculateFaceVertices(stackingRect, tile.TileSize, Mesh.Layer.Map.TileHeight, tile.Offset);
            positions = new FaceVertices { Vertices = pos2, Depth_z = GetDepthValueAt(position) };
            if (stackingRect.Size == Size.Empty)
            {
                // We didn't find any larger quad.
                texcoords = CalculateFaceTextureCoordinates(tile, false, false, false);
            }
            else
            {
                // We found a larger quad.
                stackingRect.Location = oldPos;
                texcoords = CalculateFaceTextureCoordinatesForStackingHeuristic(stackingRect, tile);
            }
        }

        private PointF[] CalculateFaceTextureCoordinatesForStackingHeuristic(Rectangle stackingRect, TmxTile tile)
        {
            bool flip0, flip1, flip2;
            var points0 = CalculateFaceTextureCoordinatesInLayerSpace(GetTile(stackingRect.Left, stackingRect.Top, out flip0, out flip1, out flip2));
            var points1 = CalculateFaceTextureCoordinatesInLayerSpace(GetTile(stackingRect.Right, stackingRect.Top, out flip0, out flip1, out flip2));
            var points2 = CalculateFaceTextureCoordinatesInLayerSpace(GetTile(stackingRect.Right, stackingRect.Bottom, out flip0, out flip1, out flip2));
            var points3 = CalculateFaceTextureCoordinatesInLayerSpace(GetTile(stackingRect.Left, stackingRect.Bottom, out flip0, out flip1, out flip2));

            var points = new PointF[] { points0[0], points1[1], points2[2], points3[3] };
            var coordinates = new PointF[4];
            Size imageSize = GetTile(stackingRect.Left, stackingRect.Top, out flip0, out flip1, out flip2).TmxImage.Size;
            coordinates[3] = TiledMapExporter.PointToTextureCoordinate(points[0], imageSize);
            coordinates[2] = TiledMapExporter.PointToTextureCoordinate(points[1], imageSize);
            coordinates[1] = TiledMapExporter.PointToTextureCoordinate(points[2], imageSize);
            coordinates[0] = TiledMapExporter.PointToTextureCoordinate(points[3], imageSize);

            // Apply a small bias to the "inner" edges of the texels
            // This keeps us from seeing seams
            if (Tiled2Unity.Settings.TexelBias > 0)
            {
                float bias = 1.0f / Tiled2Unity.Settings.TexelBias;

                PointF[] multiply = new PointF[4];
                multiply[0] = new PointF(1, 1);
                multiply[1] = new PointF(-1, 1);
                multiply[2] = new PointF(-1, -1);
                multiply[3] = new PointF(1, -1);

                // This nudge has to be transformed too
                TmxMath.TransformPoints_DiagFirst(multiply, Point.Empty, false, false, false);

                coordinates[0] = TmxMath.AddPoints(coordinates[0], TmxMath.ScalePoints(multiply[0], bias));
                coordinates[1] = TmxMath.AddPoints(coordinates[1], TmxMath.ScalePoints(multiply[1], bias));
                coordinates[2] = TmxMath.AddPoints(coordinates[2], TmxMath.ScalePoints(multiply[2], bias));
                coordinates[3] = TmxMath.AddPoints(coordinates[3], TmxMath.ScalePoints(multiply[3], bias));
            }

            return coordinates;
        }

        private Rectangle DoStackingHeuristic(int x, int y)
        {
            var rect = new Rectangle(x, y, 0, 0);
            DoStackingHeuristic(ref rect);
            return rect;
        }

        private void DoStackingHeuristic(ref Rectangle rect)
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

            // Here's the big loop that attempts to enlarge the rectangle.
            while (continueInHorDir || continueInVertDir)
            {
                if (continueInHorDir)
                {
                    continueInHorDir = CheckFrontierForStacking(ref rect, horDir == 1 ? rect.GetRightFrontier() : rect.GetLeftFrontier());
                }
                if (continueInVertDir)
                {
                    continueInVertDir = CheckFrontierForStacking(ref rect, verDir == 1 ? rect.GetBottomFrontier() : rect.GetTopFrontier());
                }
                if (continueInHorDir && continueInVertDir)
                {
                    // We're gonna check that one diagonal tile in the corner of the two frontiers.
                    if (!CheckForStackingDiagonalCorner(rect))
                    {
                        // Here we're making a choice. Do we continue
                        // in the horizontal direection, or continue
                        // in the vertical direction? What is wise?
                        // As it stands, we choose to continue in the
                        // vertical direction. It might be good to refactor
                        // this into an option that can be set by the user
                        // in the GUI or console app.
                        continueInVertDir = true;
                        continueInHorDir = false;
                        if (verDir == 1) RectangleUtils.EnlargeBottom(ref rect);
                        else RectangleUtils.EnlargeTop(ref rect);
                    }
                    else
                    {
                        if (horDir == 1) RectangleUtils.EnlargeRight(ref rect);
                        else RectangleUtils.EnlargeLeft(ref rect);
                        if (verDir == 1) RectangleUtils.EnlargeBottom(ref rect);
                        else RectangleUtils.EnlargeTop(ref rect);
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

        private bool CheckForStackingDiagonalCorner(Rectangle rect)
        {
            bool flip0, flip1, flip2;
            int hor = Mesh.Layer.Map.DrawOrderHorizontal;
            int ver = Mesh.Layer.Map.DrawOrderVertical;
            // We're gonna check that one diagonal tile in the corner of the two frontiers.
            int x = hor == 1 ? rect.Right + 1 : rect.Left - 1;
            int y = ver == 1 ? rect.Bottom + 1 : rect.Top - 1;
            //int xh = hor == 1 ? x + 1 : x - 1;
            int xh = x - hor;
            int yv = y - ver;
            int yh = y;
            int xv = x;
            //int yv = ver == 1 ? y - 1 : y + 1;



            var diagTile = GetTile(x, y, out flip0, out flip1, out flip2);
            if (x < 0 || y < 0 ||  x >= mTileUsed.Width || y >= mTileUsed.Height || mTileUsed.Get(x, y)) return false;

            if (diagTile == null || flip0 || flip1 || flip2) return false;

            var horTile = GetTile(xh, yh, out flip0, out flip1, out flip2);
            if (xh < 0 || yh < 0 || xh >= mTileUsed.Width || yh >= mTileUsed.Height || mTileUsed.Get(xh, yh)) return false;
            if (horTile == null || flip0 || flip1 || flip2) return false;

            var verTile = GetTile(xv, yv, out flip0, out flip1, out flip2);
            if (xv < 0 || yv < 0 || xv >= mTileUsed.Width || yv >= mTileUsed.Height || mTileUsed.Get(xv, yv)) return false;
            if (verTile == null || flip0 || flip1 || flip2) return false;

            var diagTexcoords = CalculateFaceTextureCoordinatesInLayerSpace(diagTile);
            var horTexcoords = CalculateFaceTextureCoordinatesInLayerSpace(horTile);
            var verTexcoords = CalculateFaceTextureCoordinatesInLayerSpace(verTile);

            if (hor == 1)
            {
                if (ver == 1)
                {
                    // bottom right
                    if (diagTexcoords[0] != horTexcoords[1] || diagTexcoords[3] != horTexcoords[2]) return false;
                    if (diagTexcoords[0] != verTexcoords[3] || diagTexcoords[1] != verTexcoords[2]) return false;
                }
                else // ver == -1
                {
                    // top right
                    if (diagTexcoords[3] != horTexcoords[2] || diagTexcoords[0] != horTexcoords[1]) return false;
                    if (diagTexcoords[3] != verTexcoords[0] || diagTexcoords[2] != verTexcoords[1]) return false;

                }
            }
            else // hor == -1
            {
                if (ver == 1)
                {
                    // bottom left
                    if (diagTexcoords[1] != horTexcoords[0] || diagTexcoords[2] != horTexcoords[3]) return false;
                    if (diagTexcoords[0] != verTexcoords[3] || diagTexcoords[1] != verTexcoords[2]) return false;
                }
                else // ver == -1
                {
                    // top left
                    if (diagTexcoords[2] != horTexcoords[3] || diagTexcoords[1] != horTexcoords[0]) return false;
                    if (diagTexcoords[3] != verTexcoords[0] || diagTexcoords[2] != verTexcoords[1]) return false;
                }
            }

            return true;
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
                    // We're gonna check that one diagonal tile in the corner of the two frontiers.

                    int x = horDir == 1 ? rect.Right + 1 : rect.Left - 1;
                    int y = verDir == 1 ? rect.Bottom + 1 : rect.Top - 1;

                    // Wether a tile is flipped should really be part of the
                    // tile itself. It's tiresome to constantly pass those bool
                    // references along. Make a distinction between "Layer tiles"
                    // and "Tileset tiles". "Tileset tiles" contain a reference
                    // to their image and their location etc, while a "Layer tile"
                    // simply knows its position in the layer, has three bool fields
                    // for the flips and has a reference to a "Tileset tile".
                    var diagTile = GetTile(x, y, out flip0, out flip1, out flip2);
                    if (x >= mTileUsed.Width || y >= mTileUsed.Height || mTileUsed.Get(x, y) || 
                        diagTile == null || flip0 || flip1 || flip2 || 
                        diagTile.IsSingleColor == false || diagTile.TopLeftColor != startColor)
                    {
                        // Here we're making a choice. Do we continue
                        // in the horizontal direection, or continue
                        // in the vertical direction? What is wise?
                        // As it stands, we choose to continue in the
                        // vertical direction. It might be good to refactor
                        // this into an option that can be set by the user
                        // in the GUI or console app.
                        continueInVertDir = true;
                        continueInHorDir = false;
                        if (verDir == 1) RectangleUtils.EnlargeBottom(ref rect);
                        else RectangleUtils.EnlargeTop(ref rect);
                    }
                    else
                    {
                        // All this checking should really be implemented
                        // in terms of a class hierarchy; have an abstract
                        // base class called MeshWriter and derive classes
                        // called RightUpMeshWriter, RightDownMeshWriter, etc.
                        if (horDir == 1) RectangleUtils.EnlargeRight(ref rect);
                        else RectangleUtils.EnlargeLeft(ref rect);
                        if (verDir == 1) RectangleUtils.EnlargeBottom(ref rect);
                        else RectangleUtils.EnlargeTop(ref rect);
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

        private bool CheckFrontierForStacking(ref Rectangle rect, RectangleFrontierHorizontalProxy frontier)
        {
            PointF[] currTexCoords = null;
            PointF[] prevTexCoords = null;
            PointF[] neighborTexCoords = null;
            bool flip0, flip1, flip2;
            foreach (var pt in frontier)
            {
                if (pt.X >= mTileUsed.Width || pt.Y >= mTileUsed.Height || pt.X < 0 || pt.Y < 0) return false;
                if (mTileUsed.Get(pt.X, pt.Y)) return false;
                var currTile = GetTile(pt, out flip0, out flip1, out flip2);
                if (currTile == null || flip0 || flip1 || flip2) return false;
                prevTexCoords = currTexCoords;
                currTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(currTile);
                if (prevTexCoords != null)
                {
                    if (prevTexCoords[1] != currTexCoords[0] || prevTexCoords[2] != currTexCoords[3])
                    {
                        return false;
                    }
                }
                if (Mesh.Layer.Map.DrawOrderVertical == 1)
                {
                    var neighborTile = GetTile(pt.X, pt.Y - 1, out flip0, out flip1, out flip2);
                    if (neighborTile == null || flip0 || flip1 || flip2) return false;
                    neighborTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(neighborTile);
                    if (neighborTexCoords[3] != currTexCoords[0] || neighborTexCoords[2] != currTexCoords[1])
                    {
                        return false;
                    }
                }
                else // Mesh.Layer.Map.DrawOrderVertical == -1
                {
                    var neighborTile = GetTile(pt.X, pt.Y + 1, out flip0, out flip1, out flip2);
                    if (neighborTile == null || flip0 || flip1 || flip2) return false;
                    neighborTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(neighborTile);
                    if (neighborTexCoords[0] != currTexCoords[3] || neighborTexCoords[1] != currTexCoords[2])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool CheckFrontierForStacking(ref Rectangle rect, RectangleFrontierVerticalProxy frontier)
        {
            PointF[] currTexCoords = null;
            PointF[] prevTexCoords = null;
            PointF[] neighborTexCoords = null;
            bool flip0, flip1, flip2;
            foreach (var pt in frontier)
            {
                if (pt.X >= mTileUsed.Width || pt.Y >= mTileUsed.Height || pt.X < 0 || pt.Y < 0) return false;
                if (mTileUsed.Get(pt.X, pt.Y)) return false;
                var currTile = GetTile(pt, out flip0, out flip1, out flip2);
                if (currTile == null || flip0 || flip1 || flip2) return false;
                prevTexCoords = currTexCoords;
                currTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(currTile);
                if (prevTexCoords != null)
                {
                    if (prevTexCoords[1] != currTexCoords[0] || prevTexCoords[2] != currTexCoords[3])
                    {
                        return false;
                    }
                }
                if (Mesh.Layer.Map.DrawOrderHorizontal == 1)
                {
                    var neighborTile = GetTile(pt.X - 1, pt.Y, out flip0, out flip1, out flip2);
                    if (neighborTile == null || flip0 || flip1 || flip2) return false;
                    neighborTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(neighborTile);
                    if (neighborTexCoords[1] != currTexCoords[0] || neighborTexCoords[2] != currTexCoords[3])
                    {
                        return false;
                    }
                }
                else // Mesh.Layer.Map.DrawOrderHorizontal == -1
                {
                    var neighborTile = GetTile(pt.X + 1, pt.Y, out flip0, out flip1, out flip2);
                    if (neighborTile == null || flip0 || flip1 || flip2) return false;
                    neighborTexCoords = CalculateFaceTextureCoordinatesInLayerSpace(neighborTile);
                    if (neighborTexCoords[0] != currTexCoords[1] || neighborTexCoords[3] != currTexCoords[2])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool CheckFrontierForSameColor(Color startColor, RectangleFrontierProxy frontier)
        {
            bool flip0, flip1, flip2;
            foreach (var pt in frontier)
            {
                if (pt.X >= mTileUsed.Width || pt.Y >= mTileUsed.Height || pt.X < 0 || pt.Y < 0) return false;
                if (mTileUsed.Get(pt.X, pt.Y)) return false;
                var tile = GetTile(pt, out flip0, out flip1, out flip2);
                if (tile == null || flip0 || flip1 || flip2 || tile.IsSingleColor == false ||
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
            PointF[] vertices = new PointF[4];
            var mapLocation = rect.Location;
            var width = new Size(tileSize.Width * (rect.Width + 1), 0);
            var height = new Size(0, tileSize.Height * (rect.Height + 1));
            var upOffset = new Point(0, -rect.Height * tileSize.Height);
            var leftOffset = new Point(-rect.Width * tileSize.Width, 0);

            // Location on map is complicated by tiles that are 'higher' than the tile size
            // given for the overall map.
            mapLocation.Offset(0, -tileSize.Height + mapTileHeight);

            vertices[3] = mapLocation;
            vertices[2] = PointF.Add(vertices[3], width);
            vertices[1] = PointF.Add(vertices[3], Size.Add(width, height));
            vertices[0] = PointF.Add(vertices[3], height);

            // Apply the tile offset.
            for (int i = 0; i < 4; ++i) vertices[i] = TmxMath.AddPoints(vertices[i], offset);

            // Need to account for draw order.
            if (Mesh.Layer.Map.DrawOrderVertical == -1)
            {
                for (int i = 0; i < 4; ++i) vertices[i] = TmxMath.AddPoints(vertices[i], upOffset);
            }
            if (Mesh.Layer.Map.DrawOrderHorizontal == -1)
            {
                for (int i = 0; i < 4; ++i) vertices[i] = TmxMath.AddPoints(vertices[i], leftOffset);
            }

            // Move vertices from grid space, to ... grid space, but account for negative zero and whatnot.
            for (int i = 0; i < 4; ++i) vertices[i] = TiledMapExporter.PointFToObjVertex(vertices[i]);
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
            return points;
        }

        private PointF[] CalculateFaceTextureCoordinatesInLayerSpace(TmxTile tile)
        {
            Point imageLocation = tile.LocationOnSource;
            Size tileSize = tile.TileSize;

            var points = new PointF[4];
            points[0] = imageLocation;
            points[1] = PointF.Add(imageLocation, new Size(tileSize.Width, 0));
            points[2] = PointF.Add(imageLocation, tileSize);
            points[3] = PointF.Add(imageLocation, new Size(0, tileSize.Height));

            return points;
        }

        private PointF[] CalculateFaceTextureCoordinatesInLayerSpace(TmxTile tile, bool flipDiagonal, bool flipHorizontal, bool flipVertical)
        {
            Point imageLocation = tile.LocationOnSource;
            Size tileSize = tile.TileSize;
            var points = CalculateFaceTextureCoordinatesInLayerSpace(tile);
            PointF center = new PointF(tileSize.Width * 0.5f, tileSize.Height * 0.5f);
            center.X += imageLocation.X;
            center.Y += imageLocation.Y;
            TmxMath.TransformPoints_DiagFirst(points, center, flipDiagonal, flipHorizontal, flipVertical);
            return points;
        }

        private PointF[] CalculateFaceTextureCoordinates(TmxTile tmxTile, bool flipDiagonal, bool flipHorizontal, bool flipVertical)
        {
            var points = CalculateFaceTextureCoordinatesInLayerSpace(tmxTile, flipDiagonal, flipHorizontal, flipVertical);
            var coordinates = new PointF[4];
            Size imageSize = tmxTile.TmxImage.Size;
            coordinates[3] = TiledMapExporter.PointToTextureCoordinate(points[0], imageSize);
            coordinates[2] = TiledMapExporter.PointToTextureCoordinate(points[1], imageSize);
            coordinates[1] = TiledMapExporter.PointToTextureCoordinate(points[2], imageSize);
            coordinates[0] = TiledMapExporter.PointToTextureCoordinate(points[3], imageSize);

            // Apply a small bias to the "inner" edges of the texels
            // This keeps us from seeing seams
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

        #endregion // Helper methods
    }
}
