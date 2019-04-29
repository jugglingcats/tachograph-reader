using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	/// Simple class to "eat" a specified number of bytes in the file
	public class PaddingRegion : Region
	{
		[XmlAttribute]
		public int Size;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			byte[] buf=new byte[Size];
			int amountRead=reader.Read(buf, 0, Size);
			if ( amountRead != Size )
				throw new InvalidOperationException("End of file reading padding (size "+Size+")");
		}

		public override string ToString()
		{
			return string.Format("{0} bytes (0x{0:X4})", Size);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("Size", this.Size.ToString());
		}
	}
}
