using System.Drawing;
using System.Collections.Generic;
using System;
using System.Collections;

namespace Tiled2Unity
{
    public static class RectangleUtils
    {
        /// <summary>
        /// Get the area of a rectangle.
        /// </summary>
        /// <param name="rect">this Rectangle.</param>
        /// <returns>The area of the rectangle.</returns>
        public static uint GetArea(this Rectangle rect)
        {
            return (uint)rect.Width * (uint)rect.Height;
        }

        /// <summary>
        /// The closed area of a rectangle is defined to be
        /// (width + 1) * (height + 1).
        /// </summary>
        /// <param name="rect">This Rectangle.</param>
        /// <returns>The closed area of a rectangle.</returns>
        public static uint GetClosedArea(this Rectangle rect)
        {
            return ((uint)rect.Width + 1) * ((uint)rect.Height + 1);
        }

        /// <summary>
        /// Adds a bottom row to the given rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to enlarge.</param>
        public static void EnlargeBottom(ref Rectangle rect)
        {
            rect.Size = new Size(rect.Width, rect.Height + 1);
        }

        /// <summary>
        /// Adds a top row to the given rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to enlarge.</param>
        public static void EnlargeTop(ref Rectangle rect)
        {
            EnlargeBottom(ref rect);
            rect.Location = new Point(rect.X, rect.Location.Y - 1);
        }

        /// <summary>
        /// Adds a column to the right of the given rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to enlarge.</param>
        public static void EnlargeRight(ref Rectangle rect)
        {
            rect.Size = new Size(rect.Width + 1, rect.Height);
        }

        /// <summary>
        /// Adds a column to the left of the given rectangle.
        /// </summary>
        /// <param name="rect">The rectangle to enlarge.</param>
        public static void EnlargeLeft(ref Rectangle rect)
        {
            EnlargeRight(ref rect);
            rect.Location = new Point(rect.X - 1, rect.Y);
        }

        /// <summary>
        /// Get the bottom frontier of this rectangle.
        /// </summary>
        /// <param name="rect">This rectangle.</param>
        /// <returns>A proxy class to use in a foreach loop.</returns>
        /// <example>
        ///     <code>
        ///         var rect = new System.Drawing.Rectangle(2, 5, 4, 6);
        ///         var frontier = rect.GetBottomFrontier();
        ///         foreach (var point in frontier)
        ///         {
        ///             // Do interesting things with point...
        ///         }
        ///     </code>
        /// </example>
        public static RectangleFrontierHorizontalProxy GetBottomFrontier(this Rectangle rect)
        {
            return new RectangleFrontierHorizontalProxy(new Point(rect.X, rect.Bottom + 1), new Point(rect.Right, rect.Bottom + 1));
        }

        public static RectangleFrontierHorizontalProxy GetTopFrontier(this Rectangle rect)
        {
            return new RectangleFrontierHorizontalProxy(new Point(rect.X, rect.Top - 1), new Point(rect.Right, rect.Top - 1));
        }

        public static RectangleFrontierVerticalProxy GetLeftFrontier(this Rectangle rect)
        {
            return new RectangleFrontierVerticalProxy(new Point(rect.Left - 1, rect.Top), new Point(rect.Left - 1, rect.Bottom));
        }

        public static RectangleFrontierVerticalProxy GetRightFrontier(this Rectangle rect)
        {
            return new RectangleFrontierVerticalProxy(new Point(rect.Right + 1, rect.Top), new Point(rect.Right + 1, rect.Bottom));
        }

    }
}
