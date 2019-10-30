# Tachograph Reader

This is a C# project I worked on with my late Godfather, James "Walter" Boyle. He wrote some sophisticated software to handle analogue tachographs and report on working hours, compliance to speed limits, etc, and needed to add support for the digital tachograph system that was introduced in around 2002 (EEC 3821/85).

### Usage

Usage is quite simple. There is a main class DataFileReader and two subclasses: VehicleUnitDataFile and DriverCardDataFile. You can create an instance of one of the sublasses using the following methods:

```c#
DataFile vudf=VehicleUnitDataFile.Create();
DataFile dcdf=DriverCardDataFile.Create();
```

Once you have a reader instance you can give it a binary file to read and an XML Writer:

```c#
vudf.Process("file.ddd", writer);
```

Or alternativly you can work with proccessed regions directly, like

```c#
vudf.Process(ddd);
var transferDataOverview = (IdentifiedObjectRegion)vudf.ProcessedRegions.Where(r => r.Name == "TransferDataOverview").First();
var vehicleIdentificationNumber = transferDataOverview.ProcessedRegions["VehicleIdentificationNumber"];
```

#### Signature Validation

By default signature validation is disabled, to enable:

```c#
Validator.ValidateSignatures = true;
```

Now when processing file if there will any issue with validating signed data `InvalidSignatureException` will be thrown.

### About project

The project tachograph-reader.csproj is suitable for using with .NET Core and VScode, and contains a simple command line app. Run the command without args to scan ./data/vehicle and ./data/driver and process all files found.
Run the command with --driver <driverfile> or --vehicle <vehiclefile> to process individual files. You can also specify an output filename for the resulting XML. Tasks are set up in VScode for running the command.

Most of the sections/features of both data file formats are catered for. It's possible to modify the data file formats using `DriverCardData.config` and `VehicleUnitData.config`. These are two XML files defining the structure of the data with features specific to the standard (such as cyclic buffer support).

The standard is not particularly well written and it took quite a bit of work to decipher it. This project encapsulates that learning in a reasonably succint form, mainly in the XML config files. Some code comments refer to pages in the specification 
[1360_2002-Annex_1B-GB.pdf](./1360_2002-Annex_1B-GB.pdf) which is available in this repository for convenience.

It's amusing that the authors of the standard went to great pains to fit the data in the space available on the cards of the time... a very cheap USB stick would now hold a lifetime's worth of driver data!

I created this project with the hope that someone would find it useful and clearly people have. There have been number of great pull requests and people are starting to think about v2 file format support (see issues).

If you have issues or questions, other developers may be able to help so please raise an issue. Even better, create your own pull request!

---

Copyright (C) 2014-2019 Alfie Kirkpatrick

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
