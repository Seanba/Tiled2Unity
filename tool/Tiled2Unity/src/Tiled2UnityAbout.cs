using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Tiled2Unity
{
    public partial class Tiled2UnityAbout : Form
    {
        public Tiled2UnityAbout()
        {
            InitializeComponent();
            this.labelVersion.Text = String.Format("Tiled2Unity, {0}", Program.GetVersion());
            LoadAboutText();
        }

        private void LoadAboutText()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tiled2Unity.Resources.About.rtf"))
            {
                this.richTextBoxAbout.LoadFile(stream, RichTextBoxStreamType.RichText);
            }
        }

        private void buttonOkay_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void richTextBoxAbout_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.LinkText);
        }

    }

    // The old RichTextBox decorates URLs with angle brackets. Bleh.
    public class RichText50W : RichTextBox
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadLibrary(string lpFileName);
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams prams = base.CreateParams;
                if (LoadLibrary("msftedit.dll") != IntPtr.Zero)
                {
                    prams.ClassName = "RICHEDIT50W";
                }
                return prams;
            }
        }
    }
}
