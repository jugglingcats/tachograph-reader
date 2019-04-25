using System;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
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

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			SignatureRegion.signedDataOffsetBegin = reader.BaseStream.Position;

			base.ProcessInternal(reader);

			SignatureRegion.signedDataOffsetEnd = reader.BaseStream.Position;
		}
	}
}
