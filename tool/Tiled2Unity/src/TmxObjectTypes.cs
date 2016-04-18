using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tiled2Unity
{
    // The "objecttypes.xml" file has project-specific data to be used with the TmxObject instances
    public class TmxObjectTypes
    {
        public Dictionary<string, TmxObjectType> TmxObjectTypeMapping { get; private set; }

        public TmxObjectTypes()
        {
            this.TmxObjectTypeMapping = new Dictionary<string, TmxObjectType>(StringComparer.InvariantCultureIgnoreCase);
        }

        public TmxObjectType GetValueOrDefault(string key)
        {
            if (this.TmxObjectTypeMapping.ContainsKey(key))
            {
                return this.TmxObjectTypeMapping[key];
            }

            return new TmxObjectType();
        }

        public TmxObjectType GetValueOrNull(string key)
        {
            if (key != null && this.TmxObjectTypeMapping.ContainsKey(key))
            {
                return this.TmxObjectTypeMapping[key];
            }

            return null;
        }


        public static TmxObjectTypes FromXmlFile(string xmlPath)
        {
            TmxObjectTypes xmlObjectTypes = new TmxObjectTypes();

            XDocument doc = XDocument.Load(xmlPath);

            foreach (var xml in doc.Element("objecttypes").Elements("objecttype"))
            {
                TmxObjectType tmxObjectType = TmxObjectType.FromXml(xml);
                xmlObjectTypes.TmxObjectTypeMapping[tmxObjectType.Name] = tmxObjectType;
            }

            return xmlObjectTypes;
        }
    }
}
