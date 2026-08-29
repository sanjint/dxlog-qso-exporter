using System;
using System.IO;
using System.Threading;
using DxLogQsoExporter.Export;
using DxLogQsoExporter.Recordings;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class ClipWindowTests
    {
        [TestMethod]
        public void UsesFrameAtOrBeforeSavedPositionAsAnchor()
        {
            var index = BuildIndex(10);
            var anchor = index.Frames[4];
            var positionBetweenFrames = anchor.FileOffset + 3;

            var window = ClipWindow.Create(
                index,
                SyntheticMp3.Create(10).LongLength,
                positionBetweenFrames,
                TimeSpan.FromMilliseconds(20),
                TimeSpan.FromMilliseconds(80));

            Assert.AreEqual(anchor.StartTime, window.AnchorTime);
            Assert.AreEqual(index.Frames[3].FileOffset, window.ByteStart);
            Assert.IsFalse(window.IsShortened);
        }

        [TestMethod]
        public void ClampsAtStartAndMarksShortened()
        {
            var index = BuildIndex(4);
            var window = ClipWindow.Create(
                index,
                SyntheticMp3.Create(4).LongLength,
                index.Frames[0].FileOffset,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3));

            Assert.IsTrue(window.IsShortened);
            Assert.AreEqual(TimeSpan.Zero, window.ActualStart);
            Assert.AreEqual(index.Frames[0].FileOffset, window.ByteStart);
        }

        [TestMethod]
        public void ClampsAtEndAndMarksShortened()
        {
            var bytes = SyntheticMp3.Create(4);
            var index = BuildIndex(4);
            var window = ClipWindow.Create(
                index,
                bytes.LongLength,
                index.Frames[3].FileOffset,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromSeconds(10));

            Assert.IsTrue(window.IsShortened);
            Assert.AreEqual(index.Duration, window.ActualEnd);
            Assert.AreEqual(bytes.LongLength, window.ByteEnd);
        }

        [TestMethod]
        public void RejectsPositionAtEndOfSource()
        {
            var bytes = SyntheticMp3.Create(3);
            var index = BuildIndex(3);

            var exception = Assert.ThrowsExactly<ClipWindowException>(
                () => ClipWindow.Create(index, bytes.LongLength, bytes.LongLength, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

            Assert.AreEqual(ClipWindowFailureKind.OutOfRangePosition, exception.Kind);
        }

        private static Mp3FrameIndex BuildIndex(int frameCount)
        {
            using (var stream = new MemoryStream(SyntheticMp3.Create(frameCount)))
            {
                return Mp3FrameIndex.Build(stream, CancellationToken.None);
            }
        }
    }
}
