using System;
using System.IO;
namespace DataFileReader
{
    // Absolute positions preserve signature and cyclic-buffer offsets.
    internal sealed class BoundedStream : Stream
    {
        private readonly Stream source;
        private readonly long start, end;
        public BoundedStream(Stream source, long length)
        {
            this.source = source;
            start = source.Position;
            end = checked(start + length);
            if (length < 0 || end > source.Length) throw new EndOfStreamException("Truncated elementary file");
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position < start || Position > end) throw new IOException("Position outside elementary file");
            return source.Read(buffer, offset, (int)Math.Min(count, end - Position));
        }
        public override long Position
        {
            get { return source.Position; }
            set { if (value < start || value > end) throw new IOException("Seek outside elementary file"); source.Position = value; }
        }
        public override long Length { get { return end; } }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? checked(Position + offset) : checked(end + offset);
            return Position;
        }
        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
