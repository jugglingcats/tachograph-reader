using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class CardNumberRegion : Region
	{
		protected string driverIdentification;
		protected byte replacementIndex;
		protected byte renewalIndex;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			driverIdentification=reader.ReadString(14);
			replacementIndex=reader.ReadByte();
			renewalIndex=reader.ReadByte();

			writer.WriteAttributeString("ReplacementIndex", replacementIndex.ToString());
			writer.WriteAttributeString("RenewalIndex", renewalIndex.ToString());

			writer.WriteString(driverIdentification);
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}",
				driverIdentification, replacementIndex, renewalIndex);
		}
	}
}
