using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    interface TmxHasPoints
    {
        List<PointF> Points { get; }
        bool ArePointsClosed();
    }
}
