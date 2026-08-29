using DxLogQsoExporter.Export;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class RadioChannelTests
    {
        [TestMethod]
        public void MapsR1ToLeftAndR2ToRight()
        {
            RadioChannel channel;

            Assert.IsTrue(RadioChannelMapping.TryResolve("R1", out channel));
            Assert.AreEqual(RadioChannel.Left, channel);
            Assert.AreEqual("R1", RadioChannelMapping.GetFolderName(channel));

            Assert.IsTrue(RadioChannelMapping.TryResolve(" r2 ", out channel));
            Assert.AreEqual(RadioChannel.Right, channel);
            Assert.AreEqual("R2", RadioChannelMapping.GetFolderName(channel));
        }

        [TestMethod]
        public void RejectsUnknownRadioAssignment()
        {
            RadioChannel channel;

            Assert.IsFalse(RadioChannelMapping.TryResolve("R", out channel));
            Assert.IsFalse(RadioChannelMapping.TryResolve(null, out channel));
        }
    }
}
