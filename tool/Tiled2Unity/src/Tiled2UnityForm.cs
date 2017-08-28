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
    public partial class Tiled2UnityForm : Form
    {
        private static readonly string Tiled2UnityExportHelperFilter = "Tiled2Unity export|Tiled2Unity.export.txt";
        private static readonly string Tiled2UnityExportHelperFile = "Tiled2Unity.export.txt";
        private static readonly string Tiled2UnityObjectTypesXmlFilter = "Tiled Object Types XML|*.xml";

        private Tiled2Unity.Session tmxSession = new Session();

        public Tiled2UnityForm()
        {
            InitializeComponent();
            this.Text = String.Format("Tiled2Unity, {0} ({1})", Tiled2Unity.Info.GetVersion(), Tiled2Unity.Info.GetPlatform());

            Logger.OnWriteVerbose += new Logger.WriteVerboseDelegate(Tiled2UnityForm_OnWriteVerbose);
            Logger.OnWriteInfo += new Logger.WriteInfoDelegate(Tiled2UnityForm_OnWriteInfo);
            Logger.OnWriteWarning += new Logger.WriteWarningDelegate(Tiled2UnityForm_OnWriteWarning);
            Logger.OnWriteError += new Logger.WriteErrorDelegate(Tiled2UnityForm_OnWriteError);
            Logger.OnWriteSuccess += new Logger.WriteSuccessDelegate(Tiled2UnityForm_OnWriteSuccess);

            Settings.PreviewingDisabled += Settings_PreviewingDisabled;
        }

        ~Tiled2UnityForm()
        {
            Logger.OnWriteVerbose -= new Logger.WriteVerboseDelegate(Tiled2UnityForm_OnWriteVerbose);
            Logger.OnWriteInfo -= new Logger.WriteInfoDelegate(Tiled2UnityForm_OnWriteInfo);
            Logger.OnWriteWarning -= new Logger.WriteWarningDelegate(Tiled2UnityForm_OnWriteWarning);
            Logger.OnWriteError -= new Logger.WriteErrorDelegate(Tiled2UnityForm_OnWriteError);
            Logger.OnWriteSuccess -= new Logger.WriteSuccessDelegate(Tiled2UnityForm_OnWriteSuccess);

            Settings.PreviewingDisabled -= Settings_PreviewingDisabled;
        }

        private void Settings_PreviewingDisabled(object sender, EventArgs e)
        {
            Logger.WriteWarning("Disabling preview due to image library exceptions. You can still export.");
            this.buttonViewer.Enabled = false;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tiled2Unity.Resources.LaunchTip.rtf"))
            {
                this.richTextBoxLaunchTip.LoadFile(stream, RichTextBoxStreamType.RichText);
            }
        }

        // Where we do all the work
        // Updates will be triggered from within this fuction
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            this.tmxSession.SetCulture();

            InitializeSessionFromSettings();

            // Ready the TMX file. Command line overrides settings.
            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            this.tmxSession.InitializeWithArgs(args, true);

            InitializeUIFromSettings();

            // Load the TMX file if it is ready
            this.tmxSession.LoadInitialTmxFile();
        }

        private void InitializeSessionFromSettings()
        {
            this.tmxSession.UnityExportFolderPath = Properties.Settings.Default.LastExportDirectory;

            Tiled2Unity.Settings.ObjectTypeXml = Properties.Settings.Default.LastObjectTypeXmlFile;
            Tiled2Unity.Settings.Scale = Properties.Settings.Default.LastVertexScale;
            Tiled2Unity.Settings.PreferConvexPolygons = Properties.Settings.Default.LastPreferConvexPolygons;
            Tiled2Unity.Settings.DepthBufferEnabled = Properties.Settings.Default.LastDepthBufferEnabled;
        }

        private void InitializeUIFromSettings()
        {
            // Set the export path
            this.textBoxExportFolder.Text = this.tmxSession.UnityExportFolderPath;

            // Set the scale
            float inverse = 1.0f / Tiled2Unity.Settings.Scale;
            this.textBoxScale.Text = inverse.ToString();
            textBoxScale_Validating(null, null);
        }

        private void Tiled2UnityForm_OnWriteVerbose(string line)
        {
            WriteText(line);
        }

        private void Tiled2UnityForm_OnWriteInfo(string line)
        {
            WriteText(line);
        }

        private void Tiled2UnityForm_OnWriteWarning(string warning)
        {
            WriteText(warning, Color.DarkOrange);
        }

        private void Tiled2UnityForm_OnWriteError(string error)
        {
            WriteText(error, Color.DarkRed);
        }

        private void Tiled2UnityForm_OnWriteSuccess(string success)
        {
            WriteText(success, Color.DarkGreen);
        }

        private void WriteText(string line)
        {
            WriteText(line, SystemColors.WindowText);
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
                this.tmxSession.UnityExportFolderPath = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName));
                Properties.Settings.Default.LastExportDirectory = this.tmxSession.UnityExportFolderPath;
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
            if (this.tmxSession.TmxMap.IsLoaded)
            {
                Tiled2UnityViewer viewer = new Tiled2UnityViewer(this.tmxSession.TmxMap);
                viewer.ShowDialog();
            }
            else
            {
                Logger.WriteError("Tiled map is not loaded. Nothing to preview.");
            }
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            this.tmxSession.ExportTmxMap();
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
                this.tmxSession.LoadTmxFile(dialog.FileName);
            }
        }

        private void clearOutputWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.richTextBoxOutput.Text = "";
            Update();
        }

        private void showHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.tmxSession.DisplayHelp();
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
                string msg = String.Format("There was an error importing Tiled2Unity.unitypackage with command:\n  {0}\n\nError: {1}", package, ex.Message);
                MessageBox.Show(msg, "Tiled2Unity Error");
            }
        }

        private void textBoxScale_Validating(object sender, CancelEventArgs e)
        {
            bool good = false;

            float scale = Tiled2Unity.Settings.Scale;
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
                float inverse = 1 / scale;
                Tiled2Unity.Settings.Scale = inverse;
                Properties.Settings.Default.LastVertexScale = Tiled2Unity.Settings.Scale;
                Properties.Settings.Default.Save();
            }
            else
            {
                // Set to 1
                this.textBoxScale.Text = "1";
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

        private void onlineDocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://tiled2unity.readthedocs.io/en/latest");
        }

        private void checkBoxPreferConvexPolygons_CheckedChanged(object sender, EventArgs e)
        {
            Tiled2Unity.Settings.PreferConvexPolygons = this.checkBoxPreferConvexPolygons.Checked;
            Properties.Settings.Default.LastPreferConvexPolygons = Tiled2Unity.Settings.PreferConvexPolygons;
            Properties.Settings.Default.Save();
        }

        private void buttonObjectTypesXml_Click(object sender, EventArgs e)
        {
            // Select a Tiled2Unity.export.txt file to let us know where to export files to
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = Tiled2UnityObjectTypesXmlFilter;
            dialog.Title = "Open Tiled Map Editor Object Types XML Files";
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.RestoreDirectory = true;
            dialog.Multiselect = false;

            if (!String.IsNullOrEmpty(Properties.Settings.Default.LastObjectTypeXmlFile))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(Properties.Settings.Default.LastObjectTypeXmlFile);
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.LastObjectTypeXmlFile = Path.GetFullPath(dialog.FileName);
                Properties.Settings.Default.Save();
            }
        }

        private void textBoxObjectTypesXml_TextChanged(object sender, EventArgs e)
        {
            Tiled2Unity.Settings.ObjectTypeXml = this.textBoxObjectTypesXml.Text;
            this.tmxSession.TmxMap.LoadObjectTypeXml(this.textBoxObjectTypesXml.Text);
        }

        private void checkBoxDepthBuffer_CheckedChanged(object sender, EventArgs e)
        {
            Tiled2Unity.Settings.DepthBufferEnabled = this.checkBoxDepthBuffer.Checked;
            Properties.Settings.Default.LastDepthBufferEnabled = Tiled2Unity.Settings.DepthBufferEnabled;
            Properties.Settings.Default.Save();
        }

        private void buttonClearObjectTypes_Click(object sender, EventArgs e)
        {
            this.textBoxObjectTypesXml.Text = "";
            this.tmxSession.TmxMap.ClearObjectTypeXml();
            Properties.Settings.Default.LastObjectTypeXmlFile = "";
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

        if (color != SystemColors.WindowText)
        {
            box.SelectionBackColor = Color.LightGray;
        }
        box.SelectionColor = color;
        box.AppendText(text);
        box.SelectionColor = box.ForeColor;
    }
}
