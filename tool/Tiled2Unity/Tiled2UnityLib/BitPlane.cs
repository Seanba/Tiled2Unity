using System;
using System.Collections;

namespace Tiled2Unity
{
    public class BitPlane : ICollection
    {
        private BitArray mBits = null;

        /// <summary>
        /// The width of this BitPlane.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// The height of this BitPlane.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// The area of this BitPlane.
        /// </summary>
        public int Count
        {
            get
            {
                return mBits.Count;
            }
        }

        public object SyncRoot
        {
            get
            {
                return mBits.SyncRoot;
            }
        }

        public bool IsSynchronized
        {
            get
            {
                return mBits.IsSynchronized;
            }
        }

        /// <summary>
        /// Construct a BitPlane with a given width and height.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public BitPlane(int width, int height)
        {
            Width = width;
            Height = height;
            if (width * height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    "width or height", 
                    "Invalid values (may be too large causing overflow)");
            }
            mBits = new BitArray(width * height);
        }

        /// <summary>
        /// Set the bit located at row and column to value.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <param name="value">The value to set.</param>
        public void Set(int row, int column, bool value)
        {
            mBits.Set(row * Height + column, value);
        }

        /// <summary>
        /// Get the bit located at row and column.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns></returns>
        public bool Get(int row, int column)
        {
            return mBits.Get(row * Height + column);
        }

        /// <summary>
        /// Set all bits in this BitPlane to a given value.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetAll(bool value)
        {
            mBits.SetAll(value);
        }

        /// <summary>
        /// Sets all the bits contained in this rectangle to the given value.
        /// Note that the rect is a closed rect (in the topological sense).
        /// That means that if you have a rect with an area of zero (i.e.
        /// a point), then a single bit is set. In general, all the bits
        /// in the range [X,Right] x [Y,Bottom] are set to true, so including
        /// Right and Bottom. This is in contrast with most iteration idioms
        /// in for loops, where you usually loop over the half-open interval
        /// [begin,end), say.
        /// </summary>
        /// <param name="rect">The rectangle that defines the bits to set.</param>
        /// <param name="value">The value of the bits that will be set.</param>
        public void Set(System.Drawing.Rectangle rect, bool value)
        {
            for (int i = rect.X; i <= rect.Right; ++i)
            {
                for (int j = rect.Y; j <= rect.Bottom; ++j)
                {
                    Set(i, j, value);
                }
            }
        }

        public void CopyTo(Array array, int index)
        {
            mBits.CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return mBits.GetEnumerator();
        }
    }
}
