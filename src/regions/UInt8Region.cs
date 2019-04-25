using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class UInt8Region : Region
	{
		private byte byteValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byteValue=reader.ReadByte();
			writer.WriteString(byteValue.ToString());
		}

		public override string ToString()
		{
			return byteValue.ToString();
		}
	}
}
