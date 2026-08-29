using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DxLogQsoExporter.Recordings
{
    public sealed class RecordingSet
    {
        private readonly Dictionary<int, RecordingSource> _sources;
        private readonly string? _singleFileFallbackPath;

        public RecordingSet(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("A recording folder is required.", nameof(folderPath));
            }

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("The selected recording folder does not exist.");
            }

            FolderPath = Path.GetFullPath(folderPath);
            var sources = new Dictionary<int, RecordingSource>();
            var duplicateIndexes = new HashSet<int>();
            string[] files;
            try
            {
                files = Directory.GetFiles(FolderPath, "*" + Dxn.DxnSchema.RecordingFileExtension, SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new IOException("The recording folder could not be enumerated.", exception);
            }

            foreach (var filePath in files)
            {
                int recordingIndex;
                if (!TryParseRecordingIndex(Path.GetFileName(filePath), out recordingIndex))
                {
                    continue;
                }

                if (duplicateIndexes.Contains(recordingIndex))
                {
                    continue;
                }

                RecordingSource source;
                try
                {
                    source = new RecordingSource(recordingIndex, filePath);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    continue;
                }

                RecordingSource existing;
                if (sources.TryGetValue(recordingIndex, out existing))
                {
                    sources.Remove(recordingIndex);
                    duplicateIndexes.Add(recordingIndex);
                    continue;
                }

                sources.Add(recordingIndex, source);
            }

            int ignoredIndex;
            _singleFileFallbackPath = files.Length == 1
                && !TryParseRecordingIndex(Path.GetFileName(files[0]), out ignoredIndex)
                ? files[0]
                : null;
            _sources = sources;
        }

        public string FolderPath { get; }

        public IReadOnlyDictionary<int, RecordingSource> Sources
        {
            get { return _sources; }
        }

        public bool TryResolve(int recordingIndex, out RecordingSource? source)
        {
            if (_sources.TryGetValue(recordingIndex, out source))
            {
                return true;
            }

            if (_singleFileFallbackPath != null)
            {
                try
                {
                    source = new RecordingSource(recordingIndex, _singleFileFallbackPath);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            source = null;
            return false;
        }

        public static string GetFileName(int recordingIndex)
        {
            if (recordingIndex < Dxn.DxnSchema.RecordingIndexBase)
            {
                throw new ArgumentOutOfRangeException(nameof(recordingIndex));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                Dxn.DxnSchema.RecordingFileNameFormat,
                recordingIndex);
        }

        private static bool TryParseRecordingIndex(string? fileName, out int recordingIndex)
        {
            recordingIndex = 0;
            if (fileName == null
                || string.IsNullOrWhiteSpace(fileName)
                || !fileName.EndsWith(Dxn.DxnSchema.RecordingFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var stem = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            if (stem.Length != Dxn.DxnSchema.RecordingIndexPaddingWidth
                || stem.Any(character => character < '0' || character > '9'))
            {
                return false;
            }

            return int.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out recordingIndex)
                && recordingIndex >= Dxn.DxnSchema.RecordingIndexBase;
        }
    }

    public sealed class RecordingSource
    {
        internal RecordingSource(int recordingIndex, string filePath)
        {
            RecordingIndex = recordingIndex;
            FullPath = Path.GetFullPath(filePath);
            Fingerprint = CaptureFingerprint(FullPath);
        }

        public int RecordingIndex { get; }

        public string FullPath { get; }

        public SourceFingerprint Fingerprint { get; }

        public FileStream OpenRead()
        {
            return new FileStream(
                FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
        }

        public bool IsUnchanged()
        {
            try
            {
                return Fingerprint.Equals(CaptureFingerprint(FullPath));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static SourceFingerprint CaptureFingerprint(string filePath)
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                throw new FileNotFoundException("The recording source does not exist.", filePath);
            }

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false))
            {
                return new SourceFingerprint(
                    filePath,
                    stream.Length,
                    info.LastWriteTimeUtc);
            }
        }
    }

    public sealed class SourceFingerprint : IEquatable<SourceFingerprint>
    {
        public SourceFingerprint(string fullPath, long length, DateTime lastWriteUtc)
        {
            FullPath = Path.GetFullPath(fullPath);
            Length = length;
            LastWriteUtc = lastWriteUtc.ToUniversalTime();
        }

        public string FullPath { get; }

        public long Length { get; }

        public DateTime LastWriteUtc { get; }

        public bool Equals(SourceFingerprint? other)
        {
            return other != null
                && string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase)
                && Length == other.Length
                && LastWriteUtc == other.LastWriteUtc;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as SourceFingerprint);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(FullPath);
                hash = (hash * 397) ^ Length.GetHashCode();
                return (hash * 397) ^ LastWriteUtc.GetHashCode();
            }
        }
    }
}
