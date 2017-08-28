using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public class Logger
    {
        // Only to be printed in verbose mode. Useful for debugging.
        public delegate void WriteVerboseDelegate(string line);
        public static event WriteVerboseDelegate OnWriteVerbose;

        // These are always printed out whether verbose is enabled or not
        public delegate void WriteInfoDelegate(string line);
        public static event WriteInfoDelegate OnWriteInfo;

        public delegate void WriteSuccessDelegate(string line);
        public static event WriteSuccessDelegate OnWriteSuccess;

        public delegate void WriteWarningDelegate(string line);
        public static event WriteWarningDelegate OnWriteWarning;

        public delegate void WriteErrorDelegate(string line);
        public static event WriteErrorDelegate OnWriteError;

        public static void WriteVerbose()
        {
            WriteVerbose("");
        }

        public static void WriteVerbose(string line)
        {
            if (Tiled2Unity.Settings.Verbose)
            {
                line += "\n";
                if (OnWriteVerbose != null)
                    OnWriteVerbose(line);
            }
        }

        public static void WriteVerbose(string fmt, params object[] args)
        {
            if (Tiled2Unity.Settings.Verbose)
            {
                WriteVerbose(String.Format(fmt, args));
            }
        }

        public static void WriteInfo()
        {
            WriteInfo("");
        }

        public static void WriteInfo(string line)
        {
            line += "\n";
            if (OnWriteInfo != null)
                OnWriteInfo(line);
            Console.Write(line);
        }

        public static void WriteInfo(string fmt, params object[] args)
        {
            WriteInfo(String.Format(fmt, args));
        }

        public static void WriteSuccess(string success)
        {
            success += "\n";
            if (OnWriteSuccess != null)
                OnWriteSuccess(success);
            Console.Write(success);
        }

        public static void WriteSuccess(string fmt, params object[] args)
        {
            WriteSuccess(String.Format(fmt, args));
        }

        public static void WriteWarning(string warning)
        {
            warning += "\n";
            if (OnWriteWarning != null)
                OnWriteWarning(warning);
            Console.Write(warning);
        }

        public static void WriteWarning(string fmt, params object[] args)
        {
            WriteWarning(String.Format(fmt, args));
        }

        public static void WriteError(string error)
        {
            error += "\n";
            if (OnWriteError != null)
                OnWriteError(error);
            Console.Write(error);
        }

        public static void WriteError(string fmt, params object[] args)
        {
            WriteError(String.Format(fmt, args));
        }
    }
}
