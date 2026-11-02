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
			reader.ReadExactlyBytes(Size);
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
