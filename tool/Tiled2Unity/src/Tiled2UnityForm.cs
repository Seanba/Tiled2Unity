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

using Ookii.Dialogs;

namespace Tiled2Unity
{
    public partial class Tiled2UnityForm : Form
    {
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

            if (success)
            {
                // Are we updating the last export directory?
                if (!String.IsNullOrEmpty(Program.ExportUnityProjectDir))
                {
                    Properties.Settings.Default.LastExportDirectory = Program.ExportUnityProjectDir;
                    Properties.Settings.Default.Save();
                }

                OpenTmxFile(Program.TmxPath);

                if (Program.AutoExport)
                {
                    Program.WriteLine("Automatically exporting: {0}", Program.ExportUnityProjectDir);
                    this.tmxExporter.Export(Program.ExportUnityProjectDir);
                    Close();
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
                ReportSummary();
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

        private void ReportSummary()
        {
            WriteText("----------------------------------------\n");
            Color complete = Color.Lime;
            if (this.errors.Count() > 0)
                complete = Color.Red;
            else if (this.warnings.Count() > 0)
                complete = Color.Yellow;

            WriteText("Compilation complete\n", complete);

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

        // Interop and Win32 API tricks to get text box selection to work like VisualStudio Output window
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, Int32 wParam, Int32 lParam);
        const int WM_USER = 0x400;
        const int EM_HIDESELECTION = WM_USER + 63;

        private void WriteText(string line, Color color)
        {
            // I'm tired of trying to get this to work, so just always scroll the textbox.
            this.richTextBoxOutput.AppendText(line, color);
            this.richTextBoxOutput.SelectionStart = this.richTextBoxOutput.TextLength;
            this.richTextBoxOutput.ScrollToCaret();

            /*// Code lifted from: http://stackoverflow.com/questions/626988/prevent-autoscrolling-in-c-sharp-richtextbox/627265#627265
            bool focused = this.richTextBoxOutput.Focused;
            //backup initial selection
            int selection = this.richTextBoxOutput.SelectionStart;
            int length = this.richTextBoxOutput.SelectionLength;
            //allow autoscroll if selection is at end of text
            bool autoscroll = (selection == this.richTextBoxOutput.Text.Length);

            if (!autoscroll)
            {
                //shift focus from RichTextBox to some other control
                if (focused) this.richTextBoxOutput.Focus();
                //hide selection
                SendMessage(this.richTextBoxOutput.Handle, EM_HIDESELECTION, 1, 0);
            }
            this.richTextBoxOutput.AppendText(line, color);

            if (!autoscroll)
            {
                //restore initial selection
                this.richTextBoxOutput.SelectionStart = selection;
                this.richTextBoxOutput.SelectionLength = length;
                //unhide selection
                SendMessage(this.richTextBoxOutput.Handle, EM_HIDESELECTION, 0, 0);
                //restore focus to RichTextBox
                if (focused) this.richTextBoxOutput.Focus();
            }*/
        }

        private void buttonFolderBrowser_Click(object sender, EventArgs e)
        {
            ChooseExportPath();
        }

        private void ChooseExportPath()
        {
            Ookii.Dialogs.VistaFolderBrowserDialog dlg = new VistaFolderBrowserDialog();
            dlg.SelectedPath = Properties.Settings.Default.LastExportDirectory;
            dlg.ShowNewFolderButton = false;

            dlg.OnFolderSelected += new VistaFolderBrowserDialog.FolderSelectedHandler(dlg_OnFolderSelected);

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.LastExportDirectory = Path.GetFullPath(dlg.SelectedPath);
                Properties.Settings.Default.Save();
            }
        }

        void dlg_OnFolderSelected(VistaFolderBrowserDialog dlg, string path)
        {
            dlg.SetTitle("Open existing project");

            // Path may be empty. In this case we have not selected a folder.
            bool enableOk = false;
            if (String.IsNullOrEmpty(path))
            {
                dlg.SetDescription("Select project folder to open");
            }
            else
            {
                bool isUnity = true;
                isUnity = isUnity && Directory.Exists(path);
                isUnity = isUnity && Directory.Exists(Path.Combine(path, "Assets"));

                if (isUnity)
                {
                    dlg.SetDescription("");
                    enableOk = true;
                }
                else
                {
                    String msg = String.Format("Selected folder is not a Unity project");
                    dlg.SetDescription(msg);
                }
            }

            dlg.EnableOk(enableOk);
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

        private void buttonExport_Click_1(object sender, EventArgs e)
        {
            string path = this.textBoxExportFolder.Text;
            this.tmxExporter.Export(path);
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
            System.Diagnostics.Process.Start(package);
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
