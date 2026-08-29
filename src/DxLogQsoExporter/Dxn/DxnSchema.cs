namespace DxLogQsoExporter.Dxn
{
    public static class DxnSchema
    {
        // Current DXLog recordings use zero-based, three-digit MP3 file indexes.
        public const int RecordingIndexBase = 0;
        public const int RecordingIndexPaddingWidth = 3;
        public const string RecordingFileExtension = ".mp3";
        public const string RecordingFileNameFormat = "{0:000}.mp3";

        // A disabled or unsaved recording position is represented by -1.
        public const long RecordingDisabledSentinel = -1L;

        // The reader intentionally verifies the table and columns before querying rows.
        public const string QsoTable = "QSO";
        public const string QsoIdColumn = "QSOID";
        public const string TimestampColumn = "QSOTime";
        public const string CallsignColumn = "Call";
        public const string BandColumn = "Band";
        public const string ModeColumn = "Mode";
        public const string XqsoColumn = "XQSO";
        public const string RecordingFileColumn = "RecordingFile";
        public const string RecordingPositionColumn = "RecordingPosition";

        public const string LegacyQsoTable = "qsodata";
        public const string LegacyQsoIdColumn = "idqso";
        public const string LegacyTimestampColumn = "qsotime";
        public const string LegacyCallsignColumn = "callsign";
        public const string LegacyBandColumn = "band";
        public const string LegacyModeColumn = "mode";
        public const string LegacyXqsoColumn = "xqso";
        public const string LegacyRecordingFileColumn = "recfileindex";
        public const string LegacyRecordingPositionColumn = "recfileposition";
        public const string LegacyRadioColumn = "stn";
        public const string CurrentRadioColumn = "Radio";
        public const string CurrentStationColumn = "STN";

        public const string SchemaVersionTable = "Settings";
        public const string SchemaVersionColumn = "SchemaVersion";
        public const string LegacySchemaVersionTable = "dbinfo";
        public const string LegacySchemaVersionKeyColumn = "infokey";
        public const string LegacySchemaVersionValueColumn = "infovalue";
        public const string LegacySchemaVersionKey = "DBVersion";
        public const int MinimumSupportedSchemaVersion = 1;
        public const int HighestTestedSchemaVersion = 1;
        public const int HighestTestedLegacySchemaVersion = 7;
        public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        public const string LegacyTimestampFormat = "dd/MM/yyyy HH:mm:ss";
        public const string LegacyIsoTimestampFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF'Z'";
        public const string LegacyIsoTimestampWithoutFractionFormat = "yyyy-MM-dd HH:mm:ss'Z'";

        // DXLog stores timestamps as UTC wall-clock text without an offset.
        public const bool TimestampStoredAsUtc = true;
    }
}
