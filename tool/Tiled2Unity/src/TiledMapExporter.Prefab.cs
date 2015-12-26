using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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

        // Helper delegate to modify points by some transformation
        private delegate void TransformVerticesFunc(PointF[] verts);

        private XElement CreatePrefabElement()
        {
            // And example of the kind of xml element we're building
            // Note that "layer" is overloaded. There is the concept of layers in both Tiled and Unity
            //  <Prefab name="NameOfTmxFile">
            //
            //    <GameObject name="FirstLayerName tag="OptionalTagName" layer="OptionalUnityLayerName">
            //      <GameObject Copy="[mesh_name]" />
            //      <GameObject Copy="[another_mesh_name]" />
            //      <GameOject name="Collision">
            //        <PolygonCollider2D>
            //          <Path>data for first path</Path>
            //          <Path>data for second path</Path>
            //        </PolygonCollider2D>
            //      </GameOject name="Collision">
            //    </GameObject>
            //
            //    <GameObject name="SecondLayerName">
            //      <GameObject Copy="[yet_another_mesh_name]" />
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

            Size sizeInPixels = this.tmxMap.MapSizeInPixels();

            XElement prefab = new XElement("Prefab");
            prefab.SetAttributeValue("name", this.tmxMap.Name);
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);
            prefab.SetAttributeValue("exportScale", Program.Scale);
            prefab.SetAttributeValue("mapWidthInPixels", sizeInPixels.Width);
            prefab.SetAttributeValue("mapHeightInPixels", sizeInPixels.Height);
            AssignUnityProperties(this.tmxMap, prefab, PrefabContext.Root);
            AssignTiledProperties(this.tmxMap, prefab);

            // We create an element for each tiled layer and add that to the prefab
            {
                List<XElement> layerElements = new List<XElement>();
                foreach (var layer in this.tmxMap.Layers)
                {
                    if (layer.Visible == false)
                        continue;

                    PointF offset = PointFToUnityVector(layer.Offset);

                    XElement layerElement =
                        new XElement("GameObject",
                            new XAttribute("name", layer.Name),
                            new XAttribute("x", offset.X),
                            new XAttribute("y", offset.Y));

                    if (layer.Ignore != TmxLayer.IgnoreSettings.Visual)
                    {
                        // Submeshes for the layer (layer+material)
                        var meshElements = CreateMeshElementsForLayer(layer);
                        layerElement.Add(meshElements);
                    }

                    // Collision data for the layer
                    if (layer.Ignore != TmxLayer.IgnoreSettings.Collision)
                    {
                        var collisionElements = CreateCollisionElementForLayer(layer);
                        layerElement.Add(collisionElements);
                    }

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

                    // Offset the object group
                    PointF offset = PointFToUnityVector(objGroup.Offset);
                    gameObject.SetAttributeValue("x", offset.X);
                    gameObject.SetAttributeValue("y", offset.Y);

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

                // Transform object locaction into map space (needed for isometric and hex modes) 
                PointF xfPosition = TmxMath.ObjectPointFToMapSpace(this.tmxMap, tmxObject.Position);
                PointF pos = PointFToUnityVector(xfPosition);
                xmlObject.SetAttributeValue("x", pos.X);
                xmlObject.SetAttributeValue("y", pos.Y);
                xmlObject.SetAttributeValue("rotation", tmxObject.Rotation);

                AssignUnityProperties(tmxObject, xmlObject, PrefabContext.Object);
                AssignTiledProperties(tmxObject, xmlObject);

                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromIsometricRectangle(this.tmxMap, tmxObject as TmxObjectRectangle);
                        objElement = CreatePolygonColliderElement(tmxIsometricRectangle);
                    }
                    else
                    {
                        objElement = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                    }
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
                    AddTileObjectElements(tmxObject as TmxObjectTile, xmlObject);
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
            List<XElement> xmlMeshes = new List<XElement>();

            foreach (TmxMesh mesh in layer.Meshes)
            {
                XElement xmlMesh = new XElement("GameObject",
                    new XAttribute("name", mesh.ObjectName),
                    new XAttribute("copy", mesh.UniqueMeshName),
                    new XAttribute("sortingLayerName", layer.SortingLayerName),
                    new XAttribute("sortingOrder", layer.SortingOrder),
                    new XAttribute("opacity", layer.Opacity));
                xmlMeshes.Add(xmlMesh);

                if (mesh.FullAnimationDurationMs > 0)
                {
                    XElement xmlAnimation = new XElement("TileAnimator",
                        new XAttribute("startTimeMs", mesh.StartTimeMs),
                        new XAttribute("durationMs", mesh.DurationMs),
                        new XAttribute("fullTimeMs", mesh.FullAnimationDurationMs));
                    xmlMesh.Add(xmlAnimation);
                }
            }

            return xmlMeshes;
        }

        private void AssignUnityProperties<T>(T tmx, XElement xml, PrefabContext context) where T : TmxHasProperties
        {
            // Only the root of the prefab can have a scale
            {
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
            }

            // Only the root of the prefab can be marked a resource
            {
                string unityResource = tmx.Properties.GetPropertyValueAsString("unity:resource", "");
                if (!String.IsNullOrEmpty(unityResource))
                {
                    bool resource = false;
                    if (context != PrefabContext.Root)
                    {
                        Program.WriteWarning("unity:resource only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Boolean.TryParse(unityResource, out resource))
                    {
                        Program.WriteError("unity:resource property value '{0}' could not be converted to a boolean", unityResource);
                    }
                    else
                    {
                        xml.SetAttributeValue("resource", unityResource);
                    }
                }
            }

            // Any object can carry the 'isTrigger' setting and we assume any children to inherit the setting
            {
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
            }

            // Any part of the prefab can be assigned a 'layer'
            {
                string unityLayer = tmx.Properties.GetPropertyValueAsString("unity:layer", "");
                if (!String.IsNullOrEmpty(unityLayer))
                {
                    xml.SetAttributeValue("layer", unityLayer);
                }
            }

            // Any part of the prefab can be assigned a 'tag'
            {
                string unityTag = tmx.Properties.GetPropertyValueAsString("unity:tag", "");
                if (!String.IsNullOrEmpty(unityTag))
                {
                    xml.SetAttributeValue("tag", unityTag);
                }
            }

            List<String> knownProperties = new List<string>();
            knownProperties.Add("unity:layer");
            knownProperties.Add("unity:tag");
            knownProperties.Add("unity:sortingLayerName");
            knownProperties.Add("unity:sortingOrder");
            knownProperties.Add("unity:scale");
            knownProperties.Add("unity:isTrigger");
            knownProperties.Add("unity:ignore");
            knownProperties.Add("unity:resource");

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
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Program.WriteError("Collision ellipse in Object Layer '{0}' is not supported in Isometric maps: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else if (!tmxEllipse.IsCircle())
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
            var points = from pt in TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolygon)
                       select PointFToUnityVector(pt);

            XElement polygonCollider =
                new XElement("PolygonCollider2D",
                    new XElement("Path", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return polygonCollider;
        }

        private XElement CreateEdgeColliderElement(TmxObjectPolyline tmxPolyline)
        {
            // The points need to be transformed into unity space
            var points = from pt in TmxMath.GetPointsInMapSpace(this.tmxMap, tmxPolyline)
                         select PointFToUnityVector(pt);

            XElement edgeCollider =
                new XElement("EdgeCollider2D",
                    new XElement("Points", String.Join(" ", points.Select(pt => String.Format("{0},{1}", pt.X, pt.Y)))));

            return edgeCollider;
        }

        private void AddTileObjectElements(TmxObjectTile tmxObjectTile, XElement xmlTileObjectRoot)
        {
            // We combine the properties of the tile that is referenced and add it to our own properties
            AssignTiledProperties(tmxObjectTile.Tile, xmlTileObjectRoot);

            // TileObjects can be scaled (this is separate from vertex scaling)
            SizeF scale = tmxObjectTile.GetTileObjectScale();
            xmlTileObjectRoot.SetAttributeValue("scaleX", scale.Width);
            xmlTileObjectRoot.SetAttributeValue("scaleY", scale.Height);

            // Need another transform to help us with flipping of the tile (and their collisions)
            XElement xmlTileObject = new XElement("GameObject");
            xmlTileObject.SetAttributeValue("name", "TileObject");

            if (tmxObjectTile.FlippedHorizontal)
            {
                xmlTileObject.SetAttributeValue("x", tmxObjectTile.Tile.TileSize.Width * Program.Scale);
                xmlTileObject.SetAttributeValue("flipX", true);
            }
            if (tmxObjectTile.FlippedVertical)
            {
                xmlTileObject.SetAttributeValue("y", tmxObjectTile.Tile.TileSize.Height * Program.Scale);
                xmlTileObject.SetAttributeValue("flipY", true);
            }

            // Add any colliders that might be on the tile
            foreach (TmxObject tmxObject in tmxObjectTile.Tile.ObjectGroup.Objects)
            {
                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    // Note: Tile objects have orthographic rectangles even in isometric orientations so no need to transform rectangle points
                    objElement = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    objElement = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, tmxObjectTile.Tile.ObjectGroup.Name);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    objElement = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);
                }
                else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    objElement = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);
                }

                if (objElement != null)
                {
                    // Objects can be offset (and we need to make up for the bottom-left corner being the origin in a TileObject)
                    objElement.SetAttributeValue("offsetX", tmxObject.Position.X * Program.Scale);
                    objElement.SetAttributeValue("offsetY", (tmxObjectTile.Size.Height - tmxObject.Position.Y) * Program.Scale);

                    xmlTileObject.Add(objElement);
                }
            }

            // Add a child for each mesh (with animation if needed)
            foreach (var mesh in tmxObjectTile.Tile.Meshes)
            {
                XElement xmlMeshObject = new XElement("GameObject");

                xmlMeshObject.SetAttributeValue("name", mesh.ObjectName);
                xmlMeshObject.SetAttributeValue("copy", mesh.UniqueMeshName);
                xmlMeshObject.SetAttributeValue("sortingLayerName", tmxObjectTile.ParentObjectGroup.SortingLayerName);
                xmlMeshObject.SetAttributeValue("sortingOrder", tmxObjectTile.ParentObjectGroup.SortingOrder);

                // This object, that actually displays the tile, has to be bumped up to account for the bottom-left corner problem with Tile Objects in Tiled
                xmlMeshObject.SetAttributeValue("x", 0);
                xmlMeshObject.SetAttributeValue("y", tmxObjectTile.Tile.TileSize.Height * Program.Scale);

                if (mesh.FullAnimationDurationMs > 0)
                {
                    XElement xmlAnimation = new XElement("TileAnimator",
                        new XAttribute("startTimeMs", mesh.StartTimeMs),
                        new XAttribute("durationMs", mesh.DurationMs),
                        new XAttribute("fullTimeMs", mesh.FullAnimationDurationMs));
                    xmlMeshObject.Add(xmlAnimation);
                }

                xmlTileObject.Add(xmlMeshObject);
            }

            xmlTileObjectRoot.Add(xmlTileObject);
        }



    } // end class
} // end namespace
