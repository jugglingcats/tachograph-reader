using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public enum EquipmentType
	{
		DriverCard=1,
		WorkshopCard=2,
		ControlCard=3,
		CompanyCard=4
		// TODO: M: there are more
	}

	// See page 72; company card numbers have a separate consecutive index.
	public class FullCardNumberRegion : CardNumberRegion
	{
		EquipmentType type;
		byte issuingMemberState;
		byte? consecutiveIndex;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			consecutiveIndex = null;
			type=(EquipmentType) reader.ReadByte();
			issuingMemberState=reader.ReadByte();

			if (type == EquipmentType.CompanyCard || type == EquipmentType.ControlCard || type == EquipmentType.WorkshopCard)
			{
				driverIdentification = reader.ReadString(13);
				consecutiveIndex = reader.ReadByte();
				replacementIndex = reader.ReadByte();
				renewalIndex = reader.ReadByte();
			}
			else base.ProcessInternal(reader);
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

			if (consecutiveIndex.HasValue) writer.WriteAttributeString("ConsecutiveIndex", consecutiveIndex.Value.ToString());
			base.InternalToXML(writer);
		}
	}
}
