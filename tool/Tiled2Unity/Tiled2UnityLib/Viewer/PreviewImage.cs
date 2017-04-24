using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Tiled2Unity.Viewer
{
    public class PreviewImage
    {
        private static readonly float GridSize = 3.0f;
        private static readonly int MaxPreviewTilesWide = 256;
        private static readonly int MaxPreviewTilesHigh = 256;

        private static float Scale { get; set; } = 1.0f;

        private static float InvScale
        {
            get
            {
                return 1.0f / PreviewImage.Scale;
            }
        }

        private static float StrokeWidthThin
        {
            get
            {
                return PreviewImage.InvScale;
            }
        }

        private static float StrokeWidthThick
        {
            get
            {
                return StrokeWidthThin * 3.0f;
            }
        }

        public static SKBitmap CreatePreviewBitmap(TmxMap tmxMap)
        {
            return CreatePreviewBitmap(tmxMap, 1.0f);
        }

        public static SKBitmap CreatePreviewBitmap(TmxMap tmxMap, float scale)
        {
            SummaryReport report = new SummaryReport();
            report.Capture("Previewing");

            // Set our scale to be used throughout the image
            Scale = scale;

            // What is the boundary of the bitmap we are creating?
            SKRect bounds = CalculateBoundary(tmxMap);
            SKBitmap bitmap = null;

            try
            {
                int width = (int)Math.Ceiling(bounds.Width * scale);
                int height = (int)Math.Ceiling(bounds.Height * scale);
                bitmap = new SKBitmap(width, height);
            }
            catch (System.ArgumentException)
            {
                StringBuilder warning = new StringBuilder();
                warning.AppendFormat("Map cannot be previewed at '{0}' scale. Try a smaller scale.\n", scale);
                warning.AppendLine("Image will be constructed on a 1024x1024 canvas. Parts of your map may be cropped.");
                Logger.WriteWarning(warning.ToString());

                bitmap = new SKBitmap(1024, 1024);
            }

            using (SKCanvas canvas = new SKCanvas(bitmap))
            using (SKPaint paint = new SKPaint())
            using (new SKAutoCanvasRestore(canvas))
            {
                // Apply scale then translate
                canvas.Clear(SKColors.WhiteSmoke);
                canvas.Scale(scale, scale);
                canvas.Translate(-bounds.Left, -bounds.Top);

                // Draw all the elements of the previewer
                DrawBackground(canvas, bounds);
                DrawGrid(canvas, tmxMap);
                DrawAllTilesInLayerNodes(canvas, tmxMap, tmxMap.LayerNodes);
                DrawAllCollidersInLayerNodes(canvas, tmxMap, tmxMap.LayerNodes);
            }

            report.Report();
            return bitmap;
        }

        private static SKRect CalculateBoundary(TmxMap tmxMap)
        {
            SKRect rcMap = SKRect.Create(0, 0, tmxMap.MapSizeInPixels.Width, tmxMap.MapSizeInPixels.Height);

            // Any tile layers in the map can be offset
            var tileLayerBounds = from layer in tmxMap.EnumerateTileLayers()
                                  where layer.Visible == true
                                  let offset = layer.GetCombinedOffset()
                                  select SKRect.Create(offset.X, offset.Y, rcMap.Size.Width, rcMap.Size.Height);

            // Take boundaries from object groups
            var objBounds = from g in tmxMap.EnumerateObjectLayers()
                            from o in g.Objects
                            where o.Visible == true
                            let b = o.GetOffsetWorldBounds()
                            select SKRect.Create(b.X, b.Y, b.Width, b.Height);

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
                             select SKRect.Create(bound.X + point.X, bound.Y + point.Y, bound.Width, bound.Height);

            var allBounds = tileLayerBounds.Concat(objBounds).Concat(tileBounds);
            var union = allBounds.Aggregate(rcMap, SKRect.Union);

            // Inflate a tile size to make room for the grid
            union.Inflate(tmxMap.TileWidth, tmxMap.TileHeight);
            union.Inflate(PreviewImage.GridSize, PreviewImage.GridSize);

            return union;
        }


        private static void DrawBackground(SKCanvas canvas, SKRect bounds)
        {
            // Draw the background for the map
            using (SKPaint paint = new SKPaint())
            {
                paint.Color = SKColors.WhiteSmoke;
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(bounds, paint);

                paint.Color = SKColors.Black;
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThin;
                paint.IsAntialias = true;
                canvas.DrawRect(bounds, paint);
            }
        }

        private static void DrawGrid(SKCanvas canvas, TmxMap tmxMap)
        {
            if (tmxMap.Orientation == TmxMap.MapOrientation.Hexagonal)
            {
                DrawGridHex(canvas, tmxMap);
            }
            else
            {
                DrawGridQuad(canvas, tmxMap);
            }
        }

        private static void DrawGridQuad(SKCanvas canvas, TmxMap tmxMap)
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

            // Can take for granted that background is always white in drawing black rectangles
            using (SKPaint paint = new SKPaint())
            {
                paint.Color = SKColors.Black;
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThin;
                foreach (var p in points)
                {
                    SKRect rc = SKRect.Create(p.X, p.Y, PreviewImage.GridSize, PreviewImage.GridSize);
                    rc.Offset(-PreviewImage.GridSize * 0.5f, -PreviewImage.GridSize * 0.5f);
                    canvas.DrawRect(rc, paint);
                }
            }
        }

        private static void DrawGridHex(SKCanvas canvas, TmxMap tmxMap)
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

            using (SKPaint paint = new SKPaint())
            {
                foreach (var p in points)
                {
                    SKRect rc = SKRect.Create(p.X, p.Y, PreviewImage.GridSize, PreviewImage.GridSize);
                    rc.Offset(-PreviewImage.GridSize * 0.5f, -PreviewImage.GridSize * 0.5f);

                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = StrokeWidthThin;
                    paint.Color = SKColors.Black;
                    canvas.DrawRect(rc, paint);
                }
            }
        }

        private static void DrawAllTilesInLayerNodes(SKCanvas canvas, TmxMap tmxMap, List<TmxLayerNode> layerNodes)
        {
            foreach (var node in layerNodes)
            {
                if (node.Visible == false)
                    continue;

                if (node.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                // Translate by the offset
                using (new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(node.Offset.X, node.Offset.Y);

                    if (node.GetType() == typeof(TmxLayer))
                    {
                        DrawTilesInTileLayer(canvas, tmxMap, node as TmxLayer);
                    }
                    else if (node.GetType() == typeof(TmxObjectGroup))
                    {
                        DrawTilesInObjectGroup(canvas, tmxMap, node as TmxObjectGroup);
                    }
                    else if (node.GetType() == typeof(TmxGroupLayer))
                    {
                        DrawAllTilesInLayerNodes(canvas, tmxMap, node.LayerNodes);
                    }
                }
            }
        }

        private static void DrawTilesInTileLayer(SKCanvas canvas, TmxMap tmxMap, TmxLayer layer)
        {
            using (SKPaint paint = new SKPaint())
            {
                // Set the opacity for the layer
                paint.Color = SKColors.White.WithAlpha((byte)(layer.Opacity * byte.MaxValue));

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
                                Position = tmxMap.GetMapPositionAt(x, y, frame),
                                Bitmap = frame.TmxImage.ImageBitmap,
                                IsFlippedDiagnoally = TmxMath.IsTileFlippedDiagonally(rawTileId),
                                IsFlippedHorizontally = TmxMath.IsTileFlippedHorizontally(rawTileId),
                                IsFlippedVertically = TmxMath.IsTileFlippedVertically(rawTileId),
                            };

                foreach (var t in tiles)
                {
                    PointF location = t.Position;

                    // Individual tiles may be larger than the given tile size of the overall map
                    location.Y = (t.Position.Y - t.Tile.TileSize.Height) + tmxMap.TileHeight;

                    using (new SKAutoCanvasRestore(canvas))
                    {
                        bool flip_h = t.IsFlippedHorizontally;
                        bool flip_v = t.IsFlippedVertically;
                        bool flip_d = t.IsFlippedDiagnoally;

                        // Move to the center of the tile on location and perform and transforms
                        SKPoint center = new SKPoint(t.Tile.TileSize.Width * 0.5f, t.Tile.TileSize.Height * 0.5f);
                        canvas.Translate(center.X, center.Y);
                        canvas.Translate(location.X, location.Y);

                        // Flip transformations (logic taken from Tiled source: maprenderer.cpp)
                        {
                            // If we're flipping diagonally then rotate 90 degrees and reverse h/v flip flags
                            float rotate = 0;
                            if (flip_d)
                            {
                                rotate = 90;
                                flip_h = t.IsFlippedVertically;
                                flip_v = !t.IsFlippedHorizontally;
                            }

                            // Scale based on flip flags
                            float scale_x = flip_h ? -1.0f : 1.0f;
                            float scale_y = flip_v ? -1.0f : 1.0f;

                            canvas.Scale(scale_x, scale_y);
                            canvas.RotateDegrees(rotate);
                        }

                        // Move us back out of the center
                        canvas.Translate(-center.X, -center.Y);

                        // Draw the tile
                        SKRect dest = SKRect.Create(0, 0, t.Tile.TileSize.Width, t.Tile.TileSize.Height);
                        SKRect source = SKRect.Create(t.Tile.LocationOnSource.X, t.Tile.LocationOnSource.Y, t.Tile.TileSize.Width, t.Tile.TileSize.Height);
                        canvas.DrawBitmap(t.Bitmap, source, dest, paint);
                    }
                }
            }
        }

        private static void DrawTilesInObjectGroup(SKCanvas canvas, TmxMap tmxMap, TmxObjectGroup objectGroup)
        {
            using (SKPaint paint = new SKPaint())
            {
                // Draw with the given opacity
                paint.Color = SKColors.White.WithAlpha((byte)(objectGroup.Opacity * byte.MaxValue));

                foreach (var tmxObject in objectGroup.Objects)
                {
                    if (!tmxObject.Visible)
                        continue;

                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;
                    if (tmxObjectTile == null)
                        continue;

                    using (new SKAutoCanvasRestore(canvas))
                    {
                        PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                        canvas.Translate(xfPosition.X, xfPosition.Y);
                        canvas.RotateDegrees(tmxObject.Rotation);
                        using (new SKAutoCanvasRestore(canvas))
                        {
                            PrepareTransformForTileObject(canvas, tmxMap, tmxObjectTile);

                            // Draw the tile
                            SKRect destination = new RectangleF(0, -tmxObjectTile.Tile.TileSize.Height, tmxObjectTile.Tile.TileSize.Width, tmxObjectTile.Tile.TileSize.Height).ToSKRect();
                            SKRect source = new RectangleF(tmxObjectTile.Tile.LocationOnSource, tmxObjectTile.Tile.TileSize).ToSKRect();
                            canvas.DrawBitmap(tmxObjectTile.Tile.TmxImage.ImageBitmap, source, destination, paint);
                        }
                    }
                }
            }
        }

        private static void DrawAllCollidersInLayerNodes(SKCanvas canvas, TmxMap tmxMap, List<TmxLayerNode> layerNodes)
        {
            foreach (var node in layerNodes)
            {
                if (node.Visible == false)
                    continue;

                if (node.Ignore == TmxLayerNode.IgnoreSettings.True)
                    continue;

                // Set the offset for the node and draw
                using (new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(node.Offset.X, node.Offset.Y);
                    if (node is TmxLayer)
                    {
                        DrawTileLayerColliders(canvas, tmxMap, node as TmxLayer);
                    }
                    else if (node is TmxObjectGroup)
                    {
                        DrawObjectLayerColliders(canvas, tmxMap, node as TmxObjectGroup);
                    }
                    else if (node is TmxLayerNode)
                    {
                        DrawAllCollidersInLayerNodes(canvas, tmxMap, node.LayerNodes);
                    }
                }
            }
        }

        private static void DrawTileLayerColliders(SKCanvas canvas, TmxMap tmxMap, TmxLayer tileLayer)
        {
            // Bail if this layer is ignoring collision
            if (tileLayer.Ignore == TmxLayerNode.IgnoreSettings.Collision)
                return;

            foreach (TmxLayer collisionLayer in tileLayer.CollisionLayers)
            {
                TmxObjectType type = tmxMap.ObjectTypes.GetValueOrDefault(collisionLayer.Name);

                SKColor lineColor = new SKColor(type.Color.R, type.Color.B, type.Color.B);
                SKColor polyColor = new SKColor(type.Color.R, type.Color.B, type.Color.B, 128);
                DrawCollisionLayer(canvas, collisionLayer, polyColor, lineColor);
            }
        }

        private static void DrawCollisionLayer(SKCanvas canvas, TmxLayer tmxLayer, SKColor polyColor, SKColor lineColor)
        {
            LayerClipper.TransformPointFunc xfFunc = (x, y) => new ClipperLib.IntPoint(x, y);
            LayerClipper.ProgressFunc progFunc = (prog) => { }; // do nothing

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(tmxLayer.TmxMap, tmxLayer, xfFunc, progFunc);

            using (SKPaint paint = new SKPaint())
            {
                // Draw all closed polygons
                // First, add them to the path
                // (But are we using convex polygons are complex polygons?
                using (SKPath path = new SKPath())
                {
                    var polygons = tmxLayer.IsExportingConvexPolygons() ? LayerClipper.SolutionPolygons_Simple(solution) : LayerClipper.SolutionPolygons_Complex(solution);
                    foreach (var pointfArray in polygons)
                    {
                        var pts = pointfArray.ToSkPointArray();
                        path.AddPoly(pts, true);
                    }

                    // Then, fill and draw the path full of polygons
                    if (path.PointCount > 0)
                    {
                        paint.Style = SKPaintStyle.Fill;
                        paint.Color = polyColor;
                        canvas.DrawPath(path, paint);

                        paint.Style = SKPaintStyle.Stroke;
                        paint.StrokeWidth = StrokeWidthThick;
                        paint.Color = lineColor;
                        canvas.DrawPath(path, paint);
                    }
                }

                // Draw all lines (open polygons)
                using (SKPath path = new SKPath())
                {
                    foreach (var points in ClipperLib.Clipper.OpenPathsFromPolyTree(solution))
                    {
                        var pts = points.Select(pt => new SKPoint(pt.X, pt.Y)).ToArray();
                        path.AddPoly(pts, false);
                    }
                    if (path.PointCount > 0)
                    {
                        paint.Style = SKPaintStyle.Stroke;
                        paint.StrokeWidth = StrokeWidthThick;
                        paint.Color = lineColor;
                        canvas.DrawPath(path, paint);
                    }
                }
            }
        }

        private static void DrawObjectLayerColliders(SKCanvas canvas, TmxMap tmxMap, TmxObjectGroup objectLayer)
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
                    if (!(obj is TmxObjectTile))
                    {
                        DrawObjectMarker(canvas, tmxMap, obj, objColor.ToSKColor());
                    }
                }
                else
                {
                    DrawObjectCollider(canvas, tmxMap, obj, objColor.ToSKColor());
                }
            }
        }

        private static void DrawObjectMarker(SKCanvas canvas, TmxMap tmxMap, TmxObject tmxObject, SKColor color)
        {
            using (new SKAutoCanvasRestore(canvas))
            using (SKPaint paint = new SKPaint())
            {
                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                canvas.Translate(xfPosition.X, xfPosition.Y);

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThick;
                paint.Color = color;
                canvas.DrawCircle(0, 0, 2, paint);
            }
        }

        private static void PrepareTransformForTileObject(SKCanvas canvas, TmxMap tmxMap, TmxObjectTile tmxObjectTile)
        {
            // Isometric tiles are off by a half-width
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                canvas.Translate(-tmxObjectTile.Tile.TileSize.Width * 0.5f, 0);
            }

            // Apply scale
            {
                // Move to "local origin" of tile and scale
                float toCenter_x = (tmxMap.Orientation == TmxMap.MapOrientation.Isometric) ? (tmxObjectTile.Tile.TileSize.Width * 0.5f) : 0.0f;
                canvas.Translate(toCenter_x, 0);
                {
                    SizeF scale = tmxObjectTile.GetTileObjectScale();
                    canvas.Scale(scale.Width, scale.Height);
                }

                // Move back
                canvas.Translate(-toCenter_x, 0);
            }

            // Apply horizontal flip
            if (tmxObjectTile.FlippedHorizontal)
            {
                canvas.Translate(tmxObjectTile.Tile.TileSize.Width, 0);
                canvas.Scale(-1, 1);
            }

            // Apply vertical flip
            if (tmxObjectTile.FlippedVertical)
            {
                canvas.Translate(0, -tmxObjectTile.Tile.TileSize.Height);
                canvas.Scale(1, -1);
            }
        }

        private static void DrawObjectCollider(SKCanvas canvas, TmxMap tmxMap, TmxObject tmxObject, SKColor color)
        {
            using (new SKAutoCanvasRestore(canvas))
            using (SKPaint paint = new SKPaint())
            {
                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(tmxMap, tmxObject.Position);
                canvas.Translate(xfPosition.ToSKPoint());
                canvas.RotateDegrees(tmxObject.Rotation);

                if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    DrawPolygon(canvas, color, tmxMap, tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromRectangle(tmxMap, tmxObject as TmxObjectRectangle);
                        DrawPolygon(canvas, color, tmxMap, tmxIsometricRectangle);
                    }
                    else
                    {
                        // Rectangles are polygons
                        DrawPolygon(canvas, color, tmxMap, tmxObject as TmxObjectPolygon);
                    }
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    DrawEllipse(canvas, color, tmxMap, tmxObject as TmxObjectEllipse);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    DrawPolyline(canvas, color, tmxMap, tmxObject as TmxObjectPolyline);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    using (new SKAutoCanvasRestore(canvas))
                    {
                        TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;
                        PrepareTransformForTileObject(canvas, tmxMap, tmxObjectTile);

                        // Draw the collisions
                        // Temporarily set orienation to Orthogonal for tile colliders
                        TmxMap.MapOrientation restoreOrientation = tmxMap.Orientation;
                        tmxMap.Orientation = TmxMap.MapOrientation.Orthogonal;
                        {
                            // Make up for the fact that the bottom-left corner is the origin
                            canvas.Translate(0, -tmxObjectTile.Tile.TileSize.Height);
                            foreach (var obj in tmxObjectTile.Tile.ObjectGroup.Objects)
                            {
                                TmxObjectType type = tmxMap.ObjectTypes.GetValueOrDefault(obj.Type);
                                DrawObjectCollider(canvas, tmxMap, obj, type.Color.ToSKColor());
                            }
                        }
                        tmxMap.Orientation = restoreOrientation;
                    }
                }
                else
                {
                    Logger.WriteWarning("Unhandled object: {0}", tmxObject.GetNonEmptyName());
                }
            }
        }

        private static void DrawPolygon(SKCanvas canvas, SKColor color, TmxMap tmxMap, TmxObjectPolygon tmxPolygon)
        {
            using (SKPaint paint = new SKPaint())
            using (SKPath path = new SKPath())
            {
                var points = TmxMath.GetPointsInMapSpace(tmxMap, tmxPolygon).ToSkPointArray();
                path.AddPoly(points);

                paint.Style = SKPaintStyle.Fill;
                paint.StrokeWidth = StrokeWidthThick;
                paint.Color = color.WithAlpha(128);
                canvas.DrawPath(path, paint);

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThick;
                paint.Color = color;
                canvas.DrawPath(path, paint);
            }
        }

        private static void DrawPolyline(SKCanvas canvas, SKColor color, TmxMap tmxMap, TmxObjectPolyline tmxPolyline)
        {
            using (SKPaint paint = new SKPaint())
            using (SKPath path = new SKPath())
            {
                var points = TmxMath.GetPointsInMapSpace(tmxMap, tmxPolyline).ToSkPointArray();
                path.AddPoly(points, false);

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThick;
                paint.Color = color;
                canvas.DrawPath(path, paint);
            }
        }

        private static void DrawEllipse(SKCanvas canvas, SKColor color, TmxMap tmxMap, TmxObjectEllipse tmxEllipse)
        {
            SKRect rc = new SKRect(0, 0, tmxEllipse.Size.Width, tmxEllipse.Size.Height);
            SKColor stroke = color;
            SKColor fill = color.WithAlpha(128);

            using (SKPaint paint = new SKPaint())
            using (SKPath path = new SKPath())
            {
                if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                {
                    // Circles and ellipses not supported in Insometric mode
                    stroke = SKColors.Black;
                    fill = SKColors.Red;
                    Logger.WriteWarning("Circles/Ellipses not supported in isometric mode: {0}", tmxEllipse.GetNonEmptyName());
                }
                else if (!tmxEllipse.IsCircle())
                {
                    stroke = SKColors.Black;
                    fill = SKColors.Red;
                    Logger.WriteWarning("Object is an ellipse and will be ignored (only circles supported): {0}", tmxEllipse.GetNonEmptyName());
                }
                path.AddOval(rc);

                paint.Style = SKPaintStyle.Fill;
                paint.Color = fill;
                canvas.DrawPath(path, paint);

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = StrokeWidthThick;
                paint.Color = stroke;
                canvas.DrawPath(path, paint);
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

    // Helper extenstions
    public static class SkiaSharpExtentions
    {
        public static SKPoint ToSKPoint(this System.Drawing.PointF point)
        {
            return new SKPoint(point.X, point.Y);
        }

        public static SKPointI ToSKPoint(this System.Drawing.Point point)
        {
            return new SKPointI(point.X, point.Y);
        }

        public static SKRect ToSKRect(this System.Drawing.RectangleF rect)
        {
            return new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static SKColor ToSKColor(this System.Drawing.Color color)
        {
            return (SKColor)(uint)color.ToArgb();
        }

        public static SKPoint[] ToSkPointArray(this IEnumerable<PointF> points)
        {
            return points.Select(p => p.ToSKPoint()).ToArray();
        }

    }
}
