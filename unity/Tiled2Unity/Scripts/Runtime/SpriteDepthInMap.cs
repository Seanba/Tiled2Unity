using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

// Helper class to figure out how a sprite's z component should be set as they traverse a Tiled map
// Can use as a behvaior that will do the work for you each update. Note this will change your sprite's z-component value on you which may collide with other behaviours.
namespace Tiled2Unity
{
    public class SpriteDepthInMap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The TiledMap instance our sprite is interacting with.")]
        private Tiled2Unity.TiledMap _AttachedMap = null;

        [Tooltip("Which layer on the TiledMap our sprite is interacting with. Will render above lower layers and below higher layers.")]
        public int PlacedOnLayerIndex = 0;

        public Tiled2Unity.TiledMap AttachedMap
        {
            get { return this._AttachedMap; }
            set
            {
                this._AttachedMap = value;
                AttachedMapChanged();
            }
        }

        private Vector2 StaggerPositionOffset = default(Vector2);
        private Vector2 SeparationPoint1 = default(Vector2);
        private Vector2 SeparationPoint2 = default(Vector2);
        private Vector2 SeparationAxis1 = default(Vector2);
        private Vector2 SeparationAxis2 = default(Vector2);

        private void Start()
        {
            if (this.AttachedMap == null)
            {
                Debug.LogError(String.Format("Sprite must be attached to a TiledMap instance in order to calucluate the 'z-depth' on that map. Check the SpriteDepthInMap component in the Inspector."));
                return;
            }

            // Get everything ready for our relationship with an attached map
            AttachedMapChanged();
        }

        private void AttachedMapChanged()
        {
            int tileWidth = this.AttachedMap.TileWidth;
            int tileHeight = this.AttachedMap.TileHeight;
            var staggerIndex = this.AttachedMap.StaggerIndex;
            var staggerAxis = this.AttachedMap.StaggerAxis;
            var orientation = this.AttachedMap.Orientation;
            int hexSideLength = (orientation == TiledMap.MapOrientation.Hexagonal) ? this.AttachedMap.HexSideLength : 0;

            // Need to make up for stagger index and stagger axis in finding the cell found at position (0, 0)
            if (staggerIndex == TiledMap.MapStaggerIndex.Even)
            {
                // The first tile is offset by half-width in this case
                this.StaggerPositionOffset = new Vector2(tileWidth * 0.5f, 0);
            }
            else
            {
                // No offset needed
                this.StaggerPositionOffset = default(Vector2);
            }

            // Figure out which axes we'll be using to determine if point is in Isometric diamond or Hexagonal cell
            // Also determine which points on the cell we'll be using to project onto the axes (as part of a Separation of Axes Theorem test)
            // These caluculations are generalized for staggered Iso and Hex (in the Iso case, 'Side Length' is always 0)
            int sideWidth = (staggerAxis == TiledMap.MapStaggerAxis.X) ? hexSideLength : 0;
            int sideHeight = (staggerAxis == TiledMap.MapStaggerAxis.Y) ? hexSideLength : 0;

            Vector2 hexUp = new Vector2(0, -sideHeight * 0.5f);
            Vector2 hexLeft = new Vector2(-sideWidth * 0.5f, 0);

            Vector2 leftMiddle = new Vector2(0, tileHeight * 0.5f);
            Vector2 topCenter = new Vector2(tileWidth * 0.5f, 0);

            Vector2 hexPointLeft1 = topCenter + hexLeft;
            Vector2 hexPointLeft2 = leftMiddle + hexUp;

            Vector2 hexPointRight1 = hexPointLeft1 + new Vector2(sideWidth, 0);
            Vector2 hexPointRight2 = hexPointLeft2 + new Vector2(tileWidth, 0);

            // Calculate our axes (which are normals to the sides we're using for axis-testing)
            Vector2 line1 = hexPointLeft2 - hexPointLeft1;
            Vector2 line2 = hexPointRight2 - hexPointRight1;
            this.SeparationAxis1 = new Vector2(line1.y, -line1.x);
            this.SeparationAxis2 = new Vector2(-line2.y, line2.x);

            // Calculate the points we'll use to project unto the axes
            if (sideHeight > sideWidth)
            {
                // Use the top and bottom points
                this.SeparationPoint1 = topCenter;
                this.SeparationPoint2 = topCenter + new Vector2(0, tileHeight);
            }
            else
            {
                // Use the left and right points
                this.SeparationPoint1 = leftMiddle;
                this.SeparationPoint2 = leftMiddle + new Vector2(tileWidth, 0);
            }
        }

        private void Update()
        {
            var orientation = this.AttachedMap.Orientation;
            bool isStaggered = this.AttachedMap.AreTilesStaggered();
            float tileHeight = this.AttachedMap.TileHeight;
            Rect mapRect = this.AttachedMap.GetMapRect();

            // Find out which 'logical' cell we are on the grid
            // For staggered isometric we can have partial coordiates due to overlapping
            Vector2 coords = FindGridCoordinates(this.gameObject.transform.position);

            // Offset so that cooridates are in the center of the tile
            // (Staggered maps use half-width and half-width tiles that overlap each other)
            float offset = isStaggered ? 0.25f : 0.5f;
            coords.x += offset;
            coords.y += offset;

            // What is our y position in map-space?
            float pos_y = coords.y * tileHeight;

            // Isometric maps are special and get their 'y position' from a combination of x,y coordinates
            if (orientation == TiledMap.MapOrientation.Isometric)
            {
                pos_y = (coords.x + coords.y) * tileHeight * 0.5f;
            }

            // We have to muliply by -1 because the negative z axis is towards the player camera
            float depth_z = pos_y / mapRect.height * -1.0f;

            // We have to offset depth by which "layer" we are on (in other words, layers add z-buffer height)
            float perLayerDepth = tileHeight / mapRect.height * -1.0f;
            depth_z += perLayerDepth * this.PlacedOnLayerIndex;

            // Assign our depth value in the z component.
            this.gameObject.transform.position = new Vector3(this.gameObject.transform.position.x, this.gameObject.transform.position.y, depth_z);
        }


        private Vector2 FindGridCoordinates(Vector2 position)
        {
            // Put position into map space
            position -= (Vector2)this.AttachedMap.transform.position;

            // Un-scale the position
            position /= this.AttachedMap.ExportScale;

            // Negate y cooridnate because of different coordinate systems between Unity and Tiled
            position.y = -position.y;

            Vector2 coords = default(Vector2);

            var orientation = this.AttachedMap.Orientation;

            if (orientation == TiledMap.MapOrientation.Orthogonal)
            {
                coords.x = Mathf.Floor(position.x / this.AttachedMap.TileWidth);
                coords.y = Mathf.Floor(position.y / this.AttachedMap.TileHeight);
            }
            else if (orientation == TiledMap.MapOrientation.Isometric)
            {
                coords = ScreenToIsometric(position);
            }
            else if (orientation == TiledMap.MapOrientation.Staggered || orientation == TiledMap.MapOrientation.Hexagonal)
            {
                coords = ScreenToStaggered(position);
            }
            else
            {
                Debug.LogWarning(String.Format("Map orientation not supported: {0}", orientation));
            }

            return coords;
        }

        private Vector2 ScreenToIsometric(Vector2 position)
        {
            // (Taken from Tiled source, IsometricRenderer::screenToTileCoords)
            float x = position.x;
            float y = position.y;

            int tileWidth = this.AttachedMap.TileWidth;
            int tileHeight = this.AttachedMap.TileHeight;

            x -= this.AttachedMap.NumTilesHigh * tileWidth / 2;
            float tileY = y / tileHeight;
            float tileX = x / tileWidth;

            Vector2 coords = new Vector2();
            coords.x = Mathf.Floor(tileY + tileX);
            coords.y = Mathf.Floor(tileY - tileX);
            return coords;
        }

        private Vector2 ScreenToStaggered(Vector2 position)
        {
            Vector2 coords = default(Vector2);

            // Staggered maps need to make up for being staggered in order to calculate which cell is at the origin
            position += this.StaggerPositionOffset;

            // Staggered Isometric is really two orthogonal/hex grids layered (or in other words, staggered) over each other by some half-tile distance
            // How the grids are releated to each other is based on the stagger axis (X or Y) and stager index (Odd or Even)
            coords.x = Mathf.Floor(position.x / this.AttachedMap.TileWidth);
            coords.y = Mathf.Floor(position.y / this.AttachedMap.TileHeight);

            // To figure out which tile we are *really* on we're going to do a Separating Axis Test between the isometric diamond/hex and the point
            // We can take for granted that we only need to test aganst two axes (there are only two linearly independent line segments on the iso/hex)
            // If both axes pass then the point is within the diamond/hex
            // Otherwise, the point is in one of the corners and part of another diamond/hex

            float tileWidth = this.AttachedMap.TileWidth;
            float tileHeight = this.AttachedMap.TileHeight;

            float offset_x = coords.x * tileWidth;
            float offset_y = coords.y * tileHeight;
            Vector2 offset_v = new Vector2(offset_x, offset_y);

            Vector2 testPoint1 = this.SeparationPoint1 + offset_v;
            Vector2 testPoint2 = this.SeparationPoint2 + offset_v;

            // Axis 1 test
            {
                float t1 = Vector2.Dot(this.SeparationAxis1, testPoint1);
                float t2 = Vector2.Dot(this.SeparationAxis1, testPoint2);
                float projMin = Mathf.Min(t1, t2);
                float projMax = Mathf.Max(t1, t2);

                float projection = Vector2.Dot(this.SeparationAxis1, position);
                if (projection < projMin)
                {
                    // Top-left cornder of cell
                    coords.x -= 0.5f;
                    coords.y -= 0.5f;
                    return coords;
                }
                if (projection > projMax)
                {
                    // Bottom-right of cell
                    coords.x += 0.5f;
                    coords.y += 0.5f;
                    return coords;
                }
            }

            // Axis 2 test
            {
                float t1 = Vector2.Dot(this.SeparationAxis2, testPoint1);
                float t2 = Vector2.Dot(this.SeparationAxis2, testPoint2);
                float projMin = Mathf.Min(t1, t2);
                float projMax = Mathf.Max(t1, t2);

                float projection = Vector2.Dot(this.SeparationAxis2, position);
                if (projection < projMin)
                {
                    // Top-right of cell
                    coords.x += 0.5f;
                    coords.y -= 0.5f;
                    return coords;
                }
                if (projection > projMax)
                {
                    // Bottom-left of cell
                    coords.x -= 0.5f;
                    coords.y += 0.5f;
                    return coords;
                }
            }

            // If we got here then we're in the the tile diamond/hex and not one of the adjoining staggered tiles
            return coords;
        }



    }
}
