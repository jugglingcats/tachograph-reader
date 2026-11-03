using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DataFileReader
{
    /// <summary>Appendix 7 card TLVs from DF Tachograph_G2, types 02 and 03.</summary>
    public sealed class Gen2CardRegion : IdentifiedObjectRegion
    {
        [XmlIgnore] public byte BlockType { get; private set; }
        [XmlIgnore] public int Version { get; private set; }
        [XmlIgnore] public XElement Data { get; private set; }
        [XmlIgnore] public byte[] Signature { get; private set; }
        internal Gen2CardContext Context { get; set; }

        private static string FileName(int fid)
        {
            switch (fid)
            {
                case 0x0002: return "CardIccIdentificationGen2";
                case 0x0005: return "CardChipIdentificationGen2";
                case 0x0501: return "DriverCardApplicationIdentificationGen2";
                case 0x0502: return "EventsDataGen2";
                case 0x0503: return "FaultsDataGen2";
                case 0x0504: return "DriverActivityDataGen2";
                case 0x0505: return "CardVehiclesUsedGen2";
                case 0x0506: return "CardPlaceDailyWorkPeriodGen2";
                case 0x0507: return "CardCurrentUseGen2";
                case 0x0508: return "CardControlActivityDataRecordGen2";
                case 0x050E: return "CardDownloadGen2";
                case 0x0520: return "IdentificationGen2";
                case 0x0521: return "CardDrivingLicenceInformationGen2";
                case 0x0522: return "SpecificConditionsGen2";
                case 0x0523: return "CardVehicleUnitsUsedGen2";
                case 0x0524: return "GNSSAccumulatedDrivingGen2";
                case 0x0525: return "DriverCardApplicationIdentificationV2";
                case 0x0526: return "PlacesAuthentication";
                case 0x0527: return "GNSSPlacesAuthentication";
                case 0x0528: return "CardBorderCrossings";
                case 0x0529: return "CardLoadUnloadOperations";
                case 0x0530: return "CardLoadTypeEntries";
                case 0x0540: return "VuConfiguration";
                case 0xC100: return "CardMACertificateGen2";
                case 0xC101: return "CardSignCertificateGen2";
                case 0xC108: return "CACertificateGen2";
                case 0xC109: return "LinkCertificateGen2";
                default: throw new NotSupportedException("Unsupported Gen2 card EF: " + fid.ToString("X4"));
            }
        }

        protected override void ProcessInternal(CustomBinaryReader reader)
        {
            Data = null;
            Signature = null;
            if (Context == null) throw new ReaderException("Gen2 card decoding requires a file processing context");
            int fid = Convert.ToInt32(Identifier.Substring(2), 16);
            Name = FileName(fid);
            BlockType = reader.ReadByte();
            if (BlockType != 2 && BlockType != 3) throw new ReaderException("Invalid Gen2 card TLV type");
            int length = (int)reader.ReadSInt16();
            if (length == 0 || length == 65535) throw new ReaderException("Invalid card TLV length");
            if (Validator.ValidateSignatures) throw new NotSupportedException("Gen2 ECC signature verification is not implemented");
            var bytes = reader.ReadExactlyBytes(length);
            Version = Context.Version;
            if (BlockType == 3)
            {
                if (fid >= 0xC100 || fid == 0x0002 || fid == 0x0005) throw new ReaderException("Unexpected signature for unsigned EF");
                if (length != 64 && length != 96 && length != 128 && length != 132) throw new ReaderException("Invalid ECC signature size");
                Signature = bytes;
                return;
            }
            using (var record = new Gen2RecordReader(bytes, Name, 1))
            {
                record.DecodeCard(fid, Context);
                if (record.Remaining != 0) throw new ReaderException("Card EF length does not match " + Name);
                Data = record.Element;
            }
        }

        protected override void InternalToXML(XmlWriter writer)
        {
            writer.WriteAttributeString("Generation", "2");
            if (Version != 0) writer.WriteAttributeString("Version", Version.ToString());
            writer.WriteAttributeString("BlockType", BlockType.ToString());
            writer.WriteAttributeString("SignatureStatus", "NotVerified");
            if (Signature != null) writer.WriteElementString("Signature", HexValueRegion.ToHexString(Signature));
            else foreach (var element in Data.Elements()) element.WriteTo(writer);
        }
    }

    // One instance per Process invocation. Never mixes Gen1 counts or reused readers.
    internal sealed class Gen2CardContext
    {
        internal int Version { get; set; }
        internal readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        internal int Count(string name)
        {
            int count;
            if (!Counts.TryGetValue(name, out count)) throw new ReaderException("Missing Gen2 application count: " + name);
            return count;
        }
        internal static Gen2CardContext ReadApplications(CustomBinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            byte[] application = null, extension = null;
            var context = new Gen2CardContext();
            try
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int fid = (int)reader.ReadSInt16();
                    int type = reader.ReadByte();
                    int length = (int)reader.ReadSInt16();
                    if (type > 3 || length == 0 || length == 65535 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                        throw new ReaderException("Invalid card TLV header");
                    if (type == 2 && (fid == 0x0501 || fid == 0x0525))
                    {
                        if (fid == 0x0501)
                        {
                            if (application != null) throw new ReaderException("Duplicate Gen2 application identification");
                            application = reader.ReadExactlyBytes(length);
                        }
                        else
                        {
                            if (extension != null) throw new ReaderException("Duplicate Gen2 V2 application identification");
                            extension = reader.ReadExactlyBytes(length);
                        }
                    }
                    else reader.BaseStream.Position += length;
                }
                if (application != null) Load(application, 0x0501, context);
                if (extension != null) Load(extension, 0x0525, context);
                return context;
            }
            finally { reader.BaseStream.Position = start; }
        }
        private static void Load(byte[] bytes, int fid, Gen2CardContext context)
        {
            using (var record = new Gen2RecordReader(bytes, "Application", 1))
            {
                record.DecodeCard(fid, context);
                if (record.Remaining != 0) throw new ReaderException("Invalid Gen2 application length");
            }
        }
    }
}
