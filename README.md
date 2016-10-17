# Tiled2Unity

This is a fork of [Tiled2Unity](https://github.com/Seanba/Tiled2Unity) with mesh optimizations for orthogonal maps. It attempts to make your orthogonal maps run more smoothly in Unity.

It applies two heuristics:

1. If a tile consists of a single color, attempt to find neighboring tiles with the same color. Make the quad that contains all tiles of a single color as large as possible.
2. If neighboring tiles in a layer are also neighbors in the tileset, attempt to utilize that by enlarging the quad.

Tiled2Unity is made up of two parts:
- The [utility](tool/Tiled2Unity) that exports TMX files into Unity.
- The [Unity scripts](unity/Tiled2Unity) that import the output of the Tiled2Unity Utility.

