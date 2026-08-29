using System;
using System.IO;
using DxLogQsoExporter.Recordings;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class RecordingSetTests
    {
        [TestMethod]
        public void ResolvesPaddedIndexesAndLeavesGapsMissing()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                File.WriteAllBytes(workspace.GetPath("000.mp3"), SyntheticMp3.Create(2));
                File.WriteAllBytes(workspace.GetPath("002.mp3"), SyntheticMp3.Create(2));
                File.WriteAllBytes(workspace.GetPath("2.mp3"), SyntheticMp3.Create(2));
                File.WriteAllBytes(workspace.GetPath("notes.mp3"), new byte[2]);

                var set = new RecordingSet(workspace.Root);

                RecordingSource? source;
                Assert.IsTrue(set.TryResolve(0, out source));
                Assert.IsNotNull(source);
                Assert.IsTrue(set.TryResolve(2, out source));
                Assert.IsFalse(set.TryResolve(1, out source));
                Assert.AreEqual("000.mp3", RecordingSet.GetFileName(0));
                Assert.AreEqual("002.mp3", RecordingSet.GetFileName(2));
            }
        }

        [TestMethod]
        public void ResolvesSingleUnnumberedRecordingForLegacyLogs()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("d4dx.mp3");
                File.WriteAllBytes(path, SyntheticMp3.Create(2, includeId3: true));

                var set = new RecordingSet(workspace.Root);

                RecordingSource? source;
                Assert.IsTrue(set.TryResolve(6, out source));
                Assert.IsNotNull(source);
                Assert.AreEqual(Path.GetFullPath(path), source!.FullPath);
            }
        }

        [TestMethod]
        public void DetectsSourceFingerprintChanges()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("000.mp3");
                File.WriteAllBytes(path, SyntheticMp3.Create(2));
                var set = new RecordingSet(workspace.Root);
                RecordingSource? source;
                Assert.IsTrue(set.TryResolve(0, out source));
                Assert.IsNotNull(source);

                using (var append = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    append.WriteByte(1);
                    append.WriteByte(2);
                    append.WriteByte(3);
                }

                Assert.IsFalse(source!.IsUnchanged());
            }
        }
    }
}
