using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

using AppKit;
using Foundation;
using SkiaSharp;

namespace Tiled2UnityMac
{
	public partial class ViewController : NSViewController
	{
		private static readonly string LastExportDirectory = "LastExportDirectory";
		private static readonly string LastObjectTypeXmlFile = "LastObjectTypeXmlFile";
		private static readonly string LastVertexScale = "LastVertexScale";
		private static readonly string LastPreferConvexPolygons = "LastPreferConvexPolygons";
		private static readonly string LastDepthBufferEnabled = "LastDepthBufferEnabled";

		// Remember where we last used the open panel depending on context
		private static readonly string LastOpenPanel_Tmx = "LastOpenPanel_Tmx";
		private static readonly string LastOpenPanel_ObjectTypeXml = "LastOpenPanel_ObjectTypeXml";
		private static readonly string LastOpenPanel_UnityDir = "LastOpenPanel_UnityDir";

		private Tiled2Unity.Session tmxSession = new Tiled2Unity.Session();
		
		public ViewController (IntPtr handle) : base (handle)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			// Do any additional setup after loading the view.
		}	

		public override NSObject RepresentedObject 
		{
			get 
			{
				return base.RepresentedObject;
			}
			set 
			{
				base.RepresentedObject = value;
				// Update the view, if already loaded.
			}
		}

		[Action("clearOutput:")]
		private void ClearOutput(NSObject sender)
		{
			this.TextViewOutput.Value = "";
		}
			

		[Action("openTiledFile:")]
		private void OpenTiledFile(NSObject sender)
		{
			var dlg = NSOpenPanel.OpenPanel;
			dlg.CanChooseDirectories = false;
			dlg.CanChooseFiles = true;
			dlg.AllowedFileTypes = new string[1] { "tmx" };
			dlg.Title = "Open Tiled (*.tmx) File";
			dlg.AllowsMultipleSelection = false;
			dlg.AllowsOtherFileTypes = false;

			RecallLastOpenPanelDirectory (dlg, ViewController.LastOpenPanel_Tmx);

			if (dlg.RunModal () == 1) {
				RememberLastOpenPanelDirectory (dlg.Directory, ViewController.LastOpenPanel_Tmx);
				string path = Path.GetFullPath (dlg.Filename);
				OpenTmxFile (path);
			}
		}

		private void RecallLastOpenPanelDirectory(NSOpenPanel dlg, string key)
		{
			string dir = NSUserDefaults.StandardUserDefaults.StringForKey(key);
			if (String.IsNullOrEmpty (dir)) {
				return;
			}

			if (!Directory.Exists(dir)) {
				return;
			}
			
			dlg.Directory = dir;
		}

		private void RememberLastOpenPanelDirectory(string dir, string key)
		{
			NSUserDefaults.StandardUserDefaults.SetString (dir, key);
		}

		public bool OpenTmxFile(string path)
		{
			this.tmxSession.LoadTmxFile (path);

			// Note: this isn't fully supported and is throwing an exception when the path has whitespace
			// Add to the recently opened menu
			//try
			//{
			//	NSUrl url = new NSUrl("file://" + path);
			//	NSDocumentController.SharedDocumentController.NoteNewRecentDocumentURL(url);
			//}
			//catch (Exception e)
			//{
			//	StringBuilder msg = new StringBuilder();
			//	msg.AppendFormat("Error creating path reference to: {0}\n", path);
			//	msg.AppendFormat("Exception: {0}\n", e.Message);
			//	Tiled2Unity.Logger.WriteError(msg.ToString());
			//}
			return true;
		}

		[Action("importUnityPackage:")]
		private void ImportUnityPackage(NSObject sender)
		{
			string package = "Tiled2Unity.unitypackage";
			string path = System.Reflection.Assembly.GetExecutingAssembly ().Location;
			string folder = Path.GetDirectoryName (path);
			folder = Path.Combine (folder, "../Resources");

			try
			{
				RunConsoleCommand("open", package, folder);
			}
			catch (Exception ex)
			{
				var alert = new NSAlert () {
					AlertStyle = NSAlertStyle.Critical,
					InformativeText = String.Format ("There was an error importing Tiled2Unity.unitypackage with command:\n {0}\nError: {1}", Path.Combine(folder, package), ex.Message),
					MessageText = "Error Importing Package. May need to reinstall Tiled2Unity."
						
				};
				alert.RunModal ();
			}
		}

		[Action("displayHelpOutput:")]
		private void DisplayHelpOutput(NSObject sender)
		{
			this.tmxSession.DisplayHelp ();
		}

		[Action("onlineDocs:")]
		private void OnlineDocs(NSObject sender)
		{
			RunConsoleCommand("open", "http://tiled2unity.readthedocs.io");
		}

		public override void ViewWillAppear ()
		{
			base.ViewWillAppear ();

			ShowTip ();

			// Ready to show the window. First set the title.
			this.View.Window.Title = "Tiled2UnityMac " + Tiled2Unity.Info.GetVersion ();
#if DEBUG
			this.View.Window.Title += " (Debug)";
#endif

			// Keep track of logging
			Tiled2Unity.Logger.OnWriteLine += Tiled2Unity_Logger_OnWriteLine;
			Tiled2Unity.Logger.OnWriteSuccess += Tiled2Unity_Logger_OnWriteSuccess;
			Tiled2Unity.Logger.OnWriteWarning += Tiled2Unity_Logger_OnWriteWarning;
			Tiled2Unity.Logger.OnWriteError += Tiled2Unity_Logger_OnWriteError;

			this.tmxSession.SetCulture ();
			InitializeSessionFromSettings ();

			// Initialize the Tiled2Unity session
			var args = Environment.GetCommandLineArgs ();
			var argl = new List<string> (args);
			if (argl.Count > 0) {
				// Remove the first argument as it is the name of the application
				argl.RemoveAt(0);
				args = argl.ToArray ();
			}
				
			this.tmxSession.InitializeWithArgs (args, true);

			InitializeUIFromSettings ();

			// Load the TMX file if it is ready.
			this.tmxSession.LoadInitialTmxFile();
		}

		private void ShowTip()
		{
			using (Stream stream = Assembly.GetExecutingAssembly ().GetManifestResourceStream ("Tiled2UnityMac.Resources.LaunchTipMac.rtf")) {
				var rtfData = NSData.FromStream (stream);
				NSDictionary nsDict = new NSDictionary ();
				var attributedString = NSAttributedString.CreateWithRTF (rtfData, out nsDict);
				this.TextFieldLaunchTip.AttributedStringValue = attributedString;
			}
		}

		private void InitializeSessionFromSettings()
		{
			// Initial assignment of settings based on NSUserDefaults
			string lastExportDir = NSUserDefaults.StandardUserDefaults.StringForKey(ViewController.LastExportDirectory);
			AssignExportFolder (lastExportDir);

			string objectTypeXml = NSUserDefaults.StandardUserDefaults.StringForKey (ViewController.LastObjectTypeXmlFile);
			AssignObjectTypeXml (objectTypeXml, false);

			float scale = NSUserDefaults.StandardUserDefaults.FloatForKey (ViewController.LastVertexScale);
			AssignScale (scale);

			bool convex = NSUserDefaults.StandardUserDefaults.BoolForKey (ViewController.LastPreferConvexPolygons);
			AssignPreferConvexPolygons (convex);

			bool depth = NSUserDefaults.StandardUserDefaults.BoolForKey (ViewController.LastDepthBufferEnabled);
			AssignDepthBufferEnabled (depth);
		}

		private void InitializeUIFromSettings()
		{
			// Set UI from settings that may have been changed via command arguments
			AssignExportFolder(this.tmxSession.UnityExportFolderPath);
			AssignObjectTypeXml (Tiled2Unity.Settings.ObjectTypeXml, false);
			AssignScale (Tiled2Unity.Settings.Scale);
			AssignPreferConvexPolygons (Tiled2Unity.Settings.PreferConvexPolygons);
			AssignDepthBufferEnabled (Tiled2Unity.Settings.DepthBufferEnabled);
		}

		private void AssignExportFolder(string path)
		{
			if (path == null)
				path = "";
			
			this.tmxSession.UnityExportFolderPath = path;
			this.TextFieldExportTo.StringValue = path;

			NSUserDefaults.StandardUserDefaults.SetString (path, ViewController.LastExportDirectory);
		}

		private void AssignObjectTypeXml(string path, bool load)
		{
			if (path == null)
				path = "";

			Tiled2Unity.Settings.ObjectTypeXml = path;
			this.TextViewObjectTypesXml.StringValue = path;

			if (load)
			{
				if (String.IsNullOrEmpty(path))
				{
					this.tmxSession.TmxMap.ClearObjectTypeXml();
				}
				else
				{
					this.tmxSession.TmxMap.LoadObjectTypeXml(path);
				}
			}

			NSUserDefaults.StandardUserDefaults.SetString (path, ViewController.LastObjectTypeXmlFile);
		}

		private void AssignScale(float scale)
		{
			// Scale must be greater than zero
			if (scale <= 0)
				scale = 1.0f;

			Tiled2Unity.Settings.Scale = scale;

			float inverse = 1.0f / scale;
			this.TextFieldScale.StringValue = inverse.ToString ();

			NSUserDefaults.StandardUserDefaults.SetFloat (scale, ViewController.LastVertexScale);
		}

		private void AssignPreferConvexPolygons(bool convex)
		{
			Tiled2Unity.Settings.PreferConvexPolygons = convex;

			NSCellStateValue state = convex ? NSCellStateValue.On : NSCellStateValue.Off;
			this.CheckButtonPreferConvexPolygons.State = state;

			NSUserDefaults.StandardUserDefaults.SetBool (convex, ViewController.LastPreferConvexPolygons);
		}

		private void AssignDepthBufferEnabled(bool depth)
		{
			Tiled2Unity.Settings.DepthBufferEnabled = depth;

			NSCellStateValue state = depth ? NSCellStateValue.On : NSCellStateValue.Off;
			this.CheckButtonDepthBufferEnabled.State = state;

			NSUserDefaults.StandardUserDefaults.SetBool (depth, ViewController.LastDepthBufferEnabled);
		}

		void Tiled2Unity_Logger_OnWriteError (string line)
		{
			var darkRed = NSColor.FromRgba (0.53f, 0, 0, 1);
			AppendTextView (line, darkRed);
		}

		void Tiled2Unity_Logger_OnWriteWarning (string line)
		{
			var darkOrange = NSColor.FromRgba (1.0f, 0.4f, 0, 1);	
			AppendTextView (line, darkOrange);
		}

		void Tiled2Unity_Logger_OnWriteSuccess (string line)
		{
			var darkGreen = NSColor.FromRgba (0, 0.4f, 0, 1);
			AppendTextView (line, darkGreen);
		}

		void Tiled2Unity_Logger_OnWriteLine (string line)
		{
			AppendTextView(line, NSColor.Black);
		}

		private void AppendTextView(string text, NSColor color)
		{
			// Capture output to a log to help with debugging
			// The log file location is an Apple standard
			string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library");
			logPath = Path.Combine(logPath, "Logs/Tiled2UnityMac/app.log");

			// Create the directory if it doesn't exist
			var pathInfo = new FileInfo(logPath);
			pathInfo.Directory.Create();

			using (StreamWriter log = File.AppendText(logPath))
			{
				log.Write(text);
			}

			// Append the text
			var attributed = new NSAttributedString (text);
			this.TextViewOutput.TextStorage.Append (attributed);
		
			// Set the text color
			var colorRange = new NSRange (this.TextViewOutput.Value.Length - text.Length, text.Length);
			this.TextViewOutput.SetTextColor (color, colorRange);

			// Scroll to the newly added text
			var scrollRange = new NSRange (this.TextViewOutput.Value.Length, 0);
			this.TextViewOutput.ScrollRangeToVisible (scrollRange);
		}
	

		partial void ClickedButton_Preview (NSObject sender)
		{
			// Create a preview image at a tempoary location and use the default program to view it
			string path = Path.GetTempPath();
			path += Path.GetRandomFileName();
			path += ".png";

			using (SKBitmap bitmap = Tiled2Unity.Viewer.PreviewImage.CreatePreviewBitmap(this.tmxSession.TmxMap))
			using (var image = SKImage.FromBitmap(bitmap))
			using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
			{
				// save the data to a stream
				using (var stream = File.OpenWrite(path))
				{
					data.SaveTo(stream);
				}
			}


			string args = String.Format("-a Preview \"{0}\"", path);
			RunConsoleCommand("open", args);
		}

		partial void ClickedButton_ObjectTypesXml (NSObject sender)
		{
			var dlg = NSOpenPanel.OpenPanel;
			dlg.CanChooseDirectories = false;
			dlg.CanChooseFiles = true;
			dlg.AllowedFileTypes = new string[1] { "xml" };
			dlg.Title = "Open Tiled Map Editor Object Types Xxml File";
			dlg.AllowsMultipleSelection = false;
			dlg.AllowsOtherFileTypes = false;

			RecallLastOpenPanelDirectory(dlg, ViewController.LastOpenPanel_ObjectTypeXml);

			if (dlg.RunModal () == 1) {
				RememberLastOpenPanelDirectory(dlg.Directory, ViewController.LastOpenPanel_ObjectTypeXml);
				AssignObjectTypeXml(dlg.Filename, true);
		
			}	
		}


		partial void ClickedButton_ClearObjectXml(NSObject sender)
		{
			AssignObjectTypeXml("", true);
		}

		partial void ClickedButton_ExportTo (NSObject sender)
		{
			var dlg = NSOpenPanel.OpenPanel;
			dlg.CanChooseDirectories = false;
			dlg.CanChooseFiles = true;
			dlg.AllowedFileTypes = new string[1] { "txt" };
			dlg.Title = "Select Tiled2Unity Export File in Unity Project";
			dlg.AllowsMultipleSelection = false;
			dlg.AllowsOtherFileTypes = false;

			RecallLastOpenPanelDirectory(dlg, ViewController.LastOpenPanel_UnityDir);

			if (dlg.RunModal () == 1) {

				RememberLastOpenPanelDirectory(dlg.Directory, ViewController.LastOpenPanel_UnityDir);

				if (ValidateExportFolder(dlg.Filename))
				{
					// Export okay
					string path = Path.GetDirectoryName(dlg.Filename);
					AssignExportFolder(path);
				}
			}			
		}

		private bool ValidateExportFolder(string path)
		{
			// We must select the Tiled2Unity.export.txt file in the Unity project
			string export = "Tiled2Unity.export.txt";
			if (String.Compare (export, Path.GetFileName (path), true) != 0) {
				string title = "Choose File: " + export;

				StringBuilder message = new StringBuilder ();
				message.AppendLine ("Choose the file named Tiled2Unity.export.txt in your Unity project");
				message.AppendLine ("This is needed for Tiled2Unity to know where to export files to.");
				message.AppendLine ("\nexample: /Users/JoeBlow/UnityProject/Assets/Tiled2Unity/Tiled2Unity.export.txt");
				var alert = new NSAlert () {
					AlertStyle = NSAlertStyle.Informational,
					InformativeText = message.ToString (),
					MessageText = title
				};
				alert.RunModal ();
				return false;
			}
			
			return true;
		}

		partial void TextChanged_Scale (NSObject sender)
		{
			float scale = 1.0f;
			float.TryParse(this.TextFieldScale.StringValue, out scale);

			float inverse = 1.0f / scale;
			AssignScale(inverse);
		}

		partial void ClickedButton_PreferConvexPolygons (NSObject sender)
		{
			bool convex = this.CheckButtonPreferConvexPolygons.State == NSCellStateValue.On;
			AssignPreferConvexPolygons(convex);
		}

		partial void ClickedButton_DepthBufferEnabled (NSObject sender)
		{
			bool depth = this.CheckButtonDepthBufferEnabled.State == NSCellStateValue.On;
			AssignDepthBufferEnabled(depth);
		}

		partial void ButtonClicked_BigAssExport (NSObject sender)
		{
			this.tmxSession.ExportTmxMap();
		}

		private static void RunConsoleCommand(string command, string args)
		{
			RunConsoleCommand (command, args, "");
		}

		private static void RunConsoleCommand(string command, string args, string workingDirectory)
		{
			var startInfo = new ProcessStartInfo {
				FileName = command,
				Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
			};

			if (!String.IsNullOrEmpty (workingDirectory)) {
				startInfo.WorkingDirectory = workingDirectory;
			}
			
			var proc = new Process {
				StartInfo = startInfo
			};

			proc.Start();
			while (!proc.StandardOutput.EndOfStream) {
				string line = proc.StandardOutput.ReadLine();
				Tiled2Unity.Logger.WriteLine (line);
			}

		}

	}
}
