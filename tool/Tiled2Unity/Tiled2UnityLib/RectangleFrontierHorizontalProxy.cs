using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace Tiled2Unity
{
    /// <summary>
    /// Proxy class to enumerate horizontally over the frontier points of a rectangle.
    /// </summary>
    public class RectangleFrontierHorizontalProxy : RectangleFrontierProxy
    {
        private struct Enumerator : IEnumerator<Point>
        {
            private RectangleFrontierHorizontalProxy mSelf;
            private int mCurrent;
            public Enumerator(RectangleFrontierHorizontalProxy self)
            {
                mSelf = self;
                mCurrent = -1;
            }

            public Point Current
            {
                get
                {
                    return new Point(mSelf.Begin.X + mCurrent, mSelf.Begin.Y);
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
                return mSelf.Begin.X + mCurrent != mSelf.End.X;
            }

            public void Reset()
            {
                mCurrent = -1;
            }
        }

        public RectangleFrontierHorizontalProxy(Point begin, Point end) : base(begin, end)
        {
            System.Diagnostics.Debug.Assert(Begin.Y == End.Y);
        }

        public override IEnumerator<Point> GetEnumerator()
        {
            return new Enumerator(this);
        }
    }
}
