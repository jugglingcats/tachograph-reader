using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// 3 byte number, as used by OdometerShort (see page 86)
	public class UInt24Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			uintValue=reader.ReadSInt24();
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}

		public uint ToUInt()
		{
			return this.uintValue;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
