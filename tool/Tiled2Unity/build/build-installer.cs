// CS-Script build script for the Tiled2Unity installer
// from command prompt: cscsript build-installer.cs
// Requres CS-Script

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Build
{
	class Installer
	{
		static public void Main()
		{
			string pathToExe = "../src/bin/Release/Tiled2Unity.exe";

			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
			string version = versionInfo.ProductVersion;
			Console.WriteLine("Building installer for version: {0}", version);

			// Make sure we have the Unity package for Tiled2Unity
			string unityPackage = String.Format("Tiled2Unity.{0}.unitypackage", version);
			if (!File.Exists(unityPackage))
			{
				Console.Error.WriteLine("Unity package {0} does not exist. Export the Tiled2Unity files from Unity for this project.", unityPackage);
			}
			else
			{
				// Copy the Unity package into our release folder
				string destFile = Path.Combine("..\\src\\bin\\Release", unityPackage);
				File.Copy(unityPackage, destFile, true);
			}
		

			// Start to create our generated batch file
			string batchFile = "auto-gen-builder.bat";
			File.WriteAllText(batchFile, String.Empty);
			using (StreamWriter writer = File.AppendText(batchFile))
			{
				writer.WriteLine("rem Auto-generated batchfile. Do not modify by hand.");
				writer.WriteLine("rem For building installer for Tiled2Unity.{0}", version);
				writer.WriteLine("@echo off");
				writer.WriteLine("pushd %~dp0");
				writer.WriteLine("setlocal");
				writer.WriteLine();

				writer.WriteLine("set T2U_Version={0}", version);
				writer.WriteLine("set T2U_Bin=..\\src\\bin\\Release\\");
				writer.WriteLine();
				
				writer.WriteLine("set PATH=%PATH%;\"C:\\Program Files (x86)\\NSIS\\\"");
				writer.WriteLine();

				writer.WriteLine("makensis.exe tiled2unity.nsi");
				writer.WriteLine("if ERRORLEVEL 1 exit /B 1");
				
				writer.WriteLine();
				writer.WriteLine("endlocal");
				writer.WriteLine("popd");
            }

		}
	}
}

