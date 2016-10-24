using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tiled2Unity
{
    // This is really just a cheap interface that adds "stuff" to a container, returning an index
    // You can access the items (that may be unique or there may be repeats) through the List property
    // (We just want to be able to have unique or repeated collection of items polymorphically)
    interface IGenericDatabase<T>
    {
        List<T> List { get; }
        int AddToDatabase(T value);
    }
}
