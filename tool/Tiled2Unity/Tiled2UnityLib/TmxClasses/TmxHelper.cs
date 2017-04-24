using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;


namespace Tiled2Unity
{
    public class TmxHelper
    {
        public static string GetAttributeAsString(XElement elem, string attrName)
        {
            return elem.Attribute(attrName).Value;
        }

        public static string GetAttributeAsString(XElement elem, string attrName, string defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsString(elem, attrName);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName)
        {
            return Convert.ToInt32(elem.Attribute(attrName).Value);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName, int defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsInt(elem, attrName);
        }

        public static uint GetAttributeAsUInt(XElement elem, string attrName)
        {
            return Convert.ToUInt32(elem.Attribute(attrName).Value);
        }

        public static uint GetAttributeAsUInt(XElement elem, string attrName, uint defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsUInt(elem, attrName);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName)
        {
            return Convert.ToSingle(elem.Attribute(attrName).Value);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName, float defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsFloat(elem, attrName);
        }

        public static string GetAttributeAsFullPath(XElement elem, string attrName)
        {
            return Path.GetFullPath(elem.Attribute(attrName).Value);
        }

        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName)
        {
            string colorString = elem.Attribute(attrName).Value;
            System.Drawing.Color color = TmxHelper.ColorFromHtml(colorString);
            return color;
        }

        public static System.Drawing.Color GetAttributeAsColor(XElement elem, string attrName, System.Drawing.Color defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsColor(elem, attrName);
        }

        public static T GetStringAsEnum<T>(string enumString)
        {
            enumString = enumString.Replace("-", "_");

            T value = default(T);
            try
            {
                value = (T)Enum.Parse(typeof(T), enumString, true);
            }
            catch
            {
                StringBuilder msg = new StringBuilder();
                msg.AppendFormat("Could not convert '{0}' to enum of type '{1}'\n", enumString, typeof(T).ToString());
                msg.AppendFormat("Choices are:\n");

                foreach (T t in Enum.GetValues(typeof(T)))
                {
                    msg.AppendFormat("  {0}\n", t.ToString());
                }
                TmxException.ThrowFormat(msg.ToString());
            }

            return value;
        }

        public static T GetAttributeAsEnum<T>(XElement elem, string attrName)
        {
            string enumString = elem.Attribute(attrName).Value.Replace("-", "_");
            return GetStringAsEnum<T>(enumString);
        }

        public static T GetAttributeAsEnum<T>(XElement elem, string attrName, T defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsEnum<T>(elem, attrName);
        }

        public static TmxProperties GetPropertiesWithTypeDefaults(TmxHasProperties hasProperties, TmxObjectTypes objectTypes)
        {
            TmxProperties tmxProperties = new TmxProperties();

            // Fill in all the default properties first
            // (Note: At the moment, only TmxObject has default properties it inherits from TmxObjectType)
            string objectTypeName = null;
            if (hasProperties is TmxObject)
            {
                TmxObject tmxObject = hasProperties as TmxObject;
                objectTypeName = tmxObject.Type;
            }

            // If an object type has been found then copy over all the default values for properties
            TmxObjectType tmxObjectType = objectTypes.GetValueOrNull(objectTypeName);
            if (tmxObjectType != null)
            {
                foreach (TmxObjectTypeProperty tmxTypeProp in tmxObjectType.Properties.Values)
                {
                    tmxProperties.PropertyMap[tmxTypeProp.Name] = new TmxProperty() { Name = tmxTypeProp.Name, Type = tmxTypeProp.Type, Value = tmxTypeProp.Default };
                }
            }

            // Now add all the object properties (which may override some of the default properties)
            foreach (TmxProperty tmxProp in hasProperties.Properties.PropertyMap.Values)
            {
                tmxProperties.PropertyMap[tmxProp.Name] = tmxProp;
            }

            return tmxProperties;
        }

        public static Color ColorFromHtml(string html)
        {
            // Trim any leading hash from the string
            html = html.TrimStart('#');

            // Put leading zeros into anything less than 6 characters
            html = html.PadLeft(6, '0');

            // Put leading F into anthing less than 8 characters to cover alpha
            html = html.PadLeft(8, 'F');

            // Convert the hex string into a number
            try
            {
                int argb = Convert.ToInt32(html, 16);
                return Color.FromArgb(argb);
            }
            catch
            {
                return Color.FromArgb(255, 0, 255);
            }
        }
    }
}
