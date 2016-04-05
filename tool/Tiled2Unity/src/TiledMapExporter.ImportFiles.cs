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
        private class TmxImageComparer : IEqualityComparer<TmxImage>
        {
            public bool Equals(TmxImage lhs, TmxImage rhs)
            {
                return lhs.AbsolutePath.ToLower() == rhs.AbsolutePath.ToLower();
            }

            public int GetHashCode(TmxImage tmxImage)
            {
                return tmxImage.AbsolutePath.GetHashCode();
            }
        }

        private List<XElement> CreateImportFilesElements(string exportToUnityProjectPath)
        {
            List<XElement> elements = new List<XElement>();

            // Add the mesh file as raw text
            {
                StringWriter objBuilder = BuildObjString();

                XElement mesh =
                    new XElement("ImportMesh",
                        new XAttribute("filename", this.tmxMap.Name + ".obj"),
                        StringToBase64String(objBuilder.ToString()));

                elements.Add(mesh);
            }

            {
                // Add all image files as compressed base64 strings
                var layerImages = from layer in this.tmxMap.Layers
                                  where layer.Visible == true
                                  from rawTileId in layer.TileIds
                                  where rawTileId != 0
                                  let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                  let tile = this.tmxMap.Tiles[tileId]
                                  select tile.TmxImage;

                // Tile Objects may have images not yet references by a layer
                var objectImages = from objectGroup in this.tmxMap.ObjectGroups
                                   where objectGroup.Visible == true
                                   from tmxObject in objectGroup.Objects
                                   where tmxObject.Visible == true
                                   where tmxObject is TmxObjectTile
                                   let tmxTileObject = tmxObject as TmxObjectTile
                                   from mesh in tmxTileObject.Tile.Meshes
                                   select mesh.TmxImage;

                // Combine image paths from tile layers and object layers
                List<TmxImage> images = new List<TmxImage>();
                images.AddRange(layerImages);
                images.AddRange(objectImages);

                // Get rid of duplicate images
                TmxImageComparer imageComparer = new TmxImageComparer();
                images = images.Distinct(imageComparer).ToList();

                // Do not import files if they are already in the project (in the /Assets/ directory of where we're exporting too)
                string unityAssetsDir = Path.Combine(exportToUnityProjectPath, "Assets");

                foreach (TmxImage image in images)
                {
                    // If the copy from location comes from within the project we want to copy to, then don't do it.
                    // This allows us to have tileset images that are alreday in use by the Unity project
                    string saveToAssetsDir = unityAssetsDir.ToLower();
                    string copyFromDir = image.AbsolutePath.ToLower();

                    if (copyFromDir.StartsWith(saveToAssetsDir))
                    {
                        XElement xmlInternalTexture = new XElement("InternalTexture");

                        // The path to the texture will be WRT to the Unity project root
                        string assetPath = image.AbsolutePath.Remove(0, exportToUnityProjectPath.Length);
                        assetPath = assetPath.TrimStart('\\');
                        assetPath = assetPath.TrimStart('/');
                        Program.WriteLine("InternalTexture : {0}", assetPath);

                        // Path to texture in the asset directory
                        xmlInternalTexture.SetAttributeValue("assetPath", assetPath);

                        // Transparent color key?
                        if (!String.IsNullOrEmpty(image.TransparentColor))
                        {
                            xmlInternalTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                        }

                        elements.Add(xmlInternalTexture);
                    }
                    else
                    {
                        XElement xmlImportTexture = new XElement("ImportTexture");

                        // Note that compression is not available in Unity. Go with Base64 string. Blerg.
                        Program.WriteLine("ImportTexture : will import '{0}' to {1}", image.AbsolutePath, Path.Combine(unityAssetsDir, "Tiled2Unity\\Textures\\"));

                        // Is there a color key for transparency?
                        if (!String.IsNullOrEmpty(image.TransparentColor))
                        {
                            xmlImportTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                        }

                        // Bake the image file into the xml
                        xmlImportTexture.Add(new XAttribute("filename", Path.GetFileName(image.AbsolutePath)), FileToBase64String(image.AbsolutePath));

                        elements.Add(xmlImportTexture);
                    }
                }
            }

            return elements;
        }

    } // end class
} // end namespace
