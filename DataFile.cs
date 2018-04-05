using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	/// <summary>
	/// The core class that can read a configuration describing the tachograph data structure (one
    /// for driver cards, one for vehicle cards), then read the file itself into memory, then finally
    /// write the data as XML.
	/// </summary>
	public class DataFile : Region
	{
		public ArrayList regions=new ArrayList();

		/// <summary>
		/// This method loads the XML config file into a new instance
		/// of VuDataFile, prior to processing.
		/// </summary>
		/// <param name="configFile">Config to load</param>
		/// <returns>A new instance ready for processing</returns>
		// 
		public static DataFile Create(string configFile)
		{
			XmlReader xtr=XmlReader.Create(File.OpenRead(configFile));
			return Create(xtr);
		}

		protected static DataFile Create(XmlReader xtr)
		{
			XmlSerializer xs=new XmlSerializer(typeof(DataFile));
			return (DataFile) xs.Deserialize(xtr);
		}

		/// Convenience method to open a file and process it
		public void Process(string dataFile, XmlWriter writer)
		{
            WriteLine(LogLevel.INFO, "Processing {0}", dataFile);
            Stream s = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
            Process(s, writer);
		}

        public void Process(Stream s, XmlWriter writer)
        {
            CustomBinaryReader r = new CustomBinaryReader(s);
            Process(r, writer);
        }

		/// This is the core method overridden by all subclasses of Region
		// TODO: M: very inefficient if no matches found - will iterate over WORDs to end of file
		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			WriteLine(LogLevel.DEBUG, "Processing...");

			var unmatchedRegions=0;
			// in this case we read a magic and try to process it
			while ( true )
			{
				byte[] magic=new byte[2];
				int bytesRead=reader.BaseStream.Read(magic, 0, 2);
				long magicPos = reader.BaseStream.Position - 2;

				if ( bytesRead == 0 )
					// end of file - nothing more to read
					break;

				if ( bytesRead == 1 )
					// this can happen if zipping over unmatched bytes at end of file - should handle better
					//					throw new InvalidOperationException("Could only read one byte of identifier at end of stream");
					break;

				// test whether the magic matches one of our child objects
				string magicString=string.Format("0x{0:X2}{1:X2}", magic[0], magic[1]);
				bool matched = false;
				foreach ( IdentifiedObjectRegion r in regions )
				{
					if ( r.Matches(magicString) )
					{
						WriteLine(LogLevel.DEBUG, "Identified region: {0} with magic {1} at 0x{2:X4}", r.Name, magicString, magicPos);
						r.Process(reader, writer);
						matched = true;
						break;
					}
				}

				if ( !matched ) {
					unmatchedRegions++;
					if ( unmatchedRegions == 1 ) {
						WriteLine(LogLevel.WARN, "First unrecognised region with magic {1} at 0x{1:X4}", magicString, magicPos);
					}
				}
// commenting @davispuh change because some files have unknown sections, so we take a brute
// for approach and just skip over any unrecognised data
				// if (!matched)
				// {
				// 	WriteLine(LogLevel.WARN, "Unrecognized magic=0x{0:X2}{1:X2} at offset 0x{2:X4}  ", magic[0], magic[1], magicPos);
				// 	throw new NotImplementedException("Unrecognized magic " + magicString);
				// }
			}
			if ( unmatchedRegions > 0 ) {
				WriteLine(LogLevel.WARN, "There were {0} unmatched regions (magics) in the file.", unmatchedRegions);
			}
			WriteLine(LogLevel.DEBUG, "Processing done.");

		}

		/// This defines what children we can have from the XML config
		[XmlElement("IdentifiedObject", typeof(IdentifiedObjectRegion)),
		XmlElement("ElementaryFile", typeof(ElementaryFileRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}
	}
	/// Simple subclass to hold the identified or 'magic' for the region (used by VuDataFile above)
	public class IdentifiedObjectRegion : ContainerRegion
	{
		[XmlAttribute]
		public string Identifier;

		public bool Matches(string s)
		{
			// match a magic if we have null identifier or it actually matches
			// (allows provision of a catch all region which is only really useful during development)
			return Identifier == null || Identifier.Length == 0 || s.Equals(Identifier);
		}
	}

	public class ElementaryFileRegion : IdentifiedObjectRegion
	{
		protected override bool SuppressElement(CustomBinaryReader reader)
		{
			int type=reader.PeekChar();
			return type == 0x01;
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// read the type
			byte type=reader.ReadByte();

			regionLength=reader.ReadSInt16();
			long fileLength=regionLength;

			if ( type == 0x01 )
			{
				// this is just the signature
			}
			else
			{
				long start=reader.BaseStream.Position;

				base.ProcessInternal(reader, writer);

				long amountProcessed=reader.BaseStream.Position-start;
				fileLength -= amountProcessed;
			}

			if ( fileLength > 0 )
			{
				// deal with a remaining fileLength that is greater than int
				while ( fileLength > int.MaxValue )
				{
					reader.ReadBytes(int.MaxValue);
					fileLength-=int.MaxValue;
				}
				reader.ReadBytes((int) fileLength);
			}
		}
	}

	public class CyclicalActivityRegion : ContainerRegion
	{
		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			if (regionLength <= 4)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion is empty!");
				return;
			}

			uint oldest=reader.ReadSInt16();
			uint newest=reader.ReadSInt16();

			// length is length of region minus the bytes we've just read
			long effectiveLength=regionLength - 4;
			long position=reader.BaseStream.Position;

			if (position + effectiveLength > reader.BaseStream.Length)
			{
				WriteLine(LogLevel.WARN, "CyclicalActivityRegion position=0x{0:X4} + effectiveLength=0x{1:X4} > length=0x{2:X4} !", position, effectiveLength, reader.BaseStream.Length);
				return;
			}

			WriteLine(LogLevel.DEBUG, "Oldest 0x{0:X4} (offset 0x{2:X4}), newest 0x{1:X4} (offset 0x{3:X4})",
				position+oldest, position+newest,
				oldest, newest);

			if ( oldest == newest && oldest == 0 )
				// no data in file
				return;

			if ( newest >= effectiveLength || oldest >= effectiveLength)
			{
				throw new IndexOutOfRangeException("Invalid pointer to CyclicalActivity Record");
			}

			CyclicStream cyclicStream=new CyclicStream(reader.BaseStream, reader.BaseStream.Position, effectiveLength);
			CustomBinaryReader cyclicReader=new CustomBinaryReader(cyclicStream);

			reader.BaseStream.Seek(oldest, SeekOrigin.Current);

			bool last=false;
			while ( !last )
			{
				long pos=cyclicStream.Position;
				if ( pos == newest )
					last=true;

				base.ProcessInternal(cyclicReader, writer);
				// commenting @davispuh mod because it can cause premature termination
				// see https://github.com/jugglingcats/tachograph-reader/issues/28
				// if (cyclicStream.Wrapped)
				// {
				// 	last = true;
				// }
			}

			reader.BaseStream.Position = position + effectiveLength;

			writer.WriteElementString("DataBufferIsWrapped", cyclicStream.Wrapped.ToString());
		}
	}

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

			writer.WriteAttributeString("DateTime", recordDate.ToString());
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

	/// Simple logging - can be set on any region. Could be improved
	// TODO: M: make log level command line option
	public enum LogLevel
	{
		NONE=0,
		DEBUG=1,
		INFO=2,
		WARN=3,
		ERROR=4
	}

	/// Abstract base class for all regions. Holds some convenience methods
	public abstract class Region
	{
		// All regions have a name which becomes the XML element on output
		[XmlAttribute]
		public string Name;

		[XmlAttribute]
		public bool GlobalValue;

		[XmlAttribute]
		public LogLevel LogLevel=LogLevel.INFO;

		protected long byteOffset;
		protected long regionLength=0;
		protected static Hashtable globalValues=new Hashtable();
		protected static readonly String[] countries = new string[] {"No information available",
			"Austria","Albania","Andorra","Armenia","Azerbaijan","Belgium","Bulgaria","Bosnia and Herzegovina",
			"Belarus","Switzerland","Cyprus","Czech Republic","Germany","Denmark","Spain","Estonia","France",
			"Finland","Liechtenstein","Faeroe Islands","United Kingdom","Georgia","Greece","Hungary","Croatia",
			"Italy","Ireland","Iceland","Kazakhstan","Luxembourg","Lithuania","Latvia","Malta","Monaco",
			"Republic of Moldova","Macedonia","Norway","Netherlands","Portugal","Poland","Romania","San Marino",
			"Russian Federation","Sweden","Slovakia","Slovenia","Turkmenistan","Turkey","Ukraine","Vatican City",
			"Yugoslavia"};

		public void Process(CustomBinaryReader reader, XmlWriter writer)
		{
			// Store start of region (for logging only)
			byteOffset=reader.BaseStream.Position;

			bool suppress=SuppressElement(reader);

			// Write a new output element
			if ( !suppress )
				writer.WriteStartElement(Name);

			// Call subclass process method
			ProcessInternal(reader, writer);

			// End the element
			if ( !suppress )
				writer.WriteEndElement();

			long endPosition=reader.BaseStream.Position;
			if ( reader.BaseStream is CyclicStream )
				endPosition=((CyclicStream) reader.BaseStream).ActualPosition;

			WriteLine(LogLevel.DEBUG, "{0} [0x{1:X4}-0x{2:X4}/0x{3:X4}] {4}", Name, byteOffset,
				endPosition, endPosition-byteOffset, ToString());

			if ( GlobalValue )
			{
                globalValues[Name] = ToString();
			}
		}

		protected void WriteLine(LogLevel level, string format, params object[] args)
		{
			if ( level >= LogLevel )
				Console.WriteLine(format, args);
		}

		protected abstract void ProcessInternal(CustomBinaryReader reader, XmlWriter writer);

		protected virtual bool SuppressElement(CustomBinaryReader reader)
		{
			// derived classes can override this to suppress the writing of
			// a wrapper element. Used by the ElementaryFileRegion to suppress
			// altogether the signature blocks that occur for some regions
			return false;
		}

		public long RegionLength
		{
			set { regionLength=value; }
		}
	}

	/// Generic class for a region that contains other regions. Used as a simple wrapper
	/// where this is indicated in the specification
	public class ContainerRegion : Region
	{
		public ArrayList regions=new ArrayList();

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// iterate over all child regions and process them
			foreach ( Region r in regions )
			{
				r.RegionLength=regionLength;
				r.Process(reader, writer);
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
		XmlElement("HexValue", typeof(HexValueRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}
	}

	/// Simple class to "eat" a specified number of bytes in the file
	public class PaddingRegion : Region
	{
		[XmlAttribute]
		public int Size;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byte[] buf=new byte[Size];
			int amountRead=reader.Read(buf, 0, Size);
			if ( amountRead != Size )
				throw new InvalidOperationException("End of file reading padding (size "+Size+")");
		}

		public override string ToString()
		{
			return string.Format("{0} bytes (0x{0:X4})", Size);
		}

	}

	// A string that is non-international consisting of a specified number of bytes
	public class SimpleStringRegion : Region
	{
		[XmlAttribute]
		public int Length;
		protected string text;

		public SimpleStringRegion()
		{
		}

		// this is for the benefit of subclasses
		public SimpleStringRegion(int length)
		{
			this.Length=length;
		}

		// method that will read string from file in specified encoding
		protected void ProcessInternal(CustomBinaryReader s, Encoding enc)
		{
			text=s.ReadString(Length, enc).Trim();
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// we just use the default encoding in the default case
			this.ProcessInternal(reader, Encoding.ASCII);
			writer.WriteString(text);
		}

		public override string ToString()
		{
			return text;
		}

	}

	// A string that is prefixed by a code page byte
	public class CodePageStringRegion : SimpleStringRegion
	{
		static Dictionary<string, Encoding> encodingCache = new Dictionary<string, Encoding>();
		static Dictionary<byte, string> charsetMapping = new Dictionary<byte, string>();

		// private int codepage;

		static CodePageStringRegion() {
			foreach ( var i in Encoding.GetEncodings() ) {
				encodingCache.Add(i.Name.ToUpper(), i.GetEncoding());
			}

			charsetMapping[0] = "ASCII";
			charsetMapping[1] = "ISO-8859-1";
			charsetMapping[2] = "ISO-8859-2";
			charsetMapping[3] = "ISO-8859-3";
			charsetMapping[5] = "ISO-8859-5";
			charsetMapping[7] = "ISO-8859-7";
			charsetMapping[9] = "ISO-8859-9";
			charsetMapping[13] = "ISO-8859-13";
			charsetMapping[15] = "ISO-8859-15";
			charsetMapping[16] = "ISO-8859-16";
			charsetMapping[80] = "KOI8-R";
			charsetMapping[85] = "KOI8-U";

			// CodePagesEncodingProvider (System.Text.Encoding.CodePages package) on .NET Core by default (GetEncodings() method) supports only few encodings
			// https://msdn.microsoft.com/en-us/library/system.text.codepagesencodingprovider.aspx#Anchor_4
			// but if you call GetEncoding directly by name you can get other encodings too
			// so here we add those too to our cache
			foreach (var encodingName in charsetMapping.Values) {
				if (!encodingCache.ContainsKey(encodingName)) {
					try {
						var encoding = Encoding.GetEncoding(encodingName);
						encodingCache.Add(encodingName, encoding);
					} catch (ArgumentException e) {
						Console.WriteLine("Warning! Current platform doesn't support encoding with name {0}\n{1}", encodingName, e.Message);
					}
				}
			}
		}

		public CodePageStringRegion()
		{
		}

		public CodePageStringRegion(int size) : base(size)
		{
		}

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// get the codepage
			var codepage=reader.ReadByte();
			// codePage specifies the part of the ISO/IEC 8859 used to code this string

			string encodingName = charsetMapping.GetValueOrDefault(codepage, "UNKNOWN");
			Encoding enc=encodingCache.GetValueOrDefault(encodingName, null);
			if ( enc == null) {
				// we want to warn if we didn't recognize codepage because using wrong codepage will cause use of wrong codepoints and thus incorrect data
				WriteLine(LogLevel.WARN, "Unknown codepage {0}", codepage);
				enc=Encoding.ASCII;
			}

			// read string using encoding
			base.ProcessInternal(reader, enc);
			writer.WriteString(text);
		}
	}

	// A name is a string with codepage with fixed length = 35
	public class NameRegion : CodePageStringRegion
	{
		private static readonly int SIZE=35;

		public NameRegion() : base(SIZE)
		{
		}
	}

	public class HexValueRegion : Region
	{
		[XmlAttribute]
		public int Length;
		private byte[] values;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			values=new byte[Length];

			for ( int n=0; n< Length; n++ )
				values[n]=reader.ReadByte();

			writer.WriteAttributeString("Value", this.ToString());
		}

		public override string ToString()
		{
			if ( values == null )
				return "(null)";

			return ToHexString(values);
		}

		public static string ToHexString(byte[] values)
		{
			StringBuilder sb=new StringBuilder(values.Length*2+2);
			sb.Append("0x");
			foreach ( byte b in values )
				sb.AppendFormat("{0:X2}", b);			

			return sb.ToString();
		}

	}

	// See page 72. Have written class for this as seen in multiple places
	public class ExtendedSerialNumberRegion : Region
	{
		private uint serialNumber;
		private byte month;
		private byte year;
		private byte type;
		private byte manufacturerCode;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			serialNumber=reader.ReadSInt32();
			// BCD coding of Month (two digits) and Year (two last digits)
			uint monthYear=reader.ReadBCDString(2);
			type=reader.ReadByte();
			manufacturerCode=reader.ReadByte();

			month = (byte)(monthYear / 100);
			year = (byte)(monthYear % 100);

			writer.WriteAttributeString("Month", month.ToString());
			writer.WriteAttributeString("Year", year.ToString());
			writer.WriteAttributeString("Type", type.ToString());
			writer.WriteAttributeString("ManufacturerCode", manufacturerCode.ToString());

			writer.WriteString(serialNumber.ToString());
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}/{2}, type={3}, manuf code={4}",
				serialNumber, month, year, type, manufacturerCode);
		}
	}

	// see page 83 - 4 byte second offset from midnight 1 January 1970
	public class TimeRealRegion : Region
	{
		private DateTime dateTime;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			dateTime=reader.ReadTimeReal();

			writer.WriteAttributeString("DateTime", dateTime.ToString());
		}

		public override string ToString()
		{
			return string.Format("{0}", dateTime);
		}
	}

    // see page 56 (BCDString) and page 69 (Datef) - 2 byte encoded date in yyyy mm dd format
    public class DatefRegion : Region
    {
        private DateTime dateTime;

        protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
        {
            uint year = reader.ReadBCDString(2);
            uint month = reader.ReadBCDString(1);
            uint day = reader.ReadBCDString(1);

            string dateTimeString = null;
            // year 0, month 0, day 0 means date isn't set
            if (year > 0 || month > 0 || day > 0)
            {
                dateTime = new DateTime((int)year, (int)month, (int)day);
                dateTimeString = dateTime.ToString();
            }

            writer.WriteAttributeString("Datef", dateTimeString);
        }

        public override string ToString()
        {
            return string.Format("{0}", dateTime);
        }
    }

    public enum EquipmentType
	{
		DriverCard=1,
		WorkshopCard=2,
		ControlCard=3
		// TODO: M: there are more
	}

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

	// see page 72 - we only support driver cards
	public class FullCardNumberRegion : CardNumberRegion
	{
		EquipmentType type;
		byte issuingMemberState;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			type=(EquipmentType) reader.ReadByte();
			issuingMemberState=reader.ReadByte();

			writer.WriteAttributeString("Type", type.ToString());
			writer.WriteAttributeString("IssuingMemberState", issuingMemberState.ToString());

			base.ProcessInternal(reader, writer);
		}

		public override string ToString()
		{
			return string.Format("type={0}, {1}, {2}, {3}, {4}",
				type, issuingMemberState, driverIdentification, replacementIndex, renewalIndex);
		}
	}

	// 3 byte number, as used by OdometerShort (see page 86)
	public class UInt24Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			uintValue=reader.ReadSInt24();
			writer.WriteString(uintValue.ToString());
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}
	}

	public class UInt16Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			uintValue=reader.ReadSInt16();
			writer.WriteString(uintValue.ToString());
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}
	}

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

	public class UInt8Region : Region
	{
		private byte byteValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			byteValue=reader.ReadByte();
			writer.WriteString(byteValue.ToString());
		}

		public override string ToString()
		{
			return byteValue.ToString();
		}
	}

	public class BCDStringRegion : Region
	{
		[XmlAttribute]
		public int Size;
		private uint value;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			value=reader.ReadBCDString(Size);
			writer.WriteString(value.ToString());
		}

		public override string ToString()
		{
			return value.ToString();
		}
	}

	// Simple class to represent a boolean (0 or 1 in specification)
	public class FlagRegion : Region
	{
		private bool boolValue;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			boolValue=reader.ReadByte() > 0;
			writer.WriteString(boolValue.ToString());
		}

		public override string ToString()
		{
			return boolValue.ToString();
		}
	}

	public enum SizeAllocation
	{
		BYTE,
		WORD
	}

	// A collection region is a repeating region prefixed by the count of number of
	// items in the region. The count can be represented by a single byte or a word,
	// depending on the collection, so this supports a SizeAllocation property to specify
	// which it is.
	public class CollectionRegion : ContainerRegion
	{
		[XmlAttribute]
		public SizeAllocation SizeAlloc=SizeAllocation.BYTE;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// get the count according to allocation size
			uint count;
			switch (SizeAlloc)
			{
				case SizeAllocation.BYTE:
					count=reader.ReadByte();
					break;

				case SizeAllocation.WORD:
					count=reader.ReadSInt16();
					break;

				default:
					throw new InvalidOperationException("Bad size allocation");
			}

			ProcessItems(reader, writer, count);
		}

		protected void ProcessItems(CustomBinaryReader reader, XmlWriter writer, uint count)
		{
			WriteLine(LogLevel.DEBUG, "Processing repeating {0}, count={1}, offset=0x{2:X4}", Name, count, reader.BaseStream.Position);

			// repeat processing of child objects
			uint maxCount = count;
			while ( count > 0 )
			{
				try
				{
					base.ProcessInternal(reader, writer);
					count--;
				} catch (EndOfStreamException ex)
				{
					WriteLine(LogLevel.ERROR, "Repeating {0}, count={1}/{2}: {3}", Name, count, maxCount, ex);
					break;
				}
			}
		}

		public override string ToString()
		{
			return string.Format("<< end {0}", Name);
		}
	}

	public class RepeatingRegion : CollectionRegion
	{
		[XmlAttribute]
		public uint Count;

		[XmlAttribute]
		public string CountRef;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			if ( Count == 0 && CountRef != null )
			{
				string refName=CountRef.Substring(1);
				if (globalValues.Contains(refName))
				{
					Count=uint.Parse((string) globalValues[refName]);
				} else
				{
					WriteLine(LogLevel.WARN, "RepeatingRegion {0} doesn't contain ref {1}", Name, refName);
				}
			}
			ProcessItems(reader, writer, Count);
		}
	}

	public enum Activity
	{
		Break,
		Available,
		Work,
		Driving
	}

	// This is the activity change class. It has own class because the fields
	// are packed into two bytes which we need to unpack (see page 55).
	public class ActivityChangeRegion : Region
	{
		byte slot;
		byte status;
		bool inserted;
		Activity activity;
		uint time;

		protected override void ProcessInternal(CustomBinaryReader reader, XmlWriter writer)
		{
			// format: scpaattt tttttttt (16 bits)
			// s = slot, c = crew status, p = card inserted, a = activity, t = time
			byte b1=reader.ReadByte();
			byte b2=reader.ReadByte();

			slot     = (byte)     ((b1 >> 7) & 0x01);      // 7th bit
			status   = (byte)     ((b1 >> 6) & 0x01);      // 6th bit
			inserted =            ((b1 >> 5) & 0x01) == 0; // 5th bit
			activity = (Activity) ((b1 >> 3) & 0x03);      // 4th and 3rd bits
			time     = (((uint)b1 & 0x07) << 8) | b2;      // 0th, 1st, 2nd bits from b1

			if ( this.LogLevel == LogLevel.DEBUG || this.LogLevel == LogLevel.INFO )
			{
				long position=reader.BaseStream.Position;
				if ( reader.BaseStream is CyclicStream )
					position=((CyclicStream) reader.BaseStream).ActualPosition;

				writer.WriteAttributeString("FileOffset", string.Format("0x{0:X4}", position));
			}
			writer.WriteAttributeString("Slot", slot.ToString());
			writer.WriteAttributeString("Status", status.ToString());
			writer.WriteAttributeString("Inserted", inserted.ToString());
			writer.WriteAttributeString("Activity", activity.ToString());
			writer.WriteAttributeString("Time", string.Format("{0:d2}:{1:d2}", time / 60, time % 60));
		}

		public override string ToString()
		{
			return string.Format("slot={0}, status={1}, inserted={2}, activity={3}, time={4:d2}:{5:d2}",
				slot, status, inserted, activity, time / 60, time % 60);
		}

	}
}
