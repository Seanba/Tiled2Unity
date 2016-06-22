using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Tiled2UnityLite is the 'automated export' version of Tiled2Unity
// It is expected to open a TMX file, export, and close on its own as a command-line utility
namespace Tiled2Unity
{
    public class Tiled2UnityLite
    {
        public static int Run(string[] args)
        {
            int error = 0;

            // If we get an error then that changes our error code
            Tiled2Unity.Logger.OnWriteError += delegate (string line)
            {
                error = 1;
            };

            // Run the session
            Tiled2Unity.Session tmxSession = new Session();
            tmxSession.SetCulture();

            if (tmxSession.InitializeWithArgs(args, false))
            {
                // Load the Tiled file (TMX)
                if (error == 0)
                {
                    tmxSession.LoadInitialTmxFile();
                }

                // Export the Tiled file to Unity
                if (error == 0)
                {
                    tmxSession.ExportTmxMap();
                }
            }

            return error;
        }
    }
}
