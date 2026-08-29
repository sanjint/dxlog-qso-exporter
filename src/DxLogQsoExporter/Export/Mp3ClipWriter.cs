using System;
using System.Globalization;
using System.IO;
using System.Threading;
using DxLogQsoExporter.Recordings;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace DxLogQsoExporter.Export
{
    public sealed class Mp3ClipWriter
    {
        private const int DefaultBufferSize = 64 * 1024;
        private const int ChannelOutputBitRate = 64000;
        private static readonly object MediaFoundationLock = new object();
        private static bool _mediaFoundationStarted;
        private readonly int _bufferSize;

        public Mp3ClipWriter(int bufferSize = DefaultBufferSize)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            _bufferSize = bufferSize;
        }

        public Mp3ClipWriteResult Write(
            RecordingSource source,
            ClipWindow window,
            string outputPath,
            CancellationToken cancellationToken,
            RadioChannel? channel = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("An output path is required.", nameof(outputPath));
            }

            var finalPath = Path.GetFullPath(outputPath);
            var partialPath = finalPath + ".partial";
            var published = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(finalPath))
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.OutputAlreadyExists,
                        "The output file already exists.");
                }

                if (!source.IsUnchanged())
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.SourceChanged,
                        "The recording source changed before the clip could be written.");
                }

                var outputDirectory = Path.GetDirectoryName(finalPath);
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.OutputFailure,
                        "The output path has no destination folder.");
                }

                Directory.CreateDirectory(outputDirectory);
                TryDeletePartial(partialPath);
                var expectedBytes = window.ByteLength;
                long copiedBytes;
                if (channel.HasValue)
                {
                    copiedBytes = WriteSelectedChannel(source, window, partialPath, channel.Value, cancellationToken);
                }
                else
                {
                    using (var input = source.OpenRead())
                    using (var output = new FileStream(
                        partialPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        _bufferSize,
                        useAsync: false))
                    {
                        input.Position = window.ByteStart;
                        copiedBytes = CopyRange(input, output, expectedBytes, cancellationToken, _bufferSize);
                        output.Flush(true);
                    }

                    if (copiedBytes != expectedBytes || new FileInfo(partialPath).Length != expectedBytes)
                    {
                        throw new Mp3ClipWriteException(
                            Mp3ClipWriteFailureKind.ValidationFailure,
                            "The staged clip length did not match the expected frame range.");
                    }
                }

                ValidateFirstFrame(partialPath);
                cancellationToken.ThrowIfCancellationRequested();
                if (!source.IsUnchanged())
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.SourceChanged,
                        "The recording source changed while the clip was being written.");
                }

                try
                {
                    File.Move(partialPath, finalPath);
                }
                catch (IOException exception) when (File.Exists(finalPath))
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.OutputAlreadyExists,
                        "The output file already exists.",
                        exception);
                }

                published = true;
                return new Mp3ClipWriteResult(finalPath, copiedBytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Mp3ClipWriteException)
            {
                throw;
            }
            catch (IOException exception)
            {
                throw new Mp3ClipWriteException(
                    Mp3ClipWriteFailureKind.OutputFailure,
                    "The clip could not be written.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new Mp3ClipWriteException(
                    Mp3ClipWriteFailureKind.OutputFailure,
                    "The output folder is not writable.",
                    exception);
            }
            finally
            {
                if (!published)
                {
                    TryDeletePartial(partialPath);
                }
            }
        }

        private static long WriteSelectedChannel(
            RecordingSource source,
            ClipWindow window,
            string outputPath,
            RadioChannel channel,
            CancellationToken cancellationToken)
        {
            var encodedPath = outputPath + ".mp3";
            try
            {
                TryDeletePartial(encodedPath);
                EnsureMediaFoundation();
                using (var input = source.OpenRead())
                using (var clip = new StreamSlice(input, window.ByteStart, window.ByteLength))
                using (var reader = new Mp3FileReader(clip))
                {
                    if (reader.WaveFormat.Channels != 2 || reader.WaveFormat.BitsPerSample != 16)
                    {
                        throw new InvalidDataException("The recording does not contain 16-bit stereo audio.");
                    }

                    var mono = new StereoToMonoProvider16(reader);
                    if (channel == RadioChannel.Left)
                    {
                        mono.LeftVolume = 1;
                        mono.RightVolume = 0;
                    }
                    else
                    {
                        mono.LeftVolume = 0;
                        mono.RightVolume = 1;
                    }

                    var cancellable = new CancellableWaveProvider(mono, cancellationToken);
                    MediaFoundationEncoder.EncodeToMp3(cancellable, encodedPath, ChannelOutputBitRate);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var encodedInfo = new FileInfo(encodedPath);
                if (!encodedInfo.Exists || encodedInfo.Length <= 0)
                {
                    throw new InvalidDataException("The selected radio channel produced an empty MP3.");
                }

                File.Move(encodedPath, outputPath);
                var outputInfo = new FileInfo(outputPath);
                if (!outputInfo.Exists || outputInfo.Length <= 0)
                {
                    throw new InvalidDataException("The selected radio channel produced an empty MP3.");
                }

                return outputInfo.Length;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Mp3ClipWriteException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is NotSupportedException
                || exception is System.Runtime.InteropServices.COMException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                throw new Mp3ClipWriteException(
                    Mp3ClipWriteFailureKind.ValidationFailure,
                    "The selected radio channel could not be extracted.",
                    exception);
            }
            finally
            {
                TryDeletePartial(encodedPath);
            }
        }

        private static void EnsureMediaFoundation()
        {
            lock (MediaFoundationLock)
            {
                if (_mediaFoundationStarted)
                {
                    return;
                }

                MediaFoundationApi.Startup();
                _mediaFoundationStarted = true;
            }
        }

        private static long CopyRange(
            Stream input,
            Stream output,
            long bytesToCopy,
            CancellationToken cancellationToken,
            int bufferSize = DefaultBufferSize)
        {
            var buffer = new byte[bufferSize];
            long copied = 0;
            while (copied < bytesToCopy)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requested = (int)Math.Min(buffer.Length, bytesToCopy - copied);
                var read = input.Read(buffer, 0, requested);
                if (read == 0)
                {
                    throw new Mp3ClipWriteException(
                        Mp3ClipWriteFailureKind.ValidationFailure,
                        "The recording ended before the complete clip range could be copied.");
                }

                output.Write(buffer, 0, read);
                copied += read;
            }

            return copied;
        }

        private static void ValidateFirstFrame(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false))
                {
                    var tag = Id3v2Tag.ReadTag(stream);
                    if (tag == null)
                    {
                        stream.Position = 0;
                    }

                    var frame = Mp3Frame.LoadFromStream(stream);
                    if (frame == null)
                    {
                        throw new Mp3ClipWriteException(
                            Mp3ClipWriteFailureKind.ValidationFailure,
                            "The staged clip does not begin with a readable MP3 frame.");
                    }
                }
            }
            catch (Mp3ClipWriteException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is EndOfStreamException)
            {
                throw new Mp3ClipWriteException(
                    Mp3ClipWriteFailureKind.ValidationFailure,
                    "The staged clip could not be parsed as MP3.",
                    exception);
            }
        }

        private static void TryDeletePartial(string partialPath)
        {
            try
            {
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private sealed class CancellableWaveProvider : IWaveProvider
        {
            private readonly IWaveProvider _source;
            private readonly CancellationToken _cancellationToken;

            public CancellableWaveProvider(IWaveProvider source, CancellationToken cancellationToken)
            {
                _source = source;
                _cancellationToken = cancellationToken;
            }

            public WaveFormat WaveFormat
            {
                get { return _source.WaveFormat; }
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var read = _source.Read(buffer, offset, count);
                _cancellationToken.ThrowIfCancellationRequested();
                return read;
            }
        }

        private sealed class StreamSlice : Stream
        {
            private readonly Stream _source;
            private readonly long _start;
            private readonly long _length;
            private long _position;

            public StreamSlice(Stream source, long start, long length)
            {
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (!source.CanRead || !source.CanSeek)
                {
                    throw new ArgumentException("The source stream must be readable and seekable.", nameof(source));
                }

                if (start < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(start));
                }

                if (length < 0 || start > source.Length - length)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }

                _source = source;
                _start = start;
                _length = length;
            }

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return true; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Length
            {
                get { return _length; }
            }

            public override long Position
            {
                get { return _position; }
                set
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    _position = value;
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0 || count < 0 || offset > buffer.Length - count)
                {
                    throw new ArgumentException("The buffer range is invalid.", nameof(buffer));
                }

                if (_position >= _length || count == 0)
                {
                    return 0;
                }

                var requested = (int)Math.Min(count, _length - _position);
                _source.Position = _start + _position;
                var read = _source.Read(buffer, offset, requested);
                _position += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPosition;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPosition = offset;
                        break;
                    case SeekOrigin.Current:
                        newPosition = checked(_position + offset);
                        break;
                    case SeekOrigin.End:
                        newPosition = checked(_length + offset);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }

                if (newPosition < 0)
                {
                    throw new IOException("The stream position cannot be negative.");
                }

                _position = newPosition;
                return _position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }

    public sealed class Mp3ClipWriteResult
    {
        public Mp3ClipWriteResult(string outputPath, long byteCount)
        {
            OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
            ByteCount = byteCount;
        }

        public string OutputPath { get; }

        public long ByteCount { get; }
    }

    public enum Mp3ClipWriteFailureKind
    {
        OutputAlreadyExists,
        SourceChanged,
        ValidationFailure,
        OutputFailure
    }

    public sealed class Mp3ClipWriteException : Exception
    {
        public Mp3ClipWriteException(Mp3ClipWriteFailureKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public Mp3ClipWriteException(Mp3ClipWriteFailureKind kind, string message, Exception innerException)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public Mp3ClipWriteFailureKind Kind { get; }
    }
}
