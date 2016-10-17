using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // There are no mesh components to a TMX file, this is for convenience in mesh-ifying Tiled layers
    public class TmxMesh
    {
        // Unity meshes have a limit on the number of vertices they can contain (65534)
        // Each face of a mesh has 4 vertices so we are limited to 65534 / 4 = 16383 faces
        // Note: In some cases, Unity still splits up a mesh (incorrectly) into "1 parts" with 16383 faces so we go with 16382 faces to be extra safe.
        private static readonly int MaxNumberOfTiles = 16382;

        // Reference back to the layer.
        public TmxLayer Layer { get; private set; }

        public string UniqueMeshName { get; private set; }
        public string ObjectName { get; private set; }
        public TmxImage TmxImage { get; private set; }
        public uint[] TileIds { get; private set; }

        public int StartingTileIndex { get; private set; }
        public int NumberOfTiles { get; private set; }

        // Animation properties
        public int StartTimeMs { get; private set; }
        public int DurationMs { get; private set; }
        public int FullAnimationDurationMs { get; private set; }

        public bool IsMeshFull()
        {
            return this.NumberOfTiles >= TmxMesh.MaxNumberOfTiles;
        }

        public uint GetTileIdAt(int tileIndex)
        {
            int fauxIndex = tileIndex - this.StartingTileIndex;
            if (fauxIndex < 0 || fauxIndex >= this.TileIds.Length)
            {
                return 0;
            }

            return this.TileIds[fauxIndex];
        }

        private void AddTile(int index, uint tileId)
        {
            // Assumes non-zero tileIds
            this.TileIds[index] = tileId;
            this.NumberOfTiles++;

            // Is the mesh "full" now
            if (IsMeshFull())
            {
                List<uint> tiles = this.TileIds.ToList();

                // Remove leading batch of zero tiles
                int firstNonZero = tiles.FindIndex(t => t != 0);
                if (firstNonZero > 0)
                {
                    this.StartingTileIndex = firstNonZero;
                    tiles.RemoveRange(0, firstNonZero);
                }
                
                // Remove the trailing batch of zero tiles
                tiles.Reverse();
                firstNonZero = tiles.FindIndex(t => t != 0);
                if (firstNonZero > 0)
                {
                    tiles.RemoveRange(0, firstNonZero);
                }

                // Reverse the tiles back
                tiles.Reverse();

                this.TileIds = tiles.ToArray();
            }
        }

        // Splits a layer into TmxMesh instances
        public static List<TmxMesh> ListFromTmxLayer(TmxLayer layer)
        {
            List<TmxMesh> meshes = new List<TmxMesh>();

            for (int i = 0; i < layer.TileIds.Count(); ++i)
            {
                // Copy the tile unto the mesh that uses the same image
                // (In other words, we are grouping tiles by images into a mesh)
                uint tileId = layer.TileIds[i];
                TmxTile tile = layer.Map.GetTileFromTileId(tileId);
                if (tile == null)
                    continue;

                int timeMs = 0;
                foreach (var frame in tile.Animation.Frames)
                {
                    uint frameTileId = frame.GlobalTileId;

                    // Have to put any rotations/flipping from the source tile into this one
                    frameTileId |= (tileId & TmxMath.FLIPPED_HORIZONTALLY_FLAG);
                    frameTileId |= (tileId & TmxMath.FLIPPED_VERTICALLY_FLAG);
                    frameTileId |= (tileId & TmxMath.FLIPPED_DIAGONALLY_FLAG);

                    // Find a mesh to stick this tile into (if it exists)
                    TmxMesh mesh = meshes.Find(m => m.CanAddFrame(tile, timeMs, frame.DurationMs));
                    if (mesh == null)
                    {
                        // Create a new mesh and add it to our list
                        mesh = new TmxMesh();
                        mesh.Layer = layer;
                        mesh.TileIds = new uint[layer.TileIds.Count()];
                        mesh.UniqueMeshName = String.Format("mesh_{0}", layer.Map.GetUniqueId().ToString("D4"));
                        mesh.TmxImage = tile.TmxImage;

                        // Keep track of the timing for this mesh (non-animating meshes will have a start time and duration of 0)
                        mesh.StartTimeMs = timeMs;
                        mesh.DurationMs = frame.DurationMs;
                        mesh.FullAnimationDurationMs = tile.Animation.TotalTimeMs;

                        mesh.ObjectName = Path.GetFileNameWithoutExtension(tile.TmxImage.AbsolutePath);
                        if (mesh.DurationMs != 0)
                        {
                            // Decorate the name a bit with some animation details for the frame
                            mesh.ObjectName += string.Format("[{0}-{1}]", timeMs, timeMs + mesh.DurationMs);
                        }

                        meshes.Add(mesh);
                    }

                    // This mesh contains this tile
                    mesh.AddTile(i, frameTileId);

                    // Advance time
                    timeMs += frame.DurationMs;
                }
            }

            return meshes;
        }

        // Creates a TmxMesh from a tile (for tile objects)
        public static List<TmxMesh> FromTmxTile(TmxTile tmxTile, TmxMap tmxMap)
        {
            List<TmxMesh> meshes = new List<TmxMesh>();

            int timeMs = 0;
            foreach (var frame in tmxTile.Animation.Frames)
            {
                uint frameTileId = frame.GlobalTileId;
                TmxTile frameTile = tmxMap.Tiles[frameTileId];

                TmxMesh mesh = new TmxMesh();
                mesh.TileIds = new uint[1];
                mesh.TileIds[0] = frameTileId;

                mesh.UniqueMeshName = String.Format("mesh_tile_{0}", TmxMath.GetTileIdWithoutFlags(frameTileId).ToString("D4"));
                mesh.TmxImage = frameTile.TmxImage;
                mesh.ObjectName = "tile_obj";

                // Keep track of the timing for this mesh (non-animating meshes will have a start time and duration of 0)
                mesh.StartTimeMs = timeMs;
                mesh.DurationMs = frame.DurationMs;
                mesh.FullAnimationDurationMs = tmxTile.Animation.TotalTimeMs;

                if (mesh.DurationMs != 0)
                {
                    // Decorate the name a bit with some animation details for the frame
                    mesh.ObjectName += string.Format("[{0}-{1}]", timeMs, timeMs + mesh.DurationMs);
                }

                // Advance time
                timeMs += frame.DurationMs;

                // Add the animation frame to our list of meshes
                meshes.Add(mesh);
            }

            return meshes;
        }

        private bool CanAddFrame(TmxTile tile, int startMs, int durationMs)
        {
            if (IsMeshFull())
                return false;

            if (this.TmxImage != tile.TmxImage)
                return false;

            if (this.StartTimeMs != startMs)
                return false;

            if (this.DurationMs != durationMs)
                return false;

            return true;
        }

    }
}
