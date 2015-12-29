using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2UnityLite_Builder
{
    class Program
    {
        static public void Main(string[] args)
        {
            Console.WriteLine("Creating Tiled2UnityLite script for use with CS-Script");
            
            // If the filename contains this string in its path then it is ignored and not part of Tiled2UnityLite
            List<string> ignoreFiles = new List<string>();
            ignoreFiles.Add("AssemblyInfo");
            ignoreFiles.Add("DataGridView");
            ignoreFiles.Add("PerLayerColorData");
            ignoreFiles.Add("Form.cs");
            ignoreFiles.Add("Viewer.cs");
            ignoreFiles.Add("About.cs");
            ignoreFiles.Add("ThemeColor");
            ignoreFiles.Add("Designer.cs");

            List<string> keepers = new List<string>();
            foreach (string file in Directory.GetFiles("../src/", "*.cs", SearchOption.AllDirectories))
            {
                if (!ignoreFiles.Any(i => file.Contains(i)))
                {
                    keepers.Add(file);
                }
            }
			
			// Ignore some library references
			List<string> ignoreReferences = new List<string>();
			ignoreReferences.Add("PresentationCore");
			ignoreReferences.Add("Microsoft.");
			ignoreReferences.Add("Windows.Forms");
			ignoreReferences.Add("System.Deployment");
			
			// Need to crack open the project file to find all the library refences used
			List<string> references = new List<string>();
			XDocument xmlDoc = XDocument.Load("../src/Tiled2Unity.csproj");
			XNamespace ns = xmlDoc.Root.Name.Namespace;
			foreach (XElement reference in xmlDoc.Descendants(ns + "Reference"))
			{
				string library = reference.Attribute("Include").Value;
				
				if (!ignoreReferences.Any(i => library.Contains(i)))
				{
					// We want to reference this library
					string comment = string.Format("//css_reference {0}", library);
					references.Add(comment);
				}
			}			
			
            // Ignore "using [namespace]" for these
            List<string> ignoreUsers = new List<string>();
            ignoreUsers.Add("NDesk.Options");
            ignoreUsers.Add("System.Windows.Forms");
            ignoreUsers.Add("System.Windows.Media");

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

            string version = GetTiled2UnityVersion();
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

            // Write out our version function
            WriteGetVersionFunction(t2uWriter, version);

            // Write the body out
            t2uWriter.WriteLine(body.ToString());

            
            // Write out Tiled2UnityLite to file
            Console.WriteLine("Writing Tiled2UnityLite.cs");
            File.WriteAllText("Tiled2UnityLite.cs", t2uWriter.ToString());
        }

        static public string GetTiled2UnityVersion()
        {
            string versionFile = "../src/Properties/AssemblyInfo.cs";
            string allText = File.ReadAllText(versionFile);

            Regex regex = new Regex("AssemblyFileVersion\\(\"(?<version>.*)\"\\)");
            Match match = regex.Match(allText);
            if (match.Success)
            {
                return match.Groups["version"].Value;
            }

            return "";
        }

        static public void WriteGetVersionFunction(StringWriter writer, string version)
        {
            string function =
@"
namespace Tiled2Unity
{{
    static partial class Program
    {{
        public static string GetVersion()
        {{
            return ""{0}"";
        }}
    }}
}}
";
            function = string.Format(function, version);
            writer.WriteLine(function);
        }

    }
}

