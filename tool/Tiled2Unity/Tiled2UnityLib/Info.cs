using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

#if TILED2UNITY_MAC
using Foundation;
#endif

namespace Tiled2Unity
{
    // Get information about this library
    public class Info
    {
        public static string GetLibraryName()
        {
            return "Tiled2Unity";
        }

        public static string GetVersion()
        {
#if TILED2UNITY_MAC
            var info = NSBundle.MainBundle.ObjectForInfoDictionary ("CFBundleShortVersionString");
            return info.ToString();
#else
            var thisAss = Assembly.GetExecutingAssembly();
            AssemblyName name = new AssemblyName(thisAss.FullName);
            return name.Version.ToString();
#endif
        }

        public static string GetPlatform()
        {
            var thisAss = Assembly.GetExecutingAssembly();
            PortableExecutableKinds peKind;
            ImageFileMachine ifMachine;
            thisAss.ManifestModule.GetPEKind(out peKind, out ifMachine);

            if (peKind.HasFlag(PortableExecutableKinds.PE32Plus))
            {
                return "Win64";
            }

            return "Win32";
        }

    }
}
