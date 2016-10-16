using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using Tiled2Unity;

namespace Test
{
    [TestClass]
    public class RectangleTest
    {
        [TestMethod]
        public void Enlargements()
        {
            var r = new Rectangle(0, 0, 0, 0);

            Assert.AreEqual<int>(0, r.Width);
            Assert.AreEqual<int>(0, r.Height);
            Assert.AreEqual<uint>(0, r.GetArea()); // Begin with 0 area.

            RectangleUtils.EnlargeRight(ref r);
            Assert.AreEqual<Point>(Point.Empty, r.Location);
            Assert.AreEqual<int>(0, r.X);
            Assert.AreEqual<int>(1, r.Width);
            Assert.AreEqual<int>(1, r.X + r.Width);
            Assert.AreEqual<int>(r.X + r.Width, r.Right);
            Assert.AreEqual<int>(1, r.Right);
            Assert.AreEqual<int>(0, r.Left);
            Assert.AreEqual<int>(0, r.Top);
            Assert.AreEqual<int>(0, r.Bottom);
            Assert.AreEqual<uint>(0, r.GetArea()); // Still zero area.
            
            RectangleUtils.EnlargeBottom(ref r);
            Assert.AreEqual<int>(1, r.Right);
            Assert.AreEqual<int>(0, r.Left);
            Assert.AreEqual<int>(0, r.Top);
            Assert.AreEqual<int>(1, r.Bottom);
            Assert.AreEqual<uint>(1, r.GetArea()); // Should have an area of 1 now.
            
            RectangleUtils.EnlargeRight(ref r);
            Assert.AreEqual<int>(2, r.Right);
            Assert.AreEqual<int>(0, r.Left);
            Assert.AreEqual<int>(0, r.Top);
            Assert.AreEqual<int>(1, r.Bottom);
            Assert.AreEqual<uint>(2, r.GetArea()); // Should have an area of 2 now.

            RectangleUtils.EnlargeLeft(ref r);
            Assert.AreEqual<int>(2, r.Right);
            Assert.AreEqual<int>(-1, r.Left);
            Assert.AreEqual<int>(0, r.Top);
            Assert.AreEqual<int>(1, r.Bottom);
            Assert.AreEqual<uint>(3, r.GetArea());

            RectangleUtils.EnlargeTop(ref r);
            Assert.AreEqual<int>(2, r.Right);
            Assert.AreEqual<int>(-1, r.Left);
            Assert.AreEqual<int>(-1, r.Top);
            Assert.AreEqual<int>(1, r.Bottom);
            Assert.AreEqual<uint>(6, r.GetArea());
        }

        [TestMethod]
        public void FrontierConstructors()
        {
            //var rf = new RectangleFrontier(new Point(0, 0), new Point(0, 0));

            var r = new Rectangle(2, 5, 4, 6);

            RectangleFrontierProxy rf = r.GetLeftFrontier();
            Assert.AreEqual<int>(rf.Begin.X, r.Left - 1);
            Assert.AreEqual<int>(rf.End.X, r.Left - 1);
            Assert.AreEqual<int>(rf.Begin.Y, r.Top);
            Assert.AreEqual<int>(rf.End.Y, r.Bottom);

            rf = r.GetRightFrontier();
            Assert.AreEqual<int>(rf.Begin.X, r.Right + 1);
            Assert.AreEqual<int>(rf.End.X, r.Right + 1);
            Assert.AreEqual<int>(rf.Begin.Y, r.Top);
            Assert.AreEqual<int>(rf.End.Y, r.Bottom);

            rf = r.GetTopFrontier();
            Assert.AreEqual<int>(rf.Begin.Y, r.Top - 1);
            Assert.AreEqual<int>(rf.End.Y, r.Top - 1);
            Assert.AreEqual<int>(rf.Begin.X, r.Left);
            Assert.AreEqual<int>(rf.End.X, r.Right);

            rf = r.GetBottomFrontier();
            Assert.AreEqual<int>(rf.Begin.Y, r.Bottom + 1);
            Assert.AreEqual<int>(rf.End.Y, r.Bottom + 1);
            Assert.AreEqual<int>(rf.Begin.X, r.Left);
            Assert.AreEqual<int>(rf.End.X, r.Right);
        }

        [TestMethod]
        public void FrontierIteration()
        {
            var r = new Rectangle(2, 5, 4, 6);
            int manualIter = 0;
            RectangleFrontierProxy frontier = r.GetTopFrontier();
            foreach (var actualPoint in frontier)
            {
                Assert.AreEqual<int>(frontier.Begin.X + manualIter, actualPoint.X);
                Assert.AreEqual<int>(frontier.Begin.Y, actualPoint.Y);
                Assert.AreEqual<int>(frontier.End.Y, actualPoint.Y);
                ++manualIter;
            }

            manualIter = 0;
            frontier = r.GetBottomFrontier();
            foreach (var actualPoint in frontier)
            {
                Assert.AreEqual<int>(frontier.Begin.X + manualIter, actualPoint.X);
                Assert.AreEqual<int>(frontier.Begin.Y, actualPoint.Y);
                Assert.AreEqual<int>(frontier.End.Y, actualPoint.Y);
                ++manualIter;
            }

            manualIter = 0;
            frontier = r.GetLeftFrontier();
            foreach (var actualPoint in frontier)
            {
                Assert.AreEqual<int>(frontier.Begin.X, actualPoint.X);
                Assert.AreEqual<int>(frontier.End.X, actualPoint.X);
                Assert.AreEqual<int>(frontier.Begin.Y + manualIter, actualPoint.Y);
                ++manualIter;
            }

            manualIter = 0;
            frontier = r.GetRightFrontier();
            foreach (var actualPoint in frontier)
            {
                Assert.AreEqual<int>(frontier.Begin.X, actualPoint.X);
                Assert.AreEqual<int>(frontier.End.X, actualPoint.X);
                Assert.AreEqual<int>(frontier.Begin.Y + manualIter, actualPoint.Y);
                ++manualIter;
            }
        }
    }
}
