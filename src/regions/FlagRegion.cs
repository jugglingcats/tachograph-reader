using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// Simple class to represent a boolean (0 or 1 in specification)
	public class FlagRegion : Region
	{
		private bool boolValue;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			boolValue=reader.ReadByte() > 0;
		}

		public override string ToString()
		{
			return boolValue.ToString();
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
