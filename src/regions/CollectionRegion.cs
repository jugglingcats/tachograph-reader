using System;
using System.Collections.Generic;
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

		[XmlIgnore]
		public new List<Region> ProcessedRegions {get; private set;} = new List<Region>();

		protected override void ProcessInternal(CustomBinaryReader reader)
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

			ProcessItems(reader, count);
		}

		protected void ProcessItems(CustomBinaryReader reader, uint count)
		{
			WriteLine(LogLevel.DEBUG, "Processing repeating {0}, count={1}, offset=0x{2:X4}", Name, count, reader.BaseStream.Position);

			// repeat processing of child objects
			uint maxCount = count;
			this.ProcessedRegions.Capacity = (int)maxCount;
			while ( count > 0 )
			{
				try
				{
					foreach ( Region r in regions )
					{
						var newRegion = r.Copy();
						newRegion.RegionLength = regionLength;
						newRegion.Process(reader);
						this.ProcessedRegions.Add(newRegion);
					}
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

		protected override void InternalToXML(XmlWriter writer)
		{
			foreach (var region in this.ProcessedRegions)
			{
				region.ToXML(writer);
			};
		}
	}
}
