using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;


namespace Tiled2Unity
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool isAuto = false;
            NDesk.Options.OptionSet options = new NDesk.Options.OptionSet()
            {
                { "a|auto-export", "Automatic export", a => isAuto = true }
            };

            options.Parse(args);
            if (isAuto)
            {
                return Tiled2UnityLite.Run(args);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (Tiled2UnityForm form = new Tiled2UnityForm())
            {
                Application.Run(form);
            }

            return 0;
        }

    } // end class
} // end namespace
