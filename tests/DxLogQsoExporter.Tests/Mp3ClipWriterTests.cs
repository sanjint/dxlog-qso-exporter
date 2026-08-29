using System;
using System.IO;
using System.Linq;
using System.Threading;
using DxLogQsoExporter.Export;
using DxLogQsoExporter.Recordings;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class Mp3ClipWriterTests
    {
        [TestMethod]
        public void WritesACompleteFrameRangeThatReindexes()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingPath = workspace.GetPath("000.mp3");
                var recordingBytes = SyntheticMp3.Create(20, includeId3: true);
                File.WriteAllBytes(recordingPath, recordingBytes);
                var set = new RecordingSet(workspace.Root);
                RecordingSource? source;
                Assert.IsTrue(set.TryResolve(0, out source));
                Assert.IsNotNull(source);
                Mp3FrameIndex index;
                using (var input = source!.OpenRead())
                {
                    index = Mp3FrameIndex.Build(input, CancellationToken.None);
                }

                var window = ClipWindow.Create(
                    index,
                    source.Fingerprint.Length,
                    index.Frames[8].FileOffset + 2,
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(100));
                var outputPath = workspace.GetPath("clip.mp3");

                var writeResult = new Mp3ClipWriter(31).Write(source, window, outputPath, CancellationToken.None);

                Assert.AreEqual(window.ByteLength, writeResult.ByteCount);
                Assert.AreEqual(window.ByteLength, new FileInfo(outputPath).Length);
                using (var output = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var outputIndex = Mp3FrameIndex.Build(output, CancellationToken.None);
                    Assert.IsTrue(outputIndex.Frames.Count > 0);
                    Assert.AreEqual(window.ByteLength, outputIndex.Frames.Sum(frame => (long)frame.FrameLength));
                }
                Assert.IsFalse(File.Exists(outputPath + ".partial"));
            }
        }

        [TestMethod]
        public void DoesNotOverwriteAnExistingOutput()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingPath = workspace.GetPath("000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(4));
                var set = new RecordingSet(workspace.Root);
                RecordingSource? source;
                set.TryResolve(0, out source);
                Assert.IsNotNull(source);
                Mp3FrameIndex index;
                using (var input = source!.OpenRead())
                {
                    index = Mp3FrameIndex.Build(input, CancellationToken.None);
                }

                var outputPath = workspace.GetPath("existing.mp3");
                File.WriteAllBytes(outputPath, new byte[] { 7, 8, 9 });
                var window = ClipWindow.Create(index, new FileInfo(recordingPath).Length, index.Frames[1].FileOffset, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));

                var exception = Assert.ThrowsException<Mp3ClipWriteException>(
                    () => new Mp3ClipWriter().Write(source, window, outputPath, CancellationToken.None));

                Assert.AreEqual(Mp3ClipWriteFailureKind.OutputAlreadyExists, exception.Kind);
                CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, File.ReadAllBytes(outputPath));
                Assert.IsFalse(File.Exists(outputPath + ".partial"));
            }
        }

        [TestMethod]
        public void RejectsSourceMutationBeforeWriting()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingPath = workspace.GetPath("000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(4));
                var set = new RecordingSet(workspace.Root);
                RecordingSource? source;
                set.TryResolve(0, out source);
                Assert.IsNotNull(source);
                Mp3FrameIndex index;
                using (var input = source!.OpenRead())
                {
                    index = Mp3FrameIndex.Build(input, CancellationToken.None);
                }

                File.AppendAllText(recordingPath, "changed");
                var window = ClipWindow.Create(index, source.Fingerprint.Length, index.Frames[1].FileOffset, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
                var outputPath = workspace.GetPath("changed.mp3");

                var exception = Assert.ThrowsException<Mp3ClipWriteException>(
                    () => new Mp3ClipWriter().Write(source, window, outputPath, CancellationToken.None));

                Assert.AreEqual(Mp3ClipWriteFailureKind.SourceChanged, exception.Kind);
                Assert.IsFalse(File.Exists(outputPath));
                Assert.IsFalse(File.Exists(outputPath + ".partial"));
            }
        }

        [TestMethod]
        public void RemovesPartialPathWhenAlreadyCancelled()
        {
            using (var workspace = new TemporaryWorkspace())
            using (var cancellation = new CancellationTokenSource())
            {
                var recordingPath = workspace.GetPath("000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(4));
                var set = new RecordingSet(workspace.Root);
                RecordingSource? source;
                set.TryResolve(0, out source);
                Assert.IsNotNull(source);
                Mp3FrameIndex index;
                using (var input = source!.OpenRead())
                {
                    index = Mp3FrameIndex.Build(input, CancellationToken.None);
                }

                var window = ClipWindow.Create(index, source.Fingerprint.Length, index.Frames[1].FileOffset, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
                var outputPath = workspace.GetPath("cancelled.mp3");
                cancellation.Cancel();

                Assert.ThrowsException<OperationCanceledException>(
                    () => new Mp3ClipWriter().Write(source, window, outputPath, cancellation.Token));
                Assert.IsFalse(File.Exists(outputPath));
                Assert.IsFalse(File.Exists(outputPath + ".partial"));
            }
        }
    }
}
