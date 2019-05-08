using System;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	public class ElementaryFileRegion : IdentifiedObjectRegion
	{
		[XmlAttribute]
		public bool Unsigned = false;

		[XmlIgnore]
		public byte[] signature {get; private set;} = null;

		public bool IsSignature(CustomBinaryReader reader)
		{
			int type=reader.PeekChar();
			return type == 0x01;
		}

		protected override bool SuppressElement(CustomBinaryReader reader)
		{
			return this.IsSignature(reader);
		}

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			// read the type
			byte type=reader.ReadByte();

			regionLength=reader.ReadSInt16();
			long fileLength=regionLength;

			if ( type == 0x01 )
			{
				// this is just the signature
				this.signature = reader.ReadBytes((int)fileLength);
				fileLength = 0;

				long currentOffset = reader.BaseStream.Position;

				reader.BaseStream.Position = SignatureRegion.signedDataOffsetBegin;
				Validator.ValidateDelayedGen1(reader.ReadBytes(SignatureRegion.GetSignedDataLength()), this.signature);

				reader.BaseStream.Position = currentOffset;
			}
			else
			{
				long start=reader.BaseStream.Position;

				base.ProcessInternal(reader);

				long amountProcessed=reader.BaseStream.Position-start;
				fileLength -= amountProcessed;

				if (this.Name == "CardCertificate")
				{
					Validator.SetCertificate(this);
				} else if (this.Name == "CACertificate")
				{
					Validator.SetCACertificate(this);
				};
			}

			if ( fileLength > 0 )
			{
				// deal with a remaining fileLength that is greater than int
				while ( fileLength > int.MaxValue )
				{
					reader.ReadBytes(int.MaxValue);
					fileLength-=int.MaxValue;
				}
				reader.ReadBytes((int) fileLength);
			}
		}
	}
}
