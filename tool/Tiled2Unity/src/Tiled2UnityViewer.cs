using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SkiaSharp;

namespace Tiled2Unity
{
    public partial class Tiled2UnityViewer : Form
    {
        private TmxMap tmxMap = null;
        private float scale = 1.0f;

        public Tiled2UnityViewer(TmxMap tmxMap)
        {
            this.tmxMap = tmxMap;
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

            Properties.Settings.Default.LastPreviewScale = this.scale;
            Properties.Settings.Default.Save();

            this.Text = String.Format("Tiled2Unity Previewer (Scale = {0})", this.scale);

            // Convert a Skia image to a bitmap for viewing
            using (SKBitmap bitmap = Tiled2Unity.Viewer.PreviewImage.CreatePreviewBitmap(this.tmxMap, this.scale))
            {
                this.pictureBoxViewer.Image = SKImage.FromBitmap(bitmap).ToSystemDrawingBitmap();
            }
            Refresh();
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

    } // end class

    public static class SkiaImageExtensions
    {
        public static System.Drawing.Bitmap ToSystemDrawingBitmap(this SKImage skiaImage)
        {
            var bitmap = new System.Drawing.Bitmap(skiaImage.Width, skiaImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);

            // copy
            using (var pixmap = new SKPixmap(new SKImageInfo(data.Width, data.Height), data.Scan0, data.Stride))
            {
                skiaImage.ReadPixels(pixmap, 0, 0);
            }

            bitmap.UnlockBits(data);
            return bitmap;
        }
    }


} // end namespace
