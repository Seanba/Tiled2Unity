using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// Color picker cell to be used in DataGridViewer control
// Inspired from: https://msdn.microsoft.com/en-us/library/7tas5c80(v=vs.110).aspx

namespace Tiled2Unity
{
    public class DataGridViewButtonCell_ColorPicker : DataGridViewButtonCell
    {
        public override Type ValueType { get {  return typeof(Color); } }
        public override object DefaultNewRowValue { get { return Color.WhiteSmoke; } }

        [DefaultValue(false)]
        public bool IsMouseOver { get; set; }

        private ThemeColorPickerWindow colorPickerWindow = null;

        public DataGridViewButtonCell_ColorPicker()
        {
            this.colorPickerWindow = new ThemeColorPickerWindow(new Point(this.ContentBounds.Location.X, this.ContentBounds.Location.Y + this.ContentBounds.Height),
                FormBorderStyle.None,
                ThemeColorPickerWindow.Action.HideWindow,
                ThemeColorPickerWindow.Action.HideWindow);

            this.colorPickerWindow.ColorSelected += new ThemeColorPickerWindow.colorSelected(colorPickerWindow_ColorSelected);
        }

        void colorPickerWindow_ColorSelected(object sender, ColorSelectedArg e)
        {
            this.Value = e.Color;
        }

        protected override void OnClick(DataGridViewCellEventArgs e)
        {
            base.OnClick(e);

            if (this.ReadOnly == false)
            {
                // Bring up the color picker
                Point pt = this.DataGridView.PointToScreen(this.DataGridView.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false).Location);
                pt.Y += this.ContentBounds.Height;
                this.colorPickerWindow.Location = pt;
                this.colorPickerWindow.Show();
            }
        }

        protected override void OnMouseEnter(int rowIndex)
        {
            base.OnMouseEnter(rowIndex);
            this.IsMouseOver = true;
            this.DataGridView.InvalidateRow(rowIndex);
        }

        protected override void OnMouseLeave(int rowIndex)
        {
            base.OnMouseLeave(rowIndex);
            this.IsMouseOver = false;
            this.DataGridView.InvalidateRow(rowIndex);
        }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates elementState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            //base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

            int borderInflate = 2;

            if (this.ReadOnly)
            {
                borderInflate = 4;
                graphics.FillRectangle(SystemBrushes.ControlDark, cellBounds);
            }
            else
            {
                graphics.FillRectangle(SystemBrushes.Control, cellBounds);
            }

            const int RightSectionWidth = 14;
            const int ArrowWidth = 5;
            const int ArrowHeight = 3;

            Rectangle rcRightSection = new Rectangle(cellBounds.Right - RightSectionWidth, cellBounds.Top, RightSectionWidth, cellBounds.Height - 1);
            Rectangle rcLeftSection = new Rectangle(cellBounds.Left, cellBounds.Top, cellBounds.Width - rcRightSection.Width, cellBounds.Height - 1);

            // Draw the selected color
            {
                Rectangle rcColor = rcLeftSection;
                rcColor.Inflate(-borderInflate, -borderInflate);

                Color colorSolid = (Color)this.Value;
                Color colorAlpha = Color.FromArgb(128, colorSolid);

                const float PenWidth = 3.0f;
                const int PenOffset = (int)PenWidth;

                using (Pen penSolid = new Pen(colorSolid, PenWidth))
                using (Brush brushAlpha = new SolidBrush(colorAlpha))
                {
                    penSolid.Alignment = PenAlignment.Inset;

                    graphics.FillRectangle(Brushes.White, rcColor);
                    graphics.FillRectangle(brushAlpha, rcColor);

                    Rectangle rcOutline = rcColor;
                    rcOutline.Offset(PenOffset, PenOffset);
                    rcOutline.Width -= PenOffset;
                    rcOutline.Height -= PenOffset;
                    graphics.DrawRectangle(Pens.Black, rcOutline);

                    graphics.DrawRectangle(penSolid, rcColor);
                }
            }

            // Draw the drop down arrow
            if (this.ReadOnly == false)
            {
                int arrowX = rcRightSection.X + (rcRightSection.Width / 2) - 1;
                int arrowY = cellBounds.Top + (cellBounds.Height / 2) - 1;

                Brush brush = SystemBrushes.ControlText;
                Point[] arrows = new Point[] { new Point(arrowX, arrowY), new Point(arrowX + ArrowWidth, arrowY), new Point(arrowX + ArrowWidth / 2, arrowY + ArrowHeight) };
                graphics.FillPolygon(brush, arrows);
            }

            if (this.ReadOnly == false && (this.IsMouseOver || this.Selected))
            {
                graphics.DrawRectangle(SystemPens.Highlight, rcRightSection);
                graphics.DrawRectangle(SystemPens.Highlight, cellBounds.Left, cellBounds.Top, cellBounds.Width, cellBounds.Height - 1);
            }
        }
    }
}
