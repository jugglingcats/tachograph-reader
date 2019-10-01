using System;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class SignatureRegion : HexValueRegion
	{
		public static long signedDataOffsetBegin {get; set;} = 0;
		public static long signedDataOffsetEnd {get; set;} = 0;
		public static DateTimeOffset? newestDateTime {get; set;} = null;

		public static void Reset()
		{
			SignatureRegion.signedDataOffsetBegin = 0;
			SignatureRegion.signedDataOffsetEnd = 0;
			SignatureRegion.newestDateTime = null;
		}

		public static void UpdateTime(DateTime dateTime)
		{
			SignatureRegion.UpdateTime((DateTimeOffset)DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
		}

		public static void UpdateTime(DateTimeOffset dateTime)
		{

			if (SignatureRegion.newestDateTime == null ||
			    (dateTime > SignatureRegion.newestDateTime.Value &&
			     // need to compare to current time to filter out dateTime from future...
			     dateTime < DateTimeOffset.Now))
			{
				SignatureRegion.newestDateTime = dateTime;
			}
		}

		public static int GetSignedDataLength()
		{
			return (int)(SignatureRegion.signedDataOffsetEnd - SignatureRegion.signedDataOffsetBegin);
		}

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			SignatureRegion.signedDataOffsetEnd = reader.BaseStream.Position;

			base.ProcessInternal(reader);

			long currentOffset = reader.BaseStream.Position;

			reader.BaseStream.Position = SignatureRegion.signedDataOffsetBegin;
			Validator.ValidateGen1(reader.ReadBytes(SignatureRegion.GetSignedDataLength()), this.ToBytes(), SignatureRegion.newestDateTime);

			reader.BaseStream.Position = currentOffset;
		}
	}
}
