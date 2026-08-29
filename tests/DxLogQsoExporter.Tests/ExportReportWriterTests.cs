using System;
using System.IO;
using System.Linq;
using DxLogQsoExporter.Export;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class ExportReportWriterTests
    {
        [TestMethod]
        public void WritesUtf8CrLfReportAndAvoidsSameSecondCollision()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var outputFolder = workspace.GetPath("output");
                var options = new ExportOptions(
                    workspace.GetPath("contest.dxn"),
                    workspace.Root,
                    outputFolder,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60));
                var runStartUtc = new DateTime(2026, 8, 29, 17, 56, 50, DateTimeKind.Utc).AddTicks(7398867);
                var first = ExportReportWriter.Write(options, new ExportResult(runStartUtc));
                var second = ExportReportWriter.Write(options, new ExportResult(DateTime.UtcNow));

                Assert.AreNotEqual(first, second);
                var report = File.ReadAllText(first);
                StringAssert.Contains(report, "Run start UTC: 2026-08-29T17:56:50Z");
                Assert.IsFalse(report.Contains("17:56:50.7398867"));
                var bytes = File.ReadAllBytes(first);
                Assert.IsTrue(bytes.Contains((byte)'\r'));
                Assert.IsTrue(bytes.Contains((byte)'\n'));
                for (var index = 0; index < bytes.Length; index++)
                {
                    if (bytes[index] == (byte)'\n')
                    {
                        Assert.IsTrue(index > 0 && bytes[index - 1] == (byte)'\r');
                    }
                }
                Assert.AreEqual(2, Directory.GetFiles(outputFolder, "_export-report_*.txt").Length);
                Assert.AreEqual(0, Directory.GetFiles(outputFolder, "*.partial").Length);
            }
        }

        [TestMethod]
        public void RedactsAbsoluteWindowsPathsInReportHeader()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var outputFolder = workspace.GetPath("ExportOutput");
                var options = new ExportOptions(
                    @"C:\Users\SecretUser\MyContests\W1AW_2026.dxn",
                    @"C:\Users\SecretUser\AppData\Local\DXLog\AudioRecordings",
                    outputFolder,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60));

                var result = new ExportResult(DateTime.UtcNow);
                var reportPath = ExportReportWriter.Write(options, result);

                var report = File.ReadAllText(reportPath);
                Assert.IsFalse(report.Contains("SecretUser"), "Report must not leak Windows usernames.");
                Assert.IsFalse(report.Contains(@"C:\Users\"), "Report must not leak user profile paths.");
                Assert.IsFalse(report.Contains("MyContests"), "Report must not leak parent directory hierarchies.");
                StringAssert.Contains(report, "Input log: W1AW_2026.dxn");
                StringAssert.Contains(report, "Recording folder: AudioRecordings");
                StringAssert.Contains(report, "Output folder: ExportOutput");
                Assert.AreEqual(0, Directory.GetFiles(outputFolder, "*.partial").Length);
            }
        }

        [TestMethod]
        public void AtomicWriteDoesNotLeavePartialFiles()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var outputFolder = workspace.GetPath("AtomicTestOutput");
                var options = new ExportOptions(
                    workspace.GetPath("test.dxn"),
                    workspace.Root,
                    outputFolder,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60));

                var result = new ExportResult(DateTime.UtcNow);
                var reportPath = ExportReportWriter.Write(options, result);

                Assert.IsTrue(File.Exists(reportPath));
                Assert.AreEqual(0, Directory.GetFiles(outputFolder, "*.partial").Length);
            }
        }
    }
}
