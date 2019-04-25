using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class ElementaryFileRegion : IdentifiedObjectRegion
	{
		protected override bool SuppressElement(CustomBinaryReader reader)
		{
			int type=reader.PeekChar();
			return type == 0x01;
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// read the type
			byte type=reader.ReadByte();

			regionLength=reader.ReadSInt16();
			long fileLength=regionLength;

			if ( type == 0x01 )
			{
				// this is just the signature
			}
			else
			{
				long start=reader.BaseStream.Position;

				base.ProcessInternal(reader, writer);

				long amountProcessed=reader.BaseStream.Position-start;
				fileLength -= amountProcessed;
			}

			if ( fileLength > 0 )
			{
				// deal with a remaining fileLength that is greater than int
				while ( fileLength > int.MaxValue )
				{
					reader.ReadBytes(int.MaxValue);
					fileLength-=int.MaxValue;
				}
				reader.ReadBytes((int) fileLength);
			}
		}
	}
}
