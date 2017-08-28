using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxProperties
    {
        public static TmxProperties FromXml(XElement elem)
        {
            TmxProperties tmxProps = new TmxProperties();

            var props = from elem1 in elem.Elements("properties")
                        from elem2 in elem1.Elements("property")
                        select new
                        {
                            Name = TmxHelper.GetAttributeAsString(elem2, "name"),
                            Type = TmxHelper.GetAttributeAsEnum(elem2, "type", TmxPropertyType.String),

                            // Value may be attribute or inner text
                            Value = TmxHelper.GetAttributeAsString(elem2, "value", null) ?? elem2.Value,
                        };

            if (props.Count() > 0)
            {
                Logger.WriteVerbose("Parse properites ...");
            }

            foreach (var p in props)
            {
                tmxProps.PropertyMap[p.Name] = new TmxProperty { Name = p.Name, Type = p.Type, Value = p.Value };
            }

            return tmxProps;
        }

    }
}
