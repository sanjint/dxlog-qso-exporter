using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DxLogQsoExporter.Dxn
{
    public sealed class DxnLogReader
    {
        private readonly DateTimeStyles _timestampStyles = DateTimeStyles.None;

        public DxnReadResult Read(string logPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(logPath))
            {
                throw new ArgumentException("A DXN log path is required.", nameof(logPath));
            }

            if (!File.Exists(logPath))
            {
                throw new DxnReadException(
                    DxnReadFailureKind.General,
                    "The selected DXN log does not exist.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(logPath),
                Version = 3,
                ReadOnly = true,
                FailIfMissing = true,
                Pooling = false
            }.ConnectionString;

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var pragma = connection.CreateCommand())
                    {
                        pragma.CommandText = "PRAGMA query_only = ON;";
                        pragma.ExecuteNonQuery();
                    }

                    VerifyIntegrity(connection);
                    var layout = DetectLayout(connection);
                    var schemaVersion = ReadSchemaVersion(connection, layout);
                    var warnings = new List<string>();
                    if (schemaVersion > GetHighestTestedSchemaVersion(layout))
                    {
                        warnings.Add(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "The DXN schema version ({0}) is newer than the highest tested version ({1}). Required columns were verified, but review the results carefully.",
                                schemaVersion,
                                GetHighestTestedSchemaVersion(layout)));
                    }

                    VerifyQsoSchema(connection, layout);
                    var qsos = ReadQsos(connection, layout, cancellationToken);
                    return new DxnReadResult(schemaVersion, qsos, warnings);
                }
            }
            catch (DxnReadException)
            {
                throw;
            }
            catch (SQLiteException exception) when (IsLockFailure(exception))
            {
                throw new DxnReadException(
                    DxnReadFailureKind.Locked,
                    "DXLog appears to be using the selected log. Close DXLog and try again.",
                    exception);
            }
            catch (SQLiteException exception)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.General,
                    "The selected DXN log could not be opened as a readable SQLite database.",
                    exception);
            }
            catch (IOException exception)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.General,
                    "The selected DXN log could not be read.",
                    exception);
            }
        }

        private static void VerifyIntegrity(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA quick_check;";
                var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DxnReadException(
                        DxnReadFailureKind.IntegrityCheckFailed,
                        "The DXN log failed SQLite's integrity check and cannot be exported safely.");
                }
            }
        }

        private static DxnLayout DetectLayout(SQLiteConnection connection)
        {
            if (TableExists(connection, DxnSchema.QsoTable))
            {
                return DxnLayout.Current;
            }

            if (TableExists(connection, DxnSchema.LegacyQsoTable))
            {
                return DxnLayout.Legacy;
            }

            throw new DxnReadException(
                DxnReadFailureKind.InvalidSchema,
                "The DXN log does not contain the required QSO table.");
        }

        private static int GetHighestTestedSchemaVersion(DxnLayout layout)
        {
            return layout == DxnLayout.Legacy
                ? DxnSchema.HighestTestedLegacySchemaVersion
                : DxnSchema.HighestTestedSchemaVersion;
        }

        private static int ReadSchemaVersion(SQLiteConnection connection, DxnLayout layout)
        {
            long version;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version;";
                version = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            if (version == 0 && layout == DxnLayout.Current && TableExists(connection, DxnSchema.SchemaVersionTable))
            {
                var settingsColumns = ReadTableColumns(connection, DxnSchema.SchemaVersionTable);
                if (settingsColumns.Contains(DxnSchema.SchemaVersionColumn, StringComparer.OrdinalIgnoreCase))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = string.Format(
                            CultureInfo.InvariantCulture,
                            "SELECT {0} FROM {1} LIMIT 1;",
                            QuoteIdentifier(DxnSchema.SchemaVersionColumn),
                            QuoteIdentifier(DxnSchema.SchemaVersionTable));
                        var value = command.ExecuteScalar();
                        if (value != null && value != DBNull.Value)
                        {
                            version = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        }
                    }
                }
            }

            if (version == 0 && layout == DxnLayout.Legacy && TableExists(connection, DxnSchema.LegacySchemaVersionTable))
            {
                var dbInfoColumns = ReadTableColumns(connection, DxnSchema.LegacySchemaVersionTable);
                if (dbInfoColumns.Contains(DxnSchema.LegacySchemaVersionKeyColumn, StringComparer.OrdinalIgnoreCase)
                    && dbInfoColumns.Contains(DxnSchema.LegacySchemaVersionValueColumn, StringComparer.OrdinalIgnoreCase))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = string.Format(
                            CultureInfo.InvariantCulture,
                            "SELECT {0} FROM {1} WHERE lower({2}) = lower(@key) LIMIT 1;",
                            QuoteIdentifier(DxnSchema.LegacySchemaVersionValueColumn),
                            QuoteIdentifier(DxnSchema.LegacySchemaVersionTable),
                            QuoteIdentifier(DxnSchema.LegacySchemaVersionKeyColumn));
                        command.Parameters.AddWithValue("@key", DxnSchema.LegacySchemaVersionKey);
                        var value = command.ExecuteScalar();
                        if (value != null && value != DBNull.Value)
                        {
                            version = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        }
                    }
                }
            }

            if (version < DxnSchema.MinimumSupportedSchemaVersion)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.UnsupportedSchema,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The DXN schema version ({0}) is older than the minimum supported version ({1}).",
                        version,
                        DxnSchema.MinimumSupportedSchemaVersion));
            }

            if (version > int.MaxValue)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.UnsupportedSchema,
                    "The DXN schema version is outside the supported range.");
            }

            return (int)version;
        }

        private static void VerifyQsoSchema(SQLiteConnection connection, DxnLayout layout)
        {
            var tableName = layout == DxnLayout.Legacy ? DxnSchema.LegacyQsoTable : DxnSchema.QsoTable;
            var columns = ReadTableColumns(connection, tableName);
            var requiredColumns = layout == DxnLayout.Legacy
                ? new[]
                {
                    DxnSchema.LegacyQsoIdColumn,
                    DxnSchema.LegacyTimestampColumn,
                    DxnSchema.LegacyCallsignColumn,
                    DxnSchema.LegacyBandColumn,
                    DxnSchema.LegacyModeColumn,
                    DxnSchema.LegacyXqsoColumn,
                    DxnSchema.LegacyRecordingFileColumn,
                    DxnSchema.LegacyRecordingPositionColumn
                }
                : new[]
                {
                    DxnSchema.QsoIdColumn,
                    DxnSchema.TimestampColumn,
                    DxnSchema.CallsignColumn,
                    DxnSchema.BandColumn,
                    DxnSchema.ModeColumn,
                    DxnSchema.XqsoColumn,
                    DxnSchema.RecordingFileColumn,
                    DxnSchema.RecordingPositionColumn
                };
            var missing = requiredColumns
                .Where(required => !columns.Contains(required, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.InvalidSchema,
                    "The DXN log is missing required QSO columns: " + string.Join(", ", missing) + ".");
            }
        }

        private List<QsoRecord> ReadQsos(SQLiteConnection connection, DxnLayout layout, CancellationToken cancellationToken)
        {
            var fields = layout == DxnLayout.Legacy
                ? new[]
                {
                    DxnSchema.LegacyQsoIdColumn,
                    DxnSchema.LegacyTimestampColumn,
                    DxnSchema.LegacyCallsignColumn,
                    DxnSchema.LegacyBandColumn,
                    DxnSchema.LegacyModeColumn,
                    DxnSchema.LegacyXqsoColumn,
                    DxnSchema.LegacyRecordingFileColumn,
                    DxnSchema.LegacyRecordingPositionColumn
                }
                : new[]
                {
                    DxnSchema.QsoIdColumn,
                    DxnSchema.TimestampColumn,
                    DxnSchema.CallsignColumn,
                    DxnSchema.BandColumn,
                    DxnSchema.ModeColumn,
                    DxnSchema.XqsoColumn,
                    DxnSchema.RecordingFileColumn,
                    DxnSchema.RecordingPositionColumn
                };
            var radioColumn = FindRadioColumn(connection, layout);
            var selectedFields = fields.Select((field, index) =>
                layout == DxnLayout.Legacy && index == 1
                    ? "CAST(" + QuoteIdentifier(field) + " AS TEXT)"
                    : QuoteIdentifier(field)).ToList();
            selectedFields.Add(radioColumn == null ? "NULL" : QuoteIdentifier(radioColumn));
            var quotedFields = string.Join(", ", selectedFields);
            var tableName = layout == DxnLayout.Legacy ? DxnSchema.LegacyQsoTable : DxnSchema.QsoTable;
            var sql = string.Format(
                CultureInfo.InvariantCulture,
                "SELECT {0} FROM {1} ORDER BY {2};",
                quotedFields,
                QuoteIdentifier(tableName),
                QuoteIdentifier(fields[0]));
            var timestampFormats = layout == DxnLayout.Legacy
                ? new[]
                {
                    DxnSchema.LegacyTimestampFormat,
                    DxnSchema.LegacyTimestampFormat + ".fff",
                    DxnSchema.LegacyIsoTimestampFormat,
                    DxnSchema.LegacyIsoTimestampWithoutFractionFormat
                }
                : new[] { DxnSchema.TimestampFormat };

            var qsos = new List<QsoRecord>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var qsoId = ReadInt64(reader.GetValue(0), "QSO identifier");
                        var timestampUtc = ReadTimestamp(reader.GetValue(1), qsoId, timestampFormats);
                        var callsign = ReadDisplayValue(reader.GetValue(2));
                        var band = ReadDisplayValue(reader.GetValue(3));
                        var mode = ReadDisplayValue(reader.GetValue(4));
                        var isXqso = ReadBoolean(reader.GetValue(5));
                        var recordingFileIndex = ReadNullableInt32(reader.GetValue(6));
                        var recordingPosition = ReadNullableInt64(reader.GetValue(7));
                        var radio = ReadDisplayValue(reader.GetValue(8));
                        qsos.Add(new QsoRecord(
                            qsoId,
                            timestampUtc,
                            callsign,
                            band,
                            mode,
                            isXqso,
                            recordingFileIndex,
                            recordingPosition,
                            radio));
                    }
                }
            }

            return qsos;
        }

        private static string? FindRadioColumn(SQLiteConnection connection, DxnLayout layout)
        {
            var tableName = layout == DxnLayout.Legacy ? DxnSchema.LegacyQsoTable : DxnSchema.QsoTable;
            var columns = ReadTableColumns(connection, tableName);
            var candidates = layout == DxnLayout.Legacy
                ? new[] { DxnSchema.LegacyRadioColumn }
                : new[] { DxnSchema.CurrentRadioColumn, DxnSchema.CurrentStationColumn };
            return candidates.FirstOrDefault(candidate => columns.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        }

        private static bool TableExists(SQLiteConnection connection, string tableName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND lower(name) = lower(@name) LIMIT 1;";
                command.Parameters.AddWithValue("@name", tableName);
                return command.ExecuteScalar() != null;
            }
        }

        private static HashSet<string> ReadTableColumns(SQLiteConnection connection, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + QuoteIdentifier(tableName) + ");";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty);
                    }
                }
            }

            return columns;
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        private static string? ReadDisplayValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private DateTime ReadTimestamp(object value, long qsoId, IEnumerable<string> formats)
        {
            if (value == null || value == DBNull.Value)
            {
                throw InvalidData(qsoId, "the timestamp is missing");
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw InvalidData(qsoId, "the timestamp is empty");
            }

            foreach (var format in formats)
            {
                DateTime parsed;
                if (DateTime.TryParseExact(
                        text,
                        format,
                        CultureInfo.InvariantCulture,
                        _timestampStyles,
                        out parsed))
                {
                    return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }
            }

            throw InvalidData(qsoId, "the timestamp does not use the supported DXLog format");
        }

        private static bool ReadBoolean(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return false;
            }

            if (value is bool boolean)
            {
                return boolean;
            }

            if (value is byte || value is short || value is int || value is long)
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "xqso", StringComparison.OrdinalIgnoreCase)
                || text == "1";
        }

        private static int? ReadNullableInt32(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            try
            {
                var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return number < int.MinValue || number > int.MaxValue ? (int?)null : (int)number;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private static long? ReadNullableInt64(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private static long ReadInt64(object value, string fieldName)
        {
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException || exception is InvalidCastException || exception is OverflowException)
            {
                throw new DxnReadException(
                    DxnReadFailureKind.InvalidData,
                    "A required " + fieldName + " is not a signed 64-bit integer.",
                    exception);
            }
        }

        private static DxnReadException InvalidData(long qsoId, string detail)
        {
            return new DxnReadException(
                DxnReadFailureKind.InvalidData,
                string.Format(CultureInfo.InvariantCulture, "QSO {0} has invalid data: {1}.", qsoId, detail));
        }

        internal static bool IsLockFailure(SQLiteException exception)
        {
            var message = exception.Message ?? string.Empty;
            return message.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("wal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private enum DxnLayout
        {
            Current,
            Legacy
        }
    }
}
