//css_ref System.Core.dll;
//css_ref System.IO.Compression.FileSystem.dll;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2UnityLite_Builder
{
    class Program
    {
        static public void Main()
        {
            string version = GetTiled2UnityVersion();
            WriteCS(version);
            WriteZip(version);
        }

        static private void WriteZip(string version)
        {
            Console.WriteLine("Writing zip for Tiled2UnityLite distribution");
            string file = String.Format("Tiled2UnityLite-{0}.zip", version);
            string unityPackage = String.Format("Tiled2Unity.{0}.unitypackage", version);

            if (File.Exists(file))
            {
                File.Delete(file);
            }
            
            using (ZipArchive zip = ZipFile.Open(file, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(unityPackage, unityPackage);
                zip.CreateEntryFromFile("Tiled2UnityLite.cs", "Tiled2UnityLite.cs");
            }
        }

        static private void WriteCS(string version)
        {        
            Console.WriteLine("Creating Tiled2UnityLite script for use with CS-Script");
            
            // If the filename contains this string in its path then it is ignored and not part of Tiled2UnityLite
            List<string> ignoreFiles = new List<string>();
            ignoreFiles.Add("AssemblyInfo");
            ignoreFiles.Add("TemporaryGeneratedFile_");
            ignoreFiles.Add("Info.cs");
            ignoreFiles.Add("PreviewImage.cs");

            List<string> keepers = new List<string>();
            foreach (string file in Directory.GetFiles("../Tiled2UnityLib/", "*.cs", SearchOption.AllDirectories))
            {
                if (!ignoreFiles.Any(i => file.Contains(i)))
                {
                    keepers.Add(file);
                }
            }

            // Add NDesk.Options source from Windows GUI version of Tiled2Unity
            keepers.Add("../src/ThirdParty/NDesk/Options.cs");

            // Ignore some library references
            List<string> ignoreReferences = new List<string>();
            ignoreReferences.Add("PresentationCore");
            ignoreReferences.Add("Microsoft.");
            ignoreReferences.Add("System.Deployment");
            ignoreReferences.Add("NDesk.Options");

            // Need to crack open the project file to find all the library refences used
            List<string> references = new List<string>();
            XDocument xmlDoc = XDocument.Load("../Tiled2UnityLib/Tiled2UnityLib.csproj");
            XNamespace ns = xmlDoc.Root.Name.Namespace;
            foreach (XElement reference in xmlDoc.Descendants(ns + "Reference"))
            {
                string library = reference.Attribute("Include").Value;

                if (!ignoreReferences.Any(i => library.Contains(i)))
                {
                    // We want to reference this library
                    string comment = string.Format("//css_reference {0};", library);
                    references.Add(comment);
                }
            }

            // Ignore "using [namespace]" for these
            List<string> ignoreUsers = new List<string>();
            ignoreUsers.Add("NDesk.Options");
            ignoreUsers.Add("System.Drawing.Drawing2D");            

            // Start collecting lines/data for our generated script file
            List<string> defines = new List<string>();
            List<string> users = new List<string>();
            StringBuilder body = new StringBuilder();

            foreach (string file in keepers)
            {
                //Console.WriteLine("Opening: {0}", file);
                body.AppendLine();
                body.AppendLine("// ----------------------------------------------------------------------");
                body.AppendLine("// " + Path.GetFileName(file));
                body.AppendLine();

                foreach (string line in File.ReadLines(file))
                {
                    if (line.StartsWith("#define"))
                    {
                        defines.Add(line);
                        body.AppendLine("// " + line + " // Commented out and put up top of file");
                    }
                    else if (line.StartsWith("using "))
                    {
                        // Can't have "using [namespace]" just anywhere in a file so we capture those to be added to the header
                        // Some "using [namespace]" needs to be ignored completely
                        if (!ignoreUsers.Any(i => line.Contains(i)))
                        {
                            users.Add(line);
                        }
                        body.AppendLine("// " + line);
                    }
                    else
                    {
                        body.AppendLine(line);
                    }
                }
            }

            if (String.IsNullOrEmpty(version))
            {
                Console.Error.WriteLine("Could not find Tiled2Unity version.");
                return;
            }

            Console.WriteLine("Building Tiled2UnityLite version = {0}", version);

            // We have everything we need to write out Tiled2UnityLite.cs
            StringWriter t2uWriter = new StringWriter();
            t2uWriter.WriteLine("// Tiled2UnityLite is automatically generated. Do not modify by hand.");
            t2uWriter.WriteLine("// version {0}", version);
            t2uWriter.WriteLine();

            // Write out our library references
            foreach (string reference in references)
            {
                t2uWriter.WriteLine(reference);
            }
            t2uWriter.WriteLine();

            t2uWriter.WriteLine("#define TILED_2_UNITY_LITE");

            // Write out our defines
            defines = defines.Distinct().ToList();
            defines.Sort();
            foreach (string define in defines)
            {
                t2uWriter.WriteLine(define);
            }
            t2uWriter.WriteLine();

            // Write out our "using" includes
            users = users.Distinct().OrderBy(f => f.TrimEnd(';')).ToList();
            foreach (string user in users)
            {
                t2uWriter.WriteLine(user);
            }
            t2uWriter.WriteLine();

            // Write out our "main"
            WriteMainProgramFile(t2uWriter, version);

            // Write the body out
            t2uWriter.WriteLine(body.ToString());

            
            // Write out Tiled2UnityLite to file
            Console.WriteLine("Writing Tiled2UnityLite.cs");
            File.WriteAllText("Tiled2UnityLite.cs", t2uWriter.ToString());
        }

        static public string GetTiled2UnityVersion()
        {
            // Get the version from the exe
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(@"..\src\bin\x64\Release\Tiled2Unity.exe");
            return versionInfo.ProductVersion;            
        }

        static public void WriteMainProgramFile(StringWriter writer, string version)
        {
            string program =
@"
namespace Tiled2Unity
{{
    static class Program
    {{
        public static int Main(string[] args)
        {{
            return Tiled2Unity.Tiled2UnityLite.Run(args);
        }}
    }}

    static class Info
    {{
        public static string GetLibraryName()
        {{
            return ""Tiled2UnityLite"";
        }}

        public static string GetVersion()
        {{
            return ""{0}"";
        }}

        public static string GetPlatform()
        {{
            return ""CSScript"";
        }}
    }}
}}
";
            program = string.Format(program, version);
            writer.WriteLine(program);
        }
    }
}

