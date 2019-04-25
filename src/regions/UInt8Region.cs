using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class UInt8Region : Region
	{
		private byte byteValue;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			byteValue=reader.ReadByte();
		}

		public override string ToString()
		{
			return byteValue.ToString();
		}

		public byte ToByte()
		{
			return this.byteValue;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
