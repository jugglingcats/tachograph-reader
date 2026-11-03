using System;
using System.IO;
using System.Reflection;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	/// <summary>
	/// Wrapper for DataFile that initialises with driver card config
	/// </summary>
	public class DriverCardDataFile : DataFile
	{
		public static DataFile Create()
		{
			// construct using embedded config
			Assembly a = typeof(DriverCardDataFile).GetTypeInfo().Assembly;
			string name = a.FullName.Split(',')[0]+".DriverCardData.config";
			using (Stream stm = a.GetManifestResourceStream(name))
			using (XmlReader xtr = XmlReader.Create(stm))
				return Create(xtr);
		}
	}
}
