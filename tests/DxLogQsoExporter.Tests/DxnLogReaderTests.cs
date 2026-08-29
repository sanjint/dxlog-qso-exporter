using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using DxLogQsoExporter.Dxn;
using DxLogQsoExporter.Tests.Builders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class DxnLogReaderTests
    {
        [TestMethod]
        public void ReadsRowsInQsoOrderAndPreservesXqsoAndInt64Position()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("contest.dxn");
                SyntheticDxn.Create(path, new[]
                {
                    new SyntheticDxn.Row
                    {
                        QsoId = 20,
                        TimestampUtc = new DateTime(2026, 8, 28, 14, 24, 12, 345, DateTimeKind.Utc),
                        Callsign = "G4XYZ",
                        Band = "20M",
                        Mode = "CW",
                        IsXqso = true,
                        RecordingFileIndex = 2,
                        RecordingPosition = long.MaxValue - 10
                    },
                    new SyntheticDxn.Row
                    {
                        QsoId = 3,
                        TimestampUtc = new DateTime(2026, 8, 28, 14, 23, 5, 0, DateTimeKind.Utc),
                        Callsign = null,
                        Band = null,
                        Mode = null,
                        RecordingFileIndex = null,
                        RecordingPosition = -1
                    }
                });

                var result = new DxnLogReader().Read(path, CancellationToken.None);

                Assert.AreEqual(2, result.Qsos.Count);
                Assert.AreEqual(3L, result.Qsos[0].QsoId);
                Assert.AreEqual(20L, result.Qsos[1].QsoId);
                Assert.IsTrue(result.Qsos[1].IsXqso);
                Assert.AreEqual(long.MaxValue - 10, result.Qsos[1].RecordingPosition);
                Assert.AreEqual(DateTimeKind.Utc, result.Qsos[1].TimestampUtc.Kind);
                Assert.AreEqual("G4XYZ", result.Qsos[1].Callsign);
                Assert.IsNull(result.Qsos[0].Callsign);
                Assert.IsFalse(result.Qsos[0].HasValidRecordingPosition);
            }
        }

        [TestMethod]
        public void ReadsLegacyQsodataLogAndDatabaseVersion()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("legacy.dxn");
                CreateLegacyDatabase(path);

                var result = new DxnLogReader().Read(path, CancellationToken.None);

                Assert.AreEqual(7, result.SchemaVersion);
                Assert.AreEqual(1, result.Qsos.Count);
                var qso = result.Qsos[0];
                Assert.AreEqual(42L, qso.QsoId);
                Assert.AreEqual(new DateTime(2024, 11, 23, 0, 15, 2, 345, DateTimeKind.Utc).AddTicks(947), qso.TimestampUtc);
                Assert.AreEqual("WA3AER", qso.Callsign);
                Assert.AreEqual("40", qso.Band);
                Assert.AreEqual("CW", qso.Mode);
                Assert.IsTrue(qso.IsXqso);
                Assert.AreEqual(6, qso.RecordingFileIndex);
                Assert.AreEqual(38955L, qso.RecordingPosition);
                Assert.AreEqual("R1", qso.Radio);
            }
        }

        [TestMethod]
        public void WarnsForSupportedHigherSchemaVersion()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("newer.dxn");
                SyntheticDxn.Create(path, Array.Empty<SyntheticDxn.Row>(), schemaVersion: DxnSchema.HighestTestedSchemaVersion + 1);

                var result = new DxnLogReader().Read(path, CancellationToken.None);

                Assert.AreEqual(1, result.Warnings.Count);
                StringAssert.Contains(result.Warnings[0], "newer than the highest tested version");
            }
        }

        [TestMethod]
        public void RejectsSchemaBelowMinimum()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("old.dxn");
                SyntheticDxn.Create(path, Array.Empty<SyntheticDxn.Row>(), schemaVersion: 0);

                var exception = Assert.ThrowsExactly<DxnReadException>(
                    () => new DxnLogReader().Read(path, CancellationToken.None));

                Assert.AreEqual(DxnReadFailureKind.UnsupportedSchema, exception.Kind);
            }
        }

        [TestMethod]
        public void RejectsMissingQsoTable()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("missing-table.dxn");
                CreateDatabase(path, "CREATE TABLE Other (Value INTEGER);");

                var exception = Assert.ThrowsExactly<DxnReadException>(
                    () => new DxnLogReader().Read(path, CancellationToken.None));

                Assert.AreEqual(DxnReadFailureKind.InvalidSchema, exception.Kind);
            }
        }

        [TestMethod]
        public void RejectsMissingRequiredColumn()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("missing-column.dxn");
                CreateDatabase(path, "CREATE TABLE QSO (QSOID INTEGER, QSOTime TEXT, Call TEXT, Band TEXT, XQSO INTEGER, RecordingFile INTEGER, RecordingPosition INTEGER);");

                var exception = Assert.ThrowsExactly<DxnReadException>(
                    () => new DxnLogReader().Read(path, CancellationToken.None));

                Assert.AreEqual(DxnReadFailureKind.InvalidSchema, exception.Kind);
                StringAssert.Contains(exception.Message, DxnSchema.ModeColumn);
            }
        }

        [TestMethod]
        public void DoesNotModifyInputDatabase()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("unchanged.dxn");
                SyntheticDxn.Create(path, new[]
                {
                    new SyntheticDxn.Row
                    {
                        QsoId = 1,
                        TimestampUtc = DateTime.UtcNow,
                        Callsign = "N0CALL",
                        Band = "40M",
                        Mode = "SSB",
                        RecordingFileIndex = 0,
                        RecordingPosition = 100
                    }
                });
                var before = File.ReadAllBytes(path);

                new DxnLogReader().Read(path, CancellationToken.None);

                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            }
        }

        [TestMethod]
        public void RejectsTimestampOutsideConfirmedStorageFormat()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("bad-time.dxn");
                SyntheticDxn.Create(path, Array.Empty<SyntheticDxn.Row>());
                using (var connection = Open(path))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO QSO (QSOID, QSOTime, XQSO) VALUES (1, 'not-a-dxn-time', 0);";
                    command.ExecuteNonQuery();
                }

                var exception = Assert.ThrowsExactly<DxnReadException>(
                    () => new DxnLogReader().Read(path, CancellationToken.None));

                Assert.AreEqual(DxnReadFailureKind.InvalidData, exception.Kind);
            }
        }

        [TestMethod]
        public void ClassifiesDatabaseLockFailuresCorrectly()
        {
            var lockedException = new SQLiteException("The database is locked");
            var busyException = new SQLiteException("Database busy");
            var walException = new SQLiteException("WAL mode lock contention");
            var syntaxException = new SQLiteException("syntax error near WHERE");

            Assert.IsTrue(DxnLogReader.IsLockFailure(lockedException));
            Assert.IsTrue(DxnLogReader.IsLockFailure(busyException));
            Assert.IsTrue(DxnLogReader.IsLockFailure(walException));
            Assert.IsFalse(DxnLogReader.IsLockFailure(syntaxException));
        }

        [TestMethod]
        public void ThrowsLockedExceptionWhenDatabaseFileIsExclusivelyLocked()
        {
            using (var workspace = new TemporaryWorkspace())
            {
                var path = workspace.GetPath("locked.dxn");
                SyntheticDxn.Create(path, Array.Empty<SyntheticDxn.Row>());

                using (var exclusiveFile = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var exception = Assert.ThrowsExactly<DxnReadException>(
                        () => new DxnLogReader().Read(path, CancellationToken.None));

                    Assert.IsTrue(
                        exception.Kind == DxnReadFailureKind.Locked || exception.Kind == DxnReadFailureKind.General,
                        "Expected Locked or General read failure kind when file is locked by another process.");
                }
            }
        }

        private static void CreateDatabase(string path, string schemaSql)
        {
            SQLiteConnection.CreateFile(path);
            using (var connection = Open(path))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version = 1;" + schemaSql;
                command.ExecuteNonQuery();
            }
        }

        private static void CreateLegacyDatabase(string path)
        {
            SQLiteConnection.CreateFile(path);
            using (var connection = Open(path))
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "CREATE TABLE dbinfo (infokey TEXT, infovalue TEXT);"
                    + "INSERT INTO dbinfo (infokey, infovalue) VALUES ('DBVersion', '7');"
                    + "CREATE TABLE qsodata (idqso INTEGER PRIMARY KEY, qsotime DATETIME, callsign TEXT, band TEXT, mode TEXT, xqso BIT, recfileindex INTEGER, recfileposition INTEGER, stn TEXT);"
                    + "INSERT INTO qsodata (idqso, qsotime, callsign, band, mode, xqso, recfileindex, recfileposition, stn) VALUES (42, '2024-11-23 00:15:02.3450947Z', 'WA3AER', '40', 'CW', 1, 6, 38955, 'R1');";
                command.ExecuteNonQuery();
            }
        }

        private static SQLiteConnection Open(string path)
        {
            var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                Pooling = false
            }.ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
