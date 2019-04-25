using System;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	public enum Activity
	{
		Break,
		Available,
		Work,
		Driving
	}

	// This is the activity change class. It has own class because the fields
	// are packed into two bytes which we need to unpack (see page 55).
	public class ActivityChangeRegion : Region
	{
		byte slot;
		byte status;
		bool inserted;
		Activity activity;
		uint time;

		long position;

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			// format: scpaattt tttttttt (16 bits)
			// s = slot, c = crew status, p = card inserted, a = activity, t = time
			byte b1=reader.ReadByte();
			byte b2=reader.ReadByte();

			slot     = (byte)     ((b1 >> 7) & 0x01);      // 7th bit
			status   = (byte)     ((b1 >> 6) & 0x01);      // 6th bit
			inserted =            ((b1 >> 5) & 0x01) == 0; // 5th bit
			activity = (Activity) ((b1 >> 3) & 0x03);      // 4th and 3rd bits
			time     = (((uint)b1 & 0x07) << 8) | b2;      // 0th, 1st, 2nd bits from b1

			if ( this.LogLevel == LogLevel.DEBUG || this.LogLevel == LogLevel.INFO )
			{
				this.position=reader.BaseStream.Position;
				if ( reader.BaseStream is CyclicStream )
					this.position=((CyclicStream) reader.BaseStream).ActualPosition;
			}
		}

		public override string ToString()
		{
			return string.Format("slot={0}, status={1}, inserted={2}, activity={3}, time={4:d2}:{5:d2}",
				slot, status, inserted, activity, time / 60, time % 60);
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			if ( this.LogLevel == LogLevel.DEBUG || this.LogLevel == LogLevel.INFO )
			{
				writer.WriteAttributeString("FileOffset", string.Format("0x{0:X4}", this.position));
			}
			writer.WriteAttributeString("Slot", slot.ToString());
			writer.WriteAttributeString("Status", status.ToString());
			writer.WriteAttributeString("Inserted", inserted.ToString());
			writer.WriteAttributeString("Activity", activity.ToString());
			writer.WriteAttributeString("Time", string.Format("{0:d2}:{1:d2}", time / 60, time % 60));
		}

	}
}
