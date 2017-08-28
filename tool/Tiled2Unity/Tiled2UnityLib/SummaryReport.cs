using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // Helper class that gathers success, warning and error messages and blasts them back out through logging when requested
    class SummaryReport
    {
        private string name = "";
        private List<string> successes = new List<string>();
        private List<string> warnings = new List<string>();
        private List<string> errors = new List<string>();

        public void Capture(string name)
        {
            Listen();

            this.name = name;
            this.successes.Clear();
            this.warnings.Clear();
            this.errors.Clear();
        }

        private delegate void LoggingDelegate(string message, params object[] args);
        public void Report()
        {
            // Stop listening because we're going to blast back out to the logging system
            Ignore();

            // Are we going to log as success, warnings, or errors?
            LoggingDelegate func = Logger.WriteSuccess;
            if (this.warnings.Count > 0)
            {
                func = Logger.WriteWarning;
            }
            if (this.errors.Count > 0)
            {
                func = Logger.WriteError;
            }

            // Write out the summary report
            string separator = new string('-', 80);
            Logger.WriteInfo(separator);
            func("{0} summary", this.name);

            // Add successes
            Logger.WriteInfo("Succeeded: {0}", this.successes.Count);
            foreach (var success in this.successes)
            {
                Logger.WriteSuccess("  {0}", success);
            }

            // Add warnings
            Logger.WriteInfo("Warnings: {0}", this.warnings.Count);
            foreach (var warn in this.warnings)
            {
                Logger.WriteWarning("  {0}", warn);
            }

            // Add errors
            Logger.WriteInfo("Errors: {0}", this.errors.Count);
            foreach (var error in this.errors)
            {
                Logger.WriteError("  {0}", error);
            }

            Logger.WriteInfo(separator);
        }

        private void Listen()
        {
            Logger.OnWriteSuccess += Logger_OnWriteSuccess;
            Logger.OnWriteWarning += Logger_OnWriteWarning;
            Logger.OnWriteError += Logger_OnWriteError;
        }

        private void Ignore()
        {
            Logger.OnWriteSuccess -= Logger_OnWriteSuccess;
            Logger.OnWriteWarning -= Logger_OnWriteWarning;
            Logger.OnWriteError -= Logger_OnWriteError;
        }

        private void Logger_OnWriteError(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.errors.Add(line);
        }

        private void Logger_OnWriteWarning(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.warnings.Add(line);
        }

        private void Logger_OnWriteSuccess(string line)
        {
            line = line.TrimEnd('\r', '\n');
            this.successes.Add(line);
        }
    }
}
