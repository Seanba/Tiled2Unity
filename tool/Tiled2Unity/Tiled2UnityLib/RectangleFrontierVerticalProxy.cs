using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace Tiled2Unity
{

    /// <summary>
    /// Proxy class to enumerate vertically over the frontier points of a rectangle.
    /// </summary>
    public class RectangleFrontierVerticalProxy : RectangleFrontierProxy
    {
        private struct Enumerator : IEnumerator<Point>
        {
            private RectangleFrontierVerticalProxy mSelf;
            private int mCurrent;
            public Enumerator(RectangleFrontierVerticalProxy self)
            {
                mSelf = self;
                mCurrent = -1;
            }

            public Point Current
            {
                get
                {
                    return new Point(mSelf.Begin.X, mSelf.Begin.Y + mCurrent);
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                ++mCurrent;
                // Inclusive, closed interval iteration.
                return mSelf.Begin.Y + mCurrent != mSelf.End.Y + 1;
            }

            public void Reset()
            {
                mCurrent = -1;
            }
        }

        public RectangleFrontierVerticalProxy(Point begin, Point end) : base(begin, end)
        {
            System.Diagnostics.Debug.Assert(Begin.X == End.X);
        }

        public override IEnumerator<Point> GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
}
