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
			ProcessedRegions.Clear();
			DataBufferIsWrapped = false;
			if (regionLength < 4) throw new ReaderException("Truncated cyclic activity header");

			uint oldest=reader.ReadSInt16();
			uint newest=reader.ReadSInt16();

			// length is length of region minus the bytes we've just read
			long effectiveLength=regionLength - 4;
			long position=reader.BaseStream.Position;

			if (position + effectiveLength > reader.BaseStream.Length)
			{
				throw new ReaderException("Truncated cyclic activity buffer");
			}

			WriteLine(LogLevel.DEBUG, "Oldest 0x{0:X4} (offset 0x{2:X4}), newest 0x{1:X4} (offset 0x{3:X4})",
				position+oldest, position+newest,
				oldest, newest);

			if (oldest == 0 && newest == 0)
			{
				bool empty = true;
				foreach (byte value in reader.ReadExactlyBytes((int)effectiveLength)) empty &= value == 0;
				if (empty) return;
				reader.BaseStream.Position = position;
			}

			if ( newest >= effectiveLength || oldest >= effectiveLength)
			{
				throw new ReaderException("Invalid pointer to cyclic activity record");
			}

			CyclicStream cyclicStream=new CyclicStream(reader.BaseStream, reader.BaseStream.Position, effectiveLength);
			CustomBinaryReader cyclicReader=new CustomBinaryReader(cyclicStream);

			reader.BaseStream.Seek(oldest, SeekOrigin.Current);

			long consumed = 0;
			bool last=false;
			while ( !last )
			{
				long pos=cyclicStream.Position % effectiveLength;
				if ( pos == newest )
					last=true;

				foreach ( Region r in regions )
				{
					var newRegion = r.Copy();
					newRegion.RegionLength=effectiveLength;
					newRegion.Process(cyclicReader);
					this.ProcessedRegions.Add(newRegion);
				}
				long after = cyclicStream.Position;
				long step = after - pos;
				if (step <= 0) step += effectiveLength;
				consumed += step;
				if (consumed > effectiveLength || (!last && consumed == effectiveLength))
					throw new ReaderException("Cyclic activity chain does not reach newest record");
				this.DataBufferIsWrapped = cyclicStream.Wrapped;
				// Stop at the newest record, including across the physical boundary.
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
