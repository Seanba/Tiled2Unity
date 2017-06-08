@echo off
pushd %~dp0
rem This is hardcoded to expect the Tiled2Unity test project at location ..\..\..\

set TiledTmx=%1
set ObjectTypeXml="..\..\..\unity\Tiled2Unity\tiled\objecttypes.xml"
set Tiled2UnityDir="..\..\..\unity\Tiled2Unity\unity\Assets\Tiled2Unity"


echo command: cscs Tiled2UnityLite.cs --object-type-xml=%ObjectTypeXml% %TiledTmx% %Tiled2UnityDir%
cscs Tiled2UnityLite.cs --object-type-xml=%ObjectTypeXml% %TiledTmx% %Tiled2UnityDir%

popd