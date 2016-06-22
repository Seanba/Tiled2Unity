using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public class Logger
    {
        public delegate void WriteLineDelegate(string line);
        public static event WriteLineDelegate OnWriteLine;

        public delegate void WriteSuccessDelegate(string line);
        public static event WriteSuccessDelegate OnWriteSuccess;

        public delegate void WriteWarningDelegate(string line);
        public static event WriteWarningDelegate OnWriteWarning;

        public delegate void WriteErrorDelegate(string line);
        public static event WriteErrorDelegate OnWriteError;

        public static void WriteLine()
        {
            WriteLine("");
        }

        public static void WriteLine(string line)
        {
            line += "\n";
            if (OnWriteLine != null)
                OnWriteLine(line);
            Console.Write(line);
        }

        public static void WriteLine(string fmt, params object[] args)
        {
            WriteLine(String.Format(fmt, args));
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
