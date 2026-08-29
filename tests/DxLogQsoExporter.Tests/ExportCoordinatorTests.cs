using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DxLogQsoExporter.Dxn;
using DxLogQsoExporter.Export;
using DxLogQsoExporter.Recordings;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class ExportCoordinatorTests
    {
        [TestMethod]
        public void ExportsIndependentQsosAndReconcilesReportCounts()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                Directory.CreateDirectory(outputFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(24, includeId3: true));

                var set = new RecordingSet(recordingFolder);
                RecordingSource? source;
                set.TryResolve(0, out source);
                Assert.IsNotNull(source);
                Mp3FrameIndex index;
                using (var stream = source!.OpenRead())
                {
                    index = Mp3FrameIndex.Build(stream, CancellationToken.None);
                }

                var normal = CreateRow(50, index.Frames[3].FileOffset + 2, false, 0);
                var xqso = CreateRow(60, index.Frames[19].FileOffset + 2, true, 0);
                var existing = CreateRow(40, index.Frames[8].FileOffset + 2, false, 0);
                var existingName = OutputNaming.CreateFileName(ToQso(existing));
                File.WriteAllBytes(Path.Combine(outputFolder, existingName), new byte[] { 9, 9 });
                var logPath = workspace.GetPath("contest.dxn");
                SyntheticDxn.Create(logPath, new[]
                {
                    CreateRow(10, -1, false, null),
                    CreateRow(20, index.Frames[2].FileOffset, false, 1),
                    CreateRow(30, new FileInfo(recordingPath).Length, false, 0),
                    existing,
                    normal,
                    xqso
                });

                var options = new ExportOptions(
                    logPath,
                    recordingFolder,
                    outputFolder,
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(300));
                var result = new ExportCoordinator().Run(options, CancellationToken.None);

                Assert.IsFalse(result.Aborted);
                Assert.IsFalse(result.Cancelled);
                Assert.AreEqual(6, result.TotalQsoRows);
                Assert.AreEqual(1, result.XqsoCount);
                Assert.AreEqual(1, result.ExportedCount);
                Assert.AreEqual(1, result.ShortenedCount);
                Assert.AreEqual(4, result.SkippedCount);
                Assert.AreEqual(0, result.FailedCount);
                Assert.IsNotNull(result.ReportPath);
                Assert.IsTrue(File.Exists(result.ReportPath));
                Assert.AreEqual(3, Directory.GetFiles(outputFolder, "*.mp3").Length);
                Assert.AreEqual(1, result.ProblemCounts[ExportProblemCategory.MissingRecording]);
                Assert.AreEqual(2, result.ProblemCounts[ExportProblemCategory.InvalidOrOutOfRangeRecordingPosition]);
                Assert.AreEqual(1, result.ProblemCounts[ExportProblemCategory.OutputFileAlreadyPresent]);
                var report = File.ReadAllText(result.ReportPath!);
                StringAssert.Contains(report, "SUMMARY COUNTS");
                StringAssert.Contains(report, "PROBLEM DETAIL");
                StringAssert.Contains(report, "QSO 000010");
                Assert.IsTrue(report.IndexOf("QSO 000060", StringComparison.Ordinal) < 0);
            }
        }

        [TestMethod]
        public void ExportsLegacyLogFromSingleUnnumberedRecording()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                Directory.CreateDirectory(outputFolder);
                var recordingPath = workspace.GetPath("recordings\\d4dx.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(24, includeId3: true));
                var index = ReadIndex(recordingPath);
                var logPath = workspace.GetPath("legacy.dxn");
                SyntheticDxn.CreateLegacy(logPath, new[]
                {
                    CreateRow(1, index.Frames[3].FileOffset, false, 6),
                    CreateRow(2, index.Frames[8].FileOffset, true, 6)
                });

                var result = new ExportCoordinator().Run(
                    new ExportOptions(logPath, recordingFolder, outputFolder, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100)),
                    CancellationToken.None);

                Assert.IsFalse(result.Aborted);
                Assert.AreEqual(2, result.TotalQsoRows);
                Assert.AreEqual(1, result.XqsoCount);
                Assert.AreEqual(2, result.ExportedCount + result.ShortenedCount);
                Assert.AreEqual(0, result.SkippedCount);
                Assert.AreEqual(0, result.FailedCount);
                Assert.AreEqual(2, Directory.GetFiles(outputFolder, "*.mp3").Length);
            }
        }

        [TestMethod]
        public void WritesReportAfterCancellation()
        {
            using (var workspace = new TemporaryWorkspace())
            using (var cancellation = new CancellationTokenSource())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(8));
                var logPath = workspace.GetPath("cancelled.dxn");
                SyntheticDxn.Create(logPath, new[]
                {
                    CreateRow(1, 20, false, 0),
                    CreateRow(2, 40, false, 0)
                });
                cancellation.Cancel();

                var result = new ExportCoordinator().Run(
                    new ExportOptions(logPath, recordingFolder, outputFolder, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                    cancellation.Token);

                Assert.IsTrue(result.Cancelled);
                Assert.IsNotNull(result.ReportPath);
                Assert.IsTrue(File.Exists(result.ReportPath));
                StringAssert.Contains(File.ReadAllText(result.ReportPath!), "Status: Cancelled");
            }
        }

        [TestMethod]
        public void WritesReportAfterAnAbortedRead()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(outputFolder);
                var logPath = workspace.GetPath("broken.dxn");
                File.WriteAllBytes(logPath, new byte[] { 1, 2, 3, 4 });
                var result = new ExportCoordinator().Run(
                    new ExportOptions(logPath, workspace.Root, outputFolder, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                    CancellationToken.None);

                Assert.IsTrue(result.Aborted);
                Assert.IsNotNull(result.ReportPath);
                Assert.IsTrue(File.Exists(result.ReportPath));
                StringAssert.Contains(File.ReadAllText(result.ReportPath!), "Status: Aborted");
            }
        }

        [TestMethod]
        public void CancellationBetweenWritesKeepsCompletedClipAccounted()
        {
            using (var workspace = new TemporaryWorkspace())
            using (var cancellation = new CancellationTokenSource())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(16));
                var index = ReadIndex(recordingPath);
                var logPath = workspace.GetPath("cancel-between.dxn");
                SyntheticDxn.Create(logPath, new[]
                {
                    CreateRow(1, index.Frames[3].FileOffset, false, 0),
                    CreateRow(2, index.Frames[8].FileOffset, false, 0)
                });
                var writeCount = 0;
                var progress = new ActionProgress(update =>
                {
                    if (update.Operation.StartsWith("Writing ", StringComparison.Ordinal) && ++writeCount == 2)
                    {
                        cancellation.Cancel();
                    }
                });

                var result = new ExportCoordinator().Run(
                    new ExportOptions(logPath, recordingFolder, outputFolder, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100)),
                    cancellation.Token,
                    progress);

                Assert.IsTrue(result.Cancelled);
                Assert.AreEqual(1, result.ExportedCount + result.ShortenedCount);
                Assert.AreEqual(1, result.SkippedCount);
                Assert.AreEqual(1, Directory.GetFiles(outputFolder, "*.mp3").Length);
                Assert.AreEqual(0, Directory.GetFiles(outputFolder, "*.partial", SearchOption.AllDirectories).Length);
            }
        }

        [TestMethod]
        public void SourceMutationRemovesEarlierGroupOutputs()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(16));
                var index = ReadIndex(recordingPath);
                var logPath = workspace.GetPath("mutating.dxn");
                SyntheticDxn.Create(logPath, new[]
                {
                    CreateRow(1, index.Frames[3].FileOffset, false, 0),
                    CreateRow(2, index.Frames[8].FileOffset, false, 0)
                });
                var writeCount = 0;
                var progress = new ActionProgress(update =>
                {
                    if (update.Operation.StartsWith("Writing ", StringComparison.Ordinal) && ++writeCount == 2)
                    {
                        File.AppendAllText(recordingPath, "changed");
                    }
                });

                var result = new ExportCoordinator().Run(
                    new ExportOptions(logPath, recordingFolder, outputFolder, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100)),
                    CancellationToken.None,
                    progress);

                Assert.AreEqual(0, result.ExportedCount + result.ShortenedCount);
                Assert.AreEqual(2, result.FailedCount);
                Assert.AreEqual(2, result.ProblemCounts[ExportProblemCategory.SourceChangedOrCorrupt]);
                Assert.AreEqual(0, Directory.GetFiles(outputFolder, "*.mp3").Length);
            }
        }

        [TestMethod]
        public void ExtractRadioChannelsWithMissingRadioAssignmentAccountsProblem()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                Directory.CreateDirectory(outputFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(16));
                var index = ReadIndex(recordingPath);
                var logPath = workspace.GetPath("no-radio.dxn");
                SyntheticDxn.Create(logPath, new[]
                {
                    CreateRow(1, index.Frames[3].FileOffset, false, 0)
                });

                var options = new ExportOptions(
                    logPath,
                    recordingFolder,
                    outputFolder,
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(100),
                    extractRadioChannels: true);
                var result = new ExportCoordinator().Run(options, CancellationToken.None);

                Assert.AreEqual(1, result.TotalQsoRows);
                Assert.AreEqual(0, result.ExportedCount);
                Assert.AreEqual(1, result.SkippedCount);
                Assert.AreEqual(1, result.ProblemCounts[ExportProblemCategory.MissingRadioAssignment]);
            }
        }

        [TestMethod]
        public void ExtractRadioChannelsWithValidRadiosProducesSubdirectories()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var recordingFolder = workspace.GetPath("recordings");
                var outputFolder = workspace.GetPath("output");
                Directory.CreateDirectory(recordingFolder);
                Directory.CreateDirectory(outputFolder);
                var recordingPath = Path.Combine(recordingFolder, "000.mp3");
                File.WriteAllBytes(recordingPath, SyntheticMp3.Create(24));
                var index = ReadIndex(recordingPath);
                var logPath = workspace.GetPath("legacy-radios.dxn");
                SyntheticDxn.CreateLegacy(logPath, new[]
                {
                    new SyntheticDxn.Row
                    {
                        QsoId = 1,
                        TimestampUtc = new DateTime(2026, 8, 28, 14, 23, 5, DateTimeKind.Utc),
                        Callsign = "G4ABC",
                        Band = "20M",
                        Mode = "CW",
                        IsXqso = false,
                        RecordingFileIndex = 0,
                        RecordingPosition = index.Frames[3].FileOffset,
                        Radio = "R1"
                    },
                    new SyntheticDxn.Row
                    {
                        QsoId = 2,
                        TimestampUtc = new DateTime(2026, 8, 28, 14, 24, 5, DateTimeKind.Utc),
                        Callsign = "G4XYZ",
                        Band = "20M",
                        Mode = "CW",
                        IsXqso = false,
                        RecordingFileIndex = 0,
                        RecordingPosition = index.Frames[8].FileOffset,
                        Radio = "R2"
                    }
                });

                var options = new ExportOptions(
                    logPath,
                    recordingFolder,
                    outputFolder,
                    TimeSpan.FromMilliseconds(20),
                    TimeSpan.FromMilliseconds(100),
                    extractRadioChannels: true);
                var result = new ExportCoordinator().Run(options, CancellationToken.None);

                Assert.AreEqual(2, result.TotalQsoRows);
                Assert.AreEqual(2, result.ExportedCount + result.ShortenedCount);
                Assert.AreEqual(0, result.SkippedCount);
                Assert.AreEqual(0, result.FailedCount);
                Assert.IsTrue(Directory.Exists(Path.Combine(outputFolder, "R1")));
                Assert.IsTrue(Directory.Exists(Path.Combine(outputFolder, "R2")));
                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(outputFolder, "R1"), "*.mp3").Length);
                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(outputFolder, "R2"), "*.mp3").Length);
            }
        }

        private static SyntheticDxn.Row CreateRow(long id, long position, bool xqso, int? fileIndex)
        {
            return new SyntheticDxn.Row
            {
                QsoId = id,
                TimestampUtc = new DateTime(2026, 8, 28, 14, 23, 5, DateTimeKind.Utc).AddSeconds(id),
                Callsign = "G" + id.ToString("D4", CultureInfo.InvariantCulture) + "ABC",
                Band = "20M",
                Mode = "CW",
                IsXqso = xqso,
                RecordingFileIndex = fileIndex,
                RecordingPosition = position
            };
        }

        private static QsoRecord ToQso(SyntheticDxn.Row row)
        {
            return new QsoRecord(
                row.QsoId,
                row.TimestampUtc,
                row.Callsign,
                row.Band,
                row.Mode,
                row.IsXqso,
                row.RecordingFileIndex,
                row.RecordingPosition);
        }

        private static Mp3FrameIndex ReadIndex(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Mp3FrameIndex.Build(stream, CancellationToken.None);
            }
        }

        private sealed class ActionProgress : IProgress<ExportProgress>
        {
            private readonly Action<ExportProgress> _action;

            public ActionProgress(Action<ExportProgress> action)
            {
                _action = action;
            }

            public void Report(ExportProgress value)
            {
                _action(value);
            }
        }
    }
}
