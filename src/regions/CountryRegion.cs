using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class CountryRegion : Region
	{
		private string countryName;
		private byte byteValue;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			this.byteValue = reader.ReadByte();
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
		}

		public override string ToString()
		{
			return countryName;
		}

		public byte GetId()
		{
			return this.byteValue;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("Name", this.ToString());
			writer.WriteString(this.byteValue.ToString());
		}
	}
}
