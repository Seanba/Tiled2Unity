using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // Generic collection class that gives us O(1) insertion with distinct values and O(1) IndexOf
    public class HashIndexOf<T>
    {
        private Dictionary<T, int> dictionary = new Dictionary<T, int>();

        public List<T> List { get; private set; }

        public HashIndexOf()
        {
            this.List = new List<T>();
        }

        public int Add(T value)
        {
            if (this.dictionary.ContainsKey(value))
            {
                return this.dictionary[value];
            }
            else
            {
                int index = this.dictionary.Count;
                this.List.Add(value);
                this.dictionary[value] = index;
                return index;
            }
        }

        public int IndexOf(T value)
        {
            return this.dictionary[value];
        }
    }
}
