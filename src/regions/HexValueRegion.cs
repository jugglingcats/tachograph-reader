using System;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class HexValueRegion : Region
	{
		[XmlAttribute]
		public int Length;
		private byte[] values;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			values=new byte[Length];

			for ( int n=0; n< Length; n++ )
				values[n]=reader.ReadByte();
		}

		public override string ToString()
		{
			if ( values == null )
				return "(null)";

			return ToHexString(values);
		}

		public byte[] ToBytes()
		{
			return this.values;
		}

		public static string ToHexString(byte[] values)
		{
			StringBuilder sb=new StringBuilder(values.Length*2+2);
			sb.Append("0x");
			foreach ( byte b in values )
				sb.AppendFormat("{0:X2}", b);

			return sb.ToString();
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("Value", this.ToString());
		}

	}
}
