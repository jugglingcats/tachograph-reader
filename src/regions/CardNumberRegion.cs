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

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			driverIdentification=reader.ReadString(14);
			replacementIndex=reader.ReadByte();
			renewalIndex=reader.ReadByte();
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}",
				driverIdentification, replacementIndex, renewalIndex);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("ReplacementIndex", replacementIndex.ToString());
			writer.WriteAttributeString("RenewalIndex", renewalIndex.ToString());

			writer.WriteString(driverIdentification);
		}
	}
}
