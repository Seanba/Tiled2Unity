using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // A Tiled2Unity Session is made up of
    // a) A TMX file (which results in a TMX object)
    // b) An export directory
    public partial class Session
    {
        public string TmxFilePath { get; private set; }
        public string UnityExportFolderPath { get; set; }
        public TmxMap TmxMap { get; private set; }

        private SummaryReport summaryReport = new SummaryReport();

        public void SetCulture()
        {
            // Force decimal numbers to use '.' as the decimal separator
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
        }

        public bool InitializeWithArgs(string[] args, bool summary)
        {
            Logger.WriteLine("Command: {0}", String.Join(" ", args));

            // Create our map instance (which is empty/unloaded at first)
            this.TmxMap = new TmxMap();

            // Parse the arguments and let our listeners know in case any settings changed
            if (summary)
            {
                this.summaryReport.Capture("Arguments");
            }

            ParseEnvironmentVariables();
            bool success = ParseOptions(args);

            if (summary)
            {
                this.summaryReport.Report();
            }

            return success;
        }

        public void LoadInitialTmxFile()
        {
            // Only load the inital TMX file if it is set
            if (!String.IsNullOrEmpty(this.TmxFilePath))
            {
                LoadTmxFile(this.TmxFilePath);
            }
        }

        public void LoadTmxFile(string tmxFilePath)
        {
            this.TmxFilePath = tmxFilePath;

            this.summaryReport.Capture("Loading");

            // Load the TMX map
            try
            {
                this.TmxMap = TmxMap.LoadFromFile(this.TmxFilePath);

                // Load the Object Type Xml file if it exists
                if (File.Exists(Tiled2Unity.Settings.ObjectTypeXml))
                {
                    this.TmxMap.LoadObjectTypeXml(Tiled2Unity.Settings.ObjectTypeXml);
                }
            }
            catch (TmxException tmx)
            {
                this.TmxMap = new TmxMap();
                Logger.WriteError(tmx.Message);
            }
            catch (Exception e)
            {
                this.TmxMap = new TmxMap();
                Logger.WriteError(e.Message);
            }

            this.summaryReport.Report();
        }

        public void ExportTmxMap()
        {
            this.summaryReport.Capture("Exporting");
            {
                if (this.TmxMap.IsLoaded == false)
                {
                    Logger.WriteError("Tiled map file not loaded!");
                }
                else
                {
                    try
                    {
                        Logger.WriteLine("Exporting '{0}' to '{1}'", this.TmxFilePath, this.UnityExportFolderPath);
                        TiledMapExporter exporter = new TiledMapExporter(this.TmxMap);
                        exporter.Export(this.UnityExportFolderPath);
                    }
                    catch (TmxException tmx)
                    {
                        Logger.WriteError(tmx.Message);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteError(e.Message);
                    }
                }
            }
            this.summaryReport.Report();
        }

        public void DisplayHelp()
        {
            List<string> args = new List<string>() { "-h" };
            ParseOptions(args.ToArray());
        }

    }
}
