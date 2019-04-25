using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	// See page 72. Have written class for this as seen in multiple places
	public class ExtendedSerialNumberRegion : Region
	{
		private uint serialNumber;
		private byte month;
		private byte year;
		private byte type;
		private byte manufacturerCode;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			serialNumber=reader.ReadSInt32();
			// BCD coding of Month (two digits) and Year (two last digits)
			uint monthYear=reader.ReadBCDString(2);
			type=reader.ReadByte();
			manufacturerCode=reader.ReadByte();

			month = (byte)(monthYear / 100);
			year = (byte)(monthYear % 100);
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}/{2}, type={3}, manuf code={4}",
				serialNumber, month, year, type, manufacturerCode);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("Month", month.ToString());
			writer.WriteAttributeString("Year", year.ToString());
			writer.WriteAttributeString("Type", type.ToString());
			writer.WriteAttributeString("ManufacturerCode", manufacturerCode.ToString());

			writer.WriteString(serialNumber.ToString());
		}
	}
}
