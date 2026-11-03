using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class RepeatingRegion : CollectionRegion
	{
		[XmlAttribute]
		public uint Count;

		[XmlAttribute]
		public string CountRef;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			uint count = Count;
			if ( CountRef != null )
			{
				string refName=CountRef.Substring(1);
				if (globalValues.Contains(refName))
				{
					count=uint.Parse((string) globalValues[refName]);
				} else
				{
					throw new ReaderException("Missing count reference " + refName);
				}
			}
			ProcessItems(reader, count);
		}
	}
}
