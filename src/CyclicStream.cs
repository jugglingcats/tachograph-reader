using System;
using System.IO;

namespace DataFileReader
{
	/// <summary>
	/// Implementation of a Stream that deals with the cyclic buffer in driver cards
	/// </summary>
	public class CyclicStream : Stream
	{
		private Stream baseStream;
		private long startOffset;
		private long length;
		private bool wrapped=false;

		public CyclicStream(Stream baseStream, long startOffset, long length)
		{
			this.baseStream=baseStream;
			this.startOffset=startOffset;
			this.length=length;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer == null) throw new ArgumentNullException(nameof(buffer));
			if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
			if (count == 0) return 0;
			long position = Position;
			if (position < 0 || position > length) throw new EndOfStreamException("Invalid cyclic position");
			if (position == length)
			{
				if (wrapped) throw new EndOfStreamException("Cyclic stream has already wrapped");
				baseStream.Position = startOffset;
				wrapped = true;
				position = 0;
			}
			// Returning a partial read is permitted by Stream. BinaryReader fills its request.
			return baseStream.Read(buffer, offset, (int)Math.Min(count, length - position));
		}

		public bool Wrapped
		{
			get { return wrapped; }
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("Cannot seek within a cyclic stream");
		}

		public override void Flush()
		{
		}

		public override void SetLength(long value)
		{
			length=value;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override bool CanRead
		{
			get { return baseStream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return baseStream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get { return length; }
		}

		public override long Position
		{
			get { return baseStream.Position-startOffset; }
			set { baseStream.Position=startOffset+value; }
		}

		public long ActualPosition
		{
			get { return baseStream.Position; }
		}
	}
}
