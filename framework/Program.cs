using System;
using System.IO;
using System.Text;
using System.Xml;
using DataFileReader;

namespace tachograph_reader
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ProcessDataDirs();
                return;
            }

            if (args.Length < 2)
            {
                Console.Error.WriteLine("Expected --driver <file> or --vehicle <file> [output file]");
                return;
            }

            DataFile proc = null;
            if (args[0] == "--driver")
            {
                proc = DriverCardDataFile.Create();
            }

            if (args[0] == "--vehicle")
            {
                proc = VehicleUnitDataFile.Create();
            }

            if (proc == null)
            {
                Console.Error.WriteLine("Expected --driver <file> or --vehicle <file> [output file]");
                return;
            }


            proc.LogLevel = LogLevel.DEBUG;
            var xtw = args.Length > 2 ? new XmlTextWriter(args[2], Encoding.UTF8) : new XmlTextWriter(Stream.Null, Encoding.UTF8);
            try
            {
                xtw.Formatting = Formatting.Indented;
                proc.Process(args[1], xtw);
            }
            finally
            {
                xtw.Close();
            }

        }

        private static void ProcessDataDirs()
        {
            ProcessDataDir("driver", () => DriverCardDataFile.Create());
            ProcessDataDir("vehicle", () => VehicleUnitDataFile.Create());
        }

        private static void ProcessDataDir(string type, Func<DataFile> factory)
        {
            var files = Directory.GetFiles("data/" + type);
            foreach (var f in files)
            {
                var proc = factory();
                ProcessFile(proc, f);
            }
        }

        private static void ProcessFile(DataFile proc, string f)
        {
            var xtw = new XmlTextWriter(Stream.Null, Encoding.UTF8);
            try
            {
                proc.Process(f, xtw);
            }
            finally
            {
                xtw.Close();
            }
        }
    }
}
