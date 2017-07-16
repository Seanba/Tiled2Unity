using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    // Helper class to write to Editor.log file
    public static class Log
    {
        private static readonly bool Enabled = true;

        public static void Report(string msg)
        {
            if (Enabled)
            {
                System.Console.WriteLine(msg);
            }
        }

        public static void Report(string fmt, params object[] args)
        {
            string msg = String.Format(fmt, args);
            Report(msg);
        }
    }

    public class Logger : IDisposable
    {
        private readonly string message;

        public Logger(string fmt, params object[] args) : this(String.Format(fmt, args))
        {
        }

        public Logger(string message)
        {
            this.message = message;
            Log.Report("[Tiled2Unity]Begin: {0}", this.message);
        }

        public void Dispose()
        {
            Log.Report("[Tiled2Unity]End: {0}", this.message);
        }
    }
}
