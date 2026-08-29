using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DxLogQsoExporter.Dxn;

namespace DxLogQsoExporter.Export
{
    public sealed class ExportResult
    {
        private readonly List<QsoExportIssue> _issues = new List<QsoExportIssue>();
        private readonly List<string> _warnings = new List<string>();
        private readonly Dictionary<ExportProblemCategory, int> _problemCounts =
            new Dictionary<ExportProblemCategory, int>();

        public ExportResult(DateTime runStartUtc)
        {
            RunStartUtc = runStartUtc.ToUniversalTime();
        }

        public DateTime RunStartUtc { get; }

        public DateTime RunEndUtc { get; internal set; }

        public int TotalQsoRows { get; internal set; }

        public int XqsoCount { get; internal set; }

        public int ExportedCount { get; internal set; }

        public int ShortenedCount { get; internal set; }

        public int SkippedCount { get; internal set; }

        public int FailedCount { get; internal set; }

        public bool Cancelled { get; internal set; }

        public bool Aborted { get; internal set; }

        public string? ReportPath { get; internal set; }

        public string? FailureMessage { get; internal set; }

        public IReadOnlyList<QsoExportIssue> Issues
        {
            get { return new ReadOnlyCollection<QsoExportIssue>(_issues); }
        }

        public IReadOnlyList<string> Warnings
        {
            get { return new ReadOnlyCollection<string>(_warnings); }
        }

        public IReadOnlyDictionary<ExportProblemCategory, int> ProblemCounts
        {
            get { return new ReadOnlyDictionary<ExportProblemCategory, int>(_problemCounts); }
        }

        internal void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                _warnings.Add(warning);
            }
        }

        internal void AddIssue(QsoExportIssue issue)
        {
            _issues.Add(issue);
            int count;
            _problemCounts.TryGetValue(issue.Category, out count);
            _problemCounts[issue.Category] = count + 1;
        }

        internal void AddSuccess(bool shortened)
        {
            if (shortened)
            {
                ShortenedCount++;
            }
            else
            {
                ExportedCount++;
            }
        }

        internal void AddIssueAndCount(QsoExportIssue issue)
        {
            AddIssue(issue);
            if (issue.Status == QsoExportStatus.Skipped)
            {
                SkippedCount++;
            }
            else if (issue.Status == QsoExportStatus.Failed)
            {
                FailedCount++;
            }
        }
    }

    public enum QsoExportStatus
    {
        Exported,
        Shortened,
        Skipped,
        Failed
    }

    public enum ExportProblemCategory
    {
        MissingRecording,
        InvalidOrOutOfRangeRecordingPosition,
        OutputFileAlreadyPresent,
        SourceChangedOrCorrupt,
        MissingRadioAssignment,
        Other
    }

    public sealed class QsoExportIssue
    {
        public QsoExportIssue(
            QsoRecord qso,
            QsoExportStatus status,
            ExportProblemCategory category,
            string reason)
        {
            Qso = qso ?? throw new ArgumentNullException(nameof(qso));
            Status = status;
            Category = category;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public QsoRecord Qso { get; }

        public QsoExportStatus Status { get; }

        public ExportProblemCategory Category { get; }

        public string Reason { get; }
    }

    public sealed class ExportProgress
    {
        public ExportProgress(int completed, int total, string operation)
        {
            Completed = completed;
            Total = total;
            Operation = operation ?? string.Empty;
        }

        public int Completed { get; }

        public int Total { get; }

        public string Operation { get; }
    }
}
