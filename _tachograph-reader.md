# Tachograph reader

The public [usage guide](README.md) owns supported-format summaries, build and
CLI commands, API examples, signature limitations and regression instructions.
This document owns parser boundaries and wire-format details for maintainers.

## Entry points and state

`VehicleUnitDataFile.Create()` and `DriverCardDataFile.Create()` deserialize the
embedded XML configuration into a `DataFile`. `DataFile.Process` dispatches
identified blocks; `ToXML` exports `ProcessedRegions`. Unknown identifiers,
truncation and Gen2 count/size mismatches fail explicitly. `StrictProcessing`
additionally rejects unconsumed Gen1 elementary-file bytes and malformed GNSS
coordinates.

Whole-file parsing is serialized because legacy regions and `Validator` use
static state. Keep global switches stable while parsing. Reader instances and
XML exports are not safe for concurrent reuse. Each invocation clears prior
results and global counts; failures clear the file's results. Caller streams
remain open, while filename/configuration handles are disposed by their owner.
`BoundedStream` preserves absolute offsets while preventing Gen1 elementary-file
reads from crossing declared boundaries. CLI output publication is owned by
`app/Program.cs`.

## Vehicle-unit decoding

`src/VehicleUnitData.config` dispatches Gen1 identifiers 7601-7605, Gen2 version 1
identifiers 7621-7625 and Gen2 version 2 identifiers 7631, 7632, 7633 and 7635.
Detailed speed remains 7624 for both Gen2 versions. 7600 contains the two-byte
download-interface version. DTCO 4.0/4.1 product names correspond to Gen2 wire
versions 1/2, not literal 4.0/4.1 wire values.

`Gen2VehicleUnitRegion` reads Appendix 7 record arrays in their prescribed order
and checks boundaries, record widths, singleton cardinality and signature sizes.
`RecordArrays` expose type, width, offset and decoded `XElement` records. Gen2
XML identifies generation/version and carries `SignatureStatus="NotVerified"`.
The partial `Gen2RecordReader` reuses primitive regions for fields shared by
vehicle units and cards; `src/Gen2CardDecoder.cs` owns its card-specific layouts.

## Driver-card decoding

The third TLV tag byte selects Gen1 data/signatures (00/01) or Gen2
data/signatures (02/03). Shared file IDs select separate layouts.
`Gen2CardRegion` exposes `Data`, `BlockType`, application `Version` and signature
bytes. Signature TLVs stay visible in XML and must immediately follow matching
Gen2 data TLVs. Zero/reserved TLV lengths are rejected. ICC/chip identification
EFs 0002/0005 accept Gen2 data tags and retain separate generation-specific XML
sections. These EFs are unsigned; download-time EF 050E accepts signatures.

A per-invocation `Gen2CardContext` scans card TLV headers and reads application
metadata before decoding records. Metadata can therefore follow its data;
Gen1 counts cannot overwrite Gen2 counts. Application versions 0100/0101 select
card versions 1/2. Missing count metadata, duplicate application files,
unsupported versions and count/pointer/length mismatches fail explicitly.
Fixed card arrays retain physical order and their newest-record pointer. Daily
activities follow the cyclic record chain through the newest record.

Gen1 common files and application layouts stay in `DriverCardData.config`.
Gen2 versions share identification, licence, download, event/fault, activity,
vehicle, place, current-use, control, condition, vehicle-unit and GNSS layouts.
Version 2 adds EFs 0525 (application extension), 0526/0527 (place/GNSS
authentication), 0528 (border crossings), 0529 (load/unload), 0530 (load types)
and 0540 (VU configuration). Authentication records stay separate and associate
with positions by timestamp; absence does not imply successful authentication.
Length-prefixed application extensions retain unknown trailing bytes.

## Data interpretation and verification limits

Coordinates retain the signed raw integer and export decimal degrees; 7FFFFF
means unavailable. Malformed coordinate values retain `Raw` with `Valid="false"`
and `Available="false"` instead of exporting a decimal position; strict mode
rejects them. Unknown string encodings retain raw bytes with
`EncodingKnown="false"`; supported code pages decode without ASCII substitution.
Workshop, control and company card numbers use the owner/consecutive-index
layout, while driver card numbers use the driver identification layout.

Gen2 certificates, ECC signatures and manufacturer VU configuration are opaque.
ECC trust-chain/signature verification is unsupported; enabling
`Validator.ValidateSignatures` rejects Gen2 application or vehicle-unit data.
Gen1 retains opt-in RSA validation. Parsing alone does not establish authenticity.

Private mixed Gen1/Gen2 driver-card exports cover both Gen2 versions. Synthetic
tests also cover card capacities through the version 2 load-operation limit.
Real Gen2 vehicle-unit exports remain unverified. The external fixture
mode uses the five original Gen1 vehicle-unit files and can check RSA signatures
and tamper rejection. Fixtures remain outside the child repository. Legacy
build targets and hosted CI remain unverified. See the public usage guide for
commands rather than treating synthetic coverage as device interoperability.

## Specification ownership

Gen2 version 1 follows the
[2019 UNECE specification](https://unece.org/DAM/trans/doc/2019/sc1/ECE-TRANS-SC1-GE21-INF-JUNE-2019-2e.-Rev.pdf):
Appendix 1 data dictionary, Appendix 2 section 4.2.2 and Appendix 7 DDP_029-DDP_046.
A separately supplied copy resides in the parent workspace's targets directory;
it is not distributed by this child repository.

Gen2 version 2 uses
[Regulation (EU) 2021/1228](https://dtc.jrc.ec.europa.eu/iot_doc/EU_2021-1228.pdf),
including Appendix 1 card types and sections 2.120, 2.174, 2.203a, 2.205, 2.208a
and 2.219, Appendix 2 card structures, and Appendix 7 DDP_028a-DDP_046. Inspect
the schema images as well as extracted text when changing layouts: several
record definitions and card file tables are images in the supplied PDF.
