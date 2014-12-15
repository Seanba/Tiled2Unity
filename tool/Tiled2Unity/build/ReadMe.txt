Tiled2Unity Utility - http://www.seanba.com/IntroTiled2Unity.html

About Tiled2Unity
-------------------------------------------------------------------------------

Tiled2Unity is a free utility that bridges the gap between Tiled Map Editor 
(http://www.mapeditor.org/) and the Unity Game Engine (http://www.unity3d.com/) 
for use in 2D Games.

The purpose of Tiled2Unity is to provide 2D tiled-based level assets to your 
Unity game (collision supported) while being as simple as possible to use. For 
most users, you'll simply export your TMX files to your Unity project and drop 
the automatically generated prefabs into your scene.

Collision geometry can be placed in your Tiled map as Polygon, Circle (note
that ellipses are not supported), Rectangle, or Polyline objects in an Object 
Layer, but the real power comes from assigning Rectangles and/or Polygons to 
separate tiles which you place in your Tile Layers. The Tiled2Unity Utility 
will combine such geometry into one large PolygonCollider2D (per layer) in your 
Unity prefab. 

Note that polygons that are concave or contain holes *are* supported.

Limitations
-------------------------------------------------------------------------------

Tiled2Unity does not support isometric maps. This is a purposeful limitation 
based on the way collider objects are skewed in Tiled. It is recommended that 
you use orthographic maps anyway and leave it to your artwork to convey a side-
scroller, or top-down, or isometric view. 


Using Tiled2Unity
-------------------------------------------------------------------------------

1. Get Tiled Map Editor
	You will need a version of Tiled that has the Tiled Collision Editor 
	feature. Currently, this is in beta and is is available here:
	http://files.mapeditor.org/daily/
	(Tiled2Unity was tested with the 2014-05-23 build of Tiled)
	
2. Import Tiled2Unity.unitypackage to your Unity project
	In Tiled2Unity, select "Help -> Import Unity Package to Project".
	Or, double-click on the Tiled2Unity.unitypackage found in your install 
	directory.

3. Export a TMX File
	Run the Tiled2Unity Utility and open a Tiled TMX file. Select a Unity 
	project to export to and hit the "Big Ass Export Button".
	
4.	Use The Generated Prefab
	In Unity, find your new prefab in the Assets/Tiled2Unity/Prefabs folder. It 
	will have the same name of your TMX file. Simply drop it into your Unity 
	scene.

Tips
-------------------------------------------------------------------------------

1.	For Tiled map files that contain collision, the Preview Exported Map button
	is handy for seeing how the Unity colliders will appear on the exported 
	prefab.
	
2.	Run Tiled2Unity from Tiled through the command feature within Tiled
	example command:
	"c:\Program Files (x86)\Tiled2Unity\Tiled2Unity.exe" %mapfile c:\my\project
	This will open Tiled2Unity from Tiled with the current map file. Prefabs 
	will be exported to the Unity project at c:\my\project
	
Support
-------------------------------------------------------------------------------

Found a bug? Let me know: sean@seanba.com



 









