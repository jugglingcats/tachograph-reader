using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// see page 83 - 4 byte second offset from midnight 1 January 1970
	public class TimeRealRegion : Region
	{
		private DateTime dateTime;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			dateTime=reader.ReadTimeReal();
			SignatureRegion.UpdateTime(dateTime);
		}

		public override string ToString()
		{
			return string.Format("{0}", dateTime);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("DateTime", dateTime.ToString("u"));
		}
	}
}
