@echo off
pushd %~dp0

call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\animation\FlashmanLair.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\collision-layers\Cutman-CollisionLayers.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\collision-layers\Cutman-ObjectTypes.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\custom\HeatManBlocks.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\ladder\CutMan-WithLadders.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\simple\MagmaManLair-NoCollision.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\simple\MagmaManLair-WithCollision.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\slopes\RedDragon-WithSlopes.tmx"
call re-export-single-map.bat "..\..\..\unity\Tiled2Unity\tiled\water\DiveMan-water.tmx"

popd