using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class CyclicalActivityRegion : ContainerRegion
	{
		[XmlIgnore]
		public bool DataBufferIsWrapped {get; private set;} = false;

		[XmlIgnore]
		public new List<Region> ProcessedRegions {get; private set;} = new List<Region>();

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			if (regionLength <= 4)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion is empty!");
				return;
			}

			uint oldest=reader.ReadSInt16();
			uint newest=reader.ReadSInt16();

			// length is length of region minus the bytes we've just read
			long effectiveLength=regionLength - 4;
			long position=reader.BaseStream.Position;

			if (position + effectiveLength > reader.BaseStream.Length)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion position=0x{0:X4} + effectiveLength=0x{1:X4} > length=0x{2:X4} !", position, effectiveLength, reader.BaseStream.Length);
				return;
			}

			WriteLine(LogLevel.DEBUG, "Oldest 0x{0:X4} (offset 0x{2:X4}), newest 0x{1:X4} (offset 0x{3:X4})",
				position+oldest, position+newest,
				oldest, newest);

			if ( oldest == newest && oldest == 0 )
				// no data in file
				return;

			if ( newest >= effectiveLength || oldest >= effectiveLength)
			{
				throw new IndexOutOfRangeException("Invalid pointer to CyclicalActivity Record");
			}

			CyclicStream cyclicStream=new CyclicStream(reader.BaseStream, reader.BaseStream.Position, effectiveLength);
			CustomBinaryReader cyclicReader=new CustomBinaryReader(cyclicStream);

			reader.BaseStream.Seek(oldest, SeekOrigin.Current);

			bool last=false;
			while ( !last )
			{
				long pos=cyclicStream.Position;
				if ( pos == newest )
					last=true;

				foreach ( Region r in regions )
				{
					var newRegion = r.Copy();
					newRegion.RegionLength=regionLength;
					newRegion.Process(cyclicReader);
					this.ProcessedRegions.Add(newRegion);
				}
				this.DataBufferIsWrapped = cyclicStream.Wrapped;
				// commenting @davispuh mod because it can cause premature termination
				// see https://github.com/jugglingcats/tachograph-reader/issues/28
				// if (cyclicStream.Wrapped)
				// {
				// 	last = true;
				// }
			}

			reader.BaseStream.Position = position + effectiveLength;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			foreach (var region in this.ProcessedRegions)
			{
				region.ToXML(writer);
			};

			writer.WriteElementString("DataBufferIsWrapped", this.DataBufferIsWrapped.ToString());
		}
	}
}
