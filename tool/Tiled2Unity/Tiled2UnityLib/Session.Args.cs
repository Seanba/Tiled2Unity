using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Tiled2Unity
{
    partial class Session
    {
        private void ParseEnvironmentVariables()
        {
            string tmxPath = Environment.GetEnvironmentVariable("TILED2UNITY_TMXPATH");
            if (!String.IsNullOrEmpty(tmxPath))
            {
                Logger.WriteLine("Found TILED2UNITY_TMXPATH environment variable: {0}", tmxPath);
                this.TmxFilePath = tmxPath;
            }

            string unityDir = Environment.GetEnvironmentVariable("TILED2UNITY_UNITYDIR");
            if (!String.IsNullOrEmpty(unityDir))
            {
                Logger.WriteLine("Found TILED2UNITY_UNITYDIR environment variable: {0}", unityDir);
                this.TmxFilePath = tmxPath;
            }
        }

        // Returns true if the program is to keep going. Will return false if there is an error parsing options or if we were only interested in help/version information
        private bool ParseOptions(string[] args)
        {
            args = CleanseArgs(args);

            bool displayVersion = false;
            bool displayHelp = false;

            NDesk.Options.OptionSet options = new NDesk.Options.OptionSet()
            {
                { "o|object-type-xml=", "Supply an Object Type XML file for types and their properties", o => Tiled2Unity.Settings.ObjectTypeXml = !String.IsNullOrEmpty(o) ? Path.GetFullPath(o) : "" },
                { "s|scale=", "Scale the output vertices by a value.\nA value of 0.01 is popular for many Unity projects that use 'Pixels Per Unit' of 100 for sprites.\nDefault is 1 (no scaling).", s => Tiled2Unity.Settings.Scale = ParseFloatDefault(s, 1.0f) },
                { "c|convex", "Limit polygon colliders to be convex with no holes. Increases the number of polygon colliders in export. Can be overriden on map or layer basis with unity:convex property.", c => Tiled2Unity.Settings.PreferConvexPolygons = true },
                { "t|texel-bias=", "Bias for texel sampling.\nTexels are offset by 1 / value.\nDefault value is 8192.\n A value of 0 means no bias.", t => Tiled2Unity.Settings.TexelBias = ParseFloatDefault(t, Tiled2Unity.Settings.DefaultTexelBias) },
                { "d|depth-buffer", "Uses a depth buffer to render the layers of the map in order. Useful for sprites that may be drawn below or above map layers depending on location.", d => Tiled2Unity.Settings.DepthBufferEnabled = true },
                { "a|auto-export", "Automatically run exporter and exit. TMXPATH and UNITYDIR are not optional in this case.", a => Tiled2Unity.Settings.IsAutoExporting = true },
                { "w|writeable-vertices", "Exported meshes will have writable vertices. This increases the memory used by meshes significantly. Only use if you will mutate the vertices through scripting.", w => Tiled2Unity.Settings.WriteableVertices = true },
                { "v|version", "Display version information.", v => displayVersion = true },
                { "h|help", "Display this help message.", h => displayHelp = true },
            };

            // Parse the options
            List<string> extra = options.Parse(args);

            // Are we displaying the version?
            if (displayVersion)
            {
                Logger.WriteLine("{0} ({1}) version {2}", Tiled2Unity.Info.GetLibraryName(), Tiled2Unity.Info.GetPlatform(), Tiled2Unity.Info.GetVersion());
                return false;
            }

            // Are we displaying help?
            if (displayHelp)
            {
                PrintHelp(options);
                return false;
            }

            if (Tiled2Unity.Settings.IsAutoExporting)
            {
                Logger.WriteLine("Running automatic export.");
            }

            bool success = true;

            // If we're here then we're 'running' the program
            // First left over option is the TMX file we are exporting
            if (extra.Count() == 0)
            {
                Logger.WriteLine("Missing TMXPATH argument.");
                Logger.WriteLine("  If using the GUI, try opening a TMX file now");
                Logger.WriteLine("  If using the command line, provide a path to a TMX file");
                Logger.WriteLine("  If using from Tiled Map Editor, try adding %mapfile to the command");
            }
            else
            {
                this.TmxFilePath = Path.GetFullPath(extra[0]);

                if (!File.Exists(this.TmxFilePath))
                {
                    Logger.WriteError("TMXPATH file '{0}' does not exist.", this.TmxFilePath);
                    this.TmxFilePath = null;
                    success = false;
                }

                extra.RemoveAt(0);
            }

            // The next 'left over' option is the Tiled2Unity folder of the Unity project that we are exporting to
            if (extra.Count() > 0)
            {
                this.UnityExportFolderPath = Path.GetFullPath(extra[0]);

                if (String.IsNullOrEmpty(this.UnityExportFolderPath))
                {
                    Logger.WriteError("UNITYDIR argument is not a valid path '{0}'", extra[0]);
                    this.UnityExportFolderPath = null;
                    success = false;
                }
                else if (!Directory.Exists(this.UnityExportFolderPath))
                {
                    Logger.WriteError("UNITYDIR Unity Tiled2Unity Project Directory '{0}' does not exist", this.UnityExportFolderPath);
                    this.UnityExportFolderPath = null;
                    success = false;
                }
                else if (!File.Exists(Path.Combine(this.UnityExportFolderPath, "Tiled2Unity.export.txt")))
                {
                    Logger.WriteError("UNITYDIR '{0}' is not a Tiled2Unity Unity Project folder", this.UnityExportFolderPath);
                    this.UnityExportFolderPath = null;
                    success = false;
                }

                extra.RemoveAt(0);
            }

            // Do we have any other options left over? We shouldn't.
            if (extra.Count() > 0)
            {
                Logger.WriteError("Too many arguments. Can't parse '{0}'", extra[0]);
                success = false;
            }

            if (!success)
            {
                Logger.WriteError("Command line arguments: {0}", String.Join(" ", args));
                PrintHelp(options);
                return false;
            }

            return true;
        }

        private static void PrintHelp(NDesk.Options.OptionSet options)
        {
            Logger.WriteLine("{0} Utility, Version: {1}", Tiled2Unity.Info.GetLibraryName(), Tiled2Unity.Info.GetVersion());
            Logger.WriteLine("Usage: {0} [OPTIONS]+ TMXPATH [UNITYDIR]", Tiled2Unity.Info.GetLibraryName());
            Logger.WriteLine("Example: {0} -s=0.01 MyTiledMap.tmx ../../MyUnityProjectFolder/Assets/Tiled2Unity", Tiled2Unity.Info.GetLibraryName());
            Logger.WriteLine("");
            Logger.WriteLine("Options:");

            TextWriter writer = new StringWriter();
            options.WriteOptionDescriptions(writer);
            Logger.WriteLine(writer.ToString());

            Logger.WriteLine("Prefab object properties (set in TMX file for map or Tile/Object layer properties)");
            Logger.WriteLine("  unity:sortingLayerName");
            Logger.WriteLine("  unity:sortingOrder");
            Logger.WriteLine("  unity:layer");
            Logger.WriteLine("  unity:tag");
            Logger.WriteLine("  unity:scale");
            Logger.WriteLine("  unity:isTrigger");
            Logger.WriteLine("  unity:convex");
            Logger.WriteLine("  unity:ignore (value = [false|true|collision|visual])");
            Logger.WriteLine("  unity:resource (value = [false|true])");
            Logger.WriteLine("  unity:resourcePath");
            Logger.WriteLine("  unity:namePrefix (Add to tileset properties to prefix material names with this string.");
            Logger.WriteLine("  unity:namePostfix (Add to tileset properties to postfix material names with this string.");
            Logger.WriteLine("  (Other properties are exported for custom scripting in your Unity project)");
            Logger.WriteLine("Support Tiled Map Editor on Patreon: https://www.patreon.com/bjorn");
            Logger.WriteLine("Make a donation for Tiled2Unity: http://www.seanba.com/donate");
        }

        // Removes unwanted cruft from arguments
        private static string[] CleanseArgs(string[] args)
        {
            List<string> arguments = new List<string>(args);

#if TILED2UNITY_MAC
            // MacOSX adds "-psn_number_number" (process number) argument. Get rid of it.
            var regex = new Regex(@"-psn_\d+_\d+");
            arguments.RemoveAll(s => regex.IsMatch(s));
#endif

            return arguments.ToArray();

        }


        private static float ParseFloatDefault(string str, float defaultValue)
        {
            float resultValue = 0;
            if (float.TryParse(str, out resultValue))
            {
                return resultValue;
            }
            return defaultValue;
        }

    }

}
