using System;
using System.IO;

namespace DxLogQsoExporter.Export
{
    public sealed class ExportOptions
    {
        public ExportOptions(
            string logPath,
            string recordingFolder,
            string outputFolder,
            TimeSpan timeBeforeQso,
            TimeSpan clipDuration,
            bool extractRadioChannels = false)
        {
            LogPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
            RecordingFolder = recordingFolder ?? throw new ArgumentNullException(nameof(recordingFolder));
            OutputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
            TimeBeforeQso = timeBeforeQso;
            ClipDuration = clipDuration;
            ExtractRadioChannels = extractRadioChannels;
        }

        public string LogPath { get; }

        public string RecordingFolder { get; }

        public string OutputFolder { get; }

        public TimeSpan TimeBeforeQso { get; }

        public TimeSpan ClipDuration { get; }

        public bool ExtractRadioChannels { get; }

        public static ExportOptions CreateDefaults(string logPath, string recordingFolder, string outputFolder)
        {
            return new ExportOptions(
                logPath,
                recordingFolder,
                outputFolder,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60));
        }

        public void ValidateTiming()
        {
            if (TimeBeforeQso < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeBeforeQso), "The lead-in cannot be negative.");
            }

            if (ClipDuration <= TimeBeforeQso)
            {
                throw new ArgumentException("The total clip duration must be greater than the lead-in.", nameof(ClipDuration));
            }
        }

        public void ValidatePaths()
        {
            if (!File.Exists(LogPath))
            {
                throw new FileNotFoundException("The selected DXN log does not exist.", LogPath);
            }

            if (!Directory.Exists(RecordingFolder))
            {
                throw new DirectoryNotFoundException("The selected recording folder does not exist.");
            }

            if (string.IsNullOrWhiteSpace(OutputFolder))
            {
                throw new ArgumentException("An output folder is required.", nameof(OutputFolder));
            }
        }
    }
}
