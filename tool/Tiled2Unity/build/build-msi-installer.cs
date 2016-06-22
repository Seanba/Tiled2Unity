// Builds MSI installer for Tiled2Unity 
// Requires CS-Script to run

//css_ref %WIXSHARP_DIR%\WixSharp.dll;
//css_ref %WIXSHARP_DIR%\Wix_bin\SDK\Microsoft.Deployment.WindowsInstaller.dll;
//css_ref System.Core.dll;
//css_ref System.IO.Compression.FileSystem.dll;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using WixSharp;

class Script
{
    static public int Main(string[] args)
    {
        // Takes for granted that the current working directory is the same as this file
        if (args.Length == 0)
        {
            Console.WriteLine("No platform argument given (x86|x64)");
            return 1;
        }
        string platform = args[0].ToLower();
        if (platform == "x86")
        {
            Console.WriteLine("Building Win32 installer for x86 platform.");
        }
        else if (platform == "x64")
        {
            Console.WriteLine("Building Win64 installer for x64 platform.");
        }
        else
        {
            Console.WriteLine("Unknown build platform: {0}", platform);
        }

        string PATH_ROOT = Path.GetFullPath("..");
        string PATH_BUILD = PATH_ROOT + @"\build";
        string PATH_DATA = PATH_ROOT + @"\TestData";
        string PATH_SOURCE = PATH_ROOT + @"\src";
        string PATH_LIB_SOURCE = PATH_ROOT + @"\Tiled2UnityLib";
        string PATH_RELEASE = "";
        string PATH_UNITY_PACKAGE = "";
        string VERSION = "";
        string GUID = "";
        string WIN_PLATFORM = "";
        string PATH_PROGRAM_FILES = "";

        if (platform == "x86")
        {
            PATH_RELEASE = PATH_ROOT + @"\src\bin\Release";
            GUID = "91b20082-6384-40d1-b090-33dcaa49eab5";
            WIN_PLATFORM = "win32";
            PATH_PROGRAM_FILES = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        }
        else
        {
            PATH_RELEASE = PATH_ROOT + @"\src\bin\x64\Release";
            GUID = "e3a46be2-728d-492d-928f-77021c74eb15";
            WIN_PLATFORM = "win64";
            PATH_PROGRAM_FILES = Environment.GetEnvironmentVariable("ProgramFiles");
        }

        // Get the version from the exe
        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(PATH_RELEASE + @"\Tiled2UnityLib.dll");
        VERSION = versionInfo.ProductVersion;
        PATH_UNITY_PACKAGE = PATH_BUILD + @"\Tiled2Unity." + VERSION + @".unitypackage";

        Console.WriteLine("-- Building MSI installer for version: {0}", VERSION);
        Console.WriteLine("-- PATH_ROOT:          {0}", PATH_ROOT);
        Console.WriteLine("-- PATH_BUILD:         {0}", PATH_BUILD);
        Console.WriteLine("-- PATH_DATA:          {0}", PATH_DATA);
        Console.WriteLine("-- PATH_SOURCE:        {0}", PATH_SOURCE);
        Console.WriteLine("-- PATH_LIB_SOURCE:    {0}", PATH_LIB_SOURCE);
        Console.WriteLine("-- PATH_RELEASE:       {0}", PATH_RELEASE);
        Console.WriteLine("-- PATH_UNITY_PACKAGE: {0}", PATH_UNITY_PACKAGE);
        Console.WriteLine("-- GUID:               {0}", GUID);
        Console.WriteLine("-- WIN_PLATFORM:       {0}", WIN_PLATFORM);
        Console.WriteLine("-- PATH_PROGRAM_FILES: {0}", PATH_PROGRAM_FILES);
        Console.WriteLine("-- VERSION:            {0}", VERSION);

        // Fill out the Wix project
        string projectName = String.Format("Tiled2Unity ({0})", WIN_PLATFORM);
        var project = new WixSharp.Project(projectName,
                            new WixSharp.Dir(@"%ProgramFiles%\Tiled2Unity",
                                new WixSharp.File(PATH_BUILD   + @"\ReadMe.txt"),
                                new WixSharp.File(PATH_RELEASE + @"\Tiled2Unity.exe"),
                                new WixSharp.File(PATH_RELEASE + @"\Tiled2Unity.exe.config"),
                                new WixSharp.File(PATH_RELEASE + @"\Tiled2UnityLib.dll"),
                                new WixSharp.File(PATH_RELEASE + @"\NDesk.Options.dll"),
                                new WixSharp.File(PATH_BUILD   + @"\Tiled2UnityLite.cs"),
                                RenamedFile(PATH_UNITY_PACKAGE, "Tiled2Unity.unitypackage"),

                                new WixSharp.Dir("Examples",
                                    new WixSharp.DirFiles(PATH_DATA + @"\*.*")
                                    ),

                                new WixSharp.Dir("License",
                                    RenamedFile(PATH_SOURCE + @"\License.rtf", "License.Tiled2Unity.rtf"),
                                    RenamedFile(PATH_SOURCE + @"\ThirdParty\NDesk\License.txt", "License.NDesk.txt"),                                    
                                    RenamedFile(PATH_LIB_SOURCE + @"\ThirdParty\Clipper\License.txt", "License.Clipper.txt"),
                                    RenamedFile(PATH_LIB_SOURCE + @"\ThirdParty\Poly2Tri\License.txt", "License.Poly2Tri.txt")
                                    )
                            ));

        project.GUID = new Guid(GUID);
        project.LicenceFile = PATH_SOURCE + @"\License.rtf";
        project.UI = WUI.WixUI_InstallDir;

        project.Version = new Version(VERSION);
        project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
        project.MajorUpgradeStrategy.RemoveExistingProductAfter = Step.InstallInitialize;
        
        project.Platform = (WIN_PLATFORM == "win32") ? Platform.x86 : Platform.x64;

        // Compile the project
        string msiFile = WixSharp.Compiler.BuildMsi(project);
        if (msiFile == null)
        {
            Console.WriteLine("Failed to build Tiled2Unity {0} installer.", platform);
            return 1;
        }

        // Rename the MSI output file
        string renamedFile = String.Format("Tiled2Unity-{0}-{1}-setup.msi", VERSION, WIN_PLATFORM);
        Console.WriteLine("-- Renaming: {0} -> {1}", System.IO.Path.GetFileName(msiFile), renamedFile);
        if (System.IO.File.Exists(renamedFile))
        {
            System.IO.File.Delete(renamedFile);
        }
        System.IO.File.Move(msiFile, renamedFile);
        msiFile = renamedFile;

        // Install the MSI file
        Console.WriteLine("-- Install the MSI file");
        Process p = Process.Start(msiFile);
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            Console.WriteLine("-- Installation of {0} either failed or was canceled.", msiFile);
            return 1;
        }
        Console.WriteLine("-- Successfully installed {0}", msiFile);

        // Zip the installation
        string zipFromDir = PATH_PROGRAM_FILES + @"\Tiled2Unity";
        string zipToFile = "Tiled2Unity-" + VERSION + "-" + WIN_PLATFORM + ".zip";
        Console.WriteLine("-- Zipping installation {0} -> {1}", zipFromDir, zipToFile);

        if (System.IO.File.Exists(zipToFile))
        {
            System.IO.File.Delete(zipToFile);
        }
        ZipFile.CreateFromDirectory(zipFromDir, zipToFile);

        return 0;
    }

    static private WixSharp.File RenamedFile(string source, string name)
    {
        var file = new WixSharp.File(source);
        file.Attributes["Name"] = name;

        return file;
    }
    
}
