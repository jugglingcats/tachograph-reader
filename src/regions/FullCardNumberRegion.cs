using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public enum EquipmentType
	{
		DriverCard=1,
		WorkshopCard=2,
		ControlCard=3
		// TODO: M: there are more
	}

	// see page 72 - we only support driver cards
	public class FullCardNumberRegion : CardNumberRegion
	{
		EquipmentType type;
		byte issuingMemberState;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			type=(EquipmentType) reader.ReadByte();
			issuingMemberState=reader.ReadByte();

			base.ProcessInternal(reader);
		}

		public override string ToString()
		{
			return string.Format("type={0}, {1}, {2}, {3}, {4}",
				type, issuingMemberState, driverIdentification, replacementIndex, renewalIndex);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("Type", type.ToString());
			writer.WriteAttributeString("IssuingMemberState", issuingMemberState.ToString());

			base.InternalToXML(writer);
		}
	}
}
