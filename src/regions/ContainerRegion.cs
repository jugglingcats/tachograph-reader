using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	/// Generic class for a region that contains other regions. Used as a simple wrapper
	/// where this is indicated in the specification
	public class ContainerRegion : Region
	{
		[XmlIgnore]
		public ArrayList regions=new ArrayList();

		[XmlIgnore]
		public Dictionary<string, Region> ProcessedRegions {get; private set;} = new Dictionary<string, Region>();

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			// iterate over all child regions and process them
			foreach ( Region r in regions )
			{
				r.RegionLength=regionLength;
				r.Process(reader);
				this.ProcessedRegions.Add(r.Name, r);
			}

			if (this.Name == "VuCertificate")
			{
				SignatureRegion.signedDataOffsetBegin = reader.BaseStream.Position;
			}
		}

		// these are the valid regions this class can contain, along with XML name mappings
		[XmlElement("Padding", typeof(PaddingRegion)),
		XmlElement("Collection", typeof(CollectionRegion)),
		XmlElement("Cycle", typeof(CyclicalActivityRegion)),
		XmlElement("DriverCardDailyActivity", typeof(DriverCardDailyActivityRegion)),
		XmlElement("Repeat", typeof(RepeatingRegion)),
		XmlElement("Name", typeof(NameRegion)),
		XmlElement("SimpleString", typeof(SimpleStringRegion)),
		XmlElement("InternationalString", typeof(CodePageStringRegion)),
		XmlElement("ExtendedSerialNumber", typeof(ExtendedSerialNumberRegion)),
		XmlElement("Object", typeof(ContainerRegion)),
		XmlElement("TimeReal", typeof(TimeRealRegion)),
		XmlElement("Datef", typeof(DatefRegion)),
		XmlElement("ActivityChange", typeof(ActivityChangeRegion)),
		XmlElement("CardNumber", typeof(CardNumberRegion)),
		XmlElement("FullCardNumber", typeof(FullCardNumberRegion)),
		XmlElement("Flag", typeof(FlagRegion)),
		XmlElement("UInt24", typeof(UInt24Region)),
		XmlElement("UInt16", typeof(UInt16Region)),
		XmlElement("UInt8", typeof(UInt8Region)),
		XmlElement("BCDString", typeof(BCDStringRegion)),
		XmlElement("Country", typeof(CountryRegion)),
		XmlElement("HexValue", typeof(HexValueRegion)),
		XmlElement("Signature", typeof(SignatureRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			foreach (var region in this.ProcessedRegions.Values)
			{
				region.ToXML(writer);
			};
		}
	}
}
