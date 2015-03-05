using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace System.Windows.Forms
{
    public partial class ThemeColorPickerWindow : Form
    {
        Color _c;

        public enum Action
        {
            HideWindow,
            CloseWindow,
            DoNothing
        }

        bool preventClose = false;

        Action _as = Action.CloseWindow;
        Action _al = Action.CloseWindow;

        public Action ActionAfterColorSelected { get { return _as; } set { _as = value; } }
        public Action ActionAfterLostFocus { get { return _al; } set { _al = value; } }

        public int[] CustomColors { get { return themeColorPicker1.CustomColors; } set { themeColorPicker1.CustomColors = value; } }

        public Color Color
        {
            get { return _c; }
            set
            {
                _c = value;
                if (ColorSelected != null)
                {
                    ColorSelectedArg arg = new ColorSelectedArg(_c);
                    ColorSelected(this, arg);
                }
                if (ActionAfterColorSelected == Action.HideWindow)
                    this.Visible = false;
                else if (ActionAfterColorSelected == Action.CloseWindow)
                    this.Close();
                else if (ActionAfterColorSelected == Action.DoNothing)
                { }
            }
        }

        public delegate void colorSelected(object sender, ColorSelectedArg e);

        /// <summary>
        /// Occur when a color is selected.
        /// </summary>
        public event colorSelected ColorSelected;

        /// <summary>
        /// Create a new window for ThemeColorPicker.
        /// </summary>
        /// <param name="startLocation">The starting position on screen. Note: This is not location in Form.</param>
        /// <param name="borderStyle">How the border should displayed.</param>
        /// <param name="actionAfterColorSelected">The form action of 0o-.</param>
        /// <param name="actionAfterLostFocus"></param>
        public ThemeColorPickerWindow(Point startLocation, FormBorderStyle borderStyle, Action actionAfterColorSelected, Action actionAfterLostFocus)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = startLocation;
            this.FormBorderStyle = borderStyle;
            this.ActionAfterColorSelected = actionAfterColorSelected;
            this.ActionAfterLostFocus = actionAfterLostFocus;
            this.LostFocus += new EventHandler(ThemeColorPickerWindow_LostFocus);
            this.Deactivate += new EventHandler(ThemeColorPickerWindow_Deactivate);
            themeColorPicker1.ShowCustomMoreColorWindow += new ThemeColorPicker.moreColorWindowShow(themeColorPicker1_ShowCustomMoreColorWindow);
        }

        void themeColorPicker1_ShowCustomMoreColorWindow(object sender)
        {
            preventClose = true;
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = true;
            cd.FullOpen = true;
            cd.Color = _c;
            cd.CustomColors = themeColorPicker1.CustomColors;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                this.Color = cd.Color;
                themeColorPicker1.CustomColors = cd.CustomColors;
            }
            preventClose = false;
        }

        void ThemeColorPickerWindow_Deactivate(object sender, EventArgs e)
        {
            if (preventClose)
                return;

            if (ActionAfterLostFocus == Action.HideWindow)
                this.Visible = false;
            else if (ActionAfterLostFocus == Action.CloseWindow)
                this.Close();
            else
            { }
        }

        void ThemeColorPickerWindow_LostFocus(object sender, EventArgs e)
        {
            if (ActionAfterLostFocus == Action.HideWindow)
                this.Visible = false;
            else if (ActionAfterLostFocus == Action.CloseWindow)
                this.Close();
            else
            { }
        }

        private void themeColorPicker1_ColorSelected(object sender, ColorSelectedArg e)
        {
            this.Color = e.Color;
        }
    }
}
