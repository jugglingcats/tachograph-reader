# Tachograph Reader

This is a C# project I worked on with my late Godfather, James "Walter" Boyle. He wrote some sophisticated software to handle analogue tachographs and report on working hours, compliance to speed limits, etc, and needed to add support for the digital tachograph system that was introduced in around 2002 (EEC 3821/85).

Usage is very simple. There is a main class DataFileReader and two subclasses: VehicleUnitDataFile and DriverCardDataFile. You can create an instance of one of the sublasses using the following method:

```c#
DataFileReader vudf=VehicleUnitDataFile.Create();
DataFileReader dcdf=DriverCardDataFile.Create();
```

Once you have an reader you can give it a file to read and an XML Writer:

```c#
vudf.Process("file.ddd", writer);
```

Most of the features of both data files are handled already. It's possible to modify the data file formats using DriverCardData.config and VehicleUnitData.config. These are two XML files defining the structure of the data with features specific to the standard such as cyclic buffers. It's amusing that they went to great pains to fit the data in the space available on the card... a very cheap USB stick would now hold a lifetime's worth of driver data!

I am distributing the code in the hope that someone finds it useful but without any warranty of any kind.

If you have issues or questions I may be happy to help - so please raise an issue.

---

Copyright (C) 2014 Alfie Kirkpatrick

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
