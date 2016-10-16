using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Tiled2Unity;

namespace Test
{
    [TestClass]
    public class BitPlaneTest
    {
        [TestMethod]
        public void BitPlanes()
        {
            var bp = new BitPlane(5, 5);
            bp.Set(1, 1, true);
            Assert.AreEqual<bool>(true, bp.Get(1, 1));
            Assert.AreEqual<bool>(false, bp.Get(1, 2));
            Assert.AreEqual<bool>(false, bp.Get(0, 1));

            var rect = new Rectangle(1, 1, 2, 2);

            /*   01234
             * 0 ooooo
             * 1 oxxxo
             * 2 oxxxo
             * 3 oxxxo
             * 4 ooooo
             */
            bp.Set(rect, true);

            for (int i = rect.X; i < rect.Right + 1; ++i)
            {
                for (int j = rect.Y; j < rect.Bottom + 1; ++j)
                {
                    Assert.AreEqual<bool>(true, bp.Get(i, j));
                }
            }
            Assert.AreEqual<bool>(false, bp.Get(1, 4));
            Assert.AreEqual<bool>(false, bp.Get(2, 4));
            Assert.AreEqual<bool>(false, bp.Get(3, 4));
            Assert.AreEqual<bool>(false, bp.Get(4, 0));
            Assert.AreEqual<bool>(false, bp.Get(4, 1));
            Assert.AreEqual<bool>(false, bp.Get(4, 2));
            Assert.AreEqual<bool>(false, bp.Get(4, 3));
            Assert.AreEqual<bool>(false, bp.Get(4, 4));

            // Out of bounds tests.
            try
            {
                bp = new BitPlane(2, 100);
                bp.Set(0, 98, true);
                bp.Set(1, 98, true);
                bp.Set(0, 99, true);
                bp.Set(1, 99, true);
            }
            catch (System.IndexOutOfRangeException /*exception*/)
            {
                Assert.Fail();
            }
        }
    }
}
