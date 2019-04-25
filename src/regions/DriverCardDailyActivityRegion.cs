using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class DriverCardDailyActivityRegion : Region
	{
		private uint previousRecordLength;
		private uint currentRecordLength;
		private DateTime recordDate;
		private uint dailyPresenceCounter;
		private uint distance;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			previousRecordLength=reader.ReadSInt16();
			currentRecordLength=reader.ReadSInt16();
			recordDate=reader.ReadTimeReal();
			dailyPresenceCounter=reader.ReadBCDString(2);
			distance=reader.ReadSInt16();

			writer.WriteAttributeString("DateTime", recordDate.ToString("u"));
			writer.WriteAttributeString("DailyPresenceCounter", dailyPresenceCounter.ToString());
			writer.WriteAttributeString("Distance", distance.ToString());

			uint recordCount=(currentRecordLength-12)/2;
			WriteLine(LogLevel.DEBUG, "Reading {0} activity records", recordCount);

			while (recordCount > 0)
			{
				ActivityChangeRegion acr=new ActivityChangeRegion();
				acr.Name="ActivityChangeInfo";
				acr.Process(reader, writer);
				recordCount--;
			}
		}
	}
}
