using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace Tiled2Unity
{
    public abstract class RectangleFrontierProxy : IEnumerable<Point>
    {
        public Point Begin { get; private set; }
        public Point End { get; private set; }

        protected RectangleFrontierProxy(Point begin, Point end)
        {
            Begin = begin;
            End = end;
        }

        public abstract IEnumerator<Point> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
