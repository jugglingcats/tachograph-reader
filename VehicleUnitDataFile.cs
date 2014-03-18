using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	/// <summary>
	/// This is the main data file handler. It can contain any number of sub-regions that
	/// are marked with a 'magic' number, as defined on page 160 of the specification.
	/// </summary>
	public class VehicleUnitDataFile : DataFile
	{
		public static DataFile Create()
		{
			// construct using embedded config
			Assembly a = typeof(VehicleUnitDataFile).Assembly;
			string name = a.FullName.Split(',')[0]+".VehicleUnitData.config";
			Stream stm = a.GetManifestResourceStream(name);
			XmlTextReader xtr=new XmlTextReader(stm);

			return Create(xtr);
		}
	}

}
