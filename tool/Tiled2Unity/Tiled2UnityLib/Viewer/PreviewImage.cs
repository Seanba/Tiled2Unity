using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using System.Linq;
using System.Text;


namespace Tiled2Unity.Viewer
{
    public class PreviewImage
    {
        private static readonly float GridSize = 3.0f;
        private static readonly int MaxPreviewTilesWide = 256;
        private static readonly int MaxPreviewTilesHigh = 256;

        public static Bitmap CreateBitmap(TmxMap tmxMap, float scale)
        {
            SummaryReport report = new SummaryReport();
            report.Capture("Previewing");

            // What is the boundary of the bitmap we are creating?
            RectangleF bounds = CalculateBoundary(tmxMap);
            Bitmap bitmap = null;

            try
            {
                int width = (int)Math.Ceiling(bounds.Width * scale) + 1;
                int height = (int)Math.Ceiling(bounds.Height * scale) + 1;
                bitmap = TmxHelper.CreateBitmap32bpp(width, height);
            }
            catch (System.ArgumentException)
            {
                StringBuilder warning = new StringBuilder();
                warning.AppendFormat("Map cannot be previewed at '{0}' scale. Try a smaller scale.\n", scale);
                warning.AppendLine("Image will be constructed on a 1024x1024 canvas. Parts of your map may be cropped.");
                Logger.WriteWarning(warning.ToString());

                bitmap = TmxHelper.CreateBitmap32bpp(1024, 1024);
            }

            using (Pen pen = new Pen(Color.Black, 1.0f))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
#if !TILED2UNITY_MAC
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
#endif

                g.ScaleTransform(scale, scale);

                g.FillRectangle(Brushes.WhiteSmoke, 0, 0, bounds.Width, bounds.Height);
                g.DrawRectangle(pen, 1, 1, bounds.Width - 1, bounds.Height - 1);

                g.TranslateTransform(-bounds.X, -bounds.Y);
                DrawBackground(g, tmxMap);
                DrawGrid(g, tmxMap, scale);

                DrawAllTilesInLayerNodes(g, tmxMap, tmxMap.LayerNodes);
                DrawAllCollidersInLayerNodes(g, tmxMap, tmxMap.LayerNodes, scale);
            }

            report.Report();
            return bitmap;
        }

        private static RectangleF CalculateBoundary(TmxMap tmxMap)
        {
            RectangleF rcMap = new RectangleF(Point.Empty, tmxMap.MapSizeInPixels);

            // Any tile layers in the map can be offset
            var tileLayerBounds = from layer in tmxMap.EnumerateTileLayers()
                                  where layer.Visible == true
                                  select new RectangleF(layer.GetCombinedOffset(), rcMap.Size);

            // Take boundaries from object groups
            var objBounds = from g in tmxMap.EnumerateObjectLayers()
                            from o in g.Objects
                            where o.Visible == true
                            select o.GetOffsetWorldBounds();

            // Take boundaries from objects embedded in tiles
            var tileBounds = from layer in tmxMap.EnumerateTileLayers()
                             where layer.Visible == true
                             from y in Enumerable.Range(0, layer.Height)
                             from x in Enumerable.Range(0, layer.Width)
                             let tileId = layer.GetTileIdAt(x, y)
                             where tileId != 0
                             let tile = tmxMap.Tiles[tileId]
                             from o in tile.ObjectGroup.Objects
                             let bound = o.GetOffsetWorldBounds()
                             let point = TmxMath.TileCornerInScreenCoordinates(tmxMap, x, y)
                             select new RectangleF(bound.X + point.X, bound.Y + point.Y, bound.Width, bound.Height);

            var allBounds = tileLayerBounds.Concat(objBounds).Concat(tileBounds);
            var union = allBounds.Aggregate(rcMap, RectangleF.Union);

            // Inflate a tile size to make room for the grid
            union.Inflate(tmxMap.TileWidth, tmxMap.TileHeight);
            union.Inflate(PreviewImage.GridSize, PreviewImage.GridSize);

            return union;
        }


        private static void DrawBackground(Graphics g, TmxMap tmxMap)
        {
            // Draw the background for the map
            // A full white background is preferred because of the colliders we draw on the top of the layers
            Size size = tmxMap.MapSizeInPixels;
            Rectangle rect = new Rectangle(Point.Empty, size);
            g.FillRectangle(Brushes.White, rect);
        }

        private static void DrawGrid(Graphics g, TmxMap tmxMap, float scale)
        {
            if (tmxMap.Orientation == TmxMap.MapOrientation.Hexagonal)
            {
                DrawGridHex(g, tmxMap);
            }
            else
            {
                DrawGridQuad(g, tmxMap, scale);
            }
        }

        private static void DrawGridQuad(Graphics g, TmxMap tmxMap, float scale)
        {
            HashSet<Point> points = new HashSet<Point>();
            for (int x = 0; x < GetMaxTilesWide(tmxMap); ++x)
            {
                for (int y = 0; y < GetMaxTilesHigh(tmxMap); ++y)
                {
                    // Add the "top-left" corner of a tile
                    points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y));

                    // Add all other corners of the tile to our list of grid points
                    // This is complicated by different map types (espcially staggered isometric)
                    if (tmxMap.Orientation == TmxMap.MapOrientation.Orthogonal || tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x + 1, y));
                        points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x + 1, y + 1));
                        points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 1));
                    }
                    else if (tmxMap.Orientation == TmxMap.MapOrientation.Staggered)
                    {
                        bool sx = TmxMath.DoStaggerX(tmxMap, x);
                        bool sy = TmxMath.DoStaggerY(tmxMap, y);

                        if (sx)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x + 1, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x - 1, y + 1));
                        }
                        else if (sy)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x + 1, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 2));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 1));
                        }
                        else if (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x + 1, y));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x - 1, y));
                        }
                        else if (tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x, y + 2));
                            points.Add(TmxMath.TileCornerInGridCoordinates(tmxMap, x - 1, y + 1));
                        }
                    }
                }
            }

            // Can take for granted that background is always white
            float invScale = 1.0f / scale;
            List<RectangleF> rectangles = new List<RectangleF>(points.Count);
            foreach (var p in points)
            {
                RectangleF rc = new RectangleF(p.X, p.Y, PreviewImage.GridSize * invScale, PreviewImage.GridSize * invScale);
                rc.Offset(-PreviewImage.GridSize * 0.5f * invScale, -PreviewImage.GridSize * 0.5f * invScale);
                rectangles.Add(rc);
            }

            using (Pen pen = new Pen(Brushes.Black, invScale))
            {
                g.DrawRectangles(pen, rectangles.ToArray());
            }
        }

        private static void DrawGridHex(Graphics g, TmxMap tmxMap)
        {
            // Our collection of points to render
            HashSet<Point> points = new HashSet<Point>();

            // Note: borrowed heavily from Tiled source (HexagonalRenderer::drawGrid)
            int tileWidth = tmxMap.TileWidth & ~1;
            int tileHeight = tmxMap.TileHeight & ~1;

            int sideLengthX = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X ? tmxMap.HexSideLength : 0;
            int sideLengthY = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y ? tmxMap.HexSideLength : 0;

            int sideOffsetX = (tmxMap.TileWidth - sideLengthX) / 2;
            int sideOffsetY = (tmxMap.TileHeight - sideLengthY) / 2;

            int columnWidth = sideOffsetX + sideLengthX;
            int rowHeight = sideOffsetY + sideLengthY;

            bool staggerX = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X;

            // Determine the tile and pixel coordinates to start at
            Point startTile = new Point(0, 0);
            Point startPos = TmxMath.TileCornerInScreenCoordinates(tmxMap, startTile.X, startTile.Y);

            Point[] oct = new Point[8]
            {
                new Point(0,                            tileHeight - sideOffsetY),
                new Point(0,                            sideOffsetY),
                new Point(sideOffsetX,                  0),
                new Point(tileWidth - sideOffsetX,      0),
                new Point(tileWidth,                    sideOffsetY),
                new Point(tileWidth,                    tileHeight - sideOffsetY),
                new Point(tileWidth - sideOffsetX,      tileHeight),
                new Point(sideOffsetX,                  tileHeight)
            };

            if (staggerX)
            {
                // Odd row shifting is applied in the rendering loop, so un-apply it here
                if (TmxMath.DoStaggerX(tmxMap, startTile.X))
                {
                    startPos.Y -= rowHeight;
                }

                for (; startTile.X < GetMaxTilesWide(tmxMap); startTile.X++)
                {
                    Point rowTile = startTile;
                    Point rowPos = startPos;

                    if (TmxMath.DoStaggerX(tmxMap, startTile.X))
                    {
                        rowPos.Y += rowHeight;
                    }

                    for (; rowTile.Y < GetMaxTilesHigh(tmxMap); rowTile.Y++)
                    {
                        points.Add(TmxMath.AddPoints(rowPos, oct[1]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[2]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[3]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[4]));

                        bool isStaggered = TmxMath.DoStaggerX(tmxMap, startTile.X);
                        bool lastRow = rowTile.Y == tmxMap.Height - 1;
                        bool lastColumn = rowTile.X == tmxMap.Width - 1;
                        bool bottomLeft = rowTile.X == 0 || (lastRow && isStaggered);
                        bool bottomRight = lastColumn || (lastRow && isStaggered);

                        if (bottomRight)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[5]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[6]));
                        }
                        if (lastRow)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[6]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[7]));
                        }
                        if (bottomLeft)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[7]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[0]));
                        }

                        rowPos.Y += tileHeight + sideLengthY;
                    }

                    startPos.X += columnWidth;
                }
            }
            else
            {
                // Odd row shifting is applied in the rendering loop, so un-apply it here
                if (TmxMath.DoStaggerY(tmxMap, startTile.Y))
                {
                    startPos.X -= columnWidth;
                }

                for (; startTile.Y < tmxMap.Height; startTile.Y++)
                {
                    Point rowTile = startTile;
                    Point rowPos = startPos;

                    if (TmxMath.DoStaggerY(tmxMap, startTile.Y))
                    {
                        rowPos.X += columnWidth;
                    }

                    for (; rowTile.X < tmxMap.Width; rowTile.X++)
                    {
                        points.Add(TmxMath.AddPoints(rowPos, oct[0]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[1]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[2]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[3]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[4]));


                        bool isStaggered = TmxMath.DoStaggerY(tmxMap, startTile.Y);
                        bool lastRow = rowTile.Y == tmxMap.Height - 1;
                        bool lastColumn = rowTile.Y == tmxMap.Width - 1;
                        bool bottomLeft = lastRow || (rowTile.X == 0 && !isStaggered);
                        bool bottomRight = lastRow || (lastColumn && isStaggered);

                        if (lastColumn)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[4]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[5]));
                        }
                        if (bottomRight)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[5]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[6]));
                        }
                        if (bottomLeft)
                        {
                            points.Add(TmxMath.AddPoints(rowPos, oct[7]));
                            points.Add(TmxMath.AddPoints(rowPos, oct[0]));
                        }

                        rowPos.X += tileWidth + sideLengthX;
                    }

                    startPos.Y += rowHeight;
                }
            }

            foreach (var p in points)
            {
                RectangleF rc = new RectangleF(p.X, p.Y, PreviewImage.GridSize, PreviewImage.GridSize);
                rc.Offset(-PreviewImage.GridSize * 0.5f, -PreviewImage.GridSize * 0.5f);

                g.DrawRectangle(Pens.Black, rc.X, rc.Y, rc.Width, rc.Height);
            }
        }

        private static void DrawAllTilesInLayerNodes(Graphics g, TmxMap tmxMap, List<TmxLayerNode> layerNodes)
        {
            foreach (var node in layerNodes)
            {
                if (node.Visible == false)
                    continue;

                if (node.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                // Translate by the offset
                GraphicsState state = g.Save();
                g.TranslateTransform(node.Offset.X, node.Offset.Y);

                if (node.GetType() == typeof(TmxLayer))
                {
                    DrawTilesInTileLayer(g, tmxMap, node as TmxLayer);
                }
                else if (node.GetType() == typeof(TmxObjectGroup))
                {
                    DrawTilesInObjectGroup(g, tmxMap, node as TmxObjectGroup);
                }
                else if (node.GetType() == typeof(TmxGroupLayer))
                {
                    DrawAllTilesInLayerNodes(g, tmxMap, node.LayerNodes);
                }

                g.Restore(state);
            }
        }

        private static void DrawTilesInTileLayer(Graphics g, TmxMap tmxMap, TmxLayer layer)
        {
            // Set the opacity for the layer (Not supported on Mac builds)
#if !TILED2UNITY_MAC
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = layer.Opacity;

            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
#endif
            // The range of x and y depends on the render order of the tiles
            // By default we draw right and down but may reverse the tiles we visit
            var range_x = Enumerable.Range(0, GetMaxTilesWide(layer));
            var range_y = Enumerable.Range(0, GetMaxTilesHigh(layer));

            if (tmxMap.DrawOrderHorizontal == -1)
                range_x = range_x.Reverse();

            if (tmxMap.DrawOrderVertical == -1)
                range_y = range_y.Reverse();

            // Visit the tiles we are going to draw
            var tiles = from y in range_y
                        from x in range_x
                        let rawTileId = layer.GetRawTileIdAt(x, y)
                        let tileId = layer.GetTileIdAt(x, y)
                        where tileId != 0

                        let tile = tmxMap.Tiles[tileId]

                        // Support for animated tiles. Just show the first frame of the animation.
                        let frame = tmxMap.Tiles[tile.Animation.Frames[0].GlobalTileId]

                        select new
                        {
                            Tile = frame,
                            Position = TmxMath.TileCornerInScreenCoordinates(tmxMap, x, y),
                            Bitmap = frame.TmxImage.ImageBitmap,
                            IsFlippedDiagnoally = TmxMath.IsTileFlippedDiagonally(rawTileId),
                            IsFlippedHorizontally = TmxMath.IsTileFlippedHorizontally(rawTileId),
                            IsFlippedVertically = TmxMath.IsTileFlippedVertically(rawTileId),
                        };

            PointF[] destPoints = new PointF[4];
            PointF[] destPoints3 = new PointF[3];
            foreach (var t in tiles)
            {
                PointF location = t.Position;

                // Individual tiles may be larger than the given tile size of the overall map
                location.Y = (t.Position.Y - t.Tile.TileSize.Height) + tmxMap.TileHeight;

                // Take tile offset into account
                location.X += t.Tile.Offset.X;
                location.Y += t.Tile.Offset.Y;

                // Make up the 'quad' of texture points and transform them
                PointF center = new PointF(t.Tile.TileSize.Width * 0.5f, t.Tile.TileSize.Height * 0.5f);
                destPoints[0] = new Point(0, 0);
                destPoints[1] = new Point(t.Tile.TileSize.Width, 0);
                destPoints[2] = new Point(t.Tile.TileSize.Width, t.Tile.TileSize.Height);
                destPoints[3] = new Point(0, t.Tile.TileSize.Height);

                // Transform the points based on our flipped flags
                TmxMath.TransformPoints(destPoints, center, t.IsFlippedDiagnoally, t.IsFlippedHorizontally, t.IsFlippedVertically);

                // Put the destination points back into world space
                TmxMath.TranslatePoints(destPoints, location);

                // Stupid DrawImage function only takes 3 destination points otherwise it throws an exception
                destPoints3[0] = destPoints[0];
                destPoints3[1] = destPoints[1];
                destPoints3[2] = destPoints[3];

                // Draw the tile
                Rectangle source = new Rectangle(t.Tile.LocationOnSource, t.Tile.TileSize);
#if !TILED2UNITY_MAC
                g.DrawImage(t.Bitmap, destPoints3, source, GraphicsUnit.Pixel, imageAttributes);
#else
                g.DrawImage(t.Bitmap, destPoints3, source, GraphicsUnit.Pixel);
#endif
            }
        }

        private static void DrawTilesInObjectGroup(Graphics g, TmxMap tmxMap, TmxObjectGroup objectGroup)
        {
            // Get opacity in eventually
#if !TILED2UNITY_MAC
            ColorMatrix colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = objectGroup.Opacity;

            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
#endif

            foreach (var tmxObject in objectGroup.Objects)
            {
                if (!tmxObject.Visible)
                    continue;

                TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;
                if (tmxObjectTile == null)
                    continue;

                GraphicsState state = g.Save();
                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                g.TranslateTransform(xfPosition.X, xfPosition.Y);
                g.RotateTransform(tmxObject.Rotation);
                {
                    GraphicsState tileState = g.Save();
                    PrepareTransformForTileObject(g, tmxMap, tmxObjectTile);

                    // Draw the tile
                    Rectangle destination = new Rectangle(0, -tmxObjectTile.Tile.TileSize.Height, tmxObjectTile.Tile.TileSize.Width, tmxObjectTile.Tile.TileSize.Height);
                    Rectangle source = new Rectangle(tmxObjectTile.Tile.LocationOnSource, tmxObjectTile.Tile.TileSize);
                    //g.DrawRectangle(Pens.Black, destination);

#if !TILED2UNITY_MAC
                    g.DrawImage(tmxObjectTile.Tile.TmxImage.ImageBitmap, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, imageAttributes);
#else
                    g.DrawImage(tmxObjectTile.Tile.TmxImage.ImageBitmap, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel);
#endif

                    g.Restore(tileState);
                }
                g.Restore(state);
            }
        }

        private static void DrawAllCollidersInLayerNodes(Graphics g, TmxMap tmxMap, List<TmxLayerNode> layerNodes, float scale)
        {
            foreach (var node in layerNodes)
            {
                if (node.Visible == false)
                    continue;

                if (node.Ignore == TmxLayerNode.IgnoreSettings.True)
                    continue;

                // Set the offset
                GraphicsState state = g.Save();
                g.TranslateTransform(node.Offset.X, node.Offset.Y);
                {
                    if (node is TmxLayer)
                    {
                        DrawTileLayerColliders(g, tmxMap, node as TmxLayer, scale);
                    }
                    else if (node is TmxObjectGroup)
                    {
                        DrawObjectLayerColliders(g, tmxMap, node as TmxObjectGroup);
                    }
                    else if (node is TmxLayerNode)
                    {
                        DrawAllCollidersInLayerNodes(g, tmxMap, node.LayerNodes, scale);
                    }
                }
                g.Restore(state);
            }
        }

        private static void DrawTileLayerColliders(Graphics g, TmxMap tmxMap, TmxLayer tileLayer, float scale)
        {
            // Bail if this layer is ignoring collision
            if (tileLayer.Ignore == TmxLayerNode.IgnoreSettings.Collision)
                return;

            foreach (TmxLayer collisionLayer in tileLayer.CollisionLayers)
            {
                TmxObjectType type = tmxMap.ObjectTypes.GetValueOrDefault(collisionLayer.Name);

                Color lineColor = type.Color;
                Color polyColor = Color.FromArgb(128, lineColor);
                DrawCollisionLayer(g, collisionLayer, polyColor, lineColor, scale);
            }
        }

        private static void DrawCollisionLayer(Graphics g, TmxLayer tmxLayer, Color polyColor, Color lineColor, float scale)
        {
            LayerClipper.TransformPointFunc xfFunc = (x, y) => new ClipperLib.IntPoint(x, y);
            LayerClipper.ProgressFunc progFunc = (prog) => { }; // do nothing

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(tmxLayer.TmxMap, tmxLayer, xfFunc, progFunc);

            float inverseScale = 1.0f / scale;
            if (inverseScale > 1)
                inverseScale = 1;

            using (GraphicsPath path = new GraphicsPath())
            using (Pen pen = new Pen(lineColor, 2.0f * inverseScale))
            using (Brush brush = TmxHelper.CreateLayerColliderBrush(polyColor))
            {
                // Draw all closed polygons
                // First, add them to the path
                // (But are we using convex polygons are complex polygons?
                var polygons = tmxLayer.IsExportingConvexPolygons() ? LayerClipper.SolutionPolygons_Simple(solution) : LayerClipper.SolutionPolygons_Complex(solution);
                foreach (var pointfArray in polygons)
                {
                    path.AddPolygon(pointfArray);
                }

                // Then, fill and draw the path full of polygons
                if (path.PointCount > 0)
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                // Draw all lines (open polygons)
                path.Reset();
                foreach (var points in ClipperLib.Clipper.OpenPathsFromPolyTree(solution))
                {
                    var pointfs = points.Select(pt => new PointF(pt.X, pt.Y));
                    path.StartFigure();
                    path.AddLines(pointfs.ToArray());
                }
                if (path.PointCount > 0)
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawObjectLayerColliders(Graphics g, TmxMap tmxMap, TmxObjectGroup objectLayer)
        {
            foreach (var obj in objectLayer.Objects)
            {
                if (!obj.Visible)
                    continue;

                // Either type color or object color or unity:layer color
                Color objColor = objectLayer.Color;
                string collisionType = objectLayer.UnityLayerOverrideName;

                if (String.IsNullOrEmpty(collisionType))
                {
                    collisionType = obj.Type;
                }

                if (tmxMap.ObjectTypes.TmxObjectTypeMapping.ContainsKey(collisionType))
                {
                    objColor = tmxMap.ObjectTypes.TmxObjectTypeMapping[collisionType].Color;
                }

                if (objectLayer.Ignore == TmxLayerNode.IgnoreSettings.Collision)
                {
                    // We're ignoring collisions but the game object is still a part of the map.
                    DrawObjectMarker(g, tmxMap, obj, objColor);
                }
                else
                {
                    DrawObjectCollider(g, tmxMap, obj, objColor);
                }
            }
        }

        private static void DrawObjectMarker(Graphics g, TmxMap tmxMap, TmxObject tmxObject, Color color)
        {
            using (Pen pen = new Pen(color))
            {
                GraphicsState state = g.Save();

                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                g.TranslateTransform(xfPosition.X, xfPosition.Y);
                g.RotateTransform(tmxObject.Rotation);

                Rectangle rc = new Rectangle(-2, -2, 4, 4);
                g.DrawEllipse(pen, rc);
                g.Restore(state);
            }
        }

        private static void PrepareTransformForTileObject(Graphics g, TmxMap tmxMap, TmxObjectTile tmxObjectTile)
        {
            // Isometric tiles are off by a half-width
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                g.TranslateTransform(-tmxObjectTile.Tile.TileSize.Width * 0.5f, 0);
            }

            // Apply scale
            {
                // Move to "local origin" of tile and scale
                float toCenter_x = (tmxMap.Orientation == TmxMap.MapOrientation.Isometric) ? (tmxObjectTile.Tile.TileSize.Width * 0.5f) : 0.0f;
                g.TranslateTransform(toCenter_x, 0);
                {
                    SizeF scale = tmxObjectTile.GetTileObjectScale();
                    g.ScaleTransform(scale.Width, scale.Height);
                }

                // Move back
                g.TranslateTransform(-toCenter_x, 0);
            }

            // Apply horizontal flip
            if (tmxObjectTile.FlippedHorizontal)
            {
                g.TranslateTransform(tmxObjectTile.Tile.TileSize.Width, 0);
                g.ScaleTransform(-1, 1);
            }

            // Apply vertical flip
            if (tmxObjectTile.FlippedVertical)
            {
                g.TranslateTransform(0, -tmxObjectTile.Tile.TileSize.Height);
                g.ScaleTransform(1, -1);
            }
        }

        private static void DrawObjectCollider(Graphics g, TmxMap tmxMap, TmxObject tmxObject, Color color)
        {
            using (Brush brush = TmxHelper.CreateObjectColliderBrush(color))
            using (Pen pen = new Pen(color))
            {
                GraphicsState state = g.Save();

                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                g.TranslateTransform(xfPosition.X, xfPosition.Y);
                g.RotateTransform(tmxObject.Rotation);

                if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    DrawPolygon(g, pen, brush, tmxMap, tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromRectangle(tmxMap, tmxObject as TmxObjectRectangle);
                        DrawPolygon(g, pen, brush, tmxMap, tmxIsometricRectangle);
                    }
                    else
                    {
                        // Rectangles are polygons
                        DrawPolygon(g, pen, brush, tmxMap, tmxObject as TmxObjectPolygon);
                    }
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    DrawEllipse(g, pen, brush, tmxMap, tmxObject as TmxObjectEllipse);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    DrawPolyline(g, pen, tmxMap, tmxObject as TmxObjectPolyline);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    GraphicsState tileState = g.Save();

                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;
                    PrepareTransformForTileObject(g, tmxMap, tmxObjectTile);

                    // Draw the collisions
                    // Temporarily set orienation to Orthogonal for tile colliders
                    TmxMap.MapOrientation restoreOrientation = tmxMap.Orientation;
                    tmxMap.Orientation = TmxMap.MapOrientation.Orthogonal;
                    {
                        // Make up for the fact that the bottom-left corner is the origin
                        g.TranslateTransform(0, -tmxObjectTile.Tile.TileSize.Height);
                        foreach (var obj in tmxObjectTile.Tile.ObjectGroup.Objects)
                        {
                            DrawObjectCollider(g, tmxMap, obj, Color.Gray);
                        }
                    }
                    tmxMap.Orientation = restoreOrientation;

                    g.Restore(tileState);
                }
                else
                {
                    g.Restore(state);
                    Logger.WriteWarning("Unhandled object: {0}", tmxObject.GetNonEmptyName());
                }

                // Restore our state
                g.Restore(state);
            }
        }

        private static void DrawPolygon(Graphics g, Pen pen, Brush brush, TmxMap tmxMap, TmxObjectPolygon tmxPolygon)
        {
            var points = TmxMath.GetPointsInMapSpace(tmxMap, tmxPolygon).ToArray();
            g.FillPolygon(brush, points);
            g.DrawPolygon(pen, points);
        }

        private static void DrawPolyline(Graphics g, Pen pen, TmxMap tmxMap, TmxObjectPolyline tmxPolyline)
        {
            var points = TmxMath.GetPointsInMapSpace(tmxMap, tmxPolyline).ToArray();
            g.DrawLines(pen, points);
        }

        private static void DrawEllipse(Graphics g, Pen pen, Brush brush, TmxMap tmxMap, TmxObjectEllipse tmxEllipse)
        {
            RectangleF rc = new RectangleF(new PointF(0, 0), tmxEllipse.Size);
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                // Circles and ellipses not supported in Insometric mode
                g.FillEllipse(Brushes.Red, rc);
                g.DrawEllipse(Pens.White, rc);

                Logger.WriteWarning(" Not supported (isometric): {0}", tmxEllipse.GetNonEmptyName());
            }
            else if (!tmxEllipse.IsCircle())
            {
                // We don't really support ellipses, especially as colliders
                g.FillEllipse(Brushes.Red, rc);
                g.DrawEllipse(Pens.White, rc);

                Logger.WriteWarning(" Not supported (ellipse): {0}", tmxEllipse.GetNonEmptyName());
            }
            else
            {
                g.FillEllipse(brush, rc);
                g.DrawEllipse(pen, rc);
            }
        }

        private static int GetMaxTilesWide(TmxMap tmxMap)
        {
            return Math.Min(tmxMap.Width, MaxPreviewTilesWide);
        }

        private static int GetMaxTilesHigh(TmxMap tmxMap)
        {
            return Math.Min(tmxMap.Height, MaxPreviewTilesHigh);
        }

        private static int GetMaxTilesWide(TmxLayer layer)
        {
            return Math.Min(layer.Width, MaxPreviewTilesWide);
        }

        private static int GetMaxTilesHigh(TmxLayer layer)
        {
            return Math.Min(layer.Height, MaxPreviewTilesHigh);
        }

    }
}
