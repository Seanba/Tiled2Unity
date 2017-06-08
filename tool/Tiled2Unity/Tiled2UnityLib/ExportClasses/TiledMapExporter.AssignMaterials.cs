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
            // Each mesh in each viewable layer needs to have its material assigned to it
            List<XElement> elements = new List<XElement>();
            foreach (var layer in this.tmxMap.EnumerateTileLayers())
            {
                if (layer.Visible == false)
                    continue;
                if (layer.Ignore == TmxLayer.IgnoreSettings.Visual)
                    continue;

                foreach (TmxMesh mesh in layer.Meshes)
                {
                    XElement assignment =
                        new XElement("AssignMaterial",
                            new XAttribute("mesh", mesh.UniqueMeshName),
                            new XAttribute("material", Path.GetFileNameWithoutExtension(mesh.TmxImage.AbsolutePath)));

                    elements.Add(assignment);
                }
            }

            // Each mesh for each TileObject needs its material assigned
            foreach (var tmxMesh in this.tmxMap.GetUniqueListOfVisibleObjectTileMeshes())
            {
                XElement assignment =
                     new XElement("AssignMaterial",
                         new XAttribute("mesh", tmxMesh.UniqueMeshName),
                         new XAttribute("material", Path.GetFileNameWithoutExtension(tmxMesh.TmxImage.AbsolutePath)));

                    elements.Add(assignment);
            }

            return elements;
        }
    }
}
