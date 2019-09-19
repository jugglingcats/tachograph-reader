using System;
using System.Collections.Generic;
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

		[XmlIgnore]
		public List<ActivityChangeRegion> ProcessedRegions {get; private set;} = new List<ActivityChangeRegion>();

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			previousRecordLength=reader.ReadSInt16();
			currentRecordLength=reader.ReadSInt16();
			recordDate=reader.ReadTimeReal();
			SignatureRegion.UpdateTime(recordDate);
			dailyPresenceCounter=reader.ReadBCDString(2);
			distance=reader.ReadSInt16();

			uint recordCount=(currentRecordLength-12)/2;
			this.ProcessedRegions.Capacity = (int)recordCount;

			WriteLine(LogLevel.DEBUG, "Reading {0} activity records", recordCount);

			while (recordCount > 0)
			{
				ActivityChangeRegion acr=new ActivityChangeRegion();
				acr.Name="ActivityChangeInfo";
				acr.Process(reader);
				this.ProcessedRegions.Add(acr);
				recordCount--;
			}
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("DateTime", recordDate.ToString("u"));
			writer.WriteAttributeString("DailyPresenceCounter", dailyPresenceCounter.ToString());
			writer.WriteAttributeString("Distance", distance.ToString());

			foreach (var region in this.ProcessedRegions)
			{
				region.ToXML(writer);
			};
		}
	}
}
