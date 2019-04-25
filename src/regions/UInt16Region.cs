using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
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
}
