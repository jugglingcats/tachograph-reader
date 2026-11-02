using System;
using System.IO;
using System.Text;
using System.Xml;
using DataFileReader;

namespace tachograph_reader
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                ProcessDataDirs();
                return 0;
            }

            if (args.Length < 2)
            {
                Console.Error.WriteLine("Expected --driver <file> or --vehicle <file> [output file]");
                return 2;
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
                return 2;
            }


            proc.LogLevel = LogLevel.INFO;
            string output = args.Length > 2 ? Path.GetFullPath(args[2]) : null;
            if (output != null && string.Equals(output, Path.GetFullPath(args[1]), StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Eingabe- und Ausgabedatei muessen unterschiedlich sein.");
                return 2;
            }
            string temporary = output == null ? null : Path.Combine(Path.GetDirectoryName(output), ".tacho-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (Stream stream = temporary == null ? Stream.Null : new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                using (var xtw = new XmlTextWriter(stream, Encoding.UTF8))
                {
                    xtw.Formatting = Formatting.Indented;
                    proc.Process(args[1], xtw);
                }
                if (output != null)
                {
                    if (File.Exists(output)) File.Replace(temporary, output, null);
                    else File.Move(temporary, output);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Datei konnte nicht verarbeitet werden: " + ex.Message);
                return 1;
            }
            finally
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
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
