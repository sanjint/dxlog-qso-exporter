using System;

namespace DxLogQsoExporter.Dxn
{
    public sealed class QsoRecord
    {
        public QsoRecord(
            long qsoId,
            DateTime timestampUtc,
            string? callsign,
            string? band,
            string? mode,
            bool isXqso,
            int? recordingFileIndex,
            long? recordingPosition,
            string? radio = null)
        {
            QsoId = qsoId;
            TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
                ? timestampUtc
                : timestampUtc.ToUniversalTime();
            Callsign = callsign;
            Band = band;
            Mode = mode;
            IsXqso = isXqso;
            RecordingFileIndex = recordingFileIndex;
            RecordingPosition = recordingPosition;
            Radio = string.IsNullOrWhiteSpace(radio) ? null : radio!.Trim();
        }

        public long QsoId { get; }

        public DateTime TimestampUtc { get; }

        public string? Callsign { get; }

        public string? Band { get; }

        public string? Mode { get; }

        public bool IsXqso { get; }

        public int? RecordingFileIndex { get; }

        public long? RecordingPosition { get; }

        public string? Radio { get; }

        public bool HasValidRecordingPosition
        {
            get
            {
                return RecordingFileIndex.HasValue
                    && RecordingFileIndex.Value >= DxnSchema.RecordingIndexBase
                    && RecordingPosition.HasValue
                    && RecordingPosition.Value >= 0;
            }
        }
    }
}
