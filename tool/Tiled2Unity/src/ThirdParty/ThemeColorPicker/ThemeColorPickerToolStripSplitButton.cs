using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Text;
using System.Drawing;

namespace ExHtmlEditor.ColorPicker
{
    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.All)]
    public class ThemeColorPickerToolStripSplitButton : ToolStripSplitButton
    {
        public delegate void colorSelected(object sender, ColorSelectedArg e);

        /// <summary>
        /// Occur when a color is selected.
        /// </summary>
        public event colorSelected ColorSelected;

        int _imageWidth = 32;
        int _imageHeight = 16;

        public int ImageWidth { get { return _imageWidth; } set { _imageWidth = value; } }
        public int ImageHeight { get { return _imageHeight; } set { _imageHeight = value; } }

        Image _img; 
        Color _c = Color.White;
        public Color Color
        {
            get
            {
                return _c;
            }
            set
            {
                _c = value;
                if (this.GetCurrentParent() != null)
                {
                    this.GetCurrentParent().SuspendLayout();
                }
                DrawImage();
                if (this.GetCurrentParent() != null)
                {
                    this.GetCurrentParent().ResumeLayout(false);
                    this.GetCurrentParent().PerformLayout();
                }
                if (ColorSelected != null)
                {
                    ColorSelectedArg arg = new ColorSelectedArg(_c);
                    ColorSelected(this, arg);
                }
            } 
        }

        public override Image Image
        {
            get
            {
                if (_img == null)
                {
                    DrawImage();
                }
                return _img;
            }
        }

        public override ToolStripItemDisplayStyle DisplayStyle { get { return ToolStripItemDisplayStyle.Image; } }
        protected override ToolStripItemDisplayStyle DefaultDisplayStyle { get { return ToolStripItemDisplayStyle.Image; } }

        ThemeColorPickerWindow f;
        public ThemeColorPickerToolStripSplitButton()
        {
            this.AutoSize = true;
            this.ImageScaling = ToolStripItemImageScaling.None;
            f = new ThemeColorPickerWindow(new Point(this.Bounds.Location.X, this.Bounds.Location.Y + this.Bounds.Height), FormBorderStyle.None, ThemeColorPickerWindow.Action.HideWindow, ThemeColorPickerWindow.Action.HideWindow);
            f.ColorSelected += new ThemeColorPickerWindow.colorSelected(f_ColorSelected);
            this.DropDownOpening += new EventHandler(ThemeColorPickerToolStripSpliButton_DropDownOpening);
        }

        void DrawImage()
        {
            if (_img== null)
                _img = new Bitmap(ImageWidth, ImageHeight);
            using (Graphics gfx = Graphics.FromImage(_img))
            {
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    gfx.FillRectangle(brush, 0, 0, ImageWidth, ImageHeight);
                    brush.Color = this.Color;
                    gfx.FillRectangle(brush, 1, 1, ImageWidth -2, ImageHeight-2);
                }
            }
        }

        void f_ColorSelected(object sender, ColorSelectedArg e)
        {
            this.Color = e.Color;
        }

        void ThemeColorPickerToolStripSpliButton_DropDownOpening(object sender, EventArgs e)
        {
            Point pt = this.GetCurrentParent().PointToScreen(new Point(this.Bounds.Location.X, this.Bounds.Location.Y + this.Bounds.Height));
            f.Location = pt;
            f.Show();
        }

        protected override void OnButtonClick(EventArgs e)
        {
            this.Color = this.Color;
            base.OnButtonClick(e);
        }
    }
}
