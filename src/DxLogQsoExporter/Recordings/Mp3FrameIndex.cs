using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace DxLogQsoExporter.Recordings
{
    public sealed class Mp3FrameIndex
    {
        private const long ProgressIntervalBytes = 4L * 1024 * 1024;
        private const int ReaderBufferSize = 1024 * 1024;
        private readonly ReadOnlyCollection<Mp3FrameInfo> _frames;

        private Mp3FrameIndex(long dataStartOffset, IList<Mp3FrameInfo> frames)
        {
            DataStartOffset = dataStartOffset;
            _frames = new ReadOnlyCollection<Mp3FrameInfo>(frames);
        }

        public long DataStartOffset { get; }

        public IReadOnlyList<Mp3FrameInfo> Frames
        {
            get { return _frames; }
        }

        public TimeSpan Duration
        {
            get { return _frames.Count == 0 ? TimeSpan.Zero : _frames[_frames.Count - 1].EndTime; }
        }

        public static Mp3FrameIndex Build(
            Stream stream,
            CancellationToken cancellationToken,
            IProgress<Mp3IndexProgress>? progress = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("The MP3 stream must be readable and seekable.", nameof(stream));
            }

            var streamLength = stream.Length;
            stream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();

            Id3v2Tag? tag;
            try
            {
                tag = Id3v2Tag.ReadTag(stream);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is EndOfStreamException)
            {
                throw new Mp3IndexException(
                    Mp3IndexFailureKind.InvalidMetadata,
                    "The MP3 metadata tag could not be read.",
                    exception);
            }

            var dataStartOffset = tag == null ? 0L : stream.Position;
            if (dataStartOffset < 0 || dataStartOffset > streamLength)
            {
                throw new Mp3IndexException(
                    Mp3IndexFailureKind.InvalidMetadata,
                    "The MP3 metadata tag extends beyond the end of the recording.");
            }

            if (tag == null)
            {
                stream.Position = 0;
            }

            var frames = new List<Mp3FrameInfo>();
            var nextProgressOffset = dataStartOffset + ProgressIntervalBytes;
            var cumulativeSamples = 0L;
            int sampleRate = 0;
            int mpegVersion = -1;
            int mpegLayer = -1;
            var hasLegacyMetadata = false;
            ReportProgress(progress, dataStartOffset, streamLength);

            using (var reader = new Mp3StreamReader(stream, dataStartOffset, streamLength, ReaderBufferSize))
            {
                while (reader.Position < streamLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frameOffset = reader.Position;
                    var header = reader.ReadHeader();
                    if (header.BytesRead < 4)
                    {
                        if (frames.Count == 0)
                        {
                            throw new Mp3IndexException(
                                Mp3IndexFailureKind.NoReadableFrames,
                                "The recording contains no readable MP3 frames.");
                        }

                        var remaining = streamLength - frameOffset;
                        if (remaining < 4096 && hasLegacyMetadata)
                        {
                            break;
                        }

                        throw new Mp3IndexException(
                            remaining < 4096
                                ? Mp3IndexFailureKind.TruncatedFinalFrame
                                : Mp3IndexFailureKind.InvalidRegion,
                            remaining < 4096
                                ? "The recording ends with an incomplete MP3 frame."
                                : "The MP3 recording contains an invalid region after the final frame.");
                    }

                    if (!header.IsValid)
                    {
                        Mp3FrameHeader nextHeader;
                        if (reader.TryReadLegacyMetadata(header, out nextHeader))
                        {
                            hasLegacyMetadata = true;
                            frameOffset += 128;
                            header = nextHeader;
                        }
                        else if (frames.Count == 0)
                        {
                            throw new Mp3IndexException(
                                Mp3IndexFailureKind.NoReadableFrames,
                                "The recording contains no readable MP3 frames.");
                        }
                        else
                        {
                            throw new Mp3IndexException(
                                Mp3IndexFailureKind.InvalidRegion,
                                "The MP3 recording contains an invalid region between frames.");
                        }
                    }

                    var frameLength = header.FrameLength;
                    if (frameLength <= 0 || frameOffset > streamLength - frameLength)
                    {
                        var remaining = streamLength - frameOffset;
                        if (frames.Count > 0 && remaining < 4096 && hasLegacyMetadata)
                        {
                            break;
                        }

                        throw new Mp3IndexException(
                            Mp3IndexFailureKind.TruncatedFinalFrame,
                            "The recording contains a truncated MP3 frame.");
                    }

                    if (sampleRate == 0)
                    {
                        sampleRate = header.SampleRate;
                        mpegVersion = header.MpegVersion;
                        mpegLayer = header.MpegLayer;
                    }
                    else if (header.SampleRate != sampleRate
                        || header.MpegVersion != mpegVersion
                        || header.MpegLayer != mpegLayer)
                    {
                        throw new Mp3IndexException(
                            Mp3IndexFailureKind.IncompatibleFormat,
                            "The recording changes MP3 sample rate or MPEG format between frames.");
                    }

                    var startTime = TimeSpan.FromSeconds((double)cumulativeSamples / sampleRate);
                    cumulativeSamples += header.SampleCount;
                    var endTime = TimeSpan.FromSeconds((double)cumulativeSamples / sampleRate);
                    frames.Add(new Mp3FrameInfo(
                        frameOffset,
                        frameLength,
                        header.SampleCount,
                        header.SampleRate,
                        startTime,
                        endTime));
                    reader.Skip(frameLength - 4);
                    if (reader.Position >= nextProgressOffset)
                    {
                        ReportProgress(progress, reader.Position, streamLength);
                        nextProgressOffset = reader.Position + ProgressIntervalBytes;
                    }
                }

                stream.Position = reader.Position;
            }

            if (frames.Count == 0)
            {
                throw new Mp3IndexException(
                    Mp3IndexFailureKind.NoReadableFrames,
                    "The recording contains no readable MP3 frames.");
            }

            ReportProgress(progress, streamLength, streamLength);
            return new Mp3FrameIndex(dataStartOffset, frames);
        }

        private static void ReportProgress(IProgress<Mp3IndexProgress>? progress, long bytesProcessed, long totalBytes)
        {
            progress?.Report(new Mp3IndexProgress(bytesProcessed, totalBytes));
        }

        private sealed class Mp3StreamReader : IDisposable
        {
            private readonly Stream _stream;
            private readonly long _length;
            private readonly byte[] _buffer;
            private int _bufferOffset;
            private int _bufferCount;
            private long _position;

            public Mp3StreamReader(Stream stream, long start, long length, int bufferSize)
            {
                _stream = stream ?? throw new ArgumentNullException(nameof(stream));
                _length = length;
                _buffer = new byte[bufferSize];
                _position = start;
                _stream.Position = start;
            }

            public long Position
            {
                get { return _position; }
            }

            public Mp3FrameHeader ReadHeader()
            {
                var first = ReadByte();
                if (first < 0)
                {
                    return Mp3FrameHeader.Empty;
                }

                var second = ReadByte();
                if (second < 0)
                {
                    return new Mp3FrameHeader(1, (byte)first, 0, 0, 0);
                }

                var third = ReadByte();
                if (third < 0)
                {
                    return new Mp3FrameHeader(2, (byte)first, (byte)second, 0, 0);
                }

                var fourth = ReadByte();
                if (fourth < 0)
                {
                    return new Mp3FrameHeader(3, (byte)first, (byte)second, (byte)third, 0);
                }

                return Mp3FrameHeader.Parse((byte)first, (byte)second, (byte)third, (byte)fourth);
            }

            public bool TryReadLegacyMetadata(Mp3FrameHeader firstHeader, out Mp3FrameHeader nextHeader)
            {
                nextHeader = Mp3FrameHeader.Empty;
                if (_length - _position < 128)
                {
                    return false;
                }

                var metadata = new byte[128];
                firstHeader.CopyTo(metadata, 0);
                if (!ReadExactly(metadata, 4, 124)
                    || Encoding.ASCII.GetString(metadata).IndexOf("DXLog.net - ", StringComparison.Ordinal) < 0)
                {
                    return false;
                }

                nextHeader = ReadHeader();
                return nextHeader.BytesRead == 4 && nextHeader.IsValid;
            }

            public void Skip(int count)
            {
                while (count > 0)
                {
                    if (_bufferOffset >= _bufferCount && !Fill())
                    {
                        throw new EndOfStreamException();
                    }

                    var available = Math.Min(count, _bufferCount - _bufferOffset);
                    _bufferOffset += available;
                    _position += available;
                    count -= available;
                }
            }

            public void Dispose()
            {
            }

            private int ReadByte()
            {
                if (_bufferOffset >= _bufferCount && !Fill())
                {
                    return -1;
                }

                _position++;
                return _buffer[_bufferOffset++];
            }

            private bool ReadExactly(byte[] destination, int offset, int count)
            {
                while (count > 0)
                {
                    var value = ReadByte();
                    if (value < 0)
                    {
                        return false;
                    }

                    destination[offset++] = (byte)value;
                    count--;
                }

                return true;
            }

            private bool Fill()
            {
                if (_position >= _length)
                {
                    return false;
                }

                _stream.Position = _position;
                _bufferCount = _stream.Read(_buffer, 0, _buffer.Length);
                _bufferOffset = 0;
                return _bufferCount > 0;
            }
        }

        private struct Mp3FrameHeader
        {
            private static readonly int[] Mpeg1Layer3Bitrates =
                { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
            private static readonly int[] Mpeg2Layer3Bitrates =
                { 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
            private static readonly int[] Mpeg1SampleRates = { 44100, 48000, 32000 };
            private static readonly int[] Mpeg2SampleRates = { 22050, 24000, 16000 };
            private static readonly int[] Mpeg25SampleRates = { 11025, 12000, 8000 };

            public static readonly Mp3FrameHeader Empty = new Mp3FrameHeader(0, 0, 0, 0, 0);

            public Mp3FrameHeader(int bytesRead, byte first, byte second, byte third, byte fourth)
            {
                BytesRead = bytesRead;
                RawFirst = first;
                RawSecond = second;
                RawThird = third;
                RawFourth = fourth;
                IsValid = false;
                FrameLength = 0;
                SampleCount = 0;
                SampleRate = 0;
                MpegVersion = -1;
                MpegLayer = -1;
            }

            private Mp3FrameHeader(
                byte first,
                byte second,
                byte third,
                byte fourth,
                int frameLength,
                int sampleCount,
                int sampleRate,
                int mpegVersion,
                int mpegLayer)
                : this(4, first, second, third, fourth)
            {
                IsValid = true;
                FrameLength = frameLength;
                SampleCount = sampleCount;
                SampleRate = sampleRate;
                MpegVersion = mpegVersion;
                MpegLayer = mpegLayer;
            }

            public int BytesRead { get; }

            public byte RawFirst { get; }

            public byte RawSecond { get; }

            public byte RawThird { get; }

            public byte RawFourth { get; }

            public bool IsValid { get; }

            public int FrameLength { get; }

            public int SampleCount { get; }

            public int SampleRate { get; }

            public int MpegVersion { get; }

            public int MpegLayer { get; }

            public static Mp3FrameHeader Parse(byte first, byte second, byte third, byte fourth)
            {
                var value = ((uint)first << 24)
                    | ((uint)second << 16)
                    | ((uint)third << 8)
                    | fourth;
                if ((value & 0xFFE00000) != 0xFFE00000)
                {
                    return new Mp3FrameHeader(4, first, second, third, fourth);
                }

                var version = (int)((value >> 19) & 0x3);
                var layer = (int)((value >> 17) & 0x3);
                var bitrateIndex = (int)((value >> 12) & 0xF);
                var sampleRateIndex = (int)((value >> 10) & 0x3);
                if (version == 1 || layer != 1 || bitrateIndex == 0 || bitrateIndex == 15 || sampleRateIndex == 3)
                {
                    return new Mp3FrameHeader(4, first, second, third, fourth);
                }

                var bitrates = version == 3 ? Mpeg1Layer3Bitrates : Mpeg2Layer3Bitrates;
                var sampleRates = version == 3
                    ? Mpeg1SampleRates
                    : version == 2 ? Mpeg2SampleRates : Mpeg25SampleRates;
                var bitrateKbps = bitrates[bitrateIndex - 1];
                var sampleRate = sampleRates[sampleRateIndex];
                var coefficient = version == 3 ? 144000L : 72000L;
                var frameLength = (int)(coefficient * bitrateKbps / sampleRate + ((value >> 9) & 0x1));
                if (frameLength < 4)
                {
                    return new Mp3FrameHeader(4, first, second, third, fourth);
                }

                return new Mp3FrameHeader(
                    first,
                    second,
                    third,
                    fourth,
                    frameLength,
                    version == 3 ? 1152 : 576,
                    sampleRate,
                    version,
                    layer);
            }

            public void CopyTo(byte[] destination, int offset)
            {
                destination[offset] = RawFirst;
                destination[offset + 1] = RawSecond;
                destination[offset + 2] = RawThird;
                destination[offset + 3] = RawFourth;
            }
        }

        public Mp3FrameInfo? FindFrameAtOrBefore(long filePosition)
        {
            if (_frames.Count == 0 || filePosition < _frames[0].FileOffset)
            {
                return null;
            }

            var low = 0;
            var high = _frames.Count - 1;
            var result = 0;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                if (_frames[middle].FileOffset <= filePosition)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return _frames[result];
        }

        public int FindFirstFrameAtOrAfter(TimeSpan time)
        {
            var low = 0;
            var high = _frames.Count;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (_frames[middle].StartTime < time)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        public int FindLastFrameAtOrBefore(TimeSpan time)
        {
            var firstAfter = FindFirstFrameAtOrAfter(time);
            if (firstAfter == _frames.Count)
            {
                return _frames.Count - 1;
            }

            if (_frames[firstAfter].StartTime == time)
            {
                return firstAfter;
            }

            return firstAfter - 1;
        }

        private static bool LooksLikeTruncatedFrame(Stream stream, long position, long length)
        {
            return position >= 0 && position < length && length - position < 4096;
        }
    }

    public sealed class Mp3IndexProgress
    {
        public Mp3IndexProgress(long bytesProcessed, long totalBytes)
        {
            BytesProcessed = bytesProcessed;
            TotalBytes = totalBytes;
        }

        public long BytesProcessed { get; }

        public long TotalBytes { get; }
    }

    public sealed class Mp3FrameInfo
    {
        internal Mp3FrameInfo(
            long fileOffset,
            int frameLength,
            int sampleCount,
            int sampleRate,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            FileOffset = fileOffset;
            FrameLength = frameLength;
            SampleCount = sampleCount;
            SampleRate = sampleRate;
            StartTime = startTime;
            EndTime = endTime;
        }

        public long FileOffset { get; }

        public int FrameLength { get; }

        public int SampleCount { get; }

        public int SampleRate { get; }

        public TimeSpan StartTime { get; }

        public TimeSpan EndTime { get; }
    }

    public enum Mp3IndexFailureKind
    {
        NoReadableFrames,
        InvalidMetadata,
        InvalidRegion,
        IncompatibleFormat,
        TruncatedFinalFrame
    }

    public sealed class Mp3IndexException : Exception
    {
        public Mp3IndexException(Mp3IndexFailureKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public Mp3IndexException(Mp3IndexFailureKind kind, string message, Exception innerException)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public Mp3IndexFailureKind Kind { get; }
    }
}
