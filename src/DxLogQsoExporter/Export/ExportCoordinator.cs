using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DxLogQsoExporter.Dxn;
using DxLogQsoExporter.Recordings;

namespace DxLogQsoExporter.Export
{
    public sealed class ExportCoordinator
    {
        private readonly DxnLogReader _logReader;
        private readonly Mp3ClipWriter _clipWriter;

        public ExportCoordinator()
            : this(new DxnLogReader(), new Mp3ClipWriter())
        {
        }

        public ExportCoordinator(DxnLogReader logReader, Mp3ClipWriter clipWriter)
        {
            _logReader = logReader ?? throw new ArgumentNullException(nameof(logReader));
            _clipWriter = clipWriter ?? throw new ArgumentNullException(nameof(clipWriter));
        }

        public ExportResult Run(
            ExportOptions options,
            CancellationToken cancellationToken,
            IProgress<ExportProgress>? progress = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var result = new ExportResult(DateTime.UtcNow);
            var accounted = new HashSet<long>();
            IReadOnlyList<QsoRecord>? allQsos = null;
            try
            {
                options.ValidateTiming();
                options.ValidatePaths();
                Directory.CreateDirectory(options.OutputFolder);

                var readResult = _logReader.Read(options.LogPath, cancellationToken);
                allQsos = readResult.Qsos;
                result.TotalQsoRows = allQsos.Count;
                result.XqsoCount = allQsos.Count(qso => qso.IsXqso);
                foreach (var warning in readResult.Warnings)
                {
                    result.AddWarning(warning);
                }

                var progressClock = new ProgressClock(progress, allQsos.Count);
                progressClock.Report(0, "Reading DXN log", force: true);
                foreach (var qso in allQsos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!qso.HasValidRecordingPosition)
                    {
                        AccountIssue(
                            result,
                            accounted,
                            qso,
                            QsoExportStatus.Skipped,
                            ExportProblemCategory.InvalidOrOutOfRangeRecordingPosition,
                            GetInvalidPositionReason(qso));
                    }
                }

                var recordingSet = new RecordingSet(options.RecordingFolder);
                var groups = allQsos
                    .Where(qso => qso.HasValidRecordingPosition)
                    .GroupBy(qso => qso.RecordingFileIndex!.Value)
                    .OrderBy(group => group.Key)
                    .ToList();
                var completed = accounted.Count;
                progressClock.Report(completed, "Resolving recordings", force: true);
                foreach (var group in groups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var completedNormally = ProcessGroup(
                        options,
                        result,
                        accounted,
                        recordingSet,
                        group.Key,
                        group.OrderBy(qso => qso.RecordingPosition!.Value).ThenBy(qso => qso.QsoId).ToList(),
                        progressClock,
                        cancellationToken);
                    completed = accounted.Count;
                    progressClock.Report(completed, "Processing recordings", force: completedNormally);
                    if (!completedNormally)
                    {
                        break;
                    }
                }

                if (result.Cancelled)
                {
                    AccountRemaining(
                        result,
                        accounted,
                        allQsos,
                        QsoExportStatus.Skipped,
                        ExportProblemCategory.Other,
                        "Export was cancelled before this QSO was processed.");
                }
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                result.FailureMessage = "Export was cancelled.";
                if (allQsos != null)
                {
                    AccountRemaining(
                        result,
                        accounted,
                        allQsos,
                        QsoExportStatus.Skipped,
                        ExportProblemCategory.Other,
                        "Export was cancelled before this QSO was processed.");
                }
            }
            catch (DxnReadException exception)
            {
                result.Aborted = true;
                result.FailureMessage = exception.Message;
            }
            catch (Exception exception)
            {
                result.Aborted = true;
                result.FailureMessage = string.IsNullOrWhiteSpace(exception.Message)
                    ? "The export could not be completed."
                    : exception.Message;
                if (allQsos != null)
                {
                    AccountRemaining(
                        result,
                        accounted,
                        allQsos,
                        QsoExportStatus.Failed,
                        ExportProblemCategory.Other,
                        "The export was aborted before this QSO was processed.");
                }
            }
            finally
            {
                result.RunEndUtc = DateTime.UtcNow;
                try
                {
                    ExportReportWriter.Write(options, result);
                }
                catch (Exception exception)
                {
                    result.FailureMessage = string.IsNullOrWhiteSpace(result.FailureMessage)
                        ? "The result report could not be written: " + exception.Message
                        : result.FailureMessage + " Result report could not be written: " + exception.Message;
                }
            }

            return result;
        }

        private bool ProcessGroup(
            ExportOptions options,
            ExportResult result,
            HashSet<long> accounted,
            RecordingSet recordingSet,
            int recordingIndex,
            IReadOnlyList<QsoRecord> qsos,
            ProgressClock progress,
            CancellationToken cancellationToken)
        {
            RecordingSource? source;
            if (!recordingSet.TryResolve(recordingIndex, out source) || source == null)
            {
                foreach (var qso in qsos)
                {
                    AccountIssue(
                        result,
                        accounted,
                        qso,
                        QsoExportStatus.Skipped,
                        ExportProblemCategory.MissingRecording,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Recording file index {0} was not found or is not readable.",
                            recordingIndex));
                }

                return true;
            }

            progress.ReportIndexing(recordingIndex, 0, source.Fingerprint.Length, force: true);
            if (!source.IsUnchanged())
            {
                FailSourceGroup(
                    result,
                    accounted,
                    qsos,
                    "The recording source changed before indexing began.");
                return true;
            }

            Mp3FrameIndex index;
            try
            {
                using (var stream = source.OpenRead())
                {
                    var indexProgress = new Progress<Mp3IndexProgress>(update =>
                        progress.ReportIndexing(
                            recordingIndex,
                            update.BytesProcessed,
                            update.TotalBytes));
                    index = Mp3FrameIndex.Build(stream, cancellationToken, indexProgress);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is Mp3IndexException || exception is IOException || exception is UnauthorizedAccessException)
            {
                FailSourceGroup(
                    result,
                    accounted,
                    qsos,
                    "The recording is corrupt or could not be indexed: " + exception.Message);
                return true;
            }

            var work = new List<GroupWorkItem>();
            foreach (var qso in qsos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RadioChannel? channel = null;
                if (options.ExtractRadioChannels)
                {
                    RadioChannel resolvedChannel;
                    if (!RadioChannelMapping.TryResolve(qso.Radio, out resolvedChannel))
                    {
                        AccountIssue(
                            result,
                            accounted,
                            qso,
                            QsoExportStatus.Skipped,
                            ExportProblemCategory.MissingRadioAssignment,
                            "The QSO does not identify radio R1 or R2, so its channel could not be selected.");
                        continue;
                    }

                    channel = resolvedChannel;
                }

                try
                {
                    var window = ClipWindow.Create(
                        index,
                        source.Fingerprint.Length,
                        qso.RecordingPosition!.Value,
                        options.TimeBeforeQso,
                        options.ClipDuration);
                    var outputFolder = channel.HasValue
                        ? Path.Combine(options.OutputFolder, RadioChannelMapping.GetFolderName(channel.Value))
                        : options.OutputFolder;
                    var outputPath = Path.Combine(outputFolder, OutputNaming.CreateFileName(qso));
                    work.Add(new GroupWorkItem(qso, window, outputPath, channel));
                }
                catch (ClipWindowException exception)
                {
                    AccountIssue(
                        result,
                        accounted,
                        qso,
                        QsoExportStatus.Skipped,
                        ExportProblemCategory.InvalidOrOutOfRangeRecordingPosition,
                        exception.Message);
                }
            }

            if (work.Count == 0)
            {
                return true;
            }

            try
            {
                EnsureOutputReady(options.OutputFolder, GetEstimatedBytes(work));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                foreach (var item in work)
                {
                    AccountIssue(
                        result,
                        accounted,
                        item.Qso,
                        QsoExportStatus.Failed,
                        ExportProblemCategory.Other,
                        "The output folder is not ready: " + exception.Message);
                }

                return true;
            }

            var successes = new List<GroupSuccess>();
            foreach (var item in work)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report(
                        accounted.Count + successes.Count,
                        "Writing " + OutputNaming.CreateFileName(item.Qso));
                    _clipWriter.Write(source, item.Window, item.OutputPath, cancellationToken, item.Channel);
                    successes.Add(new GroupSuccess(item.Qso, item.Window.IsShortened, item.OutputPath));
                }
                catch (OperationCanceledException)
                {
                    CommitSuccesses(result, accounted, successes);
                    result.Cancelled = true;
                    return false;
                }
                catch (Mp3ClipWriteException exception)
                {
                    if (exception.Kind == Mp3ClipWriteFailureKind.OutputAlreadyExists)
                    {
                        AccountIssue(
                            result,
                            accounted,
                            item.Qso,
                            QsoExportStatus.Skipped,
                            ExportProblemCategory.OutputFileAlreadyPresent,
                            "The output file already exists and was not overwritten.");
                        continue;
                    }

                    if (exception.Kind == Mp3ClipWriteFailureKind.SourceChanged
                        || exception.Kind == Mp3ClipWriteFailureKind.ValidationFailure)
                    {
                        DeleteSuccesses(successes);
                        FailSourceGroup(
                            result,
                            accounted,
                            work.SkipWhile(candidate => !ReferenceEquals(candidate, item)).Select(candidate => candidate.Qso).ToList(),
                            "The recording source changed or became corrupt during export: " + exception.Message);
                        foreach (var success in successes)
                        {
                            AccountIssue(
                                result,
                                accounted,
                                success.Qso,
                                QsoExportStatus.Failed,
                                ExportProblemCategory.SourceChangedOrCorrupt,
                                "The source changed or became corrupt; the generated output was removed.");
                        }

                        return true;
                    }

                    AccountIssue(
                        result,
                        accounted,
                        item.Qso,
                        QsoExportStatus.Failed,
                        ExportProblemCategory.Other,
                        exception.Message);
                }
            }

            if (successes.Count > 0 && !source.IsUnchanged())
            {
                DeleteSuccesses(successes);
                foreach (var success in successes)
                {
                    AccountIssue(
                        result,
                        accounted,
                        success.Qso,
                        QsoExportStatus.Failed,
                        ExportProblemCategory.SourceChangedOrCorrupt,
                        "The source changed before the generated output could be committed.");
                }

                return true;
            }

            CommitSuccesses(result, accounted, successes);
            return true;
        }

        private static void CommitSuccesses(
            ExportResult result,
            HashSet<long> accounted,
            IEnumerable<GroupSuccess> successes)
        {
            foreach (var success in successes)
            {
                if (accounted.Add(success.Qso.QsoId))
                {
                    result.AddSuccess(success.Shortened);
                }
            }
        }

        private static void FailSourceGroup(
            ExportResult result,
            HashSet<long> accounted,
            IEnumerable<QsoRecord> qsos,
            string reason)
        {
            foreach (var qso in qsos)
            {
                AccountIssue(
                    result,
                    accounted,
                    qso,
                    QsoExportStatus.Failed,
                    ExportProblemCategory.SourceChangedOrCorrupt,
                    reason);
            }
        }

        private static void DeleteSuccesses(IEnumerable<GroupSuccess> successes)
        {
            foreach (var success in successes)
            {
                try
                {
                    if (File.Exists(success.OutputPath))
                    {
                        File.Delete(success.OutputPath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void EnsureOutputReady(string outputFolder, long estimatedBytes)
        {
            Directory.CreateDirectory(outputFolder);
            var probePath = Path.Combine(outputFolder, ".dxlog-qso-exporter-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, false))
                {
                    stream.WriteByte(0);
                    stream.Flush(true);
                }

                if (!IsUncPath(outputFolder))
                {
                    var root = Path.GetPathRoot(Path.GetFullPath(outputFolder));
                    if (string.IsNullOrWhiteSpace(root))
                    {
                        throw new IOException("The output drive could not be identified.");
                    }

                    var drive = new DriveInfo(root);
                    var required = estimatedBytes > long.MaxValue - (1024 * 1024)
                        ? long.MaxValue
                        : estimatedBytes + (1024 * 1024);
                    if (!drive.IsReady || drive.AvailableFreeSpace < required)
                    {
                        throw new IOException("The output drive does not have enough free space for this source group.");
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(probePath))
                    {
                        File.Delete(probePath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static long GetEstimatedBytes(IEnumerable<GroupWorkItem> work)
        {
            long total = 0;
            foreach (var item in work)
            {
                if (item.Window.ByteLength > long.MaxValue - total)
                {
                    return long.MaxValue;
                }

                total += item.Window.ByteLength;
            }

            return total;
        }

        private static bool IsUncPath(string path)
        {
            return path.StartsWith("\\\\", StringComparison.Ordinal)
                || path.StartsWith("//", StringComparison.Ordinal);
        }

        private static string GetInvalidPositionReason(QsoRecord qso)
        {
            if (!qso.RecordingFileIndex.HasValue)
            {
                return "The saved recording file index is missing or invalid.";
            }

            if (qso.RecordingFileIndex.Value == DxnSchema.RecordingDisabledSentinel)
            {
                return "No recording was saved for this QSO (DXLog uses recording index -1 for unavailable recordings).";
            }

            if (qso.RecordingFileIndex.Value < DxnSchema.RecordingIndexBase)
            {
                return "The saved recording file index is below the supported index base.";
            }

            if (!qso.RecordingPosition.HasValue || qso.RecordingPosition.Value < 0)
            {
                return "The saved recording position is missing or negative.";
            }

            return "The saved recording position is invalid.";
        }

        private static void AccountIssue(
            ExportResult result,
            HashSet<long> accounted,
            QsoRecord qso,
            QsoExportStatus status,
            ExportProblemCategory category,
            string reason)
        {
            if (!accounted.Add(qso.QsoId))
            {
                return;
            }

            result.AddIssueAndCount(new QsoExportIssue(qso, status, category, reason));
        }

        private static void AccountRemaining(
            ExportResult result,
            HashSet<long> accounted,
            IEnumerable<QsoRecord> qsos,
            QsoExportStatus status,
            ExportProblemCategory category,
            string reason)
        {
            foreach (var qso in qsos)
            {
                AccountIssue(result, accounted, qso, status, category, reason);
            }
        }

        private sealed class GroupWorkItem
        {
            public GroupWorkItem(QsoRecord qso, ClipWindow window, string outputPath, RadioChannel? channel)
            {
                Qso = qso;
                Window = window;
                OutputPath = outputPath;
                Channel = channel;
            }

            public QsoRecord Qso { get; }

            public ClipWindow Window { get; }

            public string OutputPath { get; }

            public RadioChannel? Channel { get; }
        }

        private sealed class GroupSuccess
        {
            public GroupSuccess(QsoRecord qso, bool shortened, string outputPath)
            {
                Qso = qso;
                Shortened = shortened;
                OutputPath = outputPath;
            }

            public QsoRecord Qso { get; }

            public bool Shortened { get; }

            public string OutputPath { get; }
        }

        private sealed class ProgressClock
        {
            private readonly IProgress<ExportProgress>? _progress;
            private readonly int _total;
            private DateTime _lastReport = DateTime.MinValue;
            private string _lastOperation = string.Empty;

            public ProgressClock(IProgress<ExportProgress>? progress, int total)
            {
                _progress = progress;
                _total = total;
            }

            public void Report(int completed, string operation, bool force = false)
            {
                if (_progress == null)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (!force
                    && string.Equals(operation, _lastOperation, StringComparison.Ordinal)
                    && now - _lastReport < TimeSpan.FromMilliseconds(100))
                {
                    return;
                }

                _lastReport = now;
                _lastOperation = operation;
                _progress.Report(new ExportProgress(Math.Min(completed, _total), _total, operation));
            }

            public void ReportIndexing(
                int recordingIndex,
                long bytesProcessed,
                long totalBytes,
                bool force = false)
            {
                if (_progress == null)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                if (!force && now - _lastReport < TimeSpan.FromMilliseconds(100))
                {
                    return;
                }

                var percentage = totalBytes <= 0
                    ? 100
                    : (int)Math.Max(0, Math.Min(100, bytesProcessed * 100d / totalBytes));
                _lastReport = now;
                _lastOperation = "Indexing recording file " + RecordingSet.GetFileName(recordingIndex);
                _progress.Report(
                    new ExportProgress(
                        percentage,
                        100,
                        _lastOperation + " (" + percentage.ToString(CultureInfo.InvariantCulture) + "%)"));
            }
        }
    }
}
