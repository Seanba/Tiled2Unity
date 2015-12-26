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
                var imagePaths = from layer in this.tmxMap.Layers
                                 where layer.Visible == true
                                 from rawTileId in layer.TileIds
                                 where rawTileId != 0
                                 let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                                 let tile = this.tmxMap.Tiles[tileId]
                                 select tile.TmxImage.AbsolutePath;
                imagePaths = imagePaths.Distinct();

                // Do not import files if they are already in the project (in the /Assets/ directory of where we're exporting too)
                string unityAssetsDir = Path.Combine(exportToUnityProjectPath, "Assets");

                foreach (string path in imagePaths)
                {
                    // If the copy from location comes from within the project we want to copy to, then don't do it.
                    // This allows us to have tileset images that are alreday in use by the Unity project
                    string saveToAssetsDir = unityAssetsDir.ToLower();
                    string copyFromDir = path.ToLower();
                    if (copyFromDir.StartsWith(saveToAssetsDir))
                    {
                        // The path to the texture will be WRT to the Unity project root
                        string assetPath = path.Remove(0, exportToUnityProjectPath.Length);
                        assetPath = assetPath.TrimStart('\\');
                        assetPath = assetPath.TrimStart('/');
                        Program.WriteLine("InternalTexture : {0}", assetPath);

                        XElement texture = new XElement("InternalTexture", new XAttribute("assetPath", assetPath));
                        elements.Add(texture);
                    }
                    else
                    {
                        // Note that compression is not available in Unity. Go with Base64 string. Blerg.
                        Program.WriteLine("ImportTexture : will import '{0}' to {1}", path, Path.Combine(unityAssetsDir, "Tiled2Unity\\Textures\\"));
                        XElement texture =
                            new XElement("ImportTexture",
                                new XAttribute("filename", Path.GetFileName(path)),
                                FileToBase64String(path));
                        //FileToCompressedBase64String(path));

                        elements.Add(texture);
                    }
                }
            }

            return elements;
        }

    } // end class
} // end namespace
