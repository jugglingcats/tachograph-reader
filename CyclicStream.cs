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
			if (count <= 0)
			{
				throw new ArgumentOutOfRangeException("Count must be positive!");
			}
			long currentPosition=Position;
			if (currentPosition > length)
			{
				throw new EndOfStreamException("Trying to read after end position!");
			}

			if ( currentPosition + count <= length )
				return baseStream.Read(buffer, offset, count);

			if ( wrapped )
				throw new EndOfStreamException("Cyclic stream has already wrapped");

			wrapped=true;

			int remaining=(int) (length-currentPosition);
			int read=baseStream.Read(buffer, offset, remaining);

			count-=remaining;

			baseStream.Seek(startOffset, SeekOrigin.Begin);

			read+=Read(buffer, offset+remaining, count);

			return read;
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
