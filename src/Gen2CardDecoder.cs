using System;
using System.Xml.Linq;

namespace DataFileReader
{
    internal sealed partial class Gen2RecordReader
    {
        private int CardCount(Gen2CardContext context, string name, int size, int prefix = 2)
        {
            int count = context.Count(name);
            if ((long)count * size + prefix != Remaining) throw new ReaderException("Card EF count/length mismatch: " + name);
            return count;
        }
        private void CardPointer(int count, string name)
        {
            uint pointer = reader.ReadSInt16();
            if ((count == 0 && pointer != 0) || (count > 0 && pointer >= count)) throw new ReaderException("Invalid card record pointer");
            Element.Add(new XElement(name, pointer));
        }
        private void CardRecords(int count, int size, string name, Action<Gen2RecordReader> decode, XElement target = null)
        {
            for (int i = 0; i < count; i++)
            {
                using (var record = new Gen2RecordReader(reader.ReadExactlyBytes(size), name, 1))
                {
                    decode(record);
                    if (record.Remaining != 0) throw new ReaderException("Card record size mismatch: " + name);
                    record.Element.SetAttributeValue("Index", i);
                    (target ?? Element).Add(record.Element);
                }
            }
        }
        private void ApplicationCount(Gen2CardContext context, string name, int size = 2)
        {
            int value = size == 1 ? reader.ReadByte() : (int)reader.ReadSInt16();
            context.Counts[name] = value;
            Element.Add(new XElement(name, value));
        }
        private void CardPlace()
        {
            T("EntryTime");
            U("EntryType");
            U("DailyWorkPeriodCountry");
            U("DailyWorkPeriodRegion");
            U("VehicleOdometerValue", 3);
            GNSS(false);
        }
        public void DecodeCard(int fid, Gen2CardContext context)
        {
            if ((fid >= 0x0525 && fid <= 0x0540) && context.Version != 2)
                throw new ReaderException("Version 2 card EF requires version 2 application identification");
            int count;
            switch (fid)
            {
                case 0x0002:
                    U("ClockStop");
                    Serial("CardExtendedSerialNumber");
                    S("CardApprovalNumber", 8);
                    U("CardPersonaliserId");
                    using (var assembler = new Gen2RecordReader(reader.ReadExactlyBytes(5), "EmbedderIcAssemblerId", 1))
                    {
                        assembler.S("CountryCode", 2);
                        assembler.Region(new BCDStringRegion {Size=2}, "ModuleEmbedder");
                        assembler.U("ManufacturerInformation");
                        Element.Add(assembler.Element);
                    }
                    U("IcIdentifier", 2);
                    break;
                case 0x0005:
                    H("IcSerialNumber", 4);
                    H("IcManufacturingReferences", 4);
                    break;
                case 0x0501:
                    int type = reader.ReadByte();
                    if (type != 1) throw new NotSupportedException("This decoder requires a driver card application");
                    int structure = (int)reader.ReadSInt16();
                    if (structure != 0x0100 && structure != 0x0101) throw new NotSupportedException("Unsupported Gen2 card structure version");
                    context.Version = structure == 0x0100 ? 1 : 2;
                    Element.Add(new XElement("Type", type), new XElement("CardStructureVersion", structure));
                    ApplicationCount(context, "NoOfEventsPerType", 1);
                    ApplicationCount(context, "NoOfFaultsPerType", 1);
                    ApplicationCount(context, "ActivityStructureLength");
                    ApplicationCount(context, "NoOfCardVehicleRecords");
                    ApplicationCount(context, "NoOfCardPlaceRecords");
                    ApplicationCount(context, "NoOfGNSSADRecords");
                    ApplicationCount(context, "NoOfSpecificConditionRecords");
                    ApplicationCount(context, "NoOfCardVehicleUnitRecords");
                    break;
                case 0x0525:
                    int following = (int)reader.ReadSInt16();
                    if (following != Remaining || following < 8) throw new ReaderException("Invalid application extension length");
                    Element.Add(new XElement("LengthOfFollowingData", following));
                    ApplicationCount(context, "NoOfBorderCrossingRecords");
                    ApplicationCount(context, "NoOfLoadUnloadRecords");
                    ApplicationCount(context, "NoOfLoadTypeEntryRecords");
                    ApplicationCount(context, "VuConfigurationLengthRange");
                    if (Remaining > 0) H("ExtensionData", Remaining);
                    break;
                case 0xC100: case 0xC101: case 0xC108: case 0xC109:
                    if (Remaining < 204 || Remaining > 341) throw new ReaderException("Invalid Gen2 certificate size");
                    H("Raw", Remaining);
                    break;
                case 0x0520:
                    U("CardIssuingMemberState");
                    Region(new CardNumberRegion(), "CardNumber");
                    N("CardIssuingAuthorityName");
                    T("CardIssueDate");
                    T("CardValidityBegin");
                    T("CardExpiryDate");
                    N("CardHolderSurname");
                    N("CardHolderFirstNames");
                    Region(new DatefRegion(), "CardHolderBirthDate");
                    S("CardHolderPreferredLanguage", 2);
                    break;
                case 0x0521:
                    N("DrivingLicenceIssuingAuthority");
                    U("DrivingLicenceIssuingNation");
                    S("DrivingLicenceNumber", 16);
                    break;
                case 0x050E: T("LastCardDownload"); break;
                case 0x0502: case 0x0503:
                    int categories = fid == 0x0502 ? 11 : 2;
                    count = CardCount(context, fid == 0x0502 ? "NoOfEventsPerType" : "NoOfFaultsPerType", 24 * categories, 0);
                    for (int category = 0; category < categories; category++)
                    {
                        var group = new XElement(fid == 0x0502 ? "CardEventRecords" : "CardFaultRecords", new XAttribute("Category", category));
                        CardRecords(count, 24, fid == 0x0502 ? "CardEventRecord" : "CardFaultRecord", r =>
                        {
                            r.U(fid == 0x0502 ? "EventType" : "FaultType");
                            r.T("BeginTime");
                            r.T("EndTime");
                            r.Registration();
                        }, group);
                        Element.Add(group);
                    }
                    break;
                case 0x0504:
                    int activityLength = context.Count("ActivityStructureLength");
                    if (Remaining != activityLength + 4) throw new ReaderException("Activity buffer length does not match application");
                    Region(new CyclicalActivityRegion { RegionLength = Remaining, Regions = new System.Collections.ArrayList {
                        new DriverCardDailyActivityRegion {Name="CardActivityDailyRecord"} } }, "CardDriverActivity");
                    break;
                case 0x0505:
                    count = CardCount(context, "NoOfCardVehicleRecords", 48);
                    CardPointer(count, "VehiclePointerNewestRecord");
                    CardRecords(count, 48, "CardVehicleRecord", r =>
                    {
                        r.U("VehicleOdometerBegin", 3);
                        r.U("VehicleOdometerEnd", 3);
                        r.T("VehicleFirstUse");
                        r.T("VehicleLastUse");
                        r.Registration(); r.Region(new BCDStringRegion {Size=2}, "VuDataBlockCounter"); r.S("VehicleIdentificationNumber", 17);
                    });
                    break;
                case 0x0506:
                    count = CardCount(context, "NoOfCardPlaceRecords", 21);
                    CardPointer(count, "PlacePointerNewestRecord");
                    CardRecords(count, 21, "PlaceRecord", r => r.CardPlace());
                    break;
                case 0x0507: T("SessionOpenTime"); Registration(); break;
                case 0x0508:
                    U("ControlType");
                    T("ControlTime");
                    Card("ControlCardNumber", false);
                    Registration();
                    T("ControlDownloadPeriodBegin");
                    T("ControlDownloadPeriodEnd");
                    break;
                case 0x0522:
                    count = CardCount(context, "NoOfSpecificConditionRecords", 5);
                    CardPointer(count, "ConditionPointerNewestRecord");
                    CardRecords(count, 5, "SpecificConditionRecord", r => { r.T("EntryTime"); r.U("SpecificConditionType"); });
                    break;
                case 0x0523:
                    count = CardCount(context, "NoOfCardVehicleUnitRecords", 10);
                    CardPointer(count, "VehicleUnitPointerNewestRecord");
                    CardRecords(count, 10, "CardVehicleUnitRecord", r => { r.T("TimeStamp"); r.U("ManufacturerCode"); r.U("DeviceID"); r.S("VuSoftwareVersion", 4); });
                    break;
                case 0x0524:
                    count = CardCount(context, "NoOfGNSSADRecords", 18);
                    CardPointer(count, "GNSSADPointerNewestRecord");
                    CardRecords(count, 18, "GNSSAccumulatedDrivingRecord", r => { r.T("TimeStamp"); r.GNSS(false); r.U("VehicleOdometerValue", 3); });
                    break;
                case 0x0526: case 0x0527:
                    count = CardCount(context, fid == 0x0526 ? "NoOfCardPlaceRecords" : "NoOfGNSSADRecords", 5);
                    CardPointer(count, fid == 0x0526 ? "PlaceAuthPointerNewestRecord" : "GNSSAuthADPointerNewestRecord");
                    CardRecords(count, 5, fid == 0x0526 ? "PlaceAuthStatusRecord" : "GNSSAuthStatusADRecord", r => { r.T(fid == 0x0526 ? "EntryTime" : "TimeStamp"); r.U("PositionAuthenticationStatus"); });
                    break;
                case 0x0528:
                    count = CardCount(context, "NoOfBorderCrossingRecords", 17);
                    CardPointer(count, "BorderCrossingPointerNewestRecord");
                    CardRecords(count, 17, "CardBorderCrossingRecord", r => { r.U("CountryLeft"); r.U("CountryEntered"); r.GNSS(true); r.U("VehicleOdometerValue", 3); });
                    break;
                case 0x0529:
                    count = CardCount(context, "NoOfLoadUnloadRecords", 20);
                    CardPointer(count, "LoadUnloadPointerNewestRecord");
                    CardRecords(count, 20, "CardLoadUnloadRecord", r => { r.T("TimeStamp"); r.U("OperationType"); r.GNSS(true); r.U("VehicleOdometerValue", 3); });
                    break;
                case 0x0530:
                    count = CardCount(context, "NoOfLoadTypeEntryRecords", 5);
                    CardPointer(count, "LoadTypeEntryPointerNewestRecord");
                    CardRecords(count, 5, "CardLoadTypeEntryRecord", r => { r.T("TimeStamp"); r.U("LoadTypeEntered"); });
                    break;
                case 0x0540:
                    if (Remaining != context.Count("VuConfigurationLengthRange")) throw new ReaderException("VU configuration length mismatch");
                    H("Raw", Remaining);
                    break;
                default: throw new NotSupportedException("Unsupported Gen2 driver card EF");
            }
        }
    }
}
