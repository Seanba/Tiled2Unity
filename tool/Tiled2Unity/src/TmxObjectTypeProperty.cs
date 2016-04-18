using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public class TmxObjectTypeProperty
    {
        public string Name { get; private set; }
        public TmxPropertyType Type { get; private set; }
        public string Default { get; set; }

        // Create a dictionary collection of Object Type Property instances from the parent xml element
        public static Dictionary<string, TmxObjectTypeProperty> FromObjectTypeXml(XElement xmlObjectType)
        {
            Dictionary<string, TmxObjectTypeProperty> tmxObjectTypeProperties = new Dictionary<string, TmxObjectTypeProperty>();

            foreach (var xmlProperty in xmlObjectType.Elements("property"))
            {
                TmxObjectTypeProperty tmxObjectTypeProperty = new TmxObjectTypeProperty();

                tmxObjectTypeProperty.Name = TmxHelper.GetAttributeAsString(xmlProperty, "name", "");
                tmxObjectTypeProperty.Type = TmxHelper.GetAttributeAsEnum(xmlProperty, "type", TmxPropertyType.String);
                tmxObjectTypeProperty.Default = TmxHelper.GetAttributeAsString(xmlProperty, "default", "");

                tmxObjectTypeProperties.Add(tmxObjectTypeProperty.Name, tmxObjectTypeProperty);
            }

            return tmxObjectTypeProperties;
        }
    }
}
