using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// Simple class to represent a boolean (0 or 1 in specification)
	public class FlagRegion : Region
	{
		private bool boolValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			boolValue=reader.ReadByte() > 0;
			writer.WriteString(boolValue.ToString());
		}

		public override string ToString()
		{
			return boolValue.ToString();
		}
	}
}
