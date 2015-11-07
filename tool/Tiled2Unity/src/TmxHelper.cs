using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;


namespace Tiled2Unity
{
    class TmxHelper
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
            System.Windows.Media.Color mediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
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



    }
}
