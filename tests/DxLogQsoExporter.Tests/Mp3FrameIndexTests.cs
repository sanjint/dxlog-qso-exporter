using System;
using System.IO;
using System.Text;
using System.Threading;
using DxLogQsoExporter.Recordings;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class Mp3FrameIndexTests
    {
        [TestMethod]
        public void IndexesFramesAndDerivesDurationFromSamples()
        {
            var bytes = SyntheticMp3.Create(6, bitrateKbps: 128, sampleRate: 44100);
            var updates = new System.Collections.Generic.List<Mp3IndexProgress>();
            using (var stream = new MemoryStream(bytes))
            {
                var index = Mp3FrameIndex.Build(
                    stream,
                    CancellationToken.None,
                    new SynchronousProgress(updates));

                Assert.AreEqual(6, index.Frames.Count);
                Assert.AreEqual(0L, index.DataStartOffset);
                Assert.AreEqual(1152, index.Frames[0].SampleCount);
                Assert.AreEqual(44100, index.Frames[0].SampleRate);
                Assert.AreEqual(TimeSpan.FromSeconds(6 * 1152d / 44100d), index.Duration);
                Assert.AreEqual(index.Frames[0].FileOffset + index.Frames[0].FrameLength, index.Frames[1].FileOffset);
                Assert.IsTrue(updates.Count > 0);
                Assert.AreEqual(bytes.Length, updates[updates.Count - 1].BytesProcessed);
                Assert.AreEqual(bytes.Length, updates[updates.Count - 1].TotalBytes);
            }
        }

        [TestMethod]
        public void SkipsLeadingId3TagWithoutTreatingItAsAudio()
        {
            var bytes = SyntheticMp3.Create(3, includeId3: true);
            using (var stream = new MemoryStream(bytes))
            {
                var index = Mp3FrameIndex.Build(stream, CancellationToken.None);

                Assert.AreEqual(19L, index.DataStartOffset);
                Assert.AreEqual(3, index.Frames.Count);
                Assert.AreEqual(19L, index.Frames[0].FileOffset);
            }
        }

        [TestMethod]
        public void FindsFramesAtAndAroundByteAndTimeBoundaries()
        {
            var bytes = SyntheticMp3.Create(5);
            using (var stream = new MemoryStream(bytes))
            {
                var index = Mp3FrameIndex.Build(stream, CancellationToken.None);
                var first = index.Frames[0];
                var second = index.Frames[1];

                Assert.AreEqual(first, index.FindFrameAtOrBefore(first.FileOffset));
                Assert.AreEqual(first, index.FindFrameAtOrBefore(second.FileOffset - 1));
                Assert.AreEqual(second, index.FindFrameAtOrBefore(second.FileOffset));
                Assert.IsNull(index.FindFrameAtOrBefore(0 - 1));
                Assert.AreEqual(1, index.FindFirstFrameAtOrAfter(second.StartTime));
                Assert.AreEqual(1, index.FindLastFrameAtOrBefore(second.StartTime));
            }
        }

        [TestMethod]
        public void RejectsInvalidRegionBetweenFrames()
        {
            AssertIndexFailure(
                SyntheticMp3.Create(4, gapAfterFrame: 1, gapLength: 8),
                Mp3IndexFailureKind.InvalidRegion);
        }

        [TestMethod]
        public void RejectsIncompatibleSampleRateChange()
        {
            AssertIndexFailure(
                SyntheticMp3.Create(4, alternateSampleRate: 48000),
                Mp3IndexFailureKind.IncompatibleFormat);
        }

        [TestMethod]
        public void RejectsTruncatedFinalFrame()
        {
            AssertIndexFailure(
                SyntheticMp3.Create(4, truncateLastFrame: true),
                Mp3IndexFailureKind.TruncatedFinalFrame);
        }

        [TestMethod]
        public void IndexesLegacyMetadataAndIgnoresTerminalPartialFrame()
        {
            using (var stream = new MemoryStream(CreateLegacyRecordingWithTruncatedFinalFrame()))
            {
                var index = Mp3FrameIndex.Build(stream, CancellationToken.None);

                Assert.AreEqual(3, index.Frames.Count);
                Assert.AreEqual(SyntheticMp3.GetFrameLength() + 128, index.Frames[1].FileOffset);
            }
        }

        [TestMethod]
        public void RejectsRecordingWithNoReadableFrames()
        {
            AssertIndexFailure(new byte[128], Mp3IndexFailureKind.NoReadableFrames);
        }

        [TestMethod]
        public void CancellationStopsIndexing()
        {
            using (var source = new MemoryStream(SyntheticMp3.Create(20)))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsException<OperationCanceledException>(
                    () => Mp3FrameIndex.Build(source, cancellation.Token));
            }
        }

        [TestMethod]
        public void IndexesMpeg1BitrateAndSampleRatePresets()
        {
            var bitrates = new[] { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
            var sampleRates = new[] { 32000, 44100, 48000 };
            foreach (var bitrate in bitrates)
            {
                foreach (var sampleRate in sampleRates)
                {
                    using (var stream = new MemoryStream(SyntheticMp3.Create(2, bitrate, sampleRate)))
                    {
                        var index = Mp3FrameIndex.Build(stream, CancellationToken.None);
                        Assert.AreEqual(2, index.Frames.Count, bitrate + " kbps at " + sampleRate + " Hz");
                        Assert.AreEqual(sampleRate, index.Frames[0].SampleRate);
                    }
                }
            }
        }

        private static void AssertIndexFailure(byte[] bytes, Mp3IndexFailureKind expectedKind)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var exception = Assert.ThrowsException<Mp3IndexException>(
                    () => Mp3FrameIndex.Build(stream, CancellationToken.None));
                Assert.AreEqual(expectedKind, exception.Kind);
            }
        }

        private static byte[] CreateLegacyRecordingWithTruncatedFinalFrame()
        {
            var audio = SyntheticMp3.Create(4, truncateLastFrame: true);
            var metadata = new byte[128];
            var marker = Encoding.ASCII.GetBytes("DXLog.net - legacy");
            Buffer.BlockCopy(marker, 0, metadata, 64, marker.Length);
            var firstFrameLength = SyntheticMp3.GetFrameLength();
            using (var stream = new MemoryStream())
            {
                stream.Write(audio, 0, firstFrameLength);
                stream.Write(metadata, 0, metadata.Length);
                stream.Write(audio, firstFrameLength, audio.Length - firstFrameLength);
                return stream.ToArray();
            }
        }

        private sealed class SynchronousProgress : IProgress<Mp3IndexProgress>
        {
            private readonly System.Collections.Generic.List<Mp3IndexProgress> _updates;

            public SynchronousProgress(System.Collections.Generic.List<Mp3IndexProgress> updates)
            {
                _updates = updates;
            }

            public void Report(Mp3IndexProgress value)
            {
                _updates.Add(value);
            }
        }
    }
}
