using System;
using System.Globalization;
using DxLogQsoExporter.Recordings;

namespace DxLogQsoExporter.Export
{
    public sealed class ClipWindow
    {
        private ClipWindow(
            TimeSpan anchorTime,
            TimeSpan requestedStart,
            TimeSpan requestedEnd,
            TimeSpan actualStart,
            TimeSpan actualEnd,
            int startFrameIndex,
            int endFrameExclusive,
            long byteStart,
            long byteEnd,
            bool shortened)
        {
            AnchorTime = anchorTime;
            RequestedStart = requestedStart;
            RequestedEnd = requestedEnd;
            ActualStart = actualStart;
            ActualEnd = actualEnd;
            StartFrameIndex = startFrameIndex;
            EndFrameExclusive = endFrameExclusive;
            ByteStart = byteStart;
            ByteEnd = byteEnd;
            IsShortened = shortened;
        }

        public TimeSpan AnchorTime { get; }

        public TimeSpan RequestedStart { get; }

        public TimeSpan RequestedEnd { get; }

        public TimeSpan ActualStart { get; }

        public TimeSpan ActualEnd { get; }

        public int StartFrameIndex { get; }

        public int EndFrameExclusive { get; }

        public long ByteStart { get; }

        public long ByteEnd { get; }

        public long ByteLength
        {
            get { return ByteEnd - ByteStart; }
        }

        public bool IsShortened { get; }

        public static ClipWindow Create(
            Mp3FrameIndex index,
            long sourceLength,
            long recordingPosition,
            TimeSpan timeBeforeQso,
            TimeSpan clipDuration)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (sourceLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceLength));
            }

            if (recordingPosition < 0 || recordingPosition >= sourceLength)
            {
                throw new ClipWindowException(
                    ClipWindowFailureKind.OutOfRangePosition,
                    "The saved recording position is outside the source file.");
            }

            if (timeBeforeQso < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeBeforeQso));
            }

            if (clipDuration <= timeBeforeQso)
            {
                throw new ArgumentException("The total clip duration must be greater than the lead-in.", nameof(clipDuration));
            }

            var anchorFrame = index.FindFrameAtOrBefore(recordingPosition);
            if (anchorFrame == null)
            {
                throw new ClipWindowException(
                    ClipWindowFailureKind.OutOfRangePosition,
                    "The saved recording position is before the first readable MP3 frame.");
            }

            var requestedStart = anchorFrame.StartTime - timeBeforeQso;
            var requestedEnd = requestedStart + clipDuration;
            var actualStart = requestedStart < TimeSpan.Zero ? TimeSpan.Zero : requestedStart;
            var actualEnd = requestedEnd > index.Duration ? index.Duration : requestedEnd;
            var shortened = actualStart != requestedStart || actualEnd != requestedEnd;

            if (actualEnd <= actualStart)
            {
                throw new ClipWindowException(
                    ClipWindowFailureKind.NoFramesInWindow,
                    "The requested clip window contains no complete MP3 frames.");
            }

            var startFrameIndex = index.FindLastFrameAtOrBefore(actualStart);
            var endFrameExclusive = index.FindFirstFrameAtOrAfter(actualEnd);
            if (startFrameIndex < 0 || endFrameExclusive <= startFrameIndex)
            {
                throw new ClipWindowException(
                    ClipWindowFailureKind.NoFramesInWindow,
                    "The requested clip window contains no complete MP3 frames.");
            }

            var startFrame = index.Frames[startFrameIndex];
            var endFrame = index.Frames[endFrameExclusive - 1];
            return new ClipWindow(
                anchorFrame.StartTime,
                requestedStart,
                requestedEnd,
                startFrame.StartTime,
                endFrame.EndTime,
                startFrameIndex,
                endFrameExclusive,
                startFrame.FileOffset,
                endFrame.FileOffset + endFrame.FrameLength,
                shortened);
        }
    }

    public enum ClipWindowFailureKind
    {
        OutOfRangePosition,
        NoFramesInWindow
    }

    public sealed class ClipWindowException : Exception
    {
        public ClipWindowException(ClipWindowFailureKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public ClipWindowFailureKind Kind { get; }
    }
}
