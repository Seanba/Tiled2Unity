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
        [Tooltip("The TiledMap instance our sprite is interacting with.")]
        public Tiled2Unity.TiledMap AttachedMap = null;

        [Tooltip("Which layer on the TiledMap our sprite is interacting with. Will render above lower layers and below higher layers. Render order of Tiles on same layer will depend on location.")]
        public int InteractWithLayer = 0;

        [Tooltip("For maps where tileset heights are different than map tile heights. Enter the tileset height here. Useful/crucial for isometric maps. Leave at default (0) if you don't care.")]
        public int TilesetHeight = 0;

        private void Start()
        {
            if (this.AttachedMap == null)
            {
                Debug.LogError(String.Format("Sprite must be attached to a TiledMap instance in order to calucluate the 'z-depth' on that map. Check the SpriteDepthInMap component in the Inspector."));
                return;
            }
        }

        private void Update()
        {
            UpdateSpriteDepth();
        }

        public void UpdateSpriteDepth()
        {
            // Put position into map space
            Vector3 spritePosition = this.gameObject.transform.position;
            spritePosition -= this.AttachedMap.gameObject.transform.position;

            // Some maps (like isometric) have a tileset height that is larger than the map tile height in order to get the isometric illusion. We need to know that difference in caluclating depth.
            if (TilesetHeight != 0)
            {
                int delta_y = this.AttachedMap.TileHeight - this.TilesetHeight;
                spritePosition.y += delta_y;
            }

            Rect mapRect = this.AttachedMap.GetMapRect();
            float depthPerLayer = -this.AttachedMap.TileHeight / mapRect.height;

            float depth_z = (spritePosition.y / this.AttachedMap.ExportScale / mapRect.height) + (depthPerLayer * this.InteractWithLayer);

            // Assign our depth value in the z component.
            this.gameObject.transform.position = new Vector3(this.gameObject.transform.position.x, this.gameObject.transform.position.y, depth_z);
        }

    }
}
