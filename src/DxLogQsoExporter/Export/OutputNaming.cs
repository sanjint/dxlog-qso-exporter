using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DxLogQsoExporter.Dxn;

namespace DxLogQsoExporter.Export
{
    public static class OutputNaming
    {
        public const int MaximumFileNameLength = 150;

        public static string CreateFileName(QsoRecord qso)
        {
            if (qso == null)
            {
                throw new ArgumentNullException(nameof(qso));
            }

            var identifier = qso.QsoId.ToString("D6", CultureInfo.InvariantCulture);
            var timestamp = qso.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
            var callsign = SanitizeVariable(qso.Callsign, "UNKNOWN");
            var band = SanitizeVariable(qso.Band, "UNKNOWN");
            var mode = SanitizeVariable(qso.Mode, "UNKNOWN");
            var suffix = qso.IsXqso ? "_XQSO" : string.Empty;
            var invariant = identifier + "_" + timestamp + "_";
            var extension = suffix + ".mp3";
            var available = MaximumFileNameLength - invariant.Length - extension.Length - 3;
            if (available > 0)
            {
                var totalVariableLength = callsign.Length + band.Length + mode.Length;
                while (totalVariableLength > available)
                {
                    if (callsign.Length >= band.Length && callsign.Length >= mode.Length && callsign.Length > 1)
                    {
                        callsign = callsign.Substring(0, callsign.Length - 1);
                    }
                    else if (band.Length >= mode.Length && band.Length > 1)
                    {
                        band = band.Substring(0, band.Length - 1);
                    }
                    else if (mode.Length > 1)
                    {
                        mode = mode.Substring(0, mode.Length - 1);
                    }
                    else
                    {
                        break;
                    }

                    totalVariableLength = callsign.Length + band.Length + mode.Length;
                }
            }

            return string.Concat(invariant, callsign, "_", band, "_", mode, suffix, ".mp3");
        }

        private static string SanitizeVariable(string? value, string fallback)
        {
            var source = value == null || string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source.Length);
            foreach (var character in source)
            {
                builder.Append(character < 32 || invalid.Contains(character) ? '_' : character);
            }

            var result = builder.ToString().TrimEnd(' ', '.');
            return result.Length == 0 ? fallback : result;
        }
    }
}
