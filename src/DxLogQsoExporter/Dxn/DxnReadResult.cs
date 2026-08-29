using System;
using System.Collections.Generic;

namespace DxLogQsoExporter.Dxn
{
    public sealed class DxnReadResult
    {
        public DxnReadResult(int schemaVersion, IReadOnlyList<QsoRecord> qsos, IReadOnlyList<string> warnings)
        {
            SchemaVersion = schemaVersion;
            Qsos = qsos ?? throw new ArgumentNullException(nameof(qsos));
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        }

        public int SchemaVersion { get; }

        public IReadOnlyList<QsoRecord> Qsos { get; }

        public IReadOnlyList<string> Warnings { get; }
    }

    public enum DxnReadFailureKind
    {
        General,
        Locked,
        IntegrityCheckFailed,
        UnsupportedSchema,
        InvalidSchema,
        InvalidData
    }

    public sealed class DxnReadException : Exception
    {
        public DxnReadException(DxnReadFailureKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public DxnReadException(DxnReadFailureKind kind, string message, Exception innerException)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public DxnReadFailureKind Kind { get; }
    }
}
