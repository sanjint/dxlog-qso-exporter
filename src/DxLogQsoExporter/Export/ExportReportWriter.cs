using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace DxLogQsoExporter.Export
{
    public static class ExportReportWriter
    {
        private const string ReportTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public static string Write(ExportOptions options, ExportResult result)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Directory.CreateDirectory(options.OutputFolder);
            var reportTime = result.RunEndUtc == default(DateTime)
                ? DateTime.UtcNow
                : result.RunEndUtc.ToUniversalTime();
            var lines = BuildLines(options, result);
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var candidateTime = reportTime;
            while (true)
            {
                var reportPath = GetReportPath(options.OutputFolder, candidateTime);
                if (File.Exists(reportPath))
                {
                    candidateTime = candidateTime.AddSeconds(1);
                    continue;
                }

                var partialPath = reportPath + ".partial";
                try
                {
                    using (var stream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, false))
                    using (var writer = new StreamWriter(stream, encoding))
                    {
                        writer.NewLine = "\r\n";
                        foreach (var line in lines)
                        {
                            writer.WriteLine(line);
                        }

                        writer.Flush();
                    }

                    File.Move(partialPath, reportPath);
                    result.ReportPath = reportPath;
                    return reportPath;
                }
                catch (IOException) when (File.Exists(reportPath))
                {
                    TryDeleteFile(partialPath);
                    candidateTime = candidateTime.AddSeconds(1);
                }
                catch
                {
                    TryDeleteFile(partialPath);
                    throw;
                }
            }
        }

        private static List<string> BuildLines(ExportOptions options, ExportResult result)
        {
            var lines = new List<string>
            {
                "DXLog QSO Exporter report",
                string.Empty,
                "RUN HEADER",
                "Application version: " + GetApplicationVersion(),
                "Run start UTC: " + result.RunStartUtc.ToUniversalTime().ToString(ReportTimestampFormat, CultureInfo.InvariantCulture),
                "Run end UTC: " + result.RunEndUtc.ToUniversalTime().ToString(ReportTimestampFormat, CultureInfo.InvariantCulture),
                "Status: " + GetStatus(result),
                "Input log: " + SanitizeLogPath(options.LogPath),
                "Recording folder: " + SanitizeFolderPath(options.RecordingFolder, "<recording-folder>"),
                "Output folder: " + SanitizeFolderPath(options.OutputFolder, "<output-folder>"),
                "Time before QSO: " + options.TimeBeforeQso.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " seconds",
                "Total clip duration: " + options.ClipDuration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " seconds",
                "Radio channel extraction: " + (options.ExtractRadioChannels ? "enabled (R1=left, R2=right)" : "disabled"),
                string.Empty,
                "SUMMARY COUNTS",
                "Total QSO rows: " + result.TotalQsoRows.ToString(CultureInfo.InvariantCulture),
                "Exported: " + result.ExportedCount.ToString(CultureInfo.InvariantCulture),
                "Shortened: " + result.ShortenedCount.ToString(CultureInfo.InvariantCulture),
                "Skipped: " + result.SkippedCount.ToString(CultureInfo.InvariantCulture),
                "Failed: " + result.FailedCount.ToString(CultureInfo.InvariantCulture),
                "XQSO count: " + result.XqsoCount.ToString(CultureInfo.InvariantCulture),
                "Missing recording: " + GetProblemCount(result, ExportProblemCategory.MissingRecording),
                "Missing or invalid recording position: " + GetProblemCount(result, ExportProblemCategory.InvalidOrOutOfRangeRecordingPosition),
                "Output file already present: " + GetProblemCount(result, ExportProblemCategory.OutputFileAlreadyPresent),
                "Source changed or corrupt during the run: " + GetProblemCount(result, ExportProblemCategory.SourceChangedOrCorrupt),
                "Missing radio assignment: " + GetProblemCount(result, ExportProblemCategory.MissingRadioAssignment),
                "Other problems: " + GetProblemCount(result, ExportProblemCategory.Other)
            };

            if (!string.IsNullOrWhiteSpace(result.FailureMessage))
            {
                lines.Add("Run message: " + result.FailureMessage);
            }

            foreach (var warning in result.Warnings)
            {
                lines.Add("Warning: " + warning);
            }

            lines.Add(string.Empty);
            lines.Add("PROBLEM DETAIL");
            if (result.Issues.Count == 0)
            {
                lines.Add("None");
            }
            else
            {
                foreach (var issue in result.Issues)
                {
                    var callsign = string.IsNullOrWhiteSpace(issue.Qso.Callsign) ? "<none>" : issue.Qso.Callsign;
                    var recordingIndex = issue.Qso.RecordingFileIndex.HasValue
                        ? issue.Qso.RecordingFileIndex.Value.ToString(CultureInfo.InvariantCulture)
                        : "<none>";
                    var radio = string.IsNullOrWhiteSpace(issue.Qso.Radio) ? "<none>" : issue.Qso.Radio;
                    lines.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "QSO {0} | Callsign: {1} | Radio: {2} | Recording index: {3} | Reason: {4}",
                        issue.Qso.QsoId.ToString("D6", CultureInfo.InvariantCulture),
                        callsign,
                        radio,
                        recordingIndex,
                        issue.Reason));
                }
            }

            return lines;
        }

        private static string GetReportPath(string outputFolder, DateTime reportTime)
        {
            var fileName = string.Format(
                CultureInfo.InvariantCulture,
                "_export-report_{0}.txt",
                reportTime.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            return Path.Combine(outputFolder, fileName);
        }

        private static string SanitizeLogPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "<log-file>";
            }

            try
            {
                var fileName = Path.GetFileName(path!.Trim());
                return string.IsNullOrWhiteSpace(fileName) ? "<log-file>" : fileName;
            }
            catch (ArgumentException)
            {
                return "<log-file>";
            }
        }

        private static string SanitizeFolderPath(string? path, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return defaultName;
            }

            try
            {
                var trimmed = path!.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dirName = Path.GetFileName(trimmed);
                return string.IsNullOrWhiteSpace(dirName) ? defaultName : dirName;
            }
            catch (ArgumentException)
            {
                return defaultName;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string GetApplicationVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "unknown" : version.ToString(3);
        }

        private static string GetStatus(ExportResult result)
        {
            if (result.Cancelled)
            {
                return "Cancelled";
            }

            if (result.Aborted)
            {
                return "Aborted";
            }

            return "Completed";
        }

        private static string GetProblemCount(ExportResult result, ExportProblemCategory category)
        {
            int count;
            return result.ProblemCounts.TryGetValue(category, out count)
                ? count.ToString(CultureInfo.InvariantCulture)
                : "0";
        }
    }
}
