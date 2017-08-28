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
            // Create the Xml layout for the prefab. This is imported by the Tiled2Unity scripts to build the in-game prefab.
            Size sizeInPixels = this.tmxMap.MapSizeInPixels;

            XElement prefab = new XElement("Prefab");
            prefab.SetAttributeValue("name", this.tmxMap.Name);

            prefab.SetAttributeValue("orientation", this.tmxMap.Orientation.ToString());
            prefab.SetAttributeValue("staggerAxis", this.tmxMap.StaggerAxis.ToString());
            prefab.SetAttributeValue("staggerIndex", this.tmxMap.StaggerIndex.ToString());
            prefab.SetAttributeValue("hexSideLength", this.tmxMap.HexSideLength);

            prefab.SetAttributeValue("numLayers", this.tmxMap.EnumerateTileLayers().Count());
            prefab.SetAttributeValue("numTilesWide", this.tmxMap.Width);
            prefab.SetAttributeValue("numTilesHigh", this.tmxMap.Height);
            prefab.SetAttributeValue("tileWidth", this.tmxMap.TileWidth);
            prefab.SetAttributeValue("tileHeight", this.tmxMap.TileHeight);

            prefab.SetAttributeValue("exportScale", Tiled2Unity.Settings.Scale);
            prefab.SetAttributeValue("mapWidthInPixels", sizeInPixels.Width);
            prefab.SetAttributeValue("mapHeightInPixels", sizeInPixels.Height);

            // Background color ignores alpha component
            prefab.SetAttributeValue("backgroundColor", "#" + this.tmxMap.BackgroundColor.ToArgb().ToString("x8").Substring(2));

            AssignUnityProperties(this.tmxMap, prefab, PrefabContext.Root);
            AssignTiledProperties(this.tmxMap, prefab);

            // Add all layers (tiles, objects, groups) to the prefab
            foreach (var node in this.tmxMap.LayerNodes)
            {
                AddLayerNodeToElement(node, prefab);
            }

            return prefab;
        }

        private void AddLayerNodeToElement(TmxLayerNode node, XElement xml)
        {
            // Bail if the node is invisible
            if (node.Visible == false)
                return;

            // What type of node are we dealing with?
            if (node is TmxGroupLayer)
            {
                AddGroupLayerToElement(node as TmxGroupLayer, xml);
            }
            else if (node is TmxLayer)
            {
                AddTileLayerToElement(node as TmxLayer, xml);
            }
            else if (node is TmxObjectGroup)
            {
                AddObjectLayerToElement(node as TmxObjectGroup, xml);
            }
        }

        private void AddGroupLayerToElement(TmxGroupLayer groupLayer, XElement xmlRoot)
        {
            // Add a game object for this grouping
            XElement xmlGroup = new XElement("GameObject");
            xmlGroup.SetAttributeValue("name", groupLayer.Name);

            PointF offset = PointFToUnityVector(groupLayer.Offset);
            float depth_z = CalculateLayerDepth(groupLayer);

            xmlGroup.SetAttributeValue("x", offset.X);
            xmlGroup.SetAttributeValue("y", offset.Y);
            xmlGroup.SetAttributeValue("z", depth_z);

            // Add the group layer data component
            {
                XElement component = new XElement("GroupLayer",
                                        new XAttribute("offsetX", groupLayer.Offset.X),
                                        new XAttribute("offsetY", groupLayer.Offset.Y));
                xmlGroup.Add(component);
            }

            // Add all children
            foreach (var child in groupLayer.LayerNodes)
            {
                AddLayerNodeToElement(child, xmlGroup);
            }

            // Finally, add the node to the root
            xmlRoot.Add(xmlGroup);
        }

        private void AddTileLayerToElement(TmxLayer tileLayer, XElement xmlRoot)
        {
            XElement xmlLayer = new XElement("GameObject");
            xmlLayer.SetAttributeValue("name", tileLayer.Name);

            // Figure out the offset for this layer
            PointF offset = PointFToUnityVector(tileLayer.Offset);
            float depth_z = CalculateLayerDepth(tileLayer);

            xmlLayer.SetAttributeValue("x", offset.X);
            xmlLayer.SetAttributeValue("y", offset.Y);
            xmlLayer.SetAttributeValue("z", depth_z);

            // Add a TileLayer component
            {
                XElement layerComponent = new XElement("TileLayer",
                                            new XAttribute("offsetX", tileLayer.Offset.X),
                                            new XAttribute("offsetY", tileLayer.Offset.Y));

                xmlLayer.Add(layerComponent);
            }

            if (tileLayer.Ignore != TmxLayer.IgnoreSettings.Visual)
            {
                // Submeshes for the layer (layer+material)
                var meshElements = CreateMeshElementsForLayer(tileLayer);
                xmlLayer.Add(meshElements);
            }

            // Collision data for the layer
            if (tileLayer.Ignore != TmxLayer.IgnoreSettings.Collision)
            {
                foreach (var collisionLayer in tileLayer.CollisionLayers)
                {
                    var collisionElements = CreateCollisionElementForLayer(collisionLayer);
                    xmlLayer.Add(collisionElements);
                }
            }

            // Assign and special properties
            AssignUnityProperties(tileLayer, xmlLayer, PrefabContext.TiledLayer);
            AssignTiledProperties(tileLayer, xmlLayer);

            // Finally, add the layer to our root
            xmlRoot.Add(xmlLayer);
        }

        private void AddObjectLayerToElement(TmxObjectGroup objectLayer, XElement xmlRoot)
        {
            XElement gameObject = new XElement("GameObject");
            gameObject.SetAttributeValue("name", objectLayer.Name);


            // Offset the object layer
            PointF offset = PointFToUnityVector(objectLayer.Offset);
            float depth_z = CalculateLayerDepth(objectLayer);

            gameObject.SetAttributeValue("x", offset.X);
            gameObject.SetAttributeValue("y", offset.Y);
            gameObject.SetAttributeValue("z", depth_z);

            // Add an ObjectLayer component
            {
                XElement layerComponent = new XElement("ObjectLayer",
                                            new XAttribute("offsetX", objectLayer.Offset.X),
                                            new XAttribute("offsetY", objectLayer.Offset.Y),
                                            new XAttribute("color", "#" + objectLayer.Color.ToArgb().ToString("x8")));

                gameObject.Add(layerComponent);
            }

            // Assign special properties
            AssignUnityProperties(objectLayer, gameObject, PrefabContext.ObjectLayer);
            AssignTiledProperties(objectLayer, gameObject);

            List<XElement> colliders = CreateObjectElementList(objectLayer);
            if (colliders.Count() > 0)
            {
                gameObject.Add(colliders);
            }

            // Add to our root
            xmlRoot.Add(gameObject);
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

                if (tmxObject.GetType() != typeof(TmxObjectTile))
                {
                    // If we're not using a unity:layer override and there is an Object Type to go with this object then use it
                    if (String.IsNullOrEmpty(objectGroup.UnityLayerOverrideName))
                    {
                        xmlObject.SetAttributeValue("layer", tmxObject.Type);
                    }
                }

                XElement objElement = null;
                XElement objComponent = new XElement("TmxObjectComponent",
                                            new XAttribute("tmx-object-id", tmxObject.Id),
                                            new XAttribute("tmx-object-name", tmxObject.Name),
                                            new XAttribute("tmx-object-type", tmxObject.Type),
                                            new XAttribute("tmx-object-x", tmxObject.Position.X),
                                            new XAttribute("tmx-object-y", tmxObject.Position.Y),
                                            new XAttribute("tmx-object-width", tmxObject.Size.Width),
                                            new XAttribute("tmx-object-height", tmxObject.Size.Height),
                                            new XAttribute("tmx-object-rotation", tmxObject.Rotation));

                // Do not create colliders if the are being ignored. (Still want to creat the game object though)
                bool ignoringCollisions = (objectGroup.Ignore == TmxLayerNode.IgnoreSettings.Collision);

                if (!ignoringCollisions && tmxObject.GetType() == typeof(TmxObjectRectangle))
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

                    // Set object component type
                    objComponent.Name = "RectangleObjectComponent";
                }
                else if (!ignoringCollisions && tmxObject.GetType() == typeof(TmxObjectEllipse))
                {
                    objElement = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, objectGroup.Name);

                    // Set the component type
                    objComponent.Name = "CircleObjectComponent";
                }
                else if (!ignoringCollisions && tmxObject.GetType() == typeof(TmxObjectPolygon))
                {
                    objElement = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);

                    // Set the component type
                    objComponent.Name = "PolygonObjectComponent";
                }
                else if (!ignoringCollisions && tmxObject.GetType() == typeof(TmxObjectPolyline))
                {
                    objElement = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);

                    // Set the component type
                    objComponent.Name = "PolylineObjectComponent";
                }
                else if (tmxObject.GetType() == typeof(TmxObjectTile))
                {
                    TmxObjectTile tmxObjectTile = tmxObject as TmxObjectTile;

                    // Apply z-cooridnate for use with the depth buffer
                    if (Tiled2Unity.Settings.DepthBufferEnabled)
                    {
                        float depth_z = CalculateFaceDepth(tmxObjectTile.Position.Y, tmxMap.MapSizeInPixels.Height);
                        xmlObject.SetAttributeValue("z", depth_z);
                    }

                    AddTileObjectElements(tmxObjectTile, xmlObject, objComponent);
                }
                else
                {
                    Logger.WriteInfo("Object '{0}' has been added for use with custom importers", tmxObject);
                }

                if (objElement != null)
                {
                    xmlObject.Add(objComponent);
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
                    new XAttribute("mesh", mesh.UniqueMeshName),
                    new XAttribute("sortingLayerName", layer.GetSortingLayerName()),
                    new XAttribute("sortingOrder", layer.GetSortingOrder()),
                    new XAttribute("opacity", layer.GetRecursiveOpacity()));
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

        private void AddTileObjectElements(TmxObjectTile tmxObjectTile, XElement xmlTileObjectRoot, XElement objComponent)
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

            // Treat ObjectComponent as a TileObjectComponent scripting purposes
            {
                objComponent.Name = "TileObjectComponent";
                objComponent.SetAttributeValue("tmx-tile-flip-horizontal", tmxObjectTile.FlippedHorizontal);
                objComponent.SetAttributeValue("tmx-tile-flip-vertical", tmxObjectTile.FlippedVertical);
                objComponent.SetAttributeValue("width", tmxObjectTile.Tile.TileSize.Width * scale.Width * Tiled2Unity.Settings.Scale);
                objComponent.SetAttributeValue("height", tmxObjectTile.Tile.TileSize.Height * scale.Height * Tiled2Unity.Settings.Scale);
                xmlTileObjectRoot.Add(objComponent);
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

            // Only add colliders if collisions are not being ignored
            if (tmxObjectTile.ParentObjectGroup.Ignore != TmxLayerNode.IgnoreSettings.Collision)
            {
                foreach (TmxObject tmxObject in tmxObjectTile.Tile.ObjectGroup.Objects)
                {
                    XElement objCollider = null;

                    if (tmxObject.GetType() == typeof(TmxObjectRectangle))
                    {
                        // Note: Tile objects have orthographic rectangles even in isometric orientations so no need to transform rectangle points
                        objCollider = CreateBoxColliderElement(tmxObject as TmxObjectRectangle);
                    }
                    else if (tmxObject.GetType() == typeof(TmxObjectEllipse))
                    {
                        objCollider = CreateCircleColliderElement(tmxObject as TmxObjectEllipse, tmxObjectTile.Tile.ObjectGroup.Name);
                    }
                    else if (tmxObject.GetType() == typeof(TmxObjectPolygon))
                    {
                        objCollider = CreatePolygonColliderElement(tmxObject as TmxObjectPolygon);
                    }
                    else if (tmxObject.GetType() == typeof(TmxObjectPolyline))
                    {
                        objCollider = CreateEdgeColliderElement(tmxObject as TmxObjectPolyline);
                    }

                    if (objCollider != null)
                    {
                        // This object is currently in the center of the Tile Object we are constructing
                        // The collision geometry is wrt the top-left corner
                        // The "Offset" of the collider translation to get to lop-left corner and the collider's position into account
                        float offset_x = (-half_w + tmxObject.Position.X) * Tiled2Unity.Settings.Scale;
                        float offset_y = (half_h - tmxObject.Position.Y) * Tiled2Unity.Settings.Scale;
                        objCollider.SetAttributeValue("offsetX", offset_x);
                        objCollider.SetAttributeValue("offsetY", offset_y);

                        // Each collision needs to be added as a separate child because of different collision type/layers
                        {
                            var type = tmxObjectTile.ParentObjectGroup.TmxMap.ObjectTypes.GetValueOrDefault(tmxObject.Type);
                            string layerType = String.IsNullOrEmpty(type.Name) ? "Default" : type.Name;
                            string objectName = "Collision_" + layerType;

                            XElement xmlGameObject = new XElement("GameObject");
                            xmlGameObject.SetAttributeValue("name", objectName);
                            xmlGameObject.SetAttributeValue("layer", layerType);
                            xmlGameObject.Add(objCollider);


                            xmlTileObject.Add(xmlGameObject);
                        }
                    }
                }
            }
            this.tmxMap.Orientation = restoreOrientation;

            // Add a child for each mesh
            // (The child node is needed due to animation)
            // (Only add meshes if visuals are not being ignored)
            if (tmxObjectTile.ParentObjectGroup.Ignore != TmxLayerNode.IgnoreSettings.Visual)
            {
                foreach (var mesh in tmxObjectTile.Tile.Meshes)
                {
                    XElement xmlMeshObject = new XElement("GameObject");

                    xmlMeshObject.SetAttributeValue("name", mesh.ObjectName);
                    xmlMeshObject.SetAttributeValue("mesh", mesh.UniqueMeshName);

                    xmlMeshObject.SetAttributeValue("sortingLayerName", tmxObjectTile.GetSortingLayerName());
                    xmlMeshObject.SetAttributeValue("sortingOrder", tmxObjectTile.GetSortingOrder());
                    xmlMeshObject.SetAttributeValue("opacity", tmxObjectTile.ParentObjectGroup.GetRecursiveOpacity());

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
            }

            xmlTileObjectRoot.Add(xmlTileObject);
        }



    } // end class
} // end namespace
