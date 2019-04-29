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
			ProcessItems(reader, Count);
		}
	}
}
