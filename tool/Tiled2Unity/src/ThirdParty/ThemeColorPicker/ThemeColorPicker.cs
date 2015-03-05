using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace System.Windows.Forms
{
    [DefaultEvent("ColorSelected")]
    public partial class ThemeColorPicker : UserControl
    {
        int[] _customColors = new int[0];

        public int[] CustomColors { get { return _customColors; } set { _customColors = value; } }

        Color _c;
        Dictionary<string, Color> dic;

        public delegate void colorSelected(object sender, ColorSelectedArg e);

        /// <summary>
        /// Occur when a color is selected.
        /// </summary>
        public event colorSelected ColorSelected;

        public delegate void moreColorWindowShow(object sender);

        /// <summary>
        /// Don't show pre-configure default Color Dialog
        /// </summary>
        public event moreColorWindowShow ShowCustomMoreColorWindow;

        public Color Color
        {
            get
            {
                return _c;
            }
            set
            {
                _c = value;
                if (ColorSelected != null)
                {
                    ColorSelectedArg arg = new ColorSelectedArg(_c);
                    ColorSelected(this, arg);
                }
            }
        }

        public ThemeColorPicker()
        {
            InitializeComponent();

            dic = new Dictionary<string, Color>();

            dic["p01"] = Color.FromArgb(255, 255, 255);
            dic["p02"] = Color.FromArgb(242, 242, 242);
            dic["p03"] = Color.FromArgb(216, 216, 216);
            dic["p04"] = Color.FromArgb(191, 191, 191);
            dic["p05"] = Color.FromArgb(165, 165, 165);
            dic["p06"] = Color.FromArgb(127, 127, 127);

            dic["p11"] = Color.FromArgb(0, 0, 0);
            dic["p12"] = Color.FromArgb(127, 127, 127);
            dic["p13"] = Color.FromArgb(89, 89, 89);
            dic["p14"] = Color.FromArgb(63, 63, 63);
            dic["p15"] = Color.FromArgb(38, 38, 38);
            dic["p16"] = Color.FromArgb(12, 12, 12);

            dic["p21"] = Color.FromArgb(238, 236, 225);
            dic["p22"] = Color.FromArgb(221, 217, 195);
            dic["p23"] = Color.FromArgb(196, 189, 151);
            dic["p24"] = Color.FromArgb(147, 137, 83);
            dic["p25"] = Color.FromArgb(73, 68, 41);
            dic["p26"] = Color.FromArgb(29, 27, 16);

            dic["p31"] = Color.FromArgb(31, 73, 125);
            dic["p32"] = Color.FromArgb(198, 217, 240);
            dic["p33"] = Color.FromArgb(141, 179, 226);
            dic["p34"] = Color.FromArgb(84, 141, 212);
            dic["p35"] = Color.FromArgb(23, 54, 93);
            dic["p36"] = Color.FromArgb(15, 36, 62);

            dic["p41"] = Color.FromArgb(79, 129, 189);
            dic["p42"] = Color.FromArgb(198, 217, 240);
            dic["p43"] = Color.FromArgb(184, 204, 228);
            dic["p44"] = Color.FromArgb(149, 179, 215);
            dic["p45"] = Color.FromArgb(54, 96, 146);
            dic["p46"] = Color.FromArgb(36, 64, 97);

            dic["p51"] = Color.FromArgb(192, 80, 77);
            dic["p52"] = Color.FromArgb(242, 220, 219);
            dic["p53"] = Color.FromArgb(229, 185, 183);
            dic["p54"] = Color.FromArgb(217, 150, 148);
            dic["p55"] = Color.FromArgb(140, 51, 49);
            dic["p56"] = Color.FromArgb(99, 36, 35);

            dic["p61"] = Color.FromArgb(155, 187, 89);
            dic["p62"] = Color.FromArgb(235, 241, 221);
            dic["p63"] = Color.FromArgb(215, 227, 188);
            dic["p64"] = Color.FromArgb(195, 214, 155);
            dic["p65"] = Color.FromArgb(118, 146, 60);
            dic["p66"] = Color.FromArgb(79, 97, 40);

            dic["p71"] = Color.FromArgb(128, 100, 162);
            dic["p72"] = Color.FromArgb(229, 224, 236);
            dic["p73"] = Color.FromArgb(204, 193, 217);
            dic["p74"] = Color.FromArgb(178, 162, 199);
            dic["p75"] = Color.FromArgb(95, 73, 122);
            dic["p76"] = Color.FromArgb(63, 49, 81);

            dic["p81"] = Color.FromArgb(75, 172, 198);
            dic["p82"] = Color.FromArgb(219, 238, 243);
            dic["p83"] = Color.FromArgb(183, 221, 232);
            dic["p84"] = Color.FromArgb(146, 205, 220);
            dic["p85"] = Color.FromArgb(49, 133, 155);
            dic["p86"] = Color.FromArgb(32, 88, 103);

            dic["p91"] = Color.FromArgb(247, 150, 70);
            dic["p92"] = Color.FromArgb(253, 234, 218);
            dic["p93"] = Color.FromArgb(251, 213, 181);
            dic["p94"] = Color.FromArgb(250, 192, 143);
            dic["p95"] = Color.FromArgb(171, 82, 7);
            dic["p96"] = Color.FromArgb(151, 72, 6);

            dic["p07"] = Color.FromArgb(192,0,0);
            dic["p17"] = Color.FromArgb(255,0,0);
            dic["p27"] = Color.FromArgb(255,192,0);
            dic["p37"] = Color.FromArgb(255,255,0);
            dic["p47"] = Color.FromArgb(146,208,80);
            dic["p57"] = Color.FromArgb(0,176,80);
            dic["p67"] = Color.FromArgb(0,176,240);
            dic["p77"] = Color.FromArgb(0,108,186);
            dic["p87"] = Color.FromArgb(0,32,96);
            dic["p97"] = Color.FromArgb(112,48,160);

            this.SuspendLayout();
            for (int i = 1; i < 10; i++)
            {
                for (int j = 1; j < 8; j++)
                {
                    Panel p = new Panel();
                    p.Name = "p" + i + "" + j;
                    p.Size = new Size(13, 13);
                    Point pt = ((Control[])this.Controls.Find("p" + (i - 1) + "" + j, false))[0].Location;
                    p.Location = new Point(pt.X + 4 + 13, pt.Y);
                    p.BackColor = System.Drawing.Color.Transparent;
                    p.MouseClick += new MouseEventHandler(p_MouseClick);
                    p.Cursor = System.Windows.Forms.Cursors.Hand;
                    this.Controls.Add(p);
                }
            }
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        void p_MouseClick(object sender, MouseEventArgs e)
        {
            this.Color = dic[((Control)sender).Name];
        }

        private void pnMoreColor_MouseClick(object sender, MouseEventArgs e)
        {
            if (ShowCustomMoreColorWindow != null)
            {
                ShowCustomMoreColorWindow(this);
            }
            else
            {
                ShowMoreColor();
            }
        }

        public virtual void ShowMoreColor()
        {
            ColorDialog cd = new ColorDialog();
            cd.AllowFullOpen = true;
            cd.FullOpen = true;
            cd.Color = _c;
            cd.CustomColors = _customColors;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                this.Color = cd.Color;
                _customColors = cd.CustomColors;
            }
        }
    }
}
