@echo off
pushd %~dp0
setlocal

rem Build Tiled2Unity
rem Note we're hardcoding Visual Studio 2010 here
 
call "%VS100COMNTOOLS%vsvars32.bat"
devenv /rebuild Release ../Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed

rem Call our CSharp build script. This will create the auto-gen-builder.bat file we call next.
%CSSCRIPT_DIR%\cscs build-installer.cs

rem Call the generated build file
call auto-gen-builder.bat
if ERRORLEVEL 1 goto InstallerFailed

goto :Done

endlocal
popd

rem Exit conditions
:BuildFailed
echo Tiled2Unity failed to build in Dev Studio
exit /B 1

:InstallerFailed
echo Generated script to build installer failed
exit /B 1

:Done
echo Success