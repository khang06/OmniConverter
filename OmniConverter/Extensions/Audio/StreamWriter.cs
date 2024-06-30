﻿using System;
using System.Runtime.InteropServices;
using CSCore;
using CSCore.Codecs.WAV;

// Code from ChimeCore, written by Arduano
// https://github.com/arduano

namespace OmniConverter
{
    public unsafe interface ISampleWriter
    {
        int Position { get; set; }

        void Write(float[] buffer, int offset, int count);
        void Write(float* buffer, int offset, int count);
    }

    public sealed class WaveSampleWriter : ISampleWriter, IDisposable
    {
        WaveWriter writer;

        public WaveSampleWriter(WaveWriter writer)
        {
            this.writer = writer;
        }

        public int Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Dispose()
        {
            writer.Dispose();
        }

        public void Write(float[] buffer, int offset, int count)
        {
            writer.WriteSamples(buffer, offset, count);
        }

        public unsafe void Write(float* buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class MultiStreamMerger : ISampleSource
    {
        class Writer : ISampleWriter
        {
            MultiStreamMerger Stream;

            public int Position { get; set; }

            public Writer(MultiStreamMerger stream)
            {
                this.Stream = stream;
            }

            void ResizeStream(int Length)
            {
                if (Length > Stream.Length)
                {
                    lock (Stream)
                    {
                        if (Length < Stream.data.Length) Stream.Length = Length;
                        else
                        {
                            int NewSize = (int)(Stream.data.Length * 1.2);

                            if (NewSize < Length)
                                NewSize = Length;

                            Array.Resize(ref Stream.data, NewSize);
                            Stream.Length = Length;

                            GC.Collect(2);
                        }
                    }                  
                }
            }

            public unsafe void Write(float[] buffer, int offset, int count)
            {
                ResizeStream(Position + count);

                fixed (float* dest = Stream.data)
                {
                    float* _dest = dest + Position;
                    for (int i = 0; i < count; i++)
                        _dest[i] += buffer[i + offset];
                }

                Position += count;
            }

            public unsafe void Write(float* buffer, int offset, int count)
            {
                ResizeStream(Position + count);

                fixed (float* dest = Stream.data)
                {
                    float* _dest = dest + Position;
                    for (int i = 0; i < count; i++)
                        _dest[i] += buffer[i + offset];
                }

                Position += count;
            }
        }

        public bool CanSeek => true;

        public WaveFormat WaveFormat { get; }

        public long Position
        {
            get => position;
            set
            {
                if (value > Length || value < 0) throw new Exception(string.Format("Position out of range!\n\nValue: {0}\nMaximum value: {1}", value, Length));
                position = value;
            }
        }
        public long Length { get; private set; } = 0;

        float[] data = Array.Empty<float>();
        private long position = 0;

        public MultiStreamMerger(WaveFormat format)
        {
            WaveFormat = format;
        }

        public void Dispose()
        {
            //data = null;
        }

        public ISampleWriter GetWriter()
        {
            return new Writer(this);
        }

        public unsafe int Read(float[] buffer, int offset, int count)
        {
            if (Position >= Length)
                return 0;
            if (Length - position < count)
                count = (int)(Length - position);

            fixed (float* src = data)
            {
                float* _src = src + position;
                Marshal.Copy((IntPtr)_src, buffer, offset, count);
            }

            position += count;
            return count;
        }
    }
}