using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public enum SizeAllocation
	{
		BYTE,
		WORD
	}

	// A collection region is a repeating region prefixed by the count of number of
	// items in the region. The count can be represented by a single byte or a word,
	// depending on the collection, so this supports a SizeAllocation property to specify
	// which it is.
	public class CollectionRegion : ContainerRegion
	{
		[XmlAttribute]
		public SizeAllocation SizeAlloc=SizeAllocation.BYTE;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// get the count according to allocation size
			uint count;
			switch (SizeAlloc)
			{
				case SizeAllocation.BYTE:
					count=reader.ReadByte();
					break;

				case SizeAllocation.WORD:
					count=reader.ReadSInt16();
					break;

				default:
					throw new InvalidOperationException("Bad size allocation");
			}

			ProcessItems(reader, writer, count);
		}

		protected void ProcessItems(CustomBinaryReader reader, XmlWriter writer, uint count)
		{
			WriteLine(LogLevel.DEBUG, "Processing repeating {0}, count={1}, offset=0x{2:X4}", Name, count, reader.BaseStream.Position);

			// repeat processing of child objects
			uint maxCount = count;
			while ( count > 0 )
			{
				try
				{
					base.ProcessInternal(reader, writer);
					count--;
				} catch (EndOfStreamException ex)
				{
					WriteLine(LogLevel.ERROR, "Repeating {0}, count={1}/{2}: {3}", Name, count, maxCount, ex);
					break;
				}
			}
		}

		public override string ToString()
		{
			return string.Format("<< end {0}", Name);
		}
	}
}
