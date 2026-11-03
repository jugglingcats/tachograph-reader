# Tachograph Reader

This is a C# project I worked on with my late Godfather, James "Walter" Boyle. He wrote some sophisticated software to handle analogue tachographs and report on working hours, compliance to speed limits, etc, and needed to add support for the digital tachograph system that was introduced in around 2002 (EEC 3821/85).

## Supported downloads

This fork decodes tachograph downloads into XML and exposes the decoded regions
through a C# API.

| Format | Vehicle-unit downloads | Driver-card downloads |
|---|---|---|
| Generation 1 | Overview, activities, events/faults, detailed speed and technical data | Common files and the Gen1 driver-card application |
| Generation 2, version 1 (DTCO 4.0 vehicle units) | All five transfer groups, including GNSS positions | Identification, events/faults, activities, vehicles with VINs, places, controls, specific conditions, vehicle units used and GNSS positions |
| Generation 2, version 2 (DTCO 4.1 vehicle units) | Adds authenticated GNSS fields, border crossings, load/unload records, map versions and extended calibration data | Adds place/GNSS authentication records, border crossings, load/unload operations and load-type entries |

A Gen2 card download can contain both Gen1 and Gen2 application data. The reader
uses each TLV's type byte to select the correct layout, even when file IDs match.
Gen2 application-identification files may appear after their data files.

Gen2 certificates, signatures and manufacturer-specific VU configuration remain
opaque bytes. **ECC certificate-chain and signature verification is not
implemented.** Gen2 output carries `SignatureStatus="NotVerified"`. The new
formats have synthetic regression coverage, and private Gen2 version 1/2
driver-card exports have been decoded. Real Gen2 vehicle-unit exports remain
unverified. Decoding does not establish authenticity or regulatory compliance.

Malformed GNSS coordinates retain their raw value with `Valid="false"` and
`Available="false"`; they have no decimal position. Strict mode rejects them.

## Build

Install the .NET 9 SDK and run commands from this repository's root:

```powershell
dotnet build core/tachograph-reader-core.csproj -p:TargetFrameworks=net9.0
```

The library targets `netstandard2.0`, `netcoreapp2.1` and `net9.0`. The console
application and regression runner target `net9.0`. The command above selects the
.NET 9 build without restoring the older library targets. Building the entire
solution requires the corresponding legacy SDK/reference packs and dependencies:

```powershell
dotnet build tachograph-reader.sln
```

The legacy targets remain configured but have not been validated with these
extensions.

## Command-line usage

After building, decode one vehicle-unit or driver-card file into XML:

```powershell
dotnet core/bin/Debug/net9.0/tachograph-reader-core.dll --vehicle vehicle.ddd vehicle.xml
```

```powershell
dotnet core/bin/Debug/net9.0/tachograph-reader-core.dll --driver driver.ddd driver.xml
```

The output filename is optional. Without it, the file is decoded without saving
XML. With no arguments, the console reads all files in `data/vehicle` and
`data/driver`; both directories must exist.

Input and output paths must differ. The console publishes its output only after
parsing succeeds, preserving an existing output file after a parsing failure.
Exit codes are `0` for success, `1` for a processing failure and `2` for invalid
arguments or identical input/output paths.

## Library usage

The factories return `DataFile` instances configured for each download type:

```csharp
using System.IO;
using System.Xml;
using DataFileReader;

DataFile vehicle = VehicleUnitDataFile.Create();
using (XmlWriter writer = XmlWriter.Create("vehicle.xml",
    new XmlWriterSettings { Indent = true }))
{
    vehicle.Process("vehicle.ddd", writer);
}

DataFile card = DriverCardDataFile.Create();
using (Stream input = File.OpenRead("driver.ddd"))
{
    card.Process(input);
}
// Inspect card.ProcessedRegions, or call card.ToXML(writer).
```

Streams must support seeking. Caller-owned streams stay open after `Process`;
filename overloads close the handles they open. Each parse replaces the reader's
previous results; failed parses clear its results. Do not reuse one reader
concurrently or change global processing switches during a parse.

Gen1 blocks use the existing `IdentifiedObjectRegion`/`ElementaryFileRegion`
types. Gen2 vehicle blocks are `Gen2VehicleUnitRegion` instances whose
`RecordArrays` contain decoded `XElement` records. Gen2 card blocks are
`Gen2CardRegion` instances exposing `Data`, `BlockType`, `Version` and `Signature`.
Card block type `2` contains data; type `3` retains the associated ECC signature.
Fixed card arrays retain physical order and their newest-record pointer; daily
activities follow the cyclic record chain. See the
[maintainer reference](_tachograph-reader.md) for wire identifiers and ownership.

Unknown block identifiers and truncated input raise errors in every processing
mode. `DataFile.StrictProcessing = true` additionally rejects unconsumed Gen1
elementary-file bytes and malformed GNSS coordinates. Gen2 record widths and
counts are always checked.

### Signature validation

Validation is disabled by default. Enable the existing RSA validation path when
processing **Gen1-only** downloads:

```csharp
Validator.ValidateSignatures = true;
```

Gen1 signature failures raise `InvalidSignatureException`. A Gen2 application or
vehicle-unit data block raises `NotSupportedException` with validation enabled;
this also applies to mixed Gen1/Gen2 card downloads. Keep validation disabled for
Gen2 decoding until ECC verification is implemented.

## Regression checks

Build and run the dependency-free synthetic regression suite:

```powershell
dotnet build tests/tachograph-reader-tests.csproj -p:TargetFrameworks=net9.0
```

```powershell
dotnet tests/bin/Debug/net9.0/tachograph-reader-tests.dll
```

The suite covers both Gen2 versions, vehicle-unit transfer groups, driver-card
files, realistic card capacities, cyclic wrap, mixed applications, record
boundaries, malformed/truncated inputs, signatures, stream lifetime and reuse.
ECC signature tests check framing and preservation, not cryptographic validity.

The optional directory argument selects the **five original Gen1 vehicle-unit
fixtures** supplied with this workspace. Append `--validate-gen1` to check their
RSA signatures and tamper rejection as well. For the nested workspace layout:

```powershell
dotnet tests/bin/Debug/net9.0/tachograph-reader-tests.dll .. --validate-gen1
```

These private fixtures are not included in this fork. An ordinary clone can run
the synthetic suite without them. The fixture-directory mode expects exactly
five `*.DDD` files; it is not a general batch-import command.

## Format references and development

The original reader uses [Annex 1B](1360_2002-Annex_1B-GB.pdf). Gen1 layouts are
configured in [DriverCardData.config](src/DriverCardData.config) and
[VehicleUnitData.config](src/VehicleUnitData.config). Gen2 decoding uses
[Gen2VehicleUnitRegion.cs](src/regions/Gen2VehicleUnitRegion.cs),
[Gen2CardRegion.cs](src/regions/Gen2CardRegion.cs) and
[Gen2CardDecoder.cs](src/Gen2CardDecoder.cs).

Gen2 version 1 follows the
[2019 UNECE tachograph specification](https://unece.org/DAM/trans/doc/2019/sc1/ECE-TRANS-SC1-GE21-INF-JUNE-2019-2e.-Rev.pdf).
Version 2 adds the revised layouts from
[Commission Implementing Regulation (EU) 2021/1228](https://dtc.jrc.ec.europa.eu/iot_doc/EU_2021-1228.pdf).

Upstream identifies its [net5 branch](https://github.com/jugglingcats/tachograph-reader/tree/net5)
as the development reference. This fork's `tacho-4` branch is based on upstream
`master` and uses the targets described above. Upstream contributions may need
to be adapted to `net5`; this branch has not been rebased onto it.

---

Copyright (C) 2014-2020 Alfie Kirkpatrick

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
