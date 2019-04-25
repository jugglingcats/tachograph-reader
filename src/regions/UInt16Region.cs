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

		public uint ToUInt()
		{
			return this.uintValue;
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteString(this.ToString());
		}
	}
}
