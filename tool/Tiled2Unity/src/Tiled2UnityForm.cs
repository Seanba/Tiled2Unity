using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Tiled2Unity
{
    public partial class Tiled2UnityForm : Form
    {
        private static readonly string Tiled2UnityExportHelperFilter = "Tiled2Unity export|Tiled2Unity.export.txt";
        private static readonly string Tiled2UnityExportHelperFile = "Tiled2Unity.export.txt";

        private string[] args = null;
        private TmxMap tmxMap = null;
        private TiledMapExporter tmxExporter = null;

        private List<string> warnings = new List<string>();
        private List<string> errors = new List<string>();

        public Tiled2UnityForm(string[] args)
        {
            this.args = args;
            InitializeComponent();

            Program.OnWriteLine += new Program.WriteLineDelegate(Program_OnWriteLine);
            Program.OnWriteWarning += new Program.WriteWarningDelegate(Program_OnWriteWarning);
            Program.OnWriteError += new Program.WriteErrorDelegate(Program_OnWriteError);
            Program.OnWriteSuccess += new Program.WriteSuccessDelegate(Program_OnWriteSuccess);
            Program.OnWriteVerbose += new Program.WriteVerboseDelegate(Program_OnWriteVerbose);

            TmxMap.OnReadTmxFileCompleted += new TmxMap.ReadTmxFileCompleted(TmxMap_OnReadTmxFileCompleted);
        }

        ~Tiled2UnityForm()
        {
            Program.OnWriteLine -= new Program.WriteLineDelegate(Program_OnWriteLine);
            Program.OnWriteWarning -= new Program.WriteWarningDelegate(Program_OnWriteWarning);
            Program.OnWriteError -= new Program.WriteErrorDelegate(Program_OnWriteError);
            Program.OnWriteSuccess -= new Program.WriteSuccessDelegate(Program_OnWriteSuccess);
            Program.OnWriteVerbose -= new Program.WriteVerboseDelegate(Program_OnWriteVerbose);

            TmxMap.OnReadTmxFileCompleted -= new TmxMap.ReadTmxFileCompleted(TmxMap_OnReadTmxFileCompleted);
        }

        // Where we do all the work
        // Updates will be triggered from within this fuction
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            this.warnings.Clear();
            this.errors.Clear();

            bool success = Program.ParseOptions(this.args);
            if (success && !Program.Help)
            {
                // Are we updating the last export directory?
                if (!String.IsNullOrEmpty(Program.ExportUnityProjectDir))
                {
                    Properties.Settings.Default.LastExportDirectory = Program.ExportUnityProjectDir;
                    Properties.Settings.Default.Save();
                }

                // Set the vertex scale
                this.textBoxScale.Text = Program.Scale.ToString();

                // Open the TMX file
                OpenTmxFile(Program.TmxPath);

                if (Program.AutoExport)
                {
                    Program.WriteLine("Automatically exporting: {0}", Program.ExportUnityProjectDir);
                    this.tmxExporter.Export(Program.ExportUnityProjectDir);
                    Close();
                }
            }
            else
            {
                // Repeat any errors
                foreach (string error in this.errors)
                {
                    WriteText(error, Color.Red);
                }
            }
        }

        private void OpenTmxFile(string tmxPath)
        {
            this.warnings.Clear();
            this.errors.Clear();

            this.buttonFolderBrowser.Enabled = false;
            this.buttonViewer.Enabled = false;
            this.buttonExport.Enabled = false;

            try
            {
                this.tmxMap = TmxMap.LoadFromFile(tmxPath);
                this.tmxExporter = new TiledMapExporter(this.tmxMap);
                CheckExportButton();
                ReportSummary("Compilation complete");
            }
            catch (TmxException tmx)
            {
                Program.WriteError(tmx.Message);
            }
        }

        void TmxMap_OnReadTmxFileCompleted(TmxMap tmxMap)
        {
            this.buttonFolderBrowser.Enabled = true;
            this.buttonViewer.Enabled = true;
            CheckExportButton();
        }

        void CheckExportButton()
        {
            bool exportPathExists = false;

            string exportPath = this.textBoxExportFolder.Text;
            if (Directory.Exists(exportPath))
            {
                exportPathExists = true;
            }

            this.buttonExport.Enabled = (this.tmxExporter != null) && exportPathExists;
        }

        private void ReportSummary(string header)
        {
            WriteText("----------------------------------------\n");
            Color complete = Color.Lime;
            if (this.errors.Count() > 0)
                complete = Color.Red;
            else if (this.warnings.Count() > 0)
                complete = Color.Yellow;

            WriteText(header + "\n", complete);

            WriteText(String.Format("Warnings: {0}\n", this.warnings.Count()));
            foreach (string warning in this.warnings)
            {
                WriteText(warning, Color.Yellow);
            }
            
            WriteText(String.Format("Errors: {0}\n", this.errors.Count()));
            foreach (string error in this.errors)
            {
                WriteText(error, Color.Red);
            }

            WriteText("----------------------------------------\n");
        }

        void Program_OnWriteLine(string line)
        {
            WriteText(line);
        }

        void Program_OnWriteWarning(string warning)
        {
            this.warnings.Add(warning);
            WriteText(warning, Color.Yellow);
        }

        void Program_OnWriteError(string error)
        {
            this.errors.Add(error);
            WriteText(error, Color.Red);
        }

        void Program_OnWriteSuccess(string success)
        {
            WriteText(success, Color.Lime);
        }

        void Program_OnWriteVerbose(string line)
        {
            WriteText(line, Color.Gray);
        }

        private void WriteText(string line)
        {
            WriteText(line, Color.White);
        }

        private void WriteText(string line, Color color)
        {
            // I'm tired of trying to get this to work, so just always scroll the textbox.
            this.richTextBoxOutput.AppendText(line, color);
            this.richTextBoxOutput.SelectionStart = this.richTextBoxOutput.TextLength;
            this.richTextBoxOutput.ScrollToCaret();
        }

        private void buttonFolderBrowser_Click(object sender, EventArgs e)
        {
            ChooseExportPath();
        }

        private void ChooseExportPath()
        {
            // Select a Tiled2Unity.export.txt file to let us know where to export files to
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = Tiled2UnityExportHelperFilter;
            dialog.Title = "Select Tiled2Unity Export File in Unity Project";
            dialog.InitialDirectory = Properties.Settings.Default.LastExportDirectory;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.RestoreDirectory = true;
            dialog.Multiselect = false;
            dialog.FileName = Tiled2UnityExportHelperFile;
            dialog.FileOk += new CancelEventHandler(ChooseExportPath_FileOk);

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.LastExportDirectory = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                Properties.Settings.Default.Save();
            }

        }

        void ChooseExportPath_FileOk(object sender, CancelEventArgs e)
        {
            OpenFileDialog dialog = sender as OpenFileDialog;
            if (dialog != null)
            {
                if (String.Compare(Tiled2UnityExportHelperFile, Path.GetFileName(dialog.FileName), true) != 0)
                {
                    string title = "Choose File: Tiled2Unity.export.txt";

                    StringBuilder message = new StringBuilder();
                    message.AppendLine("Choose the file named Tiled2Unity.export.txt in your Unity project.");
                    message.AppendLine("This is needed for Tiled2Unity to know where to export files to.");
                    message.AppendLine("\nexample: c:/MyUnityProject/Assets/Tiled2Unity/Tiled2Unity.export.txt");
                    MessageBox.Show(message.ToString(), title, MessageBoxButtons.OK);

                    e.Cancel = true;
                }
            }
        }

        private void buttonViewer_Click(object sender, EventArgs e)
        {
            Tiled2UnityViewer viewer = new Tiled2UnityViewer(this.tmxMap);
            viewer.ShowDialog();
        }

        private void textBoxExportFolder_TextChanged(object sender, EventArgs e)
        {
            CheckExportButton();
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            this.warnings.Clear();
            this.errors.Clear();
            string path = this.textBoxExportFolder.Text;
            this.tmxExporter.Export(path);
            ReportSummary("Export complete");
        }

        private void openTiledFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Properties.Settings.Default.LastOpenDirectory;
            dialog.Title = "Open Tiled (*.tmx) File";
            dialog.Filter = "TMX files (*.tmx)|*.tmx";
            dialog.RestoreDirectory = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.LastOpenDirectory = Path.GetDirectoryName(dialog.FileName);
                Properties.Settings.Default.Save();
                OpenTmxFile(dialog.FileName);
            }
        }

        private void clearOutputWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.richTextBoxOutput.Text = "";
            Update();
        }

        private void showHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.PrintHelp();
        }

        private void aboutTiled2UnityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var about = new Tiled2UnityAbout();
            about.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void addUnityPackageToProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string folder = Path.GetDirectoryName(path);
            string package = Path.Combine(folder, "Tiled2Unity.unitypackage");
            try
            {
                System.Diagnostics.Process.Start(package);
            }
            catch (Exception ex)
            {
                string msg = String.Format("There was an importing Tiled2Unity.unitypackage with command:\n  {0}\n\nError: {1}", package, ex.Message);
                MessageBox.Show(msg, "Tiled2Unity Error");
            }
        }

        private void textBoxScale_Validating(object sender, CancelEventArgs e)
        {
            bool good = false;

            float scale = Program.Scale;
            if (Single.TryParse(this.textBoxScale.Text, out scale))
            {
                // Is the scale greater than 0?
                if (scale > 0)
                {
                    good = true;
                }
            }

            if (good)
            {
                Program.Scale = scale;
                Properties.Settings.Default.LastVertexScale = Program.Scale;
                Properties.Settings.Default.Save();
            }
            else
            {
                // Set to 1.0
                this.textBoxScale.Text = "1.0";
            }
        }

        private void supportTiledMapEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SupportTiledForm form = new SupportTiledForm();
            form.ShowDialog();
        }

        private void richTextBoxOutput_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.LinkText);
        }

        private void donateToTiled2UnityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.seanba.com/donate");
        }

        private void checkBoxPreferConvexPolygons_CheckedChanged(object sender, EventArgs e)
        {
            Program.PreferConvexPolygons = this.checkBoxPreferConvexPolygons.Checked;
            Properties.Settings.Default.Save();
        }

    }
}

public static class RichTextBoxExtensions
{
    public static void AppendText(this RichTextBox box, string text, Color color)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;

        box.SelectionColor = color;
        box.AppendText(text);
        box.SelectionColor = box.ForeColor;
    }
}
