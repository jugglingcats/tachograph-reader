using System;
using System.IO;
using System.Text;

namespace DataFileReader
{
	/// <summary>
	/// Simple extension to BinaryReader with convenience methods for reading from tachograph file
	/// </summary>
	public class CustomBinaryReader : System.IO.BinaryReader
	{
		// get clock ticks since 1 January 1970
		private static readonly long ticks1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks; 

		public CustomBinaryReader(Stream s) : base(s)
		{
		}

		public uint ReadSInt32()
		{
			// in tachograph file number is little-endian

			byte r1=ReadByte();
			byte r2=ReadByte();
			byte r3=ReadByte();
			byte r4=ReadByte();

			return (uint) (r4 | r3 << 8 | r2 << 16 | r1 << 24);
		}

		public uint ReadSInt24()
		{
			byte r1=ReadByte();
			byte r2=ReadByte();
			byte r3=ReadByte();

			return (uint) (r3 | r2 << 8 | r1 << 16);
		}

		public uint ReadSInt16()
		{
			byte r1=ReadByte();
			byte r2=ReadByte();

			return (uint) (r2 | r1 << 8);
		}

		public string ReadString(int length)
		{
			return ReadString(length, Encoding.ASCII);
		}

		public string ReadString(int length, Encoding enc)
		{
			byte[] buf=new byte[length];

			int amountRead=Read(buf, 0, length);
			if ( amountRead != length )
				throw new InvalidOperationException("End of file while reading a string");

			int nullPos = Array.IndexOf(buf, (byte)0x00);
			if (nullPos >= 0 && nullPos < length)
			{
				Array.Resize(ref buf, nullPos);
			}

			char[] chars=enc.GetChars(buf);
			return new string(chars);
		}

		public DateTime ReadTimeReal()
		{
			// the offset is seconds since 1 January 1970
			uint offset=ReadSInt32();

			// Calculate the absolute number of ticks (100ths of nanoseconds since 0000)
			long absTicks=ticks1970 + offset * 10000000L;

			// and convert to actual date time class
			return new DateTime(absTicks);
		}

		public uint ReadBCDString(int lengthInBytes)
		{
			const byte frontMask = 0xF0;
			const byte backMask = 0x0F;

			if (lengthInBytes > 4)
			{
				throw new InvalidOperationException("Length too big");
			}

			byte[] octets = new byte[lengthInBytes];
			int amountRead = Read(octets, 0, lengthInBytes);
			if (amountRead != lengthInBytes)
			{
				throw new InvalidOperationException("End of file while reading a string");
			}

			uint result = 0;
			for (int i = 0; i < lengthInBytes; ++i)
			{
				byte octet = octets[i];
				int front = (octet & frontMask) >> 4;
				int back = octet & backMask;
				result *= 100;
				result += (uint) (front * 10);
				result += (uint) back;
			}

			return result;
		}
	}
}
