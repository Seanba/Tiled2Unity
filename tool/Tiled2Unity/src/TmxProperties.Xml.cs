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
                            Value = TmxHelper.GetAttributeAsString(elem2, "value"),
                        };

            if (props.Count() > 0)
            {
                Program.WriteLine("Parse properites ...");
                Program.WriteVerbose(elem.Element("properties").ToString());
            }

            foreach (var p in props)
            {
                tmxProps.PropertyMap[p.Name] = p.Value;
            }

            return tmxProps;
        }
    }
}
