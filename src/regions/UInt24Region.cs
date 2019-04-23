using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// 3 byte number, as used by OdometerShort (see page 86)
	public class UInt24Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			uintValue=reader.ReadSInt24();
			writer.WriteString(uintValue.ToString());
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}
	}
}
