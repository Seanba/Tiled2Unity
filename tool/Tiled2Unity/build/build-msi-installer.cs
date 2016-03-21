// Builds MSI installer for Tiled2Unity 
// Requires CS-Script to run

//css_ref %WIXSHARP_DIR%\WixSharp.dll;
//css_ref %WIXSHARP_DIR%\Wix_bin\SDK\Microsoft.Deployment.WindowsInstaller.dll;
//css_ref System.Core.dll;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using WixSharp;

class Script
{
    static public void Main()
    {
        string PATH_ROOT = Path.GetFullPath("..");
        string PATH_RELEASE = PATH_ROOT + @"\src\bin\Release";
        string PATH_BUILD = PATH_ROOT + @"\build";
        string PATH_DATA = PATH_ROOT + @"\TestData";
        string PATH_SOURCE = PATH_ROOT + @"\src";
        string VERSION = "";
        string PATH_UNITY_PACKAGE = "";

        // Get the version from the exe
        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(PATH_RELEASE + @"\Tiled2Unity.exe");
        VERSION = versionInfo.ProductVersion;
        PATH_UNITY_PACKAGE = PATH_BUILD + @"\Tiled2Unity." + VERSION + @".unitypackage";

        Console.WriteLine("-- Building MSI installer for version: {0}", VERSION);
        Console.WriteLine("-- PATH_ROOT:          {0}", PATH_ROOT);
        Console.WriteLine("-- PATH_RELEASE:       {0}", PATH_RELEASE);
        Console.WriteLine("-- PATH_BUILD:         {0}", PATH_BUILD);
        Console.WriteLine("-- PATH_DATA:          {0}", PATH_DATA);
        Console.WriteLine("-- PATH_SOURCE:        {0}", PATH_SOURCE);
        Console.WriteLine("-- PATH_UNITY_PACKAGE: {0}", PATH_UNITY_PACKAGE);
        Console.WriteLine("-- VERSION:            {0}", VERSION);        

        Console.WriteLine("-- Write {0} to t2u_version.txt", VERSION);
        System.IO.File.WriteAllText("t2u-version.txt", VERSION);        

        // Fill out the Wix project
        var project = new WixSharp.Project("Tiled2Unity",
                            new WixSharp.Dir(@"%ProgramFiles%\Tiled2Unity",
                                new WixSharp.File(PATH_BUILD   + @"\ReadMe.txt"),
                                new WixSharp.File(PATH_RELEASE + @"\ThemeColorPicker.dll"),
                                new WixSharp.File(PATH_RELEASE + @"\Tiled2Unity.exe"),
                                new WixSharp.File(PATH_RELEASE + @"\Tiled2Unity.exe.config"),
                                new WixSharp.File(PATH_BUILD   + @"\Tiled2UnityLite.cs"),
                                RenamedFile(PATH_UNITY_PACKAGE, "Tiled2Unity.unitypackage"),

                                new WixSharp.Dir("Examples",
                                    new WixSharp.DirFiles(PATH_DATA + @"\*.*")
                                    ),

                                new WixSharp.Dir("License",
                                    RenamedFile(PATH_SOURCE + @"\License.rtf", "License.Tiled2Unity.rtf"),
                                    RenamedFile(PATH_SOURCE + @"\ThirdParty\Clipper\License.txt", "License.Clipper.txt"),
                                    RenamedFile(PATH_SOURCE + @"\ThirdParty\NDesk\License.txt", "License.NDesk.txt"),
                                    RenamedFile(PATH_SOURCE + @"\ThirdParty\Poly2Tri\License.txt", "License.Poly2Tri.txt")
                                    )
                            ));

        project.GUID = new Guid("91b20082-6384-40d1-b090-33dcaa49eab5");
        project.LicenceFile = PATH_SOURCE + @"\License.rtf";
        project.UI = WUI.WixUI_InstallDir;

        project.Version = new Version(VERSION);
        project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
        project.MajorUpgradeStrategy.RemoveExistingProductAfter = Step.InstallInitialize;

        // Compile the project
        WixSharp.Compiler.BuildMsi(project);
    }

    static private WixSharp.File RenamedFile(string source, string name)
    {
        var file = new WixSharp.File(source);
        file.Attributes["Name"] = name;

        return file;
    }
    
}
