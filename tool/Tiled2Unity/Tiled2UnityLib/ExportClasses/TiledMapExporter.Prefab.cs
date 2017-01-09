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

            prefab.SetAttributeValue("orientation", this.tmxMap.Orientation.ToString());
            prefab.SetAttributeValue("staggerAxis", this.tmxMap.StaggerAxis.ToString());
            prefab.SetAttributeValue("staggerIndex", this.tmxMap.StaggerIndex.ToString());
            prefab.SetAttributeValue("hexSideLength", this.tmxMap.HexSideLength);

            prefab.SetAttributeValue("numLayers", this.tmxMap.Layers.Count);
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);
            prefab.SetAttributeValue("backgroundColor", this.tmxMap.BackgroundColor);

            prefab.SetAttributeValue("exportScale", Tiled2Unity.Settings.Scale);
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

                    // Is we're using depth shaders for our materials then each layer needs depth assigned to it
                    // The "depth" of the layer is negative because the Unity camera is along the negative z axis
                    float depth_z = 0;
                    if (Tiled2Unity.Settings.DepthBufferEnabled && layer.SortingOrder != 0)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileHeight = this.tmxMap.TileHeight;
                        depth_z = CalculateLayerDepth(layer.SortingOrder, tileHeight, mapLogicalHeight);
                    }

                    XElement layerElement =
                        new XElement("GameObject",
                            new XAttribute("name", layer.Name),
                            new XAttribute("x", offset.X),
                            new XAttribute("y", offset.Y),
                            new XAttribute("z", depth_z));

                    if (layer.Ignore != TmxLayer.IgnoreSettings.Visual)
                    {
                        // Submeshes for the layer (layer+material)
                        var meshElements = CreateMeshElementsForLayer(layer);
                        layerElement.Add(meshElements);
                    }

                    // Collision data for the layer
                    if (layer.Ignore != TmxLayer.IgnoreSettings.Collision)
                    {
                        foreach (var collisionLayer in layer.CollisionLayers)
                        {
                            var collisionElements = CreateCollisionElementForLayer(collisionLayer);
                            layerElement.Add(collisionElements);
                        }
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

                    // Is we're using depth shaders for our materials then each object group needs depth assigned to it
                    // The "depth" of the layer is negative because the Unity camera is along the negative z axis
                    float depth_z = 0;
                    if (Tiled2Unity.Settings.DepthBufferEnabled && objGroup.SortingOrder != 0)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileHeight = this.tmxMap.TileHeight;

                        depth_z = CalculateLayerDepth(objGroup.SortingOrder, tileHeight, mapLogicalHeight);
                    }

                    gameObject.SetAttributeValue("z", depth_z);

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

                // If we're not using a unity:layer override and there is an Object Type to go with this object then use it
                if (String.IsNullOrEmpty(objectGroup.UnityLayerOverrideName))
                {
                    xmlObject.SetAttributeValue("layer", tmxObject.Type);
                }

                XElement objElement = null;

                if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                {
                    if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
                    {
                        TmxObjectPolygon tmxIsometricRectangle = TmxObjectPolygon.FromRectangle(this.tmxMap, tmxObject as TmxObjectRectangle);
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
                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;

                    // Apply z-cooridnate for use with the depth buffer
                    // (Again, this is complicated by the fact that object tiles position is WRT the bottom edge of a tile)
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        float mapLogicalHeight = this.tmxMap.MapSizeInPixels().Height;
                        float tileLogicalHeight = this.tmxMap.TileHeight;
                        float logicalPos_y = (-pos.Y / Tiled2Unity.Settings.Scale) - tileLogicalHeight;

                        float depth_z = CalculateFaceDepth(logicalPos_y, mapLogicalHeight);
                        xmlObject.SetAttributeValue("z", depth_z);
                    }

                    AddTileObjectElements(tmxObject as TmxObjectTile, xmlObject);
                }
                else
                {
                    Logger.WriteLine("Object '{0}' has been added for use with custom importers", tmxObject);
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

        private void AssignUnityProperties(TmxHasProperties tmxHasProperties, XElement xml, PrefabContext context)
        {
            var properties = TmxHelper.GetPropertiesWithTypeDefaults(tmxHasProperties, this.tmxMap.ObjectTypes);

            // Only the root of the prefab can have a scale
            {
                string unityScale = properties.GetPropertyValueAsString("unity:scale", "");
                if (!String.IsNullOrEmpty(unityScale))
                {
                    float scale = 1.0f;
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:scale only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Single.TryParse(unityScale, out scale))
                    {
                        Logger.WriteError("unity:scale property value '{0}' could not be converted to a float", unityScale);
                    }
                    else
                    {
                        xml.SetAttributeValue("scale", unityScale);
                    }
                }
            }

            // Only the root of the prefab can be marked a resource
            {
                string unityResource = properties.GetPropertyValueAsString("unity:resource", "");
                if (!String.IsNullOrEmpty(unityResource))
                {
                    bool resource = false;
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:resource only applies to map properties\n{0}", xml.ToString());
                    }
                    else if (!Boolean.TryParse(unityResource, out resource))
                    {
                        Logger.WriteError("unity:resource property value '{0}' could not be converted to a boolean", unityResource);
                    }
                    else
                    {
                        xml.SetAttributeValue("resource", unityResource);
                    }
                }
            }

            // Some users may want resource prefabs to be saved to a particular path
            {
                string unityResourcePath = properties.GetPropertyValueAsString("unity:resourcePath", "");
                if (!String.IsNullOrEmpty(unityResourcePath))
                {
                    if (context != PrefabContext.Root)
                    {
                        Logger.WriteWarning("unity:resourcePath only applies to map properties\n{0}", xml.ToString());
                    }
                    else
                    {
                        bool isInvalid = Path.GetInvalidPathChars().Any(c => unityResourcePath.Contains(c));
                        if (isInvalid)
                        {
                            Logger.WriteError("unity:resourcePath has invalid path characters: {0}", unityResourcePath);
                        }
                        else
                        {
                            xml.SetAttributeValue("resourcePath", unityResourcePath);
                        }
                    }
                }
            }

            // Any object can carry the 'isTrigger' setting and we assume any children to inherit the setting
            {
                string unityIsTrigger = properties.GetPropertyValueAsString("unity:isTrigger", "");
                if (!String.IsNullOrEmpty(unityIsTrigger))
                {
                    bool isTrigger = false;
                    if (!Boolean.TryParse(unityIsTrigger, out isTrigger))
                    {
                        Logger.WriteError("unity:isTrigger property value '{0}' cound not be converted to a boolean", unityIsTrigger);
                    }
                    else
                    {
                        xml.SetAttributeValue("isTrigger", unityIsTrigger);
                    }
                }
            }

            // Any part of the prefab can be assigned a 'layer'
            {
                string unityLayer = properties.GetPropertyValueAsString("unity:layer", "");
                if (!String.IsNullOrEmpty(unityLayer))
                {
                    xml.SetAttributeValue("layer", unityLayer);
                }
            }

            // Any part of the prefab can be assigned a 'tag'
            {
                string unityTag = properties.GetPropertyValueAsString("unity:tag", "");
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
            knownProperties.Add("unity:convex");
            knownProperties.Add("unity:ignore");
            knownProperties.Add("unity:resource");
            knownProperties.Add("unity:resourcePath");

            var unknown = from p in properties.PropertyMap
                          where p.Key.StartsWith("unity:")
                          where knownProperties.Contains(p.Key) == false
                          select p.Key;
            foreach (var p in unknown)
            {
                Logger.WriteWarning("Unknown unity property '{0}' in GameObject '{1}'", p, tmxHasProperties.ToString());
            }
        }

        private void AssignTiledProperties(TmxHasProperties tmxHasProperties, XElement xml)
        {
            var properties = TmxHelper.GetPropertiesWithTypeDefaults(tmxHasProperties, this.tmxMap.ObjectTypes);

            List<XElement> xmlProperties = new List<XElement>();

            foreach (var prop in properties.PropertyMap)
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


                XElement xmlProp = new XElement("Property", new XAttribute("name", prop.Key), new XAttribute("value", prop.Value.Value));
                xmlProperties.Add(xmlProp);
            }

            xml.Add(xmlProperties);
        }

        private XElement CreateBoxColliderElement(TmxObjectRectangle tmxRectangle)
        {
            XElement xmlCollider =
                new XElement("BoxCollider2D",
                    new XAttribute("width", tmxRectangle.Size.Width * Tiled2Unity.Settings.Scale),
                    new XAttribute("height", tmxRectangle.Size.Height * Tiled2Unity.Settings.Scale));

            return xmlCollider;
        }

        private XElement CreateCircleColliderElement(TmxObjectEllipse tmxEllipse, string objGroupName)
        {
            if (this.tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                Logger.WriteError("Collision ellipse in Object Layer '{0}' is not supported in Isometric maps: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else if (!tmxEllipse.IsCircle())
            {
                Logger.WriteError("Collision ellipse in Object Layer '{0}' is not a circle: {1}", objGroupName, tmxEllipse);
                return null;
            }
            else
            {
                XElement circleCollider =
                    new XElement("CircleCollider2D",
                        new XAttribute("radius", tmxEllipse.Radius * Tiled2Unity.Settings.Scale));

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
            // TileObjects can be scaled (this is separate from vertex scaling)
            SizeF scale = tmxObjectTile.GetTileObjectScale();

            // Flipping is done through negative-scaling on the child object
            float flip_w = tmxObjectTile.FlippedHorizontal ? -1.0f : 1.0f;
            float flip_h = tmxObjectTile.FlippedVertical ? -1.0f : 1.0f;

            // Helper values for moving tile about local origin
            float full_w = tmxObjectTile.Tile.TileSize.Width;
            float full_h = tmxObjectTile.Tile.TileSize.Height;
            float half_w = full_w * 0.5f;
            float half_h = full_h * 0.5f;

            // Scale goes onto root node
            xmlTileObjectRoot.SetAttributeValue("scaleX", scale.Width);
            xmlTileObjectRoot.SetAttributeValue("scaleY", scale.Height);

            // We combine the properties of the tile that is referenced and add it to our own properties
            AssignTiledProperties(tmxObjectTile.Tile, xmlTileObjectRoot);

            // Add a TileObject component for scripting purposes
            {
                XElement xmlTileObjectComponent = new XElement("TileObjectComponent");
                xmlTileObjectComponent.SetAttributeValue("width", tmxObjectTile.Tile.TileSize.Width * scale.Width * Tiled2Unity.Settings.Scale);
                xmlTileObjectComponent.SetAttributeValue("height", tmxObjectTile.Tile.TileSize.Height * scale.Height * Tiled2Unity.Settings.Scale);
                xmlTileObjectRoot.Add(xmlTileObjectComponent);
            }

            // Child node positions game object to match center of tile so can flip along x and y axes
            XElement xmlTileObject = new XElement("GameObject");
            xmlTileObject.SetAttributeValue("name", "TileObject");
            if (tmxMap.Orientation == TmxMap.MapOrientation.Isometric)
            {
                // In isometric mode the local origin of the tile is at the bottom middle
                xmlTileObject.SetAttributeValue("x", 0);
                xmlTileObject.SetAttributeValue("y", half_h * Tiled2Unity.Settings.Scale);
            }
            else
            {
                // For non-isometric maps the local origin of the tile is the bottom left
                xmlTileObject.SetAttributeValue("x", half_w * Tiled2Unity.Settings.Scale);
                xmlTileObject.SetAttributeValue("y", half_h * Tiled2Unity.Settings.Scale);
            }
            xmlTileObject.SetAttributeValue("scaleX", flip_w);
            xmlTileObject.SetAttributeValue("scaleY", flip_h);

            // Add any colliders that might be on the tile
            // Note: Colliders on a tile object are always treated as if they are in Orthogonal space
            TmxMap.MapOrientation restoreOrientation = tmxMap.Orientation;
            this.tmxMap.Orientation = TmxMap.MapOrientation.Orthogonal;
            {
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
                        // This object is currently in the center of the Tile Object we are constructing
                        // The collision geometry is wrt the top-left corner
                        // The "Offset" of the collider translation to get to lop-left corner and the collider's position into account
                        float offset_x = (-half_w + tmxObject.Position.X) * Tiled2Unity.Settings.Scale;
                        float offset_y = (half_h - tmxObject.Position.Y) * Tiled2Unity.Settings.Scale;
                        objElement.SetAttributeValue("offsetX", offset_x);
                        objElement.SetAttributeValue("offsetY", offset_y);

                        xmlTileObject.Add(objElement);
                    }
                }
            }
            this.tmxMap.Orientation = restoreOrientation;

            // Add a child for each mesh
            // (The child node is needed due to animation)
            foreach (var mesh in tmxObjectTile.Tile.Meshes)
            {
                XElement xmlMeshObject = new XElement("GameObject");

                xmlMeshObject.SetAttributeValue("name", mesh.ObjectName);
                xmlMeshObject.SetAttributeValue("copy", mesh.UniqueMeshName);

                xmlMeshObject.SetAttributeValue("sortingLayerName", tmxObjectTile.SortingLayerName ?? tmxObjectTile.ParentObjectGroup.SortingLayerName);
                xmlMeshObject.SetAttributeValue("sortingOrder", tmxObjectTile.SortingOrder ?? tmxObjectTile.ParentObjectGroup.SortingOrder);

                // Game object that contains mesh moves position to that local origin of Tile Object (from Tiled's point of view) matches the root position of the Tile game object
                // Put another way: This translation moves away from center to local origin
                xmlMeshObject.SetAttributeValue("x", -half_w * Tiled2Unity.Settings.Scale);
                xmlMeshObject.SetAttributeValue("y", half_h * Tiled2Unity.Settings.Scale);

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
