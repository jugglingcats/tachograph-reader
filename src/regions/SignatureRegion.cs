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
			Validator.ValidateGen1(reader.ReadBytes(SignatureRegion.GetSignedDataLength()), this.ToBytes());

			reader.BaseStream.Position = currentOffset;
		}
	}
}
