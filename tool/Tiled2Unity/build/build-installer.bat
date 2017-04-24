@echo off
pushd %~dp0
setlocal

rem Build Tiled2Unity
rem Note we're hardcoding Visual Studio 2015 here
 
call "%VS140COMNTOOLS%vsvars32.bat"

if defined WIXSHARP_DIR (
    echo WIXSHARP_DIR path: %WIXSHARP_DIR% 
) else (
    echo Error: Install WixSharp and add WIXSHARP_DIR environment variable
    goto InstallFailed
)

if defined WIXSHARP_WIXDIR (
    echo WIXSHARP_WIXDIR path: %WIXSHARP_WIXDIR% 
) else (
    echo Error: Install WixSharp and add WIXSHARP_WIXDIR environment variable. Example: C:\WixSharp\Wix_bin\bin
    goto InstallFailed
)

if defined CSSCRIPT_DIR (
	echo CSSCRIPT_DIR path: %CSSCRIPT_DIR%
) else (
	echo Error: Install CS-Script and add CSSCRIPT_DIR environment variable
	goto InstallFailed
)

rem Build Win64/x64
echo -- Building Tiled2Unity x64
devenv /rebuild "Release|x64" ..\Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed
echo -- Successfully built Tiled2Unity x64

rem Build Win32/x86
echo -- Building Tiled2Unity x86
devenv /rebuild "Release|x86" ..\Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed
echo -- Successfully built Tiled2Unity x86

rem Use CS-Script to build Tiled2UnityLite
echo -- Building Tiled2UnityLite.cs
"%CSSCRIPT_DIR%"\cscs build-tiled2unitylite.cs
if ERRORLEVEL 1 goto LiteFailed
echo -- Successfully built Tiled2UnityLite

echo -- Building installer for Tiled2Unity x64
"%CSSCRIPT_DIR%"\cscs build-msi-installer.cs x64
if ERRORLEVEL 1 goto MSIFailed

echo -- Building installer for Tiled2Unity x86
"%CSSCRIPT_DIR%"\cscs build-msi-installer.cs x86
if ERRORLEVEL 1 goto MSIFailed

goto :Done

endlocal
popd

rem Exit conditions
:BuildFailed
echo Tiled2Unity failed to build in Dev Studio
exit /B 1

:LiteFailed
echo Failed to build Tiled2UnityLite
exit /B 1

:MSIFailed
echo Failed to build MSI installer
exit /B 1

:InstallFailed
echo Tile2Unity MSI installation failed or was canceled
exit /B 1

:Done
echo Success
