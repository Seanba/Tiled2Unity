using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;
using System.Xml.Linq;


namespace Tiled2Unity
{
    partial class TiledMapExporter
    {
        // As we build the prefab, what context are we in?
        public enum PrefabContext
        {
            Root,
            TiledLayer,
            ObjectLayer,
            Object,
        }

        private XElement CreatePrefabElement()
        {
            // And example of the kind of xml element we're building
            // Note that "layer" is overloaded. There is the concept of layers in both Tiled and Unity
            //  <Prefab name="NameOfTmxFile">
            //
            //    <GameObject name="FirstLayerName tag="OptionalTagName" layer="OptionalUnityLayerName">
            //      <GameObject Copy="FirstLayerName+FirstTilesetName" />
            //      <GameObject Copy="FirstLayerName+SecondTilesetName" />
            //      <GameOject name="Collision">
            //        <PolygonCollider2D>
            //          <Path>data for first path</Path>
            //          <Path>data for second path</Path>
            //        </PolygonCollider2D>
            //      </GameOject name="Collision">
            //    </GameObject>
            //
            //    <GameObject name="SecondLayerName">
            //      <GameObject Copy="SecondLayerName+AnotherTilesetName" />
            //    </GameObject>
            //
            //    <GameObject name="Colliders">
            //      <PolygonCollider2D> ...
            //      <CircleCollider2D> ...
            //      <BoxCollider2D>...
            //    </GameObject>
            //
            //    <GameObject name="ObjectGroupName">
            //      <GameObject name="ObjectName">
            //          <Property name="PropertyName"> ... some custom data ...
            //          <Property name="PropertyName"> ... some custom data ...
            //      </GameObject>
            //    </GameObject>
            //
            //  </Prefab>

            XElement prefab = new XElement("Prefab");
            prefab.SetAttributeValue("name", this.tmxMap.Name);
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);
            prefab.SetAttributeValue("exportScale", Program.Scale);
            AssignUnityProperties(this.tmxMap, prefab, PrefabContext.Root);
            AssignTiledProperties(this.tmxMap, prefab);

            // We create an element for each tiled layer and add that to the prefab
            {
                List<XElement> layerElements = new List<XElement>();
                foreach (var layer in this.tmxMap.Layers)
                {
                    if (layer.Visible == false)
                        continue;

                    XElement layerElement =
                        new XElement("GameObject",
                            new XAttribute("name", layer.Name));

                    if (layer.Properties.GetPropertyValueAsBoolean("unity:collisionOnly", false) == false)
                    {
                        // Submeshes for the layer (layer+material)
                        var meshElements = CreateMeshElementsForLayer(layer);
                        layerElement.Add(meshElements);
                    }

                    // Collision data for the layer
                    var collisionElements = CreateCollisionElementForLayer(layer);
                    layerElement.Add(collisionElements);

                    AssignUnityProperties(layer, layerElement, PrefabContext.TiledLayer);
                    AssignTiledProperties(layer, layerElement);

                    // Add the element to our list of layers
                    layerElements.Add(layerElement);
                }

                prefab.Add(layerElements);
            }

            // Add all our object groups (may contain colliders)
            {
                var collidersObjectGroup = from item in this.tmxMap.ObjectGroups
                                           where item.Visible == true
                                           select item;

                List<XElement> objectGroupElements = new List<XElement>();
                foreach (var objGroup in collidersObjectGroup)
                {
                    XElement gameObject = new XElement("GameObject", new XAttribute("name", objGroup.Name));
                    AssignUnityProperties(objGroup, gameObject, PrefabContext.ObjectLayer);
                    AssignTiledProperties(objGroup, gameObject);

                    List<XElement> colliders = CreateObjectElementList(objGroup);
                    if (colliders.Count() > 0)
                    {
                        gameObject.Add(colliders);
                    }

                    objectGroupElements.Add(gameObject);
                }

                if (objectGroupElements.Count() > 0)
                {
                    prefab.Add(objectGroupElements);
                }
            }

            return prefab;
        }

        private List<XElement> CreateObjectElementList(TmxObjectGroup objectGroup)
        {
            List<XElement> elements = new List<XElement>();

            foreach (TmxObject tmxObject in objectGroup.Objects)
            {
                // All the objects/colliders in our object group need to be separate game objects because they can have unique tags/layers
                XElement xmlObject = new XElement("GameObject", new XAttribute("name", tmxObject.GetNonEmptyName()));

                Vector3D pos = PointFToUnityVector(tmxObject.Position);
                xmlObject.SetAttributeValue("x", pos.X);
                xmlObject.SetAttributeValue("y", pos.Y);
                xmlObject.SetAttributeValue("rotation", tmxObject.Rotation);

                AssignUnityProperties(tmxObject, xmlObject, PrefabContext.Object);
                AssignTiledProperties(tmxObject, xmlObject);

                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    objElement = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    objElement = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, objectGroup.Name);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    objElement = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    objElement = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    AssignTileObjectProperites(tmxObject as TmxObjectTile, xmlObject);
                }
                else
                {
                    Program.WriteLine("Object '{0}' has been added for use with custom importers", tmxObject);
                }

                if (objElement != null)
                {
                    xmlObject.Add(objElement);
                }

                elements.Add(xmlObject);
            }

            return elements;
        }

        private List<XElement> CreateMeshElementsForLayer(TmxLayer layer)
        {
            // Mesh elements look like this:
            // <GameObject copy="LayerName+TilesetName" />
            // (The importer in Unity will look a submesh of that name and copy it to our prefab)
            // (This is complicated by potential tile animations now)

            var meshes = from rawTileId in layer.TileIds
                         where rawTileId != 0
                         let tileId = TmxMath.GetTileIdWithoutFlags(rawTileId)
                         let tile = this.tmxMap.Tiles[tileId]
                         let name = TiledMapExpoterUtils.UnityFriendlyMeshName(tmxMap, layer.Name, Path.GetFileNameWithoutExtension(tile.TmxImage.Path))
                         group tile.Animation by name into meshGroup
                         select meshGroup;

            List<XElement> xmlMeshes = new List<XElement>();
            foreach (var m in meshes)
            {
                XElement xmlMesh = new XElement("GameObject", new XAttribute("copy", m.Key));

                // Do we have any animations?
                var animations = m.Distinct();
                foreach (var anim in animations)
                {
                    if (anim != null)
                    {
                        XElement xmlAnim = new XElement("TileAnimator");
                        foreach (var frame in anim.Frames)
                        {
                            xmlAnim.Add(new XElement("Frame", new XAttribute("vertex_z", frame.UniqueFrameId), new XAttribute("duration", frame.DurationMs)));
                        }
                        xmlMesh.Add(xmlAnim);
                    }
                }

                xmlMeshes.Add(xmlMesh);
            }

            return xmlMeshes;
        }

        private void AssignUnityProperties<T>(T tmx, XElement xml, PrefabContext context) where T : TmxHasProperties
        {
            // Only the root of the prefab can have a scale
            string unityScale = tmx.Properties.GetPropertyValueAsString("unity:scale", "");
            if (!String.IsNullOrEmpty(unityScale))
            {
                float scale = 1.0f;
                if (context != PrefabContext.Root)
                {
                    Program.WriteWarning("unity:scale only applies to map properties\n{0}", xml.ToString());
                }
                else if (!Single.TryParse(unityScale, out scale))
                {
                    Program.WriteError("unity:scale property value '{0}' could not be converted to a float", unityScale);
                }
                else
                {
                    xml.SetAttributeValue("scale", unityScale);
                }
            }

            // Any object can carry the 'isTrigger' setting and we assume any children to inherit the setting
            string unityIsTrigger = tmx.Properties.GetPropertyValueAsString("unity:isTrigger", "");
            if (!String.IsNullOrEmpty(unityIsTrigger))
            {
                bool isTrigger = false;
                if (!Boolean.TryParse(unityIsTrigger, out isTrigger))
                {
                    Program.WriteError("unity:isTrigger property value '{0}' cound not be converted to a boolean", unityIsTrigger);
                }
                else
                {
                    xml.SetAttributeValue("isTrigger", unityIsTrigger);
                }
            }

            // Any part of the prefab can be assigned a 'layer'
            string unityLayer = tmx.Properties.GetPropertyValueAsString("unity:layer", "");
            if (!String.IsNullOrEmpty(unityLayer))
            {
                xml.SetAttributeValue("layer", unityLayer);
            }

            // Any part of the prefab can be assigned a 'tag'
            string unityTag = tmx.Properties.GetPropertyValueAsString("unity:tag", "");
            if (!String.IsNullOrEmpty(unityTag))
            {
                xml.SetAttributeValue("tag", unityTag);
            }

            List<String> knownProperties = new List<string>();
            knownProperties.Add("unity:layer");
            knownProperties.Add("unity:tag");
            knownProperties.Add("unity:sortingLayerName");
            knownProperties.Add("unity:sortingOrder");
            knownProperties.Add("unity:scale");
            knownProperties.Add("unity:isTrigger");
            knownProperties.Add("unity:ignore");
            knownProperties.Add("unity:collisionOnly");

            var unknown = from p in tmx.Properties.PropertyMap
                          where p.Key.StartsWith("unity:")
                          where knownProperties.Contains(p.Key) == false
                          select p.Key;
            foreach (var p in unknown)
            {
                Program.WriteWarning("Unknown unity property '{0}' in GameObject '{1}'", p, tmx.ToString());
            }
        }

        private void AssignTiledProperties<T>(T tmx, XElement xml) where T : TmxHasProperties
        {
            List<XElement> xmlProperties = new List<XElement>();

            foreach (var prop in tmx.Properties.PropertyMap)
            {
                // Ignore properties that start with "unity:"
                if (prop.Key.StartsWith("unity:"))
                    continue;

                var alreadyProperty = from p in xml.Elements("Property")
                                      where p.Attribute("name") != null
                                      where p.Attribute("name").Value == prop.Key
                                      select p;
                if (alreadyProperty.Count() > 0)
                {
                    // Don't override property that is already there
                    continue;
                }


                XElement xmlProp = new XElement("Property", new XAttribute("name", prop.Key), new XAttribute("value", prop.Value));
                xmlProperties.Add(xmlProp);
            }

            xml.Add(xmlProperties);
        }


        private XElement CreateBoxColliderElement(TmxObjectRectangle tmxRectangle)
        {
            XElement xmlCollider =
                new XElement("BoxCollider2D",
                    new XAttribute("width", tmxRectangle.Size.Width * Program.Scale),
                    new XAttribute("height", tmxRectangle.Size.Height * Program.Scale));

            return xmlCollider;
        }

        private XElement CreateCircleColliderElement(TmxObjectEllipse tmxEllipse, string objGroupName)
        {

            if (!tmxEllipse.IsCircle())
            {
                Program.WriteError("Collision ellipse in Object Layer '{0}' is not a circle: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else
            {
                XElement circleCollider =
                    new XElement("CircleCollider2D",
                        new XAttribute("radius", tmxEllipse.Radius * Program.Scale));

                return circleCollider;
            }
        }

        private XElement CreatePolygonColliderElement(TmxObjectPolygon tmxPolygon)
        {
            // The points need to be transformed into unity space
            var points = from pt in tmxPolygon.Points
                         select PointFToUnityVector(pt);

            XElement polygonCollider =
                new XElement("PolygonCollider2D",
                    new XElement("Path", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return polygonCollider;
        }

        private XElement CreateEdgeColliderElement(TmxObjectPolyline tmxPolyine)
        {
            // The points need to be transformed into unity space
            var points = from pt in tmxPolyine.Points
                         select PointFToUnityVector(pt);

            XElement edgeCollider =
                new XElement("EdgeCollider2D",
                    new XElement("Points", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return edgeCollider;
        }

        private void AssignTileObjectProperites(TmxObjectTile tmxTile, XElement xmlObject)
        {
            // We combine the properties of the tile that is referenced and add it to our own properties
            AssignTiledProperties(tmxTile.Tile, xmlObject);
        }



    } // end class
} // end namespace
