using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DataFileReader;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Failed: " + name);
        checks++;
    }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (Exception ex) when (ex is ReaderException || ex is EndOfStreamException || ex is NotSupportedException || ex is InvalidOperationException) { checks++; return; }
        throw new Exception("Accepted malformed input: " + name);
    }
    private static byte[] Join(params byte[][] parts) { return parts.SelectMany(x => x).ToArray(); }
    private static byte[] Array(byte type, byte[] record)
    {
        return Join(new byte[] {type, (byte)(record.Length >> 8), (byte)record.Length, 0, 1}, record);
    }
    private static byte[] Z(int size) { return new byte[size]; }
    private static byte[] Block(int version, int transfer)
    {
        int[] types;
        switch (transfer)
        {
            case 1: types = new[] {4,15,10,version == 1 ? 11 : 36,3,19,2,20,16,17,8}; break;
            case 2: types = version == 1 ? new[] {6,5,13,1,28,22,9,8} : new[] {6,5,13,1,28,22,9,34,35,8}; break;
            case 3: types = new[] {24,21,26,27,30,8}; break;
            case 4: types = new[] {18,8}; break;
            default: types = new[] {25,32,33,12,14,23,31,8}; break;
        }
        // Independently calculated byte widths from Appendix 1 field definitions.
        var widths = new Dictionary<int,int> {{1,2},{2,1},{3,4},{4,204},{5,3},{6,4},{8,64},{9,5},{10,17},{11,14},
            {12,version == 1 ? 222 : 252},{13,131},{14,45},{15,205},{16,99},{17,32},{18,64},{19,8},{20,59},
            {21,91},{22,version == 1 ? 56 : 57},{23,20},{24,90},{25,version == 1 ? 126 : 138},{26,9},{27,32},
            {28,version == 1 ? 40 : 41},{30,99},{31,87},{32,28},{33,28},{34,55},{35,58},{36,15}};
        var parts = new List<byte[]> { new byte[] {0x76, (byte)(transfer == 4 ? 0x24 : (version == 1 ? 0x20 : 0x30) + transfer)} };
        foreach (int type in types)
        {
            byte[] record = Z(widths[type]);
            if (type == 10) record = Encoding.ASCII.GetBytes("TESTVIN0000000001");
            if (type == 5) record = new byte[] {1,2,3};
            if (type == 1) record = new byte[] {0x18,0x3c}; // Driving at 01:00, driver slot.
            if (type == 34)
            {
                record[38] = 1; record[39] = 13; // Austria to Germany.
                record[45] = 0; record[46] = 0xBB; record[47] = 0x80; // 48,000 => 48 degrees.
                record[48] = 0xFF; record[49] = 0xC8; record[50] = 0x56; // -14,250 => -14 degrees 25.0 minutes.
                record[51] = 1;
            }
            parts.Add(Array((byte)type, record));
        }
        return Join(parts.ToArray());
    }
    private static XElement Parse(byte[] bytes, DataFile file = null)
    {
        file = file ?? VehicleUnitDataFile.Create(); file.LogLevel = LogLevel.NONE;
        using (var stream = new MemoryStream(bytes))
        {
            file.Process(stream); Check(stream.CanRead, "caller stream remains open");
            Check(stream.Position == stream.Length, "full consumption");
            var output = new StringBuilder();
            using (var writer = XmlWriter.Create(output, new XmlWriterSettings { OmitXmlDeclaration = true })) file.ToXML(writer);
            return XElement.Parse(output.ToString());
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            DataFile.StrictProcessing = true;
            checks += Gen2CardTests.Run();
            for (int v = 1; v <= 2; v++)
                for (int t = 1; t <= 5; t++)
                {
                    var bytes = Block(v,t); var xml = Parse(bytes);
                    Check(xml.Descendants("Signature").Count() == 1, "signature retained");
                    Check(xml.Descendants().Attributes("SignatureStatus").Single().Value == "NotVerified", "unsigned status");
                    for (int i = 0; i < bytes.Length; i++) { int length = i; Reject(() => Parse(bytes.Take(length).ToArray()), "every truncated prefix"); }
                }
            var combined = Join(Block(1,1), Block(1,2), Block(2,1), Block(2,2), Block(2,5));
            var decoded = Parse(combined);
            Check(decoded.Descendants("VehicleIdentificationNumber").Any(x => x.Value == "TESTVIN0000000001"), "VIN");
            Check(decoded.Descendants("OdometerValueMidnight").Any(x => x.Value == "66051"), "big-endian odometer");
            Check(decoded.Descendants("Latitude").Any(x => x.Value == "48"), "latitude");
            Check(decoded.Descendants("Longitude").Any(x => decimal.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture) < -14), "signed longitude");
            Check(decoded.Descendants("CountryEntered").Single().Value == "13", "border destination");
            Check(decoded.Descendants("ActivityChangeInfo").Attributes("Time").Any(x => x.Value == "01:00"), "activity bits");
            var unknownPosition = Block(2,2);
            // Locate the boundary record from its array header, then change its latitude to the unavailable sentinel.
            int border = Find(unknownPosition, new byte[] {34,0,55,0,1});
            unknownPosition[border+5+45]=0x7f; unknownPosition[border+5+46]=255; unknownPosition[border+5+47]=255;
            Check(Parse(unknownPosition).Descendants("Latitude").Any(x => (string)x.Attribute("Available") == "false"), "unknown coordinate sentinel");
            var interfaceXml = Parse(new byte[] {0x76,0,2,2});
            Check(interfaceXml.Descendants("DownloadInterfaceVersion").Single().Value == "0x0202", "download interface version");
            var reusable = VehicleUnitDataFile.Create(); Parse(Block(1,1), reusable); Parse(Block(2,2), reusable);
            Check(reusable.ProcessedRegions.Count == 1, "reader reuse");
            Reject(() => Parse(new byte[] {0x76}, reusable), "truncated identifier"); Check(reusable.ProcessedRegions.Count == 0, "failure clears output");
            Reject(() => Parse(new byte[] {0x76,0x7f}), "unknown identifier");
            byte[] bad = Block(1,2); bad[2] = 4; Reject(() => Parse(bad), "wrong record order");
            bad = Block(1,2); bad[3] = 0; bad[4] = 3; Reject(() => Parse(bad), "short record size");
            Validator.ValidateSignatures = true;
            Reject(() => Parse(Block(2,1)), "unsupported ECC validation fails closed"); Validator.ValidateSignatures = false;
            Check(!new Validator.SignatureMessageGen1(new byte[3]).IsValid(), "short RSA message");
            var padding = Enumerable.Repeat((byte)255,128).ToArray(); padding[0]=0; padding[1]=1;
            Check(!new Validator.SignatureMessageGen1(padding).IsValid(), "unterminated RSA padding");
            using (var shortStream = new ShortReadStream(Block(2,2))) Check(ParseStream(shortStream), "short stream reads");
            using (var shortStream = new ShortReadStream(Block(2,5))) Check(ParseStream(shortStream), "short serial-number reads");
            using (var stream = new MemoryStream(new byte[] {1,2,3,4}))
            {
                var cycle = new CyclicStream(stream,0,4); stream.Position=3; var reader = new CustomBinaryReader(cycle);
                Check(reader.ReadSInt16() == 0x0401, "cyclic boundary read");
                Check(cycle.Read(new byte[0],0,0) == 0, "zero-length cyclic read");
            }
            // Gen1 EF boundaries, ring pointers and data at offset zero.
            byte[] day = new byte[] {0,0,0,12,0x60,0,0,0,0,1,0,5};
            byte[] activity = Join(new byte[] {5,4,0,0,16,0,0,0,0}, day);
            var card = DriverCardDataFile.Create(); card.LogLevel=LogLevel.NONE;
            using (var stream = new MemoryStream(activity)) card.Process(stream);
            var ring = (CyclicalActivityRegion)((ContainerRegion)card.ProcessedRegions[0]).ProcessedRegions["CardDriverActivity"];
            Check(ring.ProcessedRegions.Count == 1, "one daily record at zero");
            byte[] emptyRing = new byte[16];
            using (var stream = new MemoryStream(Join(new byte[] {5,4,0,0,16},emptyRing))) card.Process(stream);
            ring = (CyclicalActivityRegion)((ContainerRegion)card.ProcessedRegions[0]).ProcessedRegions["CardDriverActivity"];
            Check(ring.ProcessedRegions.Count == 0, "empty ring consumed");
            byte[] badDay = (byte[])activity.Clone(); badDay[12] = 1;
            Reject(() => { using (var stream = new MemoryStream(badDay)) card.Process(stream); }, "daily record underflow");
            byte[] missingCount = Join(new byte[] {5,2,0,0,144},new byte[144]);
            Reject(() => { using (var stream = new MemoryStream(missingCount)) card.Process(stream); }, "missing count reference");
            byte[] overrun = new byte[] {5,7,0,0,1,0,5,14,0,0,4,0,0,0,0};
            Reject(() => { using (var stream = new MemoryStream(overrun)) card.Process(stream); }, "EF overrun");
            byte[] gen2Card = new byte[] {5,1,2,0,0};
            Reject(() => { using (var stream = new MemoryStream(gen2Card)) card.Process(stream); }, "empty Gen2 card TLV");
            using (var reader = new CustomBinaryReader(new MemoryStream(new byte[] {16,0xaa,0xfe})))
            {
                var name = new CodePageStringRegion(2); name.Process(reader); Check(name.ToString() == "\u0218\u021b", "ISO 8859-16");
            }
            using (var reader = new CustomBinaryReader(new MemoryStream(Join(new byte[] {4,1},Encoding.ASCII.GetBytes("ABCDEFGHIJKLM123")))))
            {
                var number = new FullCardNumberRegion { Name = "Number", LogLevel = LogLevel.NONE }; number.Process(reader);
                var sb = new StringBuilder(); using (var writer = XmlWriter.Create(sb)) number.ToXML(writer);
                var xml = XElement.Parse(sb.ToString()); Check(xml.Value == "ABCDEFGHIJKLM" && (string)xml.Attribute("ConsecutiveIndex") == "49", "company card number");
            }
            using (var referenceReader = new System.IO.BinaryReader(new MemoryStream(new byte[] {1,65,66,67,2,0x12,0x34,3})))
            {
                var reference = new Validator.CertificationAuthorityReference(referenceReader);
                Check(reference.additionalInfo == 0x1234, "authority reference byte order");
            }
            using (var reader = new CustomBinaryReader(new MemoryStream(new byte[4])))
            {
                var date = new DatefRegion {Name="Date"}; date.Process(reader);
                var sb = new StringBuilder(); using (var writer = XmlWriter.Create(sb)) date.ToXML(writer);
                Check((string)XElement.Parse(sb.ToString()).Attribute("Datef") == "", "unset birth date");
            }
            Reject(() => Parse(new byte[] {0x76,4,0,1,0,0,0,0,1,2,3}), "truncated legacy collection");
            var previousOutput = Console.Out;
            using (var silentOutput = new StringWriter())
            {
                try { Console.SetOut(silentOutput); Reject(() => Parse(new byte[] {0x76,0x7f}), "silent rejection"); }
                finally { Console.SetOut(previousOutput); }
                Check(silentOutput.ToString() == "", "LogLevel.NONE");
            }
            Parallel.For(0,20,n => { var reader = VehicleUnitDataFile.Create(); using (var stream = new MemoryStream(Block(n%2+1,2))) reader.Process(stream); });
            checks++;
            if (args.Length > 0)
            {
                if (args.Length > 1 && args[1] == "--validate-gen1") Validator.ValidateSignatures = true;
                var files = Directory.GetFiles(args[0],"*.DDD").OrderBy(x=>x).ToArray(); Check(files.Length == 5, "five supplied fixtures");
                for (int i = 0; i < files.Length; i++)
                {
                    byte[] bytes = File.ReadAllBytes(files[i]);
                    var xml = Parse(bytes);
                    if (i == 0)
                    {
                        var fileReader = VehicleUnitDataFile.Create(); fileReader.LogLevel=LogLevel.NONE;
                        using (var writer = XmlWriter.Create(Stream.Null)) fileReader.Process(files[i], writer);
                        using (var exclusive = new FileStream(files[i],FileMode.Open,FileAccess.Read,FileShare.None))
                            Check(exclusive.Length == bytes.Length, "owned filename handle disposed");
                    }
                    Check(xml.Element("TransferDataOverview") != null, "legacy overview");
                    Check(xml.Descendants("VehicleIdentificationNumber").Any(x=>x.Value.Length == 17), "legacy VIN length");
                    Check(xml.Elements("TransferDataActivities").Any(), "legacy activities");
                    if (Validator.ValidateSignatures)
                    {
                        byte[] tampered = (byte[])bytes.Clone(); tampered[390] ^= 1;
                        Reject(() => Parse(tampered), "tampered Gen1 signature");
                    }
                    Console.WriteLine("Fixture {0}: {1} bytes, {2} blocks", i+1, bytes.Length, xml.Elements().Count());
                }
            }
            Console.WriteLine("Passed {0} checks", checks); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static int Find(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length-pattern.Length; i++) if (data.Skip(i).Take(pattern.Length).SequenceEqual(pattern)) return i;
        throw new Exception("Missing synthetic record");
    }
    private static bool ParseStream(Stream stream) { var reader = VehicleUnitDataFile.Create(); reader.Process(stream); return stream.Position == stream.Length; }
    private sealed class ShortReadStream : MemoryStream
    {
        public ShortReadStream(byte[] bytes) : base(bytes) { }
        public override int Read(byte[] buffer, int offset, int count) { return base.Read(buffer,offset,Math.Min(count,1)); }
    }
}
