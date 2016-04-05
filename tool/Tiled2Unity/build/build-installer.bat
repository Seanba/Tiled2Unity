@echo off
pushd %~dp0
setlocal

rem Build Tiled2Unity
rem Note we're hardcoding Visual Studio 2015 here
 
call "%VS140COMNTOOLS%vsvars32.bat"

rem Build Win32/x86
echo -- Building Tiled2Unity x86
devenv /rebuild "Release|x86" ..\Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed
echo -- Successfully built Tiled2Unity x86

rem Build Win64/x64
echo -- Building Tiled2Unity x64
devenv /rebuild "Release|x64" ..\Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed
echo -- Successfully built Tiled2Unity x64

rem Use CS-Script to build Tiled2UnityLite
%CSSCRIPT_DIR%\cscs build-tiled2unitylite.cs

rem Call our CSharp build script. This will install and zip as well.
echo -- Building installer for Tiled2Unity x86
%CSSCRIPT_DIR%\cscs build-msi-installer.cs x86
if ERRORLEVEL 1 goto MSIFailed

echo -- Building installer for Tiled2Unity x64
%CSSCRIPT_DIR%\cscs build-msi-installer.cs x64
if ERRORLEVEL 1 goto MSIFailed

goto :Done

endlocal
popd

rem Exit conditions
:BuildFailed
echo Tiled2Unity failed to build in Dev Studio
exit /B 1

:MSIFailed
echo Failed to build MSI installer
exit /B 1

:InstallFailed
echo Tile2Unity MSI installation failed or was canceled
exit /B 1

:Done
echo Success
