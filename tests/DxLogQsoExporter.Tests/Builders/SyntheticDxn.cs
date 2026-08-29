using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using DxLogQsoExporter.Dxn;

namespace DxLogQsoExporter.Tests.Builders
{
    internal static class SyntheticDxn
    {
        public static void Create(string path, IEnumerable<Row> rows, int schemaVersion = 1)
        {
            SQLiteConnection.CreateFile(path);
            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                Pooling = false
            }.ConnectionString;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                Execute(connection, "PRAGMA user_version = " + schemaVersion.ToString(CultureInfo.InvariantCulture) + ";");
                Execute(connection, "CREATE TABLE QSO (QSOID INTEGER NOT NULL, QSOTime TEXT NOT NULL, Call TEXT, Band TEXT, Mode TEXT, XQSO INTEGER, RecordingFile INTEGER, RecordingPosition INTEGER);");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO QSO (QSOID, QSOTime, Call, Band, Mode, XQSO, RecordingFile, RecordingPosition) VALUES (@id, @time, @call, @band, @mode, @xqso, @file, @position);";
                    var id = command.Parameters.Add("@id", System.Data.DbType.Int64);
                    var time = command.Parameters.Add("@time", System.Data.DbType.String);
                    var call = command.Parameters.Add("@call", System.Data.DbType.String);
                    var band = command.Parameters.Add("@band", System.Data.DbType.String);
                    var mode = command.Parameters.Add("@mode", System.Data.DbType.String);
                    var xqso = command.Parameters.Add("@xqso", System.Data.DbType.Int32);
                    var file = command.Parameters.Add("@file", System.Data.DbType.Int32);
                    var position = command.Parameters.Add("@position", System.Data.DbType.Int64);
                    foreach (var row in rows)
                    {
                        id.Value = row.QsoId;
                        time.Value = row.TimestampUtc.ToUniversalTime().ToString(DxnSchema.TimestampFormat, CultureInfo.InvariantCulture);
                        call.Value = (object?)row.Callsign ?? DBNull.Value;
                        band.Value = (object?)row.Band ?? DBNull.Value;
                        mode.Value = (object?)row.Mode ?? DBNull.Value;
                        xqso.Value = row.IsXqso ? 1 : 0;
                        file.Value = (object?)row.RecordingFileIndex ?? DBNull.Value;
                        position.Value = (object?)row.RecordingPosition ?? DBNull.Value;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void CreateLegacy(string path, IEnumerable<Row> rows, int schemaVersion = 7)
        {
            SQLiteConnection.CreateFile(path);
            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                Pooling = false
            }.ConnectionString;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                Execute(connection, "CREATE TABLE dbinfo (infokey TEXT, infovalue TEXT);");
                Execute(
                    connection,
                    "INSERT INTO dbinfo (infokey, infovalue) VALUES ('DBVersion', "
                    + schemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ");");
                Execute(
                    connection,
                    "CREATE TABLE qsodata (idqso INTEGER PRIMARY KEY, qsotime DATETIME, callsign TEXT, band TEXT, mode TEXT, xqso BIT, recfileindex INTEGER, recfileposition INTEGER, stn TEXT);");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO qsodata (idqso, qsotime, callsign, band, mode, xqso, recfileindex, recfileposition, stn) VALUES (@id, @time, @call, @band, @mode, @xqso, @file, @position, @radio);";
                    var id = command.Parameters.Add("@id", System.Data.DbType.Int64);
                    var time = command.Parameters.Add("@time", System.Data.DbType.String);
                    var call = command.Parameters.Add("@call", System.Data.DbType.String);
                    var band = command.Parameters.Add("@band", System.Data.DbType.String);
                    var mode = command.Parameters.Add("@mode", System.Data.DbType.String);
                    var xqso = command.Parameters.Add("@xqso", System.Data.DbType.Int32);
                    var file = command.Parameters.Add("@file", System.Data.DbType.Int32);
                    var position = command.Parameters.Add("@position", System.Data.DbType.Int64);
                    var radio = command.Parameters.Add("@radio", System.Data.DbType.String);
                    foreach (var row in rows)
                    {
                        id.Value = row.QsoId;
                        time.Value = row.TimestampUtc.ToUniversalTime().ToString(DxnSchema.LegacyTimestampFormat, CultureInfo.InvariantCulture);
                        call.Value = (object?)row.Callsign ?? DBNull.Value;
                        band.Value = (object?)row.Band ?? DBNull.Value;
                        mode.Value = (object?)row.Mode ?? DBNull.Value;
                        xqso.Value = row.IsXqso ? 1 : 0;
                        file.Value = (object?)row.RecordingFileIndex ?? DBNull.Value;
                        position.Value = (object?)row.RecordingPosition ?? DBNull.Value;
                        radio.Value = (object?)row.Radio ?? DBNull.Value;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        internal sealed class Row
        {
            public long QsoId { get; set; }

            public DateTime TimestampUtc { get; set; }

            public string? Callsign { get; set; }

            public string? Band { get; set; }

            public string? Mode { get; set; }

            public bool IsXqso { get; set; }

            public int? RecordingFileIndex { get; set; }

            public long? RecordingPosition { get; set; }

            public string? Radio { get; set; }
        }
    }

    internal sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DxLogQsoExporterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string GetPath(string fileName)
        {
            return Path.Combine(Root, fileName);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
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
}
