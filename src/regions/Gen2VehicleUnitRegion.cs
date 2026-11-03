using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
namespace DataFileReader
{
    /// <summary>Annex IC Appendix 7 ESM vehicle-unit blocks, Gen2 versions 1 and 2.</summary>
    public class Gen2VehicleUnitRegion : IdentifiedObjectRegion
    {
        [XmlAttribute] public int Version = 1;
        [XmlAttribute] public int Transfer;
        [XmlIgnore] public List<Gen2RecordArray> RecordArrays { get; } = new List<Gen2RecordArray>();
        [XmlIgnore] public byte[] DownloadInterfaceVersion { get; private set; }
        protected override void ProcessInternal(CustomBinaryReader reader)
        {
            RecordArrays.Clear();
            if (Transfer == 0) { DownloadInterfaceVersion = reader.ReadExactlyBytes(2); return; }
            if (Validator.ValidateSignatures)
                throw new NotSupportedException("Gen2 ECC signature verification is not implemented. Disable signature validation only for unsigned decoding.");
            byte[] types;
            switch (Transfer)
            {
                case 1:
                    types = new byte[] { 4, 15, 10, (byte)(Version == 1 ? 11 : 36), 3, 19, 2, 20, 16, 17, 8 }; break;
                case 2:
                    types = Version == 1 ? new byte[] { 6, 5, 13, 1, 28, 22, 9, 8 } : new byte[] { 6, 5, 13, 1, 28, 22, 9, 34, 35, 8 }; break;
                case 3:
                    types = new byte[] { 24, 21, 26, 27, 30, 8 }; break;
                case 4:
                    types = new byte[] { 18, 8 }; break;
                case 5:
                    types = new byte[] { 25, 32, 33, 12, 14, 23, 31, 8 }; break;
                default: throw new ReaderException("Unknown Gen2 transfer");
            }
            foreach (byte expected in types)
            {
                long offset = reader.BaseStream.Position;
                byte type = reader.ReadByte();
                int size = (int)reader.ReadSInt16(), count = (int)reader.ReadSInt16();
                if (type != expected) throw new ReaderException(string.Format("Expected record type 0x{0:X2}, found 0x{1:X2} at 0x{2:X}", expected, type, offset));
                if (size == 0 || (long)size * count > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new ReaderException("Invalid or truncated Gen2 record array at " + offset);
                if ((type == 4 || type == 15 || type == 8 || type == 10 || type == 11 || type == 36 || type == 3 || type == 19 || type == 2 || type == 20 || type == 6 || type == 5 || type == 26 || type == 25) && count != 1)
                    throw new ReaderException("Expected exactly one record for " + RecordName(type));
                if (type == 8 && size != 64 && size != 96 && size != 128 && size != 132)
                    throw new ReaderException("Invalid ECC signature size");
                var array = new Gen2RecordArray { RecordType = type, RecordSize = size, Offset = offset };
                for (int n = 0; n < count; n++)
                {
                    using (var record = new Gen2RecordReader(reader.ReadExactlyBytes(size), RecordName(type), Version))
                    {
                        record.Decode(type);
                        if (record.Remaining != 0) throw new ReaderException("Record size does not match " + RecordName(type));
                        array.Records.Add(record.Element);
                    }
                }
                RecordArrays.Add(array);
            }
        }
        internal static string RecordName(byte type)
        {
            string[] names = { "Unknown", "ActivityChangeInfo", "CardSlotsStatus", "CurrentDateTime", "MemberStateCertificate", "OdometerValueMidnight", "DateOfDayDownloaded", "SensorPaired", "Signature", "SpecificConditionRecord", "VehicleIdentificationNumber", "VehicleRegistrationNumber", "VuCalibrationRecord", "VuCardIWRecord", "VuCardRecord", "VuCertificate", "VuCompanyLocksRecord", "VuControlActivityRecord", "VuDetailedSpeedBlock", "VuDownloadablePeriod", "VuDownloadActivityData", "VuEventRecord", "VuGNSSADRecord", "VuITSConsentRecord", "VuFaultRecord", "VuIdentification", "VuOverSpeedingControlData", "VuOverSpeedingEventRecord", "VuPlaceDailyWorkPeriodRecord", "VuTimeAdjustmentGNSSRecord", "VuTimeAdjustmentRecord", "VuPowerSupplyInterruptionRecord", "SensorPairedRecord", "SensorExternalGNSSCoupledRecord", "VuBorderCrossingRecord", "VuLoadUnloadRecord", "VehicleRegistrationIdentification" };
            return type < names.Length ? names[type] : "Unknown";
        }
        private static string ArrayName(byte type)
        {
            switch (type)
            {
                case 1:
                    return "VuActivityDailyRecordArray";
                case 9:
                    return "VuSpecificConditionRecordArray";
                case 32:
                    return "VuSensorPairedRecordArray";
                case 33:
                    return "VuSensorExternalGNSSCoupledRecordArray";
                default:
                    string name = RecordName(type);
                    return name + (name.EndsWith("Record", StringComparison.Ordinal) ? "Array" : "RecordArray");
            }
        }
        protected override void InternalToXML(XmlWriter writer)
        {
            writer.WriteAttributeString("Generation", "2");
            writer.WriteAttributeString("Version", Version.ToString());
            if (Transfer == 0) { writer.WriteElementString("DownloadInterfaceVersion", HexValueRegion.ToHexString(DownloadInterfaceVersion)); return; }
            writer.WriteAttributeString("SignatureStatus", "NotVerified");
            foreach (var array in RecordArrays)
            {
                writer.WriteStartElement(ArrayName(array.RecordType));
                writer.WriteAttributeString("RecordType", array.RecordType.ToString());
                writer.WriteAttributeString("RecordSize", array.RecordSize.ToString());
                writer.WriteAttributeString("NoOfRecords", array.Records.Count.ToString());
                foreach (var record in array.Records) record.WriteTo(writer);
                writer.WriteEndElement();
            }
        }
    }
    public sealed class Gen2RecordArray
    {
        public byte RecordType { get; set; }
        public int RecordSize { get; set; }
        public long Offset { get; set; }
        public List<XElement> Records { get; } = new List<XElement>();
    }
    internal sealed partial class Gen2RecordReader : IDisposable
    {
        private readonly CustomBinaryReader reader;
        private readonly int version;
        public XElement Element { get; }
        public int Remaining { get { return (int)(reader.BaseStream.Length - reader.BaseStream.Position); } }
        public Gen2RecordReader(byte[] bytes, string name, int version)
        {
            reader = new CustomBinaryReader(new MemoryStream(bytes, false)); Element = new XElement(name); this.version = version;
        }
        public void Dispose() { reader.Dispose(); }
        private void Region(Region region, string name)
        {
            region.Name = name; region.LogLevel = LogLevel.NONE; region.Process(reader);
            using (var writer = Element.CreateWriter()) region.ToXML(writer);
        }
        private void U(string name, int size = 1)
        {
            uint value = size == 1 ? reader.ReadByte() : size == 2 ? reader.ReadSInt16() : size == 3 ? reader.ReadSInt24() : reader.ReadSInt32();
            Element.Add(new XElement(name, value));
        }
        private void T(string name) { Region(new TimeRealRegion(), name); }
        private void S(string name, int length) { Region(new SimpleStringRegion(length), name); }
        private void N(string name) { Region(new NameRegion(), name); }
        private void H(string name, int size) { Element.Add(new XElement(name, HexValueRegion.ToHexString(reader.ReadExactlyBytes(size)))); }
        private void Serial(string name) { Region(new ExtendedSerialNumberRegion(), name); }
        private void Card(string name, bool generation = true)
        {
            Region(new FullCardNumberRegion(), name); if (generation) U(name + "Generation");
        }
        private void Registration() { U("VehicleRegistrationNation"); Region(new CodePageStringRegion(13), "VehicleRegistrationNumber"); }
        private void GNSS(bool authenticated)
        {
            T("GNSSPositionTime");
            U("GNSSAccuracy");
            Coordinate("Latitude");
            Coordinate("Longitude");
            if (authenticated) U("PositionAuthenticationStatus");
        }
        private void Coordinate(string name)
        {
            int value = (int)reader.ReadSInt24(); if ((value & 0x800000) != 0) value -= 0x1000000;
            var element = new XElement(name, new XAttribute("Raw", value));
            if (value == 0x7FFFFF) element.Add(new XAttribute("Available", false));
            else
            {
                int magnitude = Math.Abs(value), degrees = magnitude / 1000, minutes = magnitude % 1000;
                if (minutes >= 600 || degrees > (name == "Latitude" ? 90 : 180) || (degrees == (name == "Latitude" ? 90 : 180) && minutes != 0)) throw new ReaderException("Invalid GNSS coordinate");
                element.Value = XmlConvert.ToString(Math.Sign(value) * (degrees + minutes / 600m));
            }
            Element.Add(element);
        }
        private void Place()
        {
            T("EntryTime");
            U("EntryType");
            U("DailyWorkPeriodCountry");
            U("DailyWorkPeriodRegion");
            U("VehicleOdometerValue", 3);
            GNSS(version == 2);
        }
        private void Event(bool similar, bool manufacturer, bool power = false)
        {
            U("EventFaultType");
            U("RecordPurpose");
            T("BeginTime");
            T("EndTime");
            Card("DriverSlotBegin");
            if (power) { Card("DriverSlotEnd"); Card("CodriverSlotBegin"); } else { Card("CodriverSlotBegin"); Card("DriverSlotEnd"); }
            Card("CodriverSlotEnd");
            if (similar) U("SimilarEventsNumber");
            if (manufacturer) { U("ManufacturerCode"); H("ManufacturerSpecificErrorCode", 3); }
        }
        private void Calibration()
        {
            U("CalibrationPurpose");
            N("WorkshopName");
            N("WorkshopAddress");
            Card("WorkshopCardNumber", false);
            T("WorkshopCardExpiryDate");
            S("VehicleIdentificationNumber", 17);
            Registration();
            U("VehicleCharacteristicConstant", 2);
            U("ConstantOfRecordingEquipment", 2);
            U("TyreCircumference", 2);
            S("TyreSize", 15);
            U("AuthorisedSpeed");
            U("OldOdometerValue", 3);
            U("NewOdometerValue", 3);
            T("OldTimeValue");
            T("NewTimeValue");
            T("NextCalibrationDate");
            if (version == 2) { Serial("SensorSerialNumber"); Serial("SensorGNSSSerialNumber"); Serial("RemoteCommunicationModuleSerialNumber"); }
            for (int i = 0; i < 5; i++)
            {
                var seal = new XElement("SealRecord", new XElement("EquipmentType", reader.ReadByte()), new XElement("ManufacturerCode", HexValueRegion.ToHexString(reader.ReadExactlyBytes(2))), new XElement("SealIdentifier", HexValueRegion.ToHexString(reader.ReadExactlyBytes(8))));
                Element.Add(seal);
            }
            if (version == 2) { U("ByDefaultLoadType"); U("CalibrationCountry"); T("CalibrationCountryTimestamp"); }
        }
        public void Decode(byte type)
        {
            switch (type)
            {
                case 1:
                    Region(new ActivityChangeRegion(), "ActivityChangeInfo");
                    break;
                case 2:
                    U("CardSlotsStatus");
                    break;
                case 3:
                    T("CurrentDateTime");
                    break;
                case 4:
                    case 15: case 8: H("Raw", Remaining);
                    break;
                case 5:
                    U("OdometerValueMidnight", 3);
                    break;
                case 6:
                    T("DateOfDayDownloaded");
                    break;
                case 9:
                    T("EntryTime");
                    U("SpecificConditionType");
                    break;
                case 10:
                    S("VehicleIdentificationNumber", 17);
                    break;
                case 11:
                    Region(new CodePageStringRegion(13), "VehicleRegistrationNumber");
                    break;
                case 12:
                    Calibration();
                    break;
                case 13:
                    N("CardHolderSurname");
                    N("CardHolderFirstNames");
                    Card("FullCardNumber");
                    T("CardExpiryDate");
                    T("CardInsertionTime");
                    U("VehicleOdometerValueAtInsertion", 3);
                    U("CardSlotNumber");
                    T("CardWithdrawalTime");
                    U("VehicleOdometerValueAtWithdrawal", 3);
                    Registration();
                    T("PreviousCardWithdrawalTime");
                    U("PreviousVuGeneration");
                    U("ManualInputFlag");
                    break;
                case 14:
                    Card("CardNumber");
                    Serial("CardExtendedSerialNumber");
                    U("CardStructureVersion", 2);
                    Region(new CardNumberRegion(), "ApplicationCardNumber");
                    break;
                case 16:
                    T("LockInTime");
                    T("LockOutTime");
                    N("CompanyName");
                    N("CompanyAddress");
                    Card("CompanyCardNumber");
                    break;
                case 17:
                    U("ControlType");
                    T("ControlTime");
                    Card("ControlCardNumber");
                    T("DownloadPeriodBeginTime");
                    T("DownloadPeriodEndTime");
                    break;
                case 18:
                    T("SpeedBlockBeginDate");
                    var speeds = new XElement("SpeedData");
                    for (int i = 0; i < 60; i++) speeds.Add(new XElement("DataPoint", reader.ReadByte()));
                    Element.Add(speeds);
                    break;
                case 19:
                    T("MinDownloadableTime");
                    T("MaxDownloadableTime");
                    break;
                case 20:
                    T("DownloadingTime");
                    Card("FullCardNumber");
                    N("CompanyOrWorkshopName");
                    break;
                case 21:
                    Event(true, true);
                    break;
                case 22:
                    T("TimeStamp");
                    Card("DriverSlot");
                    Card("CodriverSlot");
                    GNSS(version == 2);
                    U("VehicleOdometerValue", 3);
                    break;
                case 23:
                    Card("CardNumber");
                    U("Consent");
                    break;
                case 24:
                    Event(false, true);
                    break;
                case 25:
                    N("ManufacturerName");
                    N("ManufacturerAddress");
                    S("PartNumber", 16);
                    Serial("SerialNumber");
                    S("SoftwareVersion", 4);
                    T("SoftwareInstallationDate");
                    T("ManufacturingDate");
                    S("ApprovalNumber", 16);
                    U("VuGeneration");
                    U("VuAbility");
                    if (version == 2) S("DigitalMapVersion", 12);
                    break;
                case 26:
                    T("LastOverspeedControlTime");
                    T("FirstOverspeedSince");
                    U("NumberOfOverspeedSince");
                    break;
                case 27:
                    U("EventType");
                    U("RecordPurpose");
                    T("BeginTime");
                    T("EndTime");
                    U("MaxSpeedValue");
                    U("AverageSpeedValue");
                    Card("DriverSlotBegin");
                    U("SimilarEventsNumber");
                    break;
                case 28:
                    Card("FullCardNumber");
                    Place();
                    break;
                case 30:
                    T("OldTimeValue");
                    T("NewTimeValue");
                    N("WorkshopName");
                    N("WorkshopAddress");
                    Card("WorkshopCardNumber");
                    break;
                case 31:
                    Event(true, false, true);
                    break;
                case 32:
                    case 33: Serial("SensorSerialNumber");
                    S("SensorApprovalNumber", 16);
                    T("SensorPairingOrCouplingDate");
                    break;
                case 34:
                    Card("DriverSlot");
                    Card("CodriverSlot");
                    U("CountryLeft");
                    U("CountryEntered");
                    GNSS(true);
                    U("VehicleOdometerValue", 3);
                    break;
                case 35:
                    T("TimeStamp");
                    U("OperationType");
                    Card("DriverSlot");
                    Card("CodriverSlot");
                    GNSS(true);
                    U("VehicleOdometerValue", 3);
                    break;
                case 36:
                    Registration();
                    break;
                default: throw new NotSupportedException("Unsupported Gen2 record type " + type);
            }
        }
    }
}
