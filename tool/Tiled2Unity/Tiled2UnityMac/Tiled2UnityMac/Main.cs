using AppKit;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace Tiled2UnityMac
{
	static class MainClass
	{
		static int Main (string[] args)
		{
			bool isAuto = false;
			NDesk.Options.OptionSet options = new NDesk.Options.OptionSet () {
				{ "a|auto-export", "Automatic export", a => isAuto = true }
			};

			options.Parse (args);
			if (isAuto)
			{
				// Note: We still have to initialize and terminate the application correctly
				NSApplication.Init();

				// Capture output to a log since we can't print out to the command line
				// The log file location is an Apple standard
				string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library");
				logPath = Path.Combine(logPath, "Logs/Tiled2UnityMac/auto-export.log");

				// Create the directory if it doesn't exis
				var pathInfo = new FileInfo(logPath);
				pathInfo.Directory.Create();

				// If we get an error then that changes our error code
				Tiled2Unity.Logger.OnWriteError += delegate (string line)
				{
					using (StreamWriter log = File.AppendText(logPath))
					{
						log.Write(line);
					}
				};

				// Also print out warnings
				Tiled2Unity.Logger.OnWriteWarning += delegate (string line)
				{
					using (StreamWriter log = File.AppendText(logPath))
					{
						log.Write(line);
					}
				};

				// Also write out success
				Tiled2Unity.Logger.OnWriteSuccess += delegate (string line)
				{
					using (StreamWriter log = File.AppendText(logPath))
					{
						log.Write(line);
					}
				};

				// And also write out general usage to log
				Tiled2Unity.Logger.OnWriteLine += delegate (string line)
				{
					using (StreamWriter log = File.AppendText(logPath))
					{
						log.Write(line);
					}
				};

				// Run Tiled2UnityLite with no GUI
				int error = Tiled2Unity.Tiled2UnityLite.Run (args);

				// Shutdown the "application" and return error to the command line
				NSApplication.SharedApplication.Terminate(NSApplication.SharedApplication);
				return error;
			}

			// Run the utility as normal with a full GUI
			NSApplication.Init ();
			NSApplication.Main (args);

			return 0;
		}
	}
}
