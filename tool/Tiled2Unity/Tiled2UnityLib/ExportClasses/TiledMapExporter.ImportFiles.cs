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
                return tmxImage.AbsolutePath.ToLower().GetHashCode();
            }
        }

        private List<XElement> CreateImportFilesElements(string exportToUnityProjectPath)
        {
            List<XElement> elements = new List<XElement>();

            foreach (var importMesh in EnumerateImportMeshElements())
            {
                elements.Add(importMesh);
            }

            foreach (var texture in EnumerateTextureElements(exportToUnityProjectPath))
            {
                elements.Add(texture);
            }

            return elements;
        }

        private IEnumerable<XElement> EnumerateImportMeshElements()
        {
            foreach (var tuple in EnumerateWavefrontData())
            {
                var meshName = tuple.Item1;
                var wavefront = tuple.Item2;
                string path = String.Format("{0}.obj", meshName);
                yield return new XElement("ImportMesh",
                                            new XAttribute("filename", path),
                                            StringToBase64String(wavefront.ToString()));
            }
        }

        private IEnumerable<XElement> EnumerateTextureElements(string exportToUnityProjectPath)
        {
            // Add all image files as compressed base64 strings
            var layerImages = from layer in this.tmxMap.EnumerateTileLayers()
                              where layer.Visible == true
                              from rawTileId in layer.TileIds
                              where rawTileId != 0
                              let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                              let tile = this.tmxMap.Tiles[tileId]
                              select tile.TmxImage;

            // Find the images from the frames as well
            var frameImages = from layer in this.tmxMap.EnumerateTileLayers()
                              where layer.Visible == true
                              from rawTileId in layer.TileIds
                              where rawTileId != 0
                              let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                              let tile = this.tmxMap.Tiles[tileId]
                              from rawFrame in tile.Animation.Frames
                              let frameId = TmxMath.GetTileIdWithoutFlags(rawFrame.GlobalTileId)
                              let frame = this.tmxMap.Tiles[frameId]
                              select frame.TmxImage;


            // Tile Objects may have images not yet references by a layer
            var objectImages = from objectGroup in this.tmxMap.EnumerateObjectLayers()
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
            images.AddRange(frameImages);
            images.AddRange(objectImages);

            // Get rid of duplicate images
            TmxImageComparer imageComparer = new TmxImageComparer();
            images = images.Distinct(imageComparer).ToList();

            foreach (TmxImage image in images)
            {
                // The source texture is internal if it has a sibling *.meta file
                // We don't want to copy internal textures into Unity because they are already there.
                bool isInternal = File.Exists(image.AbsolutePath + ".meta");
                if (isInternal)
                {
                    // The texture is already in the Unity project so don't import
                    XElement xmlInternalTexture = new XElement("InternalTexture");

                    // The path to the texture will be WRT to the Unity project root
                    string assetsFolder = GetUnityAssetsPath(image.AbsolutePath);
                    string assetPath = image.AbsolutePath.Remove(0, assetsFolder.Length);
                    assetPath = "Assets" + assetPath;
                    assetPath = assetPath.Replace("\\", "/");

                    Logger.WriteLine("InternalTexture : {0}", assetPath);

                    // Path to texture in the asset directory
                    xmlInternalTexture.SetAttributeValue("assetPath", assetPath);
                    xmlInternalTexture.SetAttributeValue("materialName", image.ImageName);

                    // Transparent color key?
                    if (!String.IsNullOrEmpty(image.TransparentColor))
                    {
                        xmlInternalTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                    }

                    // Are we using depth shaders on our materials?
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        xmlInternalTexture.SetAttributeValue("usesDepthShaders", true);
                    }

                    // Will the material be loaded as a resource?
                    if (this.tmxMap.IsResource)
                    {
                        xmlInternalTexture.SetAttributeValue("isResource", true);
                    }

                    yield return xmlInternalTexture;
                }
                else
                {
                    // The texture needs to be imported into the Unity project (under Tiled2Unity's care)
                    XElement xmlImportTexture = new XElement("ImportTexture");

                    // Note that compression is not available in Unity. Go with Base64 string. Blerg.
                    Logger.WriteLine("ImportTexture : will import '{0}' to {1}", image.AbsolutePath, Path.Combine(exportToUnityProjectPath, "Textures"));

                    // Is there a color key for transparency?
                    if (!String.IsNullOrEmpty(image.TransparentColor))
                    {
                        xmlImportTexture.SetAttributeValue("alphaColorKey", image.TransparentColor);
                    }

                    // Are we using depth shaders on our materials?
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        xmlImportTexture.SetAttributeValue("usesDepthShaders", true);
                    }

                    // Will the material be loaded as a resource?
                    if (this.tmxMap.IsResource)
                    {
                        xmlImportTexture.SetAttributeValue("isResource", true);
                    }

                    // Bake the image file into the xml
                    string filename = image.ImageName + Path.GetExtension(image.AbsolutePath);
                    xmlImportTexture.Add(new XAttribute("filename", filename), FileToBase64String(image.AbsolutePath));

                    yield return xmlImportTexture;
                }
            }
        }

        // Assumes the path passed in is within the "Assets" directory of a Unity project
        private string GetUnityAssetsPath(string path)
        {
            string folderPath = Path.GetDirectoryName(path);
            while (!String.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                string folderName = Path.GetFileName(folderPath);
                if (String.Compare(folderName, "Assets", true) == 0)
                {
                    return folderPath;
                }
                folderPath = Path.GetDirectoryName(folderPath);
            }

            return Path.GetDirectoryName(path);
        }

    } // end class
} // end namespace
