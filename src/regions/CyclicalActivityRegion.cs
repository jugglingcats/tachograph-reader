using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class CyclicalActivityRegion : ContainerRegion
	{
		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
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

				base.ProcessInternal(cyclicReader, writer);
				// commenting @davispuh mod because it can cause premature termination
				// see https://github.com/jugglingcats/tachograph-reader/issues/28
				// if (cyclicStream.Wrapped)
				// {
				// 	last = true;
				// }
			}

			reader.BaseStream.Position = position + effectiveLength;

			writer.WriteElementString("DataBufferIsWrapped", cyclicStream.Wrapped.ToString());
		}
	}
}
