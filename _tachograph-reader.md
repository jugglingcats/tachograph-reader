# Tachograph reader

This branch maintains the supplied C# reader. The source entry points are
`VehicleUnitDataFile.Create()` and `DriverCardDataFile.Create()`; `Process` reads a
seekable stream or a filename and `ToXML` exports the parsed regions. File parsing
is serialized because the legacy region and validator APIs use shared static
state. Do not change global processing/validation switches while parsing; reader
instances and their XML exports are not safe for concurrent reuse. Caller streams
stay open; filename and configuration handles are owned
and disposed by the library. A failed parse clears the file's results.

## Vehicle-unit downloads

`src/VehicleUnitData.config` dispatches generation 1 identifiers 7601-7605,
Gen2 version 1 identifiers 7621-7625, and Gen2 version 2 identifiers
7631, 7632, 7633 and 7635. Detailed speed remains 7624 for both Gen2 versions.
7600 contains the two-byte download interface version.

`Gen2VehicleUnitRegion` decodes the Appendix 7 record arrays in their defined
order. It checks array boundaries, fixed record widths, singleton cardinality and
signature size. Its `RecordArrays` expose type, width, byte offset and decoded
`XElement` records. XML distinguishes generation and version. Coordinates retain
the signed raw integer and export decimal degrees; 7FFFFF is unavailable.
The decoder covers overview, activities, events/faults, detailed speed and
technical records, including version 2 authenticated positions, border crossings,
load/unload entries, map versions and extended calibration records.

Certificates and ECC signatures are retained as opaque hex values. Gen2 output
has `SignatureStatus="NotVerified"`. `Validator.ValidateSignatures = true` rejects
Gen2 downloads because ECC trust-chain/signature verification is not implemented.
Gen1 retains its opt-in RSA validation; decoding does not establish authenticity.

## Driver-card downloads

`DriverCardDataFile.Create()` reads common files and Gen1 and Gen2 application
TLVs in the same download. The third tag byte selects Gen1 data/signatures
(00/01) or Gen2 data/signatures (02/03). Identical file IDs therefore use separate
layouts. `Gen2CardRegion` exposes decoded fields in `Data` (`XElement`), the
`BlockType`, application `Version` and the unverified signature bytes.
Gen2 signatures remain visible as separate XML elements with their block type.

A per-file `Gen2CardContext` reads application metadata before decoding records,
so metadata may appear after data and Gen1 counts cannot overwrite Gen2 counts.
Gen2 application versions 0100 and 0101 select card versions 1 and 2. Counts,
record sizes, physical ring indexes, TLV lengths and signature adjacency are
checked independently of strict mode. Fixed record arrays retain physical order
and their newest-record pointer; daily activities follow the cyclic record chain.
Missing count metadata, unsupported versions and malformed files fail explicitly.

Both card versions decode identification, licence, download time, eleven event
categories, faults, cyclic driver activities, vehicles with VINs, places with
GNSS positions, current usage, controls, specific conditions, vehicle units used
and accumulated-driving GNSS positions. Version 2 adds application metadata,
place/GNSS authentication status, border crossings, load/unload operations and
load-type entries. Authentication records remain separate and can be matched to
positions by their timestamps; missing authentication is not inferred as valid.
Certificates and manufacturer-specific VU configuration bytes remain opaque.
Length-prefixed application extensions retain unrecognized bytes. ECC validation
has the same explicit unsupported behavior as vehicle-unit downloads; decoding
is available with the default `Validator.ValidateSignatures = false`.

Unknown string encodings retain their raw bytes with `EncodingKnown="false"`;
known code pages decode without ASCII substitution. Unknown top-level identifiers
and truncated input fail in every mode.
`DataFile.StrictProcessing` additionally rejects unconsumed elementary-file bytes.

## Specification ownership

Gen2 version 1 follows the user-supplied
`../ECE-TRANS-SC1-GE21-INF-JUNE-2019-2e.-Rev.pdf`, Appendix 1 data dictionary and
Appendix 7 DDP_029-DDP_034. Gen2 version 2 adds the revised layouts in
[Commission Implementing Regulation (EU) 2021/1228](https://dtc.jrc.ec.europa.eu/iot_doc/EU_2021-1228.pdf),
Appendix 1 sections 2.120, 2.174, 2.203a, 2.205, 2.208a and 2.219,
and Appendix 7 DDP_028a-DDP_034. Card decoding additionally follows Appendix 1
card data types, Appendix 2 section 4.2.2 and Appendix 7 DDP_035-DDP_046.
DTCO 4.0 corresponds to Gen2 version 1;
DTCO 4.1 corresponds to Gen2 version 2. These product names are not wire versions.

## Build and checks

The library retains netstandard2.0 and netcoreapp2.1 and adds net9.0. The console
and dependency-free regression runner target net9.0. The legacy Framework console
remains available in the solution, but requires its original reference packs.

From this child repository, build the installed-SDK route with
`dotnet build core/tachograph-reader-core.csproj -p:TargetFrameworks=net9.0`.
Build all configured targets and the Framework console with
`dotnet build tachograph-reader.sln` when the required legacy SDK/reference packs
and packages are installed.

Complete local regression validation is
`dotnet build tests/tachograph-reader-tests.csproj -p:TargetFrameworks=net9.0`
followed by
`dotnet tests/bin/Debug/net9.0/tachograph-reader-tests.dll ..`.
The optional directory argument selects the five supplied DDD fixtures; omit it
for synthetic-only checks. No raw files or decoded personal data are embedded
in the tests. The supplied fixtures are Gen1, so real Gen2 hardware exports
remain an external compatibility evidence gap.

Synthetic tests cover both Gen2 versions and every supported transfer, nonempty
records, every truncated prefix, record widths/order, coordinate signs, activity
bits, malformed RSA padding, stream ownership, reuse, short reads and concurrent
file parses. Card checks cover both versions, realistic record capacities,
application metadata after data, mixed Gen1/Gen2 files, cyclic wrap, common files,
certificate/signature lengths, unknown GNSS positions, authentication records,
count/length mismatches, pointer bounds, signature adjacency and every truncated
small-fixture TLV. Real Gen2 driver-card downloads were not supplied. Append
`--validate-gen1` after the fixture directory to also verify the supplied
files with the Gen1 RSA path. CLI output is published only after successful
parsing, and input/output path equality is rejected. No hosted pipeline or
release route is defined by this change.
