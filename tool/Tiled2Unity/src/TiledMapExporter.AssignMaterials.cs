using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        private List<XElement> CreateAssignMaterialsElements()
        {
            // Need to match all "submeshes" with a material
            // The material will have the same name as the texture
            // Each "submesh" is a Layer+Texture combination since Wavefront Obj meshes support only 1 set of texture coordinates
            var faces = from layer in tmxMap.Layers
                        where layer.Visible == true
                        from y in Enumerable.Range(0, layer.Height)
                        from x in Enumerable.Range(0, layer.Width)
                        let tileId = layer.GetTileIdAt(x, y)
                        where tileId != 0
                        let tile = this.tmxMap.Tiles[tileId]
                        select new
                        {
                            LayerName = layer.UniqueName,
                            ImageName = Path.GetFileNameWithoutExtension(tile.TmxImage.Path),
                            TransparentColor = tile.TmxImage.TransparentColor,
                            SortingLayer = layer.Properties.GetPropertyValueAsString("unity:sortingLayerName", ""),
                            SortingOrder = layer.Properties.GetPropertyValueAsInt("unity:sortingOrder", tmxMap.Layers.IndexOf(layer)),
                        };

            var groups = from f in faces
                         group f by TiledMapExpoterUtils.UnityFriendlyMeshName(tmxMap, f.LayerName, f.ImageName);

            var assignments = from g in groups
                              select new
                              {
                                  MeshName = g.Key,
                                  MaterialName = g.First().ImageName,
                                  TransparentColor = g.First().TransparentColor,
                                  SortingLayer = g.First().SortingLayer,
                                  SortingOrder = g.First().SortingOrder,
                              };

            List<XElement> elements = new List<XElement>();
            foreach (var ass in assignments)
            {
                XElement assignment =
                    new XElement("AssignMaterial",
                        new XAttribute("mesh", ass.MeshName),
                        new XAttribute("material", ass.MaterialName),
                        new XAttribute("sortingLayerName", ass.SortingLayer),
                        new XAttribute("sortingOrder", ass.SortingOrder));

                // Is there a transparent color key?
                if (!String.IsNullOrEmpty(ass.TransparentColor))
                {
                    assignment.SetAttributeValue("alphaColorKey", ass.TransparentColor);
                }

                elements.Add(assignment);
            }
            return elements;
        }
    }
}
