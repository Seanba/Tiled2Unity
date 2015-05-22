using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Tiled2Unity
{
    public partial class SupportTiledForm : Form
    {
        public SupportTiledForm()
        {
            InitializeComponent();
            LoadSupportText();
        }

        private void LoadSupportText()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tiled2Unity.Resources.TiledOnPatreon.rtf"))
            {
                this.pleaRichTextBox.LoadFile(stream, RichTextBoxStreamType.RichText);
            }
        }

        private void patreonLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(this.patreonLinkLabel.Text);
        }

    }
}
