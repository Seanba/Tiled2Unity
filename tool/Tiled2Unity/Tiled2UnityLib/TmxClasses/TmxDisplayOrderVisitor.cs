using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tiled2Unity
{
    // Visits the tmx map and gathers information used for sorting, depth and draw order
    class TmxDisplayOrderVisitor : ITmxVisitor
    {
        private int drawOrderIndex = 0;
        private int depthBufferIndex = 0;

        public void VisitMap(TmxMap map)
        {
        }

        public void VisitGroupLayer(TmxGroupLayer groupLayer)
        {
            // Group layer does not advance draw index
            groupLayer.DrawOrderIndex = this.drawOrderIndex;

            // But does advance buffer index
            groupLayer.DepthBufferIndex = this.depthBufferIndex++;
        }


        public void VisitObject(TmxObject obj)
        {
            // Objects only increase draw order if they are tiles
            if (obj is TmxObjectTile)
            {
                TmxObjectTile tile = obj as TmxObjectTile;
                tile.DrawOrderIndex = this.drawOrderIndex++;
            }
        }

        public void VisitObjectLayer(TmxObjectGroup objectLayer)
        {
            // Object layer does not advance draw index
            objectLayer.DrawOrderIndex = this.drawOrderIndex;

            // Children don't have a depth buffer index. Their parent sets the depth.
            objectLayer.DepthBufferIndex = (objectLayer.ParentNode != null) ? 0 : this.depthBufferIndex++;
        }

        public void VisitTileLayer(TmxLayer tileLayer)
        {
            // Tile layer does render something and therefore increases draw order index
            tileLayer.DrawOrderIndex = this.drawOrderIndex++;

            // Children don't have a depth buffer index. Their parent sets the depth.
            tileLayer.DepthBufferIndex = (tileLayer.ParentNode != null) ? 0 : this.depthBufferIndex++;
        }

    }
}
