using System;
using DxLogQsoExporter.Dxn;
using DxLogQsoExporter.Export;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DxLogQsoExporter.Tests
{
    [TestClass]
    public class OutputNamingTests
    {
        [TestMethod]
        public void CreatesDocumentedNormalQsoName()
        {
            var qso = new QsoRecord(
                123,
                new DateTime(2026, 8, 28, 14, 23, 5, DateTimeKind.Utc),
                "G3ABC",
                "20M",
                "CW",
                false,
                0,
                100);

            Assert.AreEqual(
                "000123_2026-08-28_1423_G3ABC_20M_CW.mp3",
                OutputNaming.CreateFileName(qso));
        }

        [TestMethod]
        public void MarksXqsoAndSanitizesVariableFields()
        {
            var qso = new QsoRecord(
                124,
                new DateTime(2026, 8, 28, 14, 24, 12, DateTimeKind.Utc),
                "G4:XYZ",
                "20/M",
                "C*W",
                true,
                0,
                100);

            var name = OutputNaming.CreateFileName(qso);

            Assert.AreEqual("000124_2026-08-28_1424_G4_XYZ_20_M_C_W_XQSO.mp3", name);
        }

        [TestMethod]
        public void CapsLongVariableFieldsWithoutTruncatingIdentifier()
        {
            var qso = new QsoRecord(
                987654321,
                DateTime.UtcNow,
                new string('C', 300),
                new string('B', 100),
                new string('M', 100),
                false,
                0,
                100);

            var name = OutputNaming.CreateFileName(qso);

            Assert.IsTrue(name.Length <= OutputNaming.MaximumFileNameLength);
            StringAssert.StartsWith(name, "987654321_");
        }
    }
}
