using System;
using System.Collections;

namespace Tiled2Unity
{
    public class BitPlane : ICollection
    {
        private BitArray mBits = null;
       
        public int Width { get; private set; }
        public int Height { get; private set; }

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

        public BitPlane(int width, int height)
        {
            Width = width;
            Width = height;
            if (width * height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    "width or height", 
                    "Invalid values (may be too large causing overflow)");
            }
            mBits = new BitArray(width * height);
        }

        public void Set(int row, int column, bool value)
        {
            mBits.Set(row * Width + column, value);
        }

        public bool Get(int row, int column)
        {
            return mBits.Get(row * Width + column);
        }

        public void SetAll(bool value)
        {
            mBits.SetAll(value);
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
