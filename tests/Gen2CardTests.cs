using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DataFileReader;

internal static class Gen2CardTests
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Card check failed: " + name);
        checks++;
    }
    private static byte[] Join(params byte[][] parts) { return parts.SelectMany(p => p).ToArray(); }
    private static byte[] Word(int value) { return new byte[] {(byte)(value >> 8), (byte)value}; }
    private static byte[] Number24(int value) { return new byte[] {(byte)(value >> 16), (byte)(value >> 8), (byte)value}; }
    private static byte[] Time(int value = 0x60000000) { return new byte[] {(byte)(value >> 24),(byte)(value >> 16),(byte)(value >> 8),(byte)value}; }
    private static byte[] Tlv(int fid, byte[] data, int type = 2) { return Join(Word(fid),new byte[] {(byte)type},Word(data.Length),data); }
    private static byte[] GNSS(bool auth = false) { return Join(Time(),new byte[] {10},Number24(48000),Number24(-14250),auth ? new byte[] {1} : new byte[0]); }
    private static byte[] Fill(int count, byte[] record) { return Enumerable.Range(0,count).SelectMany(_=>record).ToArray(); }
    private static byte[] Ring(int count, byte[] record) { return Join(Word(count == 0 ? 0 : count-1),Fill(count,record)); }
    private static byte[] Application(int version, int events=1, int faults=1, int vehicles=1, int places=1, int gnss=1, int conditions=1, int units=1, int activity=32)
    {
        return Tlv(0x0501,Join(new byte[] {1,1,(byte)(version-1),(byte)events,(byte)faults},Word(activity),Word(vehicles),Word(places),Word(gnss),Word(conditions),Word(units)));
    }
    private static byte[] Extension(int borders=1, int loads=1, int types=1, int config=3)
    {
        return Tlv(0x0525,Join(Word(8),Word(borders),Word(loads),Word(types),Word(config)));
    }
    private static readonly byte[] Registration = Join(new byte[] {1,0},Encoding.ASCII.GetBytes("TEST-VEHICLE "));
    private static byte[] Activity(int length, bool wrapped)
    {
        var day = Join(Word(14),Word(14),Time(),new byte[] {0,1,0,5,0x18,0x3c});
        byte[] ring = new byte[length];
        int oldest = wrapped ? length-8 : 0;
        for (int n=0; n<day.Length; n++) ring[(oldest+n)%length]=day[n];
        int newest = wrapped ? (oldest+day.Length)%length : 0;
        if (wrapped) for (int n=0; n<day.Length; n++) ring[(newest+n)%length]=day[n];
        return Join(Word(oldest),Word(newest),ring);
    }
    private static Dictionary<int,byte[]> Files(int version, bool full)
    {
        int ev=full ? (version==1 ? 6 : 12) : 1, faults=full ? (version==1 ? 12 : 24) : 1;
        int veh=full ? (version==1 ? 84 : 200) : 1, places=full ? (version==1 ? 84 : 112) : 1;
        int gnss=full ? (version==1 ? 252 : 336) : 1, conditions=full ? (version==1 ? 56 : 112) : 1;
        int units=veh, activity=full ? (version==1 ? 5544 : 13776) : 32;
        var files = new Dictionary<int,byte[]>();
        files[0x0002] = Tlv(0x0002,new byte[25]);
        files[0x0005] = Tlv(0x0005,new byte[8]);
        files[0x0501] = Application(version,ev,faults,veh,places,gnss,conditions,units,activity);
        foreach (int fid in new[] {0xC100,0xC101,0xC108,0xC109}) files[fid]=Tlv(fid,new byte[204]);
        files[0x0520]=Tlv(0x0520,new byte[143]); files[0x0521]=Tlv(0x0521,new byte[53]);
        files[0x050E]=Tlv(0x050E,Time());
        var eventRecord = Join(new byte[] {1},Time(),Time(),Registration);
        files[0x0502]=Tlv(0x0502,Fill(11*ev,eventRecord));
        files[0x0503]=Tlv(0x0503,Fill(2*faults,eventRecord));
        files[0x0504]=Tlv(0x0504,Activity(activity,!full));
        files[0x0505]=Tlv(0x0505,Ring(veh,Join(Number24(100),Number24(200),Time(),Time(),Registration,new byte[] {0x12,0x34},Encoding.ASCII.GetBytes("TESTVIN0000000001"))));
        files[0x0506]=Tlv(0x0506,Ring(places,Join(Time(),new byte[] {0,1,0},Number24(300),GNSS())));
        files[0x0507]=Tlv(0x0507,Join(Time(),Registration));
        var control = Join(new byte[] {3,1},Encoding.ASCII.GetBytes("CONTROL000001"),new byte[] {50,48,49});
        files[0x0508]=Tlv(0x0508,Join(new byte[] {1},Time(),control,Registration,Time(),Time()));
        files[0x0522]=Tlv(0x0522,Ring(conditions,Join(Time(),new byte[] {1})));
        files[0x0523]=Tlv(0x0523,Ring(units,Join(Time(),new byte[] {1,2},Encoding.ASCII.GetBytes("ABCD"))));
        files[0x0524]=Tlv(0x0524,Ring(gnss,Join(Time(),GNSS(),Number24(1000))));
        if (version==2)
        {
            int borders=full ? 1120 : 1, loads=full ? 1624 : 1, types=full ? 336 : 1;
            files[0x0525]=Extension(borders,loads,types,full ? 3072 : 3);
            files[0x0526]=Tlv(0x0526,Ring(places,Join(Time(),new byte[] {1})));
            files[0x0527]=Tlv(0x0527,Ring(gnss,Join(Time(),new byte[] {1})));
            files[0x0528]=Tlv(0x0528,Ring(borders,Join(new byte[] {1,13},GNSS(true),Number24(400))));
            files[0x0529]=Tlv(0x0529,Ring(loads,Join(Time(),new byte[] {3},GNSS(true),Number24(500))));
            files[0x0530]=Tlv(0x0530,Ring(types,Join(Time(),new byte[] {2})));
            files[0x0540]=Tlv(0x0540,full ? new byte[3072] : new byte[] {1,2,3});
        }
        return files;
    }
    private static XElement Parse(byte[] bytes, DataFile file = null, bool shortReads=false)
    {
        file = file ?? DriverCardDataFile.Create(); file.LogLevel=LogLevel.NONE;
        using (var stream = shortReads ? new ShortStream(bytes) : new MemoryStream(bytes))
        {
            file.Process(stream); Check(stream.Position==stream.Length && stream.CanRead,"consumption and ownership");
            var output = new StringBuilder(); using (var writer=XmlWriter.Create(output)) file.ToXML(writer);
            return XElement.Parse(output.ToString());
        }
    }
    private static void Reject(byte[] bytes, string name, DataFile file = null)
    {
        try { Parse(bytes,file); }
        catch (Exception e) when (e is ReaderException || e is EndOfStreamException || e is NotSupportedException || e is InvalidOperationException) { checks++; return; }
        throw new Exception("Malformed card accepted: "+name);
    }
    internal static int Run()
    {
        checks=0;
        for (int version=1; version<=2; version++)
        {
            var files=Files(version,false);
            var metadata=version==1 ? files[0x0501] : Join(files[0x0501],files[0x0525]);
            var xml=Parse(Join(files.Values.ToArray()));
            Check(xml.Descendants("CardEventRecord").Count()==11,"eleven event categories");
            Check(xml.Descendants("VehicleIdentificationNumber").Single().Value=="TESTVIN0000000001","vehicle VIN");
            Check(xml.Descendants("CardActivityDailyRecord").Count()==2,"wrapped card activity");
            Check(xml.Descendants("ActivityChangeInfo").All(x=>(string)x.Attribute("Time")=="01:00"),"card activity bits");
            Check(xml.Descendants("ControlCardNumber").Single().Value=="CONTROL000001","control-card owner number");
            Check(xml.Descendants("ControlCardNumber").Attributes("ConsecutiveIndex").Single().Value=="50","control consecutive index");
            Check(xml.Descendants("Latitude").Any(x=>x.Value=="48"),"card GNSS coordinate");
            Check(xml.Elements().All(x=>(string)x.Attribute("Version")==version.ToString()),"version metadata");
            var reverse=files.Values.Reverse().ToArray();
            var reordered=Parse(Join(reverse));
            Check(reordered.Descendants("CardVehicleRecord").Count()==1,"application after data");
            Parse(Join(files.Values.ToArray()),shortReads:true);
            var full=Parse(Join(Files(version,true).Values.ToArray()));
            Check(full.Descendants("CardVehicleRecord").Count()==(version==1 ? 84 : 200),"realistic vehicle capacity");
            Check(full.Descendants("GNSSAccumulatedDrivingRecord").Count()==(version==1 ? 252 : 336),"16-bit GNSS count");
            if (version==2)
            {
                Check(full.Descendants("CardBorderCrossingRecord").Count()==1120,"V2 crossing capacity");
                Check(full.Descendants("CardLoadUnloadRecord").Count()==1624,"V2 load capacity");
                Check(xml.Descendants("CountryEntered").Single().Value=="13","card border country");
                Check(xml.Descendants("OperationType").Single().Value=="3","card load operation");
                Check(xml.Descendants("LoadTypeEntered").Single().Value=="2","card load type");
                Check(xml.Descendants("VuConfiguration").Descendants("Raw").Single().Value=="0x010203","opaque VU configuration");
            }
            foreach (var pair in files)
            {
                for (int length=1; length<pair.Value.Length; length++)
                    Reject(Join(metadata,pair.Value.Take(length).ToArray()),"each TLV truncation");
                if (pair.Key<0xC100 && pair.Key!=0x0002 && pair.Key!=0x0005)
                {
                    byte[] prefix = pair.Key==0x0501 ? (version==2 ? files[0x0525] : new byte[0]) : pair.Key==0x0525 ? files[0x0501] : metadata;
                    var paired=Parse(Join(prefix,pair.Value,Tlv(pair.Key,new byte[64],3)));
                    Check(paired.Descendants("Signature").Count()==1,"signature preserved");
                    Check(paired.Elements().All(x=>(string)x.Attribute("SignatureStatus")=="NotVerified"),"signature status");
                    Reject(Join(prefix,pair.Value,Tlv(pair.Key,new byte[63],3)),"bad ECC width");
                }
            }
        }
        Reject(Tlv(0x0523,Ring(1,new byte[10])),"missing Gen2 counts");
        Reject(Join(Application(1),Extension()),"V2 metadata on V1 card");
        Reject(Join(Application(2),Extension(),Tlv(0x0530,new byte[] {0,1,0,0,0,0,0})),"pointer out of range");
        Reject(Join(Application(2),Tlv(0x0528,new byte[19])),"missing extension counts");
        Reject(Join(Application(1),Tlv(0x0501,new byte[64],3),Tlv(0x0501,new byte[64],3)),"duplicate signature");
        Reject(Tlv(0x0501,new byte[64],3),"orphan signature");
        Reject(Join(Application(1),Tlv(0x0507,new byte[64],3)),"mismatched signature");
        Reject(Join(Application(1),Application(1)),"duplicate application metadata");
        Reject(Join(Application(2),Tlv(0x0525,new byte[] {0,7,0,1,0,1,0,1,0,3})),"extension length mismatch");
        Reject(Join(Application(1),Tlv(0xC101,new byte[194])),"Gen1 certificate width in Gen2 EF");
        Reject(Tlv(0xC101,new byte[204],0),"Gen1 tag on Gen2-only EF");
        byte[] workshop=Application(1); workshop[5]=2;
        Reject(workshop,"non-driver application");
        Reject(new byte[] {5,1,2,255,255},"reserved TLV length");
        Reject(new byte[] {5,1,2,0,0},"empty TLV");
        Reject(new byte[] {5,1,4,0,1,0},"unknown TLV type");
        byte[] unknownVersion=Application(1); unknownVersion[7]=2;
        Reject(unknownVersion,"unknown application version");
        var reusable=DriverCardDataFile.Create(); Parse(Join(Files(2,false).Values.ToArray()),reusable);
        Reject(Tlv(0x0523,Ring(1,new byte[10])),"no counts from preceding parse",reusable);
        Check(reusable.ProcessedRegions.Count==0,"failed card parse clears results");
        // Gen1 and Gen2 application counts for identical FIDs remain independent.
        byte[] legacyApp=Tlv(0x0501,new byte[] {1,0,0,2,1,0,32,0,1,1},0);
        byte[] legacyEvents=Tlv(0x0502,new byte[12*24],0);
        var mixed=Parse(Join(legacyApp,Application(1),legacyEvents,Tlv(0x0502,new byte[11*24])));
        Check(mixed.Elements("EventsData").Descendants("CardEventRecord").Count()==12,"Gen1 event counts");
        Check(mixed.Elements("EventsDataGen2").Descendants("CardEventRecord").Count()==11,"Gen2 event counts");
        var common = Parse(Join(Tlv(0x0002,new byte[25],0),Tlv(0x0005,new byte[8],0),Application(1)));
        Check(common.Element("CardIccIdentification") != null && common.Element("CardChipIdentification") != null,"common card files");
        var taggedCommon = Parse(Join(Tlv(0x0002,new byte[25],0),Tlv(0x0005,new byte[8],0),Application(1),Tlv(0x0002,new byte[25]),Tlv(0x0005,new byte[8])));
        Check(taggedCommon.Element("CardIccIdentification") != null && taggedCommon.Element("CardIccIdentificationGen2") != null,"ICC files retain both generations");
        Check(taggedCommon.Element("CardChipIdentification") != null && taggedCommon.Element("CardChipIdentificationGen2") != null,"chip files retain both generations");
        byte[] icc = Join(new byte[] {1},Time(42),new byte[] {0x12,0x24,3,4},Encoding.ASCII.GetBytes("APPROVAL"),new byte[] {5},Encoding.ASCII.GetBytes("AT"),new byte[] {0x12,0x34,6},Word(0x789a));
        var identifiers = Parse(Join(Application(1),Tlv(0x0002,icc),Tlv(0x0005,new byte[] {1,2,3,4,5,6,7,8})));
        Check(identifiers.Descendants("CardApprovalNumber").Single().Value=="APPROVAL" && identifiers.Descendants("CardPersonaliserId").Single().Value=="5","ICC fields");
        Check(identifiers.Descendants("ModuleEmbedder").Single().Value=="1234" && identifiers.Descendants("IcIdentifier").Single().Value=="30874","ICC assembler and IC offsets");
        Check(identifiers.Descendants("IcSerialNumber").Single().Value=="0x01020304" && identifiers.Descendants("IcManufacturingReferences").Single().Value=="0x05060708","chip identifiers");
        foreach (int fid in new[] {0x0002,0x0005})
        {
            int size=fid==0x0002 ? 25 : 8;
            Reject(Join(Application(1),Tlv(fid,new byte[size-1])),"short identification EF");
            Reject(Join(Application(1),Tlv(fid,new byte[size+1])),"long identification EF");
            Reject(Join(Application(1),Tlv(fid,new byte[size]),Tlv(fid,new byte[64],3)),"signature on unsigned identification EF");
        }
        var empty=Parse(Join(Application(2,0,0,0,0,0,0,0,0),Extension(0,0,0),Tlv(0x0504,new byte[4]),Tlv(0x0505,new byte[2]),Tlv(0x0528,new byte[2])));
        Check(!empty.Descendants("CardActivityDailyRecord").Any() && !empty.Descendants("CardBorderCrossingRecord").Any(),"initialized empty buffers");
        foreach (int size in new[] {64,96,128,132})
        {
            var signed=Parse(Join(Application(1),Tlv(0x0501,new byte[size],3)));
            Check(signed.Descendants("Signature").Single().Value.Length == size*2+2,"ECC signature sizes");
        }
        foreach (int size in new[] {204,341})
            Check(Parse(Tlv(0xC101,new byte[size])).Descendants("Raw").Single().Value.Length == size*2+2,"variable certificate lengths");
        byte[] unavailable = Join(new byte[] {1,13},Time(),new byte[] {10,0x7f,255,255},Number24(-14250),new byte[] {1},Number24(300));
        var unknown = Parse(Join(Application(2),Extension(),Tlv(0x0528,Ring(1,unavailable))));
        Check((string)unknown.Descendants("Latitude").Single().Attribute("Available")=="false","card unknown position");
        byte[] invalidPosition = Join(Time(),Time(),new byte[] {10},Number24(48000),Number24(13600),Number24(300));
        byte[] invalidCoordinates = Join(Application(1),Tlv(0x0524,Ring(1,invalidPosition)));
        bool wasStrict=DataFile.StrictProcessing;
        try
        {
            DataFile.StrictProcessing=false;
            var invalidLongitude = Parse(invalidCoordinates).Descendants("Longitude").Single();
            Check((string)invalidLongitude.Attribute("Raw")=="13600" && (string)invalidLongitude.Attribute("Valid")=="false" && (string)invalidLongitude.Attribute("Available")=="false" && invalidLongitude.Value=="","malformed coordinate retained without invented position");
            DataFile.StrictProcessing=true;
            Reject(invalidCoordinates,"strict malformed coordinate");
        }
        finally { DataFile.StrictProcessing=wasStrict; }
        byte[] extended = Join(Word(10),Word(1),Word(1),Word(1),Word(3),new byte[] {0xab,0xcd});
        Check(Parse(Join(Application(2),Tlv(0x0525,extended))).Descendants("ExtensionData").Single().Value=="0xABCD","application extension preserved");
        Validator.ValidateSignatures=true;
        try { Reject(Application(1),"Gen2 ECC verification rejected"); }
        finally { Validator.ValidateSignatures=false; }
        Console.WriteLine("Driver-card checks passed: {0}",checks);
        return checks;
    }
    private sealed class ShortStream : MemoryStream
    {
        internal ShortStream(byte[] bytes) : base(bytes) { }
        public override int Read(byte[] buffer,int offset,int count) { return base.Read(buffer,offset,Math.Min(count,1)); }
    }
}
