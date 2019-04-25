using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class BCDStringRegion : Region
	{
		[XmlAttribute]
		public int Size;
		private uint value;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			value=reader.ReadBCDString(Size);
			writer.WriteString(value.ToString());
		}

		public override string ToString()
		{
			return value.ToString();
		}
	}
}
