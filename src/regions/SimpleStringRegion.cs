using System;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
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

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			// we just use the default encoding in the default case
			this.ProcessInternal(reader, Encoding.ASCII);
		}

		public override string ToString()
		{
			return text;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
