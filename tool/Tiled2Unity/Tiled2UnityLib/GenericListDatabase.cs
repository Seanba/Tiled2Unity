using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tiled2Unity
{
    // A simple list acting as our "database"
    // Similar items are found multiple times in this collection (as opposed to HashIndexOf)
    class GenericListDatabase<T> : IGenericDatabase<T>
    {
        public List<T> List { get; private set; }

        public GenericListDatabase()
        {
            this.List = new List<T>();
        }

        public int AddToDatabase(T value)
        {
            this.List.Add(value);
            return this.List.Count - 1;
        }
    }
}
