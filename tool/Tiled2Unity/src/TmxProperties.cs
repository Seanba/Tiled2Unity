using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxProperties
    {
        public IDictionary<string, string> PropertyMap { get; private set; }

        public TmxProperties()
        {
            this.PropertyMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string GetPropertyValueAsString(string name)
        {
            return this.PropertyMap[name];
        }

        public string GetPropertyValueAsString(string name, string defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return this.PropertyMap[name];
            return defaultValue;
        }

        public int GetPropertyValueAsInt(string name)
        {
            try
            {
                return Convert.ToInt32(this.PropertyMap[name]);
            }
            catch (System.FormatException inner)
            {
                string message = String.Format("Error evaulating property '{0}={1}'\n  '{1}' is not an integer", name, this.PropertyMap[name]);
                throw new TmxException(message, inner);
            }
        }

        public int GetPropertyValueAsInt(string name, int defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsInt(name);
            return defaultValue;
        }

        public bool GetPropertyValueAsBoolean(string name)
        {
            bool asBoolean = false;
            try
            {
                asBoolean = Convert.ToBoolean(this.PropertyMap[name]);
            }
            catch (FormatException)
            {
                Program.WriteWarning("Property '{0}' value '{1}' cannot be converted to a boolean.", name, this.PropertyMap[name]);
            }

            return asBoolean;
        }

        public bool GetPropertyValueAsBoolean(string name, bool defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsBoolean(name);
            return defaultValue;
        }

        public T GetPropertyValueAsEnum<T>(string name)
        {
            return TmxHelper.GetStringAsEnum<T>(this.PropertyMap[name]);
        }

        public T GetPropertyValueAsEnum<T>(string name, T defaultValue)
        {
            if (this.PropertyMap.ContainsKey(name))
                return GetPropertyValueAsEnum<T>(name);
            return defaultValue;
        }

    } // end class
} // end namespace
