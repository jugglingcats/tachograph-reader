using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Numerics;
using System.Reflection;
using System.Linq;
using System.Formats.Asn1;

namespace DataFileReader
{
	public class Validator
	{
		public static bool ValidateSignatures {get; set;} = false;
		private static Dictionary<CertificationAuthorityReference, RSAPublicKey> gen1RSAPublicKeys = null;

		private static EncryptedCertificateGen1 CACertificate = null;
		private static EncryptedCertificateGen1 Certificate = null;

		public static void SetCACertificate(Region certificateRegion)
		{
			Validator.CACertificate = new EncryptedCertificateGen1(certificateRegion);
		}

		public static void SetCertificate(Region certificateRegion)
		{
			Validator.Certificate = new EncryptedCertificateGen1(certificateRegion);
		}

		public class RSAPublicKey
		{
			public BigInteger Modulus;
			public BigInteger Exponent;

			public RSAPublicKey(BinaryReader reader)
			{
				this.Modulus = Validator.ToBigInteger(reader.ReadBytes(128));
				this.Exponent = Validator.ToBigInteger(reader.ReadBytes(8));
			}

			public byte[] Decrypt(BigInteger data)
			{
				var result = BigInteger.ModPow(data, this.Exponent, this.Modulus);

				#if (NETCOREAPP2_1 || NETCOREAPP2_2)
					return result.ToByteArray(true, true);
				#else
					// convert back to big-endian
					return result.ToByteArray().Reverse().ToArray();
				#endif
			}
		}

		public struct CertificationAuthorityReference : IEquatable<CertificationAuthorityReference>
		{
			public byte nation;
			public string nationCode;
			public byte serialNumber;
			public ushort additionalInfo;
			public byte caIdentifier;

			public CertificationAuthorityReference(Region region)
			{
				var container = ((ContainerRegion)region).ProcessedRegions;
				this.nation = ((CountryRegion)container["Nation"]).GetId();
				this.nationCode = ((SimpleStringRegion)container["NationCode"]).ToString();
				this.serialNumber = ((UInt8Region)container["SerialNumber"]).ToByte();
				this.additionalInfo = Convert.ToUInt16(((UInt16Region)container["AdditionalInfo"]).ToUInt());
				this.caIdentifier = ((UInt8Region)container["CaIdentifier"]).ToByte();
			}

			public CertificationAuthorityReference(BinaryReader reader)
			{
				this.nation = reader.ReadByte();
				this.nationCode = new String(reader.ReadChars(3)).Trim();
				this.serialNumber = reader.ReadByte();
				this.additionalInfo = reader.ReadUInt16();
				this.caIdentifier = reader.ReadByte();
			}

			public bool Equals(CertificationAuthorityReference other)
			{
				bool isEqual = this.nation == other.nation &&
				               this.nationCode.Equals(other.nationCode) &&
				               this.serialNumber == other.serialNumber &&
				               this.additionalInfo == other.additionalInfo &&
				               this.caIdentifier == other.caIdentifier;

				return isEqual;
			}

			public override bool Equals(object other)
			{
				if (other is null) return false;
				if (Object.ReferenceEquals(this, other)) return true;
				if (this.GetType() != other.GetType()) return false;
				return this.Equals((CertificationAuthorityReference)other);
			}

			public override int GetHashCode()
			{
				return (this.nation, this.nationCode, this.serialNumber, this.additionalInfo, this.caIdentifier).GetHashCode();
			}

			public override string ToString()
			{
				string formatString = "CertificationAuthorityReference: nation={0} (0x{0:X2}), nationCode={1}, serialNumber={2} (0x{2:X2}), " +
				                                                        "additionalInfo={3} (0x{3:X4}), caIdentifier={4} (0x{4:X2})";

				return string.Format(formatString, this.nation, this.nationCode, this.serialNumber,
				                                   this.additionalInfo, this.caIdentifier);
			}
		}

		public struct DigestAlgorithmIdentifier
		{
			public Oid algorithm;
			public byte[] parameters;

			public DigestAlgorithmIdentifier(AsnReader asnReader)
			{
				asnReader = asnReader.ReadSequence();
				this.algorithm = new Oid(asnReader.ReadObjectIdentifier());
				if (asnReader.PeekTag() == Asn1Tag.Null)
				{
					asnReader.ReadNull();
					this.parameters = null;
				} else
				{
					this.parameters = asnReader.ReadEncodedValue().ToArray();
				};

				asnReader.ThrowIfNotEmpty();
			}
		}

		public struct DigestInfo
		{
			public DigestAlgorithmIdentifier digestAlgorithm;
			public byte[] digest;

			public DigestInfo(byte[] asn1der)
			{
				var asnReader = new AsnReader(asn1der, AsnEncodingRules.DER).ReadSequence();
				this.digestAlgorithm = new DigestAlgorithmIdentifier(asnReader);
				this.digest = asnReader.ReadOctetString();

				asnReader.ThrowIfNotEmpty();
			}

			public bool VerifyData(byte[] data)
			{
				if (this.digestAlgorithm.algorithm.FriendlyName == "sha1")
				{
					using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
					{
						if (sha1.ComputeHash(data).SequenceEqual(this.digest))
						{
							return true;
						};
					};
				} else
				{
					throw new InvalidSignatureException(string.Format("Unknown DigestAlgorithm {0} ({1})!", this.digestAlgorithm.algorithm.FriendlyName,
					                                                                                        this.digestAlgorithm.algorithm.Value));
				};

				return false;
			}
		}

		public class SignatureMessageGen1
		{
			public byte zero;
			public byte one;
			public byte[] padding;
			public byte separator;
			public byte[] digestInfoData;

			public SignatureMessageGen1(byte[] message)
			{
				var size = 128;

				// European Root Certification Authority signing software has an off-by-one error...
				// so we need to prepend zero byte
				if (message.Length == size - 1)
				{
					var fullMessage = new byte[size];
					fullMessage[0] = 0x00;
					message.CopyTo(fullMessage, 1);
					message = fullMessage;
				};

				if (message.Length == size)
				{
					this.zero = message[0];
					this.one = message[1];
					this.padding = message.Skip(2).TakeWhile(m => m == 0xFF).ToArray();
					var position = 2 + this.padding.Length;
					this.separator = message[position];
					this.digestInfoData = message.Skip(position + 1).Take(size - position).ToArray();
				};
			}

			public bool IsValid()
			{
				return this.zero == 0x00 && this.one == 0x01 &&
				       this.padding.Length > 0 && this.padding[0] == 0xFF &&
				       this.separator == 0x00 && this.digestInfoData.Length > 0;
			}

			public DigestInfo GetDigestInfo()
			{
				if (this.IsValid())
				{
					return new DigestInfo(this.digestInfoData);
				};

				throw new InvalidSignatureException("Invalid SignatureMessage!");
			}
		}

		public class CertificateGen1
		{
			public int profileIdentifier;
			public CertificationAuthorityReference certificationAuthorityReference;
			public byte[] holderAuthorisation;
			public DateTimeOffset? endOfValidity;
			public byte[] holderReference;
			public RSAPublicKey publicKey;

			public CertificateGen1(byte[] certificateData)
			{
				using (var reader = new CustomBinaryReader(new MemoryStream(certificateData)))
				{
					this.profileIdentifier = Convert.ToInt32(reader.ReadByte());
					this.certificationAuthorityReference = new CertificationAuthorityReference(reader);
					this.holderAuthorisation = reader.ReadBytes(7);

					var timestamp = reader.ReadSInt32();
					if (timestamp != 0xFFFFFFFF)
					{
						this.endOfValidity = DateTimeOffset.FromUnixTimeSeconds(timestamp);
					} else
					{
						this.endOfValidity = null;
					};
					this.holderReference = reader.ReadBytes(8);
					this.publicKey = new RSAPublicKey(reader);
				};
			}

			public bool VerifyData(byte[] data, BigInteger signature, DateTimeOffset? newestDateTime)
			{
				if (this.IsValid(newestDateTime))
				{
					var message = new SignatureMessageGen1(this.publicKey.Decrypt(signature));
					return message.GetDigestInfo().VerifyData(data);
				};

				throw new InvalidSignatureException("Invalid Certificate!");
			}

			public bool IsValid(DateTimeOffset? newestDateTime)
			{
				if (this.endOfValidity != null)
				{
					// newestDateTime and endOfValidity can match and that's fine
					// because for DriverCard it has CardExpiryDate which can be same as certificate endOfValidity
					if (newestDateTime != null && this.endOfValidity.Value < newestDateTime.Value)
					{
						throw new ExpiredCertificateException(
						              string.Format("Certificate has expired! Data time {0} is after Certificate.endOfValidity {1}",
						              newestDateTime.Value.ToString("u"), this.endOfValidity.Value.ToString("u")));
					} else if (newestDateTime == null)
					{
						 Console.Error.WriteLine(string.Format("warn: {0}\n      Can't check Certificate.endOfValidity because no time value!", typeof(Validator)));
					};
				};

				return this.profileIdentifier == 0x01;
			}
		}

		public class CertificateMessageGen1
		{
			public byte j;
			public byte[] certificateHeader;
			public byte[] sha1;
			public byte BC;

			public CertificateMessageGen1(byte[] message)
			{
				if (message.Length != 128)
				{
					return;
				};

				this.j = message[0];
				this.certificateHeader = message.Skip(1).Take(106).ToArray();
				this.sha1 = message.Skip(107).Take(20).ToArray();
				this.BC = message[127];
			}

			public bool IsValid()
			{
				return this.j == 0x6A && this.BC == 0xBC;
			}

			public CertificateGen1 GetCertificate(byte[] publicKeyRemainder)
			{
				if (this.IsValid())
				{
					var certificateData = this.certificateHeader.Concat(publicKeyRemainder).ToArray();
					using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
					{
						if (sha1.ComputeHash(certificateData).SequenceEqual(this.sha1))
						{
							return new CertificateGen1(certificateData);
						};
					};
				};

				throw new InvalidSignatureException("Invalid CertificateMessage!");
			}
		}

		public class EncryptedCertificateGen1
		{
			public BigInteger signature;
			public byte[] publicKeyRemainder;
			public CertificationAuthorityReference certificationAuthorityReference;

			public EncryptedCertificateGen1(Region certificateRegion)
			{
				var container = ((ContainerRegion)certificateRegion).ProcessedRegions;
				this.signature = Validator.ToBigInteger(((HexValueRegion)container["Signature"]).ToBytes());
				this.publicKeyRemainder = ((HexValueRegion)container["PublicKeyRemainder"]).ToBytes();
				this.certificationAuthorityReference = new CertificationAuthorityReference(container["CertificationAuthorityReference"]);
			}

			public CertificateGen1 GetCertificate(RSAPublicKey publicKey)
			{
				return new CertificateMessageGen1(publicKey.Decrypt(this.signature)).GetCertificate(this.publicKeyRemainder);
			}
		}

		public static Dictionary<CertificationAuthorityReference, RSAPublicKey> LoadGen1RSAPublicKeys()
		{
			Assembly assembly = typeof(Validator).GetTypeInfo().Assembly;
			string name = assembly.FullName.Split(',')[0]+".EC_PK.bin";
			var reader = new BinaryReader(assembly.GetManifestResourceStream(name));

			var certificationAuthorityReference = new CertificationAuthorityReference(reader);
			var rsaPublicKey = new RSAPublicKey(reader);

			var rsaPublicKeys = new Dictionary<CertificationAuthorityReference, RSAPublicKey>();
			rsaPublicKeys.Add(certificationAuthorityReference, rsaPublicKey);

			return rsaPublicKeys;
		}

		public static Dictionary<CertificationAuthorityReference, RSAPublicKey> GetGen1RSAPublicKeys()
		{
			if (Validator.gen1RSAPublicKeys == null)
			{
				Validator.gen1RSAPublicKeys = Validator.LoadGen1RSAPublicKeys();
			}

			return Validator.gen1RSAPublicKeys;
		}

		private struct DataSignature
		{
			public byte[] data;
			public byte[] signature;
		}

		private static List<DataSignature> dataToValidate = new List<DataSignature>();

		public static void Reset()
		{
			Validator.CACertificate = null;
			Validator.Certificate = null;
			Validator.dataToValidate = new List<DataSignature>();
		}

		public static void CheckIfValidated(DateTimeOffset? newestDateTime)
		{
			Validator.ValidateAllDelayedGen1(() => { return newestDateTime; });
			if (Validator.dataToValidate.Count > 0)
			{
				throw new InvalidSignatureException(string.Format("{0} signatures weren't validated!", Validator.dataToValidate.Count));
			}
		}

		public static void ValidateDelayedGen1(byte[] data, byte[] signature, Func<DateTimeOffset?> getNewestDateTime)
		{
			if (!Validator.ValidateSignatures)
				return;

			Validator.dataToValidate.Add(new DataSignature {data=data, signature=signature});
			Validator.ValidateAllDelayedGen1(getNewestDateTime);
		}

		public static void ValidateAllDelayedGen1(Func<DateTimeOffset?> getNewestDateTime)
		{
			if (Validator.CACertificate != null && Validator.Certificate != null)
			{
				foreach (var data_signature in Validator.dataToValidate)
				{
					Validator.ValidateGen1(data_signature.data, data_signature.signature, getNewestDateTime());
				}
				Validator.dataToValidate.Clear();
			}
		}

		public static void ValidateGen1(byte[] data, byte[] signature, DateTimeOffset? newestDateTime)
		{
			if (!Validator.ValidateSignatures)
				return;

			if (Validator.CACertificate == null)
			{
				throw new InvalidSignatureException("CACertificate is not set!");
			}

			if (Validator.Certificate == null)
			{
				throw new InvalidSignatureException("Certificate is not set!");
			}

			var gen1RSAPublicKeys = Validator.GetGen1RSAPublicKeys();
			if (!gen1RSAPublicKeys.ContainsKey(Validator.CACertificate.certificationAuthorityReference))
			{
				throw new InvalidSignatureException("MemberStateCertificate is signed by unknown key!");
			}

			var certificate = Validator.CACertificate.GetCertificate(gen1RSAPublicKeys[Validator.CACertificate.certificationAuthorityReference]);
			if (!certificate.IsValid(newestDateTime))
			{
				throw new InvalidSignatureException("Invalid MemberStateCertificate!");
			}

			certificate = Validator.Certificate.GetCertificate(certificate.publicKey);

			if (!certificate.VerifyData(data, Validator.ToBigInteger(signature), newestDateTime))
			{
				throw new InvalidSignatureException("Signature doesn't match signed data!");
			}
		}

		private static BigInteger ToBigInteger(byte[] bytes)
		{
			#if (NETCOREAPP2_1 || NETCOREAPP2_2)
				return new BigInteger(bytes, true, true);
			#else
				// we need to convert from big-endian to unsigned little-endian for BigInteger
				return new BigInteger(bytes.Reverse().Append((byte)0x00).ToArray());
			#endif
		}

	}
}
