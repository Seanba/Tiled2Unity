using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // .NET does not have support for serializing dictionaries so this helper class will make the transformation to StringCollection to Dictionary and back
    // Think about making generic if pattern is needed for further settings
    class PerLayerColorData
    {
        public static Dictionary<string, Color> StringCollectionToDictionary(StringCollection stringCollection)
        {
            if (stringCollection == null || stringCollection.Count == 0)
            {
                return new Dictionary<string, Color>();
            }

            string[] strings = new string[stringCollection.Count];
            stringCollection.CopyTo(strings, 0);

            string[] names = strings.Where((s, i) => i % 2 == 0).ToArray();
            string[] colors = strings.Where((s, i) => i % 2 == 1).ToArray();
            if (names.Count() != colors.Count())
            {
                // There's a problem. Data is junk. Return empty dictionary.
                return new Dictionary<string, Color>();
            }

            var dict = new Dictionary<string, Color>();
            for (int i = 0; i < names.Count(); i++)
            {
                Color color;
                try
                {
                    color = ColorTranslator.FromHtml(colors[i]);
                }
                catch
                {
                    // Just do hot pink
                    color = Color.HotPink;
                }

                dict[names[i]] = color;
            }

            return dict;
        }

        public static StringCollection DictionaryToStringCollection(Dictionary<string, Color> dictionary)
        {
            StringCollection stringCollection = new StringCollection();

            foreach (var pair in dictionary)
            {
                stringCollection.Add(pair.Key);
                stringCollection.Add(ColorTranslator.ToHtml(pair.Value));
            }

            return stringCollection;
        }


    }
}
