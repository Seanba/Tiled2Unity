using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// Color picker column to be used in DataGridViewer control
// Inspired from: https://msdn.microsoft.com/en-us/library/7tas5c80(v=vs.110).aspx

namespace Tiled2Unity
{
    public class DataGridViewColumn_ColorPicker : DataGridViewColumn
    {
        public DataGridViewColumn_ColorPicker() : base(new DataGridViewButtonCell_ColorPicker()) { }

        public override DataGridViewCell CellTemplate
        {
            get
            {
                return base.CellTemplate;
            }
            set
            {
                // Ensure that the cell used for the template is a CalendarCell. 
                if (value != null && !value.GetType().IsAssignableFrom(typeof(DataGridViewButtonCell_ColorPicker)))
                {
                    throw new InvalidCastException("Must be a ColorCell");
                }
                base.CellTemplate = value;
            }
        }
    }
}
