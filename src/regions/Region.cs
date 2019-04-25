using System;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
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
}
