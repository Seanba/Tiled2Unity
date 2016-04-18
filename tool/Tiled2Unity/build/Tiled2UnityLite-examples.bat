@echo off
pushd %~dp0
rem This is hardcoded to expect the Tiled2Unity test project at location ..\..\..\

rem set ObjectTypeXml=""
set ObjectTypeXml="..\..\..\unity\Tiled2Unity\tiled\objecttypes.xml"
set TiledTmx="..\..\..\unity\Tiled2Unity\tiled\collision-layers\Cutman-CollisionLayers.tmx"
set Tiled2UnityDir="..\..\..\unity\Tiled2Unity\unity\Assets\Tiled2Unity"

cscs Tiled2UnityLite.cs --help

echo Command: cscs Tiled2UnityLite.cs --object-type-xml=%ObjectTypeXml% %TiledTmx% %Tiled2UnityDir%
cscs Tiled2UnityLite.cs --object-type-xml=%ObjectTypeXml% %TiledTmx% %Tiled2UnityDir%

popd