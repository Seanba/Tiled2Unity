using AppKit;
using Foundation;

using System;

namespace Tiled2UnityMac
{
	[Register ("AppDelegate")]
	public class AppDelegate : NSApplicationDelegate
	{
		public AppDelegate ()
		{
		}

		public override void DidFinishLaunching (NSNotification notification)
		{
			// Insert code here to initialize your application
		}

		public override void WillTerminate (NSNotification notification)
		{
			// Insert code here to tear down your application
		}
			
		/* Having this code here attempts to open a file for each command line argument given
		public override bool OpenFile (NSApplication sender, string filename)
		{
			// Trap all errors
			try {
				filename = filename.Replace (" ", "%20");
				return OpenFile(filename);
			} catch {
				return false;
			}
		}

		public bool OpenFile (string tmxPath)
		{
			Tiled2UnityMac.ViewController controller = NSApplication.SharedApplication.MainWindow.ContentViewController as Tiled2UnityMac.ViewController;
			return controller.OpenTmxFile(tmxPath);
		}*/

	}
}

