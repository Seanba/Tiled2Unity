using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Tiled2Unity
{
    using ClipperPolygon = List<ClipperLib.IntPoint>;
    using ClipperPolygons = List<List<ClipperLib.IntPoint>>;

    public partial class Tiled2UnityViewer : Form
    {
        private static readonly float GridSize = 3.0f;

        private TmxMap tmxMap = null;
        private float scale = 1.0f;
        private PreviewPreferencesForm preferencesForm;

        public Tiled2UnityViewer(TmxMap tmxMap)
        {
            this.tmxMap = tmxMap;
            this.preferencesForm = new PreviewPreferencesForm();
            this.preferencesForm.ApplyChanges += new PreviewPreferencesForm.OnApplyChanges(preferencesForm_ApplyChanges);
            this.preferencesForm.InitializePrefernces(tmxMap);
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            this.scale = Properties.Settings.Default.LastPreviewScale;
            if (this.scale <= 0.0f || this.scale > 8.0f)
            {
                this.scale = 1.0f;
            }

            CreateAndShowBitmap();
        }


        private void CreateAndShowBitmap()
        {
            // Check our scale
            this.view18ToolStripMenuItem.Checked = this.scale == 0.125f;
            this.view14ToolStripMenuItem.Checked = this.scale == 0.25f;
            this.view12ToolStripMenuItem.Checked = this.scale == 0.5f;
            this.view100ToolStripMenuItem.Checked = this.scale == 1.0f;
            this.view200ToolStripMenuItem.Checked = this.scale == 2.0f;
            this.view400ToolStripMenuItem.Checked = this.scale == 4.0f;
            this.view800ToolStripMenuItem.Checked = this.scale == 8.0f;

            this.Text = String.Format("Tiled2Unity Previewer (Scale = {0})", this.scale);

            Properties.Settings.Default.LastPreviewScale = this.scale;
            Properties.Settings.Default.Save();

            RectangleF boundary = CalculateBoundary();
            this.pictureBoxViewer.Image = CreateBitmap(boundary);
        }

        private RectangleF CalculateBoundary()
        {
            RectangleF rcMap = new RectangleF(Point.Empty, this.tmxMap.MapSizeInPixels());

            // Take boundaries from object groups
            var objBounds = from g in this.tmxMap.ObjectGroups
                            from o in g.Objects
                            where o.Visible == true
                            where IsLayerEnabled(g.Name)
                            select o.GetWorldBounds();

            // Take boundaries from objects embedded in tiles
            var tileBounds = from layer in tmxMap.Layers
                             where layer.Visible == true
                             where IsLayerEnabled(layer.DefaultName)
                             from y in Enumerable.Range(0, layer.Height)
                             from x in Enumerable.Range(0, layer.Width)
                             let tileId = layer.GetTileIdAt(x, y)
                             where tileId != 0
                             let tile = this.tmxMap.Tiles[tileId]
                             from o in tile.ObjectGroup.Objects
                             let bound = o.GetWorldBounds()
                             let point = TmxMath.TileCornerInScreenCoordinates(this.tmxMap, x, y)
                             select new RectangleF(bound.X + point.X, bound.Y + point.Y, bound.Width, bound.Height);

            var allBounds = objBounds.Concat(tileBounds);
            var union = allBounds.Aggregate(rcMap, RectangleF.Union);

            // Inflate a tile size to make room for the grid
            union.Inflate(this.tmxMap.TileWidth, this.tmxMap.TileHeight);
            union.Inflate(Tiled2UnityViewer.GridSize, Tiled2UnityViewer.GridSize);

            return union;
        }

        private Bitmap CreateBitmap(RectangleF bounds)
        {
            Bitmap bitmap = null;

            try
            {
                bitmap = new Bitmap((int)Math.Ceiling(bounds.Width * this.scale) + 1, (int)Math.Ceiling(bounds.Height * this.scale) + 1);
            }
            catch (System.ArgumentException)
            {
                MessageBox.Show("Cannot preview at these scale. Try a lower scale.", "Too Big!");
                bitmap = new Bitmap(1024, 1024);
            }

            using (Pen pen = new Pen(Color.Black, 1.0f))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                pen.Alignment = PenAlignment.Inset;

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.ScaleTransform(this.scale, this.scale);

                g.FillRectangle(Brushes.WhiteSmoke, 0, 0, bounds.Width, bounds.Height);
                g.DrawRectangle(pen, 1, 1, bounds.Width-1, bounds.Height-1);

                g.TranslateTransform(-bounds.X, -bounds.Y);
                DrawBackground(g);
                DrawGrid(g);
                DrawTiles(g);
                DrawColliders(g);
                DrawObjectColliders(g);
            }

            return bitmap;
        }

        private void DrawBackground(Graphics g)
        {
            // Draw the background for the map
            // A full white background is preferred because of the colliders we draw on the top of the layers
            Size size = this.tmxMap.MapSizeInPixels();
            Rectangle rect = new Rectangle(Point.Empty, size);
            g.FillRectangle(Brushes.White, rect);
        }

        private void DrawGrid(Graphics g)
        {
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Hexagonal)
            {
                DrawGridHex(g);
            }
            else
            {
                DrawGridQuad(g);
            }
        }

        private void DrawGridQuad(Graphics g)
        {
            HashSet<Point> points = new HashSet<Point>();
            for (int x = 0; x < this.tmxMap.Width; ++x)
            {
                for (int y = 0; y < this.tmxMap.Height; ++y)
                {
                    // Add the "top-left" corner of a tile
                    points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y));

                    // Add all other corners of the tile to our list of grid points
                    // This is complicated by different map types (espcially staggered isometric)
                    if (this.tmxMap.Orientation == TmxMap.MapOrientation.Orthogonal || this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x + 1, y));
                        points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x + 1, y + 1));
                        points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 1));
                    }
                    else if (this.tmxMap.Orientation == TmxMap.MapOrientation.Staggered)
                    {
                        bool sx = TmxMath.DoStaggerX(this.tmxMap, x);
                        bool sy = TmxMath.DoStaggerY(this.tmxMap, y);

                        if (sx)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x + 1, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x - 1, y + 1));
                        }
                        else if (sy)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x + 1, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 2));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 1));
                        }
                        else if (this.tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x + 1, y));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x - 1, y));
                        }
                        else if (this.tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y)
                        {
                            // top-right, bottom-right, and bottom-left
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 1));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x, y + 2));
                            points.Add(TmxMath.TileCornerInGridCoordinates(this.tmxMap, x - 1, y + 1));
                        }
                    }
                }
            }

            foreach (var p in points)
            {
                RectangleF rc = new RectangleF(p.X, p.Y, Tiled2UnityViewer.GridSize, Tiled2UnityViewer.GridSize);
                rc.Offset(-Tiled2UnityViewer.GridSize * 0.5f, -Tiled2UnityViewer.GridSize * 0.5f);

                g.FillRectangle(Brushes.White, rc);
                g.DrawRectangle(Pens.Black, rc.X, rc.Y, rc.Width, rc.Height);
            }
        }

        private void DrawGridHex(Graphics g)
        {
            // Our collection of points to render
            HashSet<Point> points = new HashSet<Point>();

            // Note: borrowed heavily from Tiled source (HexagonalRenderer::drawGrid)
            int tileWidth = this.tmxMap.TileWidth & ~1;
            int tileHeight = this.tmxMap.TileHeight & ~1;

            int sideLengthX = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X ? tmxMap.HexSideLength : 0;
            int sideLengthY = tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.Y ? tmxMap.HexSideLength : 0;

            int sideOffsetX = (tmxMap.TileWidth - sideLengthX) / 2;
            int sideOffsetY = (tmxMap.TileHeight - sideLengthY) / 2;

            int columnWidth = sideOffsetX + sideLengthX;
            int rowHeight = sideOffsetY + sideLengthY;

            bool staggerX = this.tmxMap.StaggerAxis == TmxMap.MapStaggerAxis.X;

            // Determine the tile and pixel coordinates to start at
            Point startTile = new Point(0, 0);
            Point startPos = TmxMath.TileCornerInScreenCoordinates(this.tmxMap, startTile.X, startTile.Y);

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
                if (TmxMath.DoStaggerX(this.tmxMap, startTile.X))
                {
                    startPos.Y -= rowHeight;
                }

                for (; startTile.X < this.tmxMap.Width; startTile.X++)
                {
                    Point rowTile = startTile;
                    Point rowPos = startPos;

                    if (TmxMath.DoStaggerX(this.tmxMap, startTile.X))
                    {
                        rowPos.Y += rowHeight;
                    }

                    for (; rowTile.Y < this.tmxMap.Height; rowTile.Y++)
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
                if (TmxMath.DoStaggerY(this.tmxMap, startTile.Y))
                {
                    startPos.X -= columnWidth;
                }

                for (; startTile.Y < tmxMap.Height; startTile.Y++)
                {
                    Point rowTile = startTile;
                    Point rowPos = startPos;

                    if (TmxMath.DoStaggerY(this.tmxMap, startTile.Y))
                    {
                        rowPos.X += columnWidth;
                    }

                    for (; rowTile.X < this.tmxMap.Width; rowTile.X++)
                    {
                        points.Add(TmxMath.AddPoints(rowPos, oct[0]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[1]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[2]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[3]));
                        points.Add(TmxMath.AddPoints(rowPos, oct[4]));


                        bool isStaggered = TmxMath.DoStaggerY(this.tmxMap, startTile.Y);
                        bool lastRow = rowTile.Y == this.tmxMap.Height - 1;
                        bool lastColumn = rowTile.Y == this.tmxMap.Width - 1;
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
                RectangleF rc = new RectangleF(p.X, p.Y, Tiled2UnityViewer.GridSize, Tiled2UnityViewer.GridSize);
                rc.Offset(-Tiled2UnityViewer.GridSize * 0.5f, -Tiled2UnityViewer.GridSize * 0.5f);

                g.FillRectangle(Brushes.White, rc);
                g.DrawRectangle(Pens.Black, rc.X, rc.Y, rc.Width, rc.Height);
            }
        }

        private void DrawTiles(Graphics g)
        {
            // Load all our tiled images
            var images = from layer in this.tmxMap.Layers
                         where layer.Properties.GetPropertyValueAsBoolean("unity:collisionOnly", false) == false
                         where layer.Visible == true
                         where IsLayerEnabled(layer.DefaultName)
                         from y in Enumerable.Range(0, layer.Height)
                         from x in Enumerable.Range(0, layer.Width)
                         let tileId = layer.GetTileIdAt(x, y)
                         where tileId != 0
                         let tile = this.tmxMap.Tiles[tileId]
                         select new
                         {
                             Path = tile.TmxImage.Path,
                             Trans = tile.TmxImage.TransparentColor,
                         };
            images = images.Distinct();

            Dictionary<string, Bitmap> tileSetBitmaps = new Dictionary<string, Bitmap>();
            foreach (var img in images)
            {
                Bitmap bmp = (Bitmap)Bitmap.FromFile(img.Path);

                if (!String.IsNullOrEmpty(img.Trans))
                {
                    System.Drawing.Color transColor = System.Drawing.ColorTranslator.FromHtml(img.Trans);
                    bmp.MakeTransparent(transColor);
                }

                tileSetBitmaps.Add(img.Path, bmp);
            }

            foreach (TmxLayer layer in this.tmxMap.Layers)
            {
                if (layer.Visible == false)
                    continue;

                if (IsLayerEnabled(layer.DefaultName) == false)
                    continue;

                if (layer.Properties.GetPropertyValueAsBoolean("unity:collisionOnly", false) == true)
                    continue;

                // The range of x and y depends on the render order of the tiles
                // By default we draw right and down but may reverse the tiles we visit
                var range_x = Enumerable.Range(0, layer.Width);
                var range_y = Enumerable.Range(0, layer.Height);

                if (this.tmxMap.DrawOrderHorizontal == -1)
                    range_x = range_x.Reverse();

                if (this.tmxMap.DrawOrderVertical == -1)
                    range_y = range_y.Reverse();

                // Visit the tiles we are going to draw
                var tiles = from y in range_y
                            from x in range_x
                            let rawTileId = layer.GetRawTileIdAt(x, y)
                            let tileId = layer.GetTileIdAt(x, y)
                            where tileId != 0
                            
                            let tile = this.tmxMap.Tiles[tileId]

                            // Support for animated tiles. Just show the first frame of the animation.
                            let frame = (tile.Animation == null) ? tile : this.tmxMap.Tiles[tile.Animation.Frames[0].GlobalTileId]

                            select new
                            {
                                Tile = frame,
                                Position = TmxMath.TileCornerInScreenCoordinates(this.tmxMap, x, y),
                                Bitmap = tileSetBitmaps[frame.TmxImage.Path],
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
                    location.Y = (t.Position.Y - t.Tile.TileSize.Height) + this.tmxMap.TileHeight;

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
                    g.DrawImage(t.Bitmap, destPoints3, source, GraphicsUnit.Pixel);
                }
            }

            tileSetBitmaps.Clear();
        }

        private void DrawColliders(Graphics g)
        {
            for (int l = 0; l < this.tmxMap.Layers.Count; ++l)
            {
                TmxLayer layer = this.tmxMap.Layers[l];
                if (layer.Visible == true && IsLayerEnabled(layer.DefaultName))
                {
                    Color lineColor = this.preferencesForm.GetLayerColor(layer.DefaultName);
                    Color polyColor = Color.FromArgb(128, lineColor);
                    DrawLayerColliders(g, layer, polyColor, lineColor);
                }
            }
        }

        private void DrawLayerColliders(Graphics g, TmxLayer layer, Color polyColor, Color lineColor)
        {
            LayerClipper.TransformPointFunc xfFunc = (x,y) => new ClipperLib.IntPoint(x, y);
            LayerClipper.ProgressFunc progFunc = (prog) => { }; // do nothing

            ClipperLib.PolyTree solution = LayerClipper.ExecuteClipper(this.tmxMap, layer, xfFunc, progFunc);

            using (GraphicsPath path = new GraphicsPath())
            using (Pen pen = new Pen(lineColor, 1.0f))
            using (Brush brush = new HatchBrush(HatchStyle.ForwardDiagonal, lineColor, polyColor))
            {
                pen.Alignment = PenAlignment.Inset;

                // Draw all closed polygons
                foreach (var points in ClipperLib.Clipper.ClosedPathsFromPolyTree(solution))
                {
                    var pointfs = points.Select(pt => new PointF(pt.X, pt.Y));
                    path.AddPolygon(pointfs.ToArray());
                }
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

        private void DrawObjectColliders(Graphics g)
        {
            var collidersObjectGroup = from item in this.tmxMap.ObjectGroups
                                       where item.Visible == true
                                       where IsLayerEnabled(item.Name)
                                       select item;

            foreach (var objGroup in collidersObjectGroup)
            {
                foreach (var obj in objGroup.Objects)
                {
                    DrawObjectCollider(g, obj, objGroup.Color);
                }
            }
        }

        private void DrawObjectCollider(Graphics g, TmxObject tmxObject, Color color)
        {
            Color brushColor = Color.FromArgb(128, color);
            using (Brush brush = new HatchBrush(HatchStyle.BackwardDiagonal, color, brushColor))
            using (Pen pen = new Pen(color))
            {
                pen.Alignment = PenAlignment.Inset;

                GraphicsState state = g.Save();

                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(this.tmxMap, tmxObject.Position);
                g.TranslateTransform(xfPosition.X, xfPosition.Y);
                g.RotateTransform(tmxObject.Rotation);

                if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    DrawPolygon(g, pen, brush, tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromIsometricRectangle(this.tmxMap, tmxObject as TmxObjectRectangle);
                        DrawPolygon(g, pen, brush, tmxIsometricRectangle);
                    }
                    else
                    {
                        // Rectangles are polygons
                        DrawPolygon(g, pen, brush, tmxObject as TmxObjectPolygon);
                    }
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    DrawEllipse(g, pen, brush, tmxObject as TmxObjectEllipse);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    DrawPolyline(g, pen, tmxObject as TmxObjectPolyline);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;

                    RectangleF rcTile = new RectangleF();
                    rcTile.X = 0;
                    rcTile.Y = -tmxObjectTile.Tile.TileSize.Height;
                    rcTile.Size = tmxObjectTile.Tile.TileSize;

                    g.FillRectangle(brush, rcTile);
                    g.DrawRectangle(pen, rcTile.X, rcTile.Y, rcTile.Width - 1, rcTile.Height - 1);
                }
                else
                {
                    g.Restore(state);
                    RectangleF bounds = tmxObject.GetWorldBounds();
                    g.FillRectangle(Brushes.Red, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    g.DrawRectangle(Pens.White, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    string message = String.Format("Unhandled object: {0}", tmxObject.GetNonEmptyName());
                    DrawString(g, message, bounds.X, bounds.Y);
                }

                // Restore our state
                g.Restore(state);
            }
        }

        private void DrawPolygon(Graphics g, Pen pen, Brush brush, TmxObjectPolygon tmxPolygon)
        {
            var points = TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolygon).ToArray();
            g.FillPolygon(brush, points);
            g.DrawPolygon(pen, points);
        }

        private void DrawPolyline(Graphics g, Pen pen, TmxObjectPolyline tmxPolyline)
        {
            var points = TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolyline).ToArray();
            g.DrawLines(pen, points);
        }

        private void DrawEllipse(Graphics g, Pen pen, Brush brush, TmxObjectEllipse tmxEllipse)
        {
            RectangleF rc = new RectangleF(new PointF(0, 0), tmxEllipse.Size);
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                // Circles and ellipses not supported in Insometric mode
                g.FillEllipse(Brushes.Red, rc);
                g.DrawEllipse(Pens.White, rc);

                string message = String.Format(" Not supported (isometric): {0}", tmxEllipse.GetNonEmptyName());
                DrawString(g, message, rc.X + rc.Width * 0.5f, rc.Y + rc.Height * 0.5f);
            }
            else if (!tmxEllipse.IsCircle())
            {
                // We don't really support ellipses, especially as colliders
                g.FillEllipse(Brushes.Red, rc);
                g.DrawEllipse(Pens.White, rc);

                string message = String.Format(" Not supported (ellipse): {0}", tmxEllipse.GetNonEmptyName());
                DrawString(g, message, rc.X + rc.Width * 0.5f, rc.Y + rc.Height * 0.5f);
            }
            else
            {
                g.FillEllipse(brush, rc);
                g.DrawEllipse(pen, rc);
            }
        }

        private void DrawString(Graphics g, string text, float x, float y)
        {
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x - 1, y - 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x , y - 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x + 1, y - 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x + 1, y);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x + 1, y + 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x, y + 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x - 1, y + 1);
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.Black, x - 1, y);
         
            g.DrawString(text, SystemFonts.DefaultFont, Brushes.White, x, y);
        }

        private void saveImageAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "PNG files (*.png)|*.png";
            dialog.RestoreDirectory = true;
            dialog.FileName = String.Format("Preview_{0}.png", this.tmxMap.Name);
            dialog.InitialDirectory = Properties.Settings.Default.LastPreviewDirectory;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.pictureBoxViewer.Image.Save(dialog.FileName);

                Properties.Settings.Default.LastPreviewDirectory = dialog.FileName;
                Properties.Settings.Default.Save();
            }
        }

        private void Tiled2UnityViewer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                Clipboard.SetImage(this.pictureBoxViewer.Image);
            }
        }

        private void copyToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.WriteVerbose("Copied preview to keyboard (can also Ctrl-C)");
            Clipboard.SetImage(this.pictureBoxViewer.Image);
        }

        private void view18ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 0.125f;
            CreateAndShowBitmap();
        }

        private void view14ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 0.25f;
            CreateAndShowBitmap();
        }

        private void view12ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 0.5f;
            CreateAndShowBitmap();
        }

        private void view100ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 1.0f;
            CreateAndShowBitmap();
        }

        private void view200ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 2.0f;
            CreateAndShowBitmap();
        }

        private void view400ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 4.0f;
            CreateAndShowBitmap();
        }

        private void view800ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.scale = 8.0f;
            CreateAndShowBitmap();
        }

        private bool IsLayerEnabled(string name)
        {
            return this.preferencesForm.GetLayerPreviewing(name);
        }

        private void previewOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPreferencesForm();
        }

        private void OpenPreferencesForm()
        {
            this.preferencesForm.Show(this);
        }

        void preferencesForm_ApplyChanges()
        {
            CreateAndShowBitmap();
        }

        private void Tiled2UnityViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.preferencesForm.ApplyChanges -= preferencesForm_ApplyChanges;
            this.preferencesForm.Close();
        }

    } // end class
} // end namespace
