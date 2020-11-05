using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class CardNumberRegion : Region
	{
		protected string driverIdentification;
		protected string replacementIndex;
		protected string renewalIndex;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			driverIdentification=reader.ReadString(14);
			replacementIndex=reader.ReadChar().ToString();
			renewalIndex=reader.ReadChar().ToString();
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}",
				driverIdentification, replacementIndex, renewalIndex);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("ReplacementIndex", replacementIndex);
			writer.WriteAttributeString("RenewalIndex", renewalIndex);

			writer.WriteString(driverIdentification);
		}
	}
}
