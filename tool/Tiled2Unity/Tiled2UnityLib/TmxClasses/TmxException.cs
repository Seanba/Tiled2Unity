using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public class TmxException : Exception
    {
        public TmxException(string message)
            : base(message)
        {
        }

        public TmxException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public static void ThrowFormat(string fmt, params object[] args)
        {
            string msg = String.Format(fmt, args);
            throw new TmxException(msg);
        }

        public static void FromAttributeException(Exception inner, XElement element)
        {
            StringBuilder builder = new StringBuilder(inner.Message);
            Array.ForEach(element.Attributes().ToArray(), a => builder.AppendFormat("\n  {0}", a.ToString()));
            TmxException.ThrowFormat("Error parsing {0} attributes\n{1}", element.Name, builder.ToString());
        }

    }
}
