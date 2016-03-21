@echo off
pushd %~dp0
setlocal

rem Build Tiled2Unity
rem Note we're hardcoding Visual Studio 2010 here
 
call "%VS100COMNTOOLS%vsvars32.bat"
devenv /rebuild Release ../Tiled2Unity.sln
if ERRORLEVEL 1 goto BuildFailed

rem Use CS-Script to build Tiled2UnityLite
%CSSCRIPT_DIR%\cscs build-tiled2unitylite.cs

rem Call our CSharp build script. This will create the auto-gen-builder.bat file we call next.
%CSSCRIPT_DIR%\cscs build-msi-installer.cs

rem Get the Tiled2Unity version
set /P T2U_VERSION=< t2u-version.txt
echo Tiled2Unity version is %T2U_VERSION%

rem Rename the MSI file to include our version
echo renaming Tiled2Unity.msi to Tiled2Unity-%T2U_VERSION%-win32-setup.msi
ren Tiled2Unity.msi Tiled2Unity-%T2U_VERSION%-win32-setup.msi

rem Install this version of Tiled2Unity waiting until it is complete
rem This is so we can zip up the install later
echo Installing Tiled2Unity ...
start /WAIT Tiled2Unity-%T2U_VERSION%-win32-setup.msi
if ERRORLEVEL 1 goto InstallFailed
echo Installation completed!

rem Zip up the installation for users that prefer it
ruby zip-tiled2unity.rb %T2U_VERSION%

goto :Done

endlocal
popd

rem Exit conditions
:BuildFailed
echo Tiled2Unity failed to build in Dev Studio
exit /B 1

:InstallFailed
echo Tile2Unity MSI installation failed or was canceled
exit /B 1

:Done
echo Success