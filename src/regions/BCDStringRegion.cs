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

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			value=reader.ReadBCDString(Size);
		}

		public override string ToString()
		{
			return value.ToString();
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(value.ToString());
		}
	}
}
