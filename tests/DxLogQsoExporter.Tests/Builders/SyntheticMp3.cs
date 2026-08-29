using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DxLogQsoExporter.Tests.Builders
{
    internal static class SyntheticMp3
    {
        public static byte[] Create(
            int frameCount,
            int bitrateKbps = 128,
            int sampleRate = 44100,
            bool includeId3 = false,
            int gapAfterFrame = -1,
            int gapLength = 0,
            bool truncateLastFrame = false,
            int alternateSampleRate = 0)
        {
            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            var frames = new List<byte[]>();
            for (var index = 0; index < frameCount; index++)
            {
                var currentSampleRate = index == 1 && alternateSampleRate > 0
                    ? alternateSampleRate
                    : sampleRate;
                frames.Add(CreateFrame(bitrateKbps, currentSampleRate, index % 2));
            }

            using (var stream = new MemoryStream())
            {
                if (includeId3)
                {
                    WriteId3Tag(stream, "synthetic");
                }

                for (var index = 0; index < frames.Count; index++)
                {
                    var frame = frames[index];
                    if (truncateLastFrame && index == frames.Count - 1)
                    {
                        stream.Write(frame, 0, Math.Max(4, frame.Length / 2));
                    }
                    else
                    {
                        stream.Write(frame, 0, frame.Length);
                    }

                    if (index == gapAfterFrame && gapLength > 0)
                    {
                        for (var gapIndex = 0; gapIndex < gapLength; gapIndex++)
                        {
                            stream.WriteByte((byte)(gapIndex + 1));
                        }
                    }
                }

                return stream.ToArray();
            }
        }

        public static int GetFrameLength(int bitrateKbps = 128, int sampleRate = 44100)
        {
            return (144 * bitrateKbps * 1000) / sampleRate;
        }

        private static byte[] CreateFrame(int bitrateKbps, int sampleRate, int padding)
        {
            var bitrateIndexes = new[] { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
            var sampleRateIndexes = new[] { 44100, 48000, 32000 };
            var bitrateIndex = Array.IndexOf(bitrateIndexes, bitrateKbps);
            var sampleRateIndex = Array.IndexOf(sampleRateIndexes, sampleRate);
            if (bitrateIndex < 0 || sampleRateIndex < 0)
            {
                throw new ArgumentException("The synthetic builder supports MPEG-1 Layer III preset values.");
            }

            var length = (144 * bitrateKbps * 1000) / sampleRate + padding;
            var frame = Enumerable.Repeat((byte)0x55, length).ToArray();
            frame[0] = 0xFF;
            frame[1] = 0xFB;
            frame[2] = (byte)(((bitrateIndex + 1) << 4) | (sampleRateIndex << 2) | (padding << 1));
            frame[3] = 0x64;
            return frame;
        }

        private static void WriteId3Tag(Stream stream, string text)
        {
            var payload = System.Text.Encoding.ASCII.GetBytes(text);
            stream.WriteByte((byte)'I');
            stream.WriteByte((byte)'D');
            stream.WriteByte((byte)'3');
            stream.WriteByte(3);
            stream.WriteByte(0);
            stream.WriteByte(0);
            WriteSyncSafe(stream, payload.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private static void WriteSyncSafe(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 21) & 0x7F));
            stream.WriteByte((byte)((value >> 14) & 0x7F));
            stream.WriteByte((byte)((value >> 7) & 0x7F));
            stream.WriteByte((byte)(value & 0x7F));
        }
    }
}
