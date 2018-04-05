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
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "\t";

            FileStream fileStream_m = new FileStream("m_file.xml", FileMode.Create);
            XmlWriter writer_m = XmlWriter.Create(fileStream_m, settings);


            DataFile vudf = VehicleUnitDataFile.Create();

            //dcdf.Process("samples/ddd/C_20170713_1355_P_MILOSEVIC_SRBSRB0000013490000.DDD", writer_c);
            vudf.Process("C:/cygwin64/home/ales.ferlan/Development/samples/ddd/M_20170303_1354_WDB96340610122689_20161203_20170303.DDD", writer_m);


            writer_m.Flush();
            fileStream_m.Flush();

            writer_m.Close();
            fileStream_m.Close();

        }
    }
}
