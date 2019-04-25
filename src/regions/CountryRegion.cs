using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class CountryRegion : Region
	{
		private string countryName;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byte byteValue=reader.ReadByte();
			if ( byteValue < countries.Length )
				countryName=countries[byteValue];
			else if (byteValue == 0xFD)
				countryName="European Community";
			else if (byteValue == 0xFE)
				countryName="Europe";
			else if (byteValue == 0xFF)
				countryName="World";
			else
				countryName="UNKNOWN";

			writer.WriteAttributeString("Name", countryName);
			writer.WriteString(byteValue.ToString());
		}

		public override string ToString()
		{
			return countryName;
		}
	}
}
