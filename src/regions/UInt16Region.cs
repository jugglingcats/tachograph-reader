using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public class UInt16Region : Region
	{
		private uint uintValue;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			uintValue=reader.ReadSInt16();
		}

		public override string ToString()
		{
			return uintValue.ToString();
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
