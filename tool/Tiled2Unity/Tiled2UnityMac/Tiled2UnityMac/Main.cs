using AppKit;

using System;
using System.Diagnostics;
using System.Drawing;

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
			if (isAuto) {
				// Run Tiled2UnityLite with no GUI
				return Tiled2Unity.Tiled2UnityLite.Run (args);
			}

			NSApplication.Init ();
			NSApplication.Main (args);

			return 0;
		}
	}
}
