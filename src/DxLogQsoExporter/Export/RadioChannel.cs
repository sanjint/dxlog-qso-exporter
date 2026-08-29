using System;

namespace DxLogQsoExporter.Export
{
    public enum RadioChannel
    {
        Left,
        Right
    }

    public static class RadioChannelMapping
    {
        public static bool TryResolve(string? radio, out RadioChannel channel)
        {
            if (string.Equals(radio?.Trim(), "R1", StringComparison.OrdinalIgnoreCase))
            {
                channel = RadioChannel.Left;
                return true;
            }

            if (string.Equals(radio?.Trim(), "R2", StringComparison.OrdinalIgnoreCase))
            {
                channel = RadioChannel.Right;
                return true;
            }

            channel = RadioChannel.Left;
            return false;
        }

        public static string GetFolderName(RadioChannel channel)
        {
            return channel == RadioChannel.Left ? "R1" : "R2";
        }
    }
}
