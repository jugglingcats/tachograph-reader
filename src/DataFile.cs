using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DataFileReader;

namespace DataFileReader
{
	/// <summary>
	/// The core class that can read a configuration describing the tachograph data structure (one
	/// for driver cards, one for vehicle cards), then read the file itself into memory, then finally
	/// write the data as XML.
	/// </summary>
	public class DataFile : Region
	{
		public static bool StrictProcessing {get; set;} = false;

		[XmlIgnore]
		public ArrayList regions=new ArrayList();

		[XmlIgnore]
		public List<Region> ProcessedRegions {get; private set;} = new List<Region>();

		/// <summary>
		/// This method loads the XML config file into a new instance
		/// of VuDataFile, prior to processing.
		/// </summary>
		/// <param name="configFile">Config to load</param>
		/// <returns>A new instance ready for processing</returns>
		// 
		public static DataFile Create(string configFile)
		{
			XmlReader xtr=XmlReader.Create(File.OpenRead(configFile));
			return Create(xtr);
		}

		protected static DataFile Create(XmlReader xtr)
		{
			XmlSerializer xs=new XmlSerializer(typeof(DataFile));
			return (DataFile) xs.Deserialize(xtr);
		}

		/// Convenience method to open a file and process it
		public void Process(string dataFile, XmlWriter writer)
		{
			WriteLine(LogLevel.INFO, "Processing {0}", dataFile);
			Stream s = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
			Process(s, writer);
		}

		public void Process(Stream s)
		{
			CustomBinaryReader r = new CustomBinaryReader(s);
			Process(r);
		}

		public void Process(Stream s, XmlWriter writer)
		{
			this.Process(s);
			this.ToXML(writer);
		}

		/// This is the core method overridden by all subclasses of Region
		// TODO: M: very inefficient if no matches found - will iterate over WORDs to end of file
		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			WriteLine(LogLevel.DEBUG, "Processing...");
			SignatureRegion.Reset();
			Validator.Reset();

			var unmatchedRegions=0;
			// in this case we read a magic and try to process it
			while ( true )
			{
				byte[] magic=new byte[2];
				int bytesRead=reader.BaseStream.Read(magic, 0, 2);
				long magicPos = reader.BaseStream.Position - 2;

				if ( bytesRead == 0 )
					// end of file - nothing more to read
					break;

				if ( bytesRead == 1 )
				{
					// this can happen if zipping over unmatched bytes at end of file - should handle better
					if (DataFile.StrictProcessing)
					{
						throw new InvalidOperationException("Could only read one byte of identifier at end of stream");
					}
					break;
				}

				// test whether the magic matches one of our child objects
				string magicString=string.Format("0x{0:X2}{1:X2}", magic[0], magic[1]);
				bool matched = false;
				foreach ( IdentifiedObjectRegion r in regions )
				{
					if ( r.Matches(magicString) )
					{
						WriteLine(LogLevel.DEBUG, "Identified region: {0} with magic {1} at 0x{2:X4}", r.Name, magicString, magicPos);
						var newRegion = r.Copy();
						newRegion.Process(reader);
						this.ProcessedRegions.Add(newRegion);
						matched = true;
						break;
					}
				}

				if ( !matched ) {
					unmatchedRegions++;
					if ( unmatchedRegions == 1 ) {
						WriteLine(LogLevel.WARN, "First unrecognised region with magic {1} at 0x{1:X4}", magicString, magicPos);
					}
				}

				if (!matched && DataFile.StrictProcessing)
				{
					WriteLine(LogLevel.WARN, "Unrecognized magic=0x{0:X2}{1:X2} at offset 0x{2:X4}  ", magic[0], magic[1], magicPos);
					throw new NotImplementedException("Unrecognized magic " + magicString);
				}
			}
			if ( unmatchedRegions > 0 ) {
				WriteLine(LogLevel.WARN, "There were {0} unmatched regions (magics) in the file.", unmatchedRegions);
			}

			// ensure that all ElementaryFileRegion has signature present
			for (var i = 0; i < this.ProcessedRegions.Count; i++)
			{
				var region = this.ProcessedRegions[i];
				if (region is ElementaryFileRegion)
				{
					if (((ElementaryFileRegion)region).Unsigned || !Validator.ValidateSignatures)
						continue;

					if (i + 1 < this.ProcessedRegions.Count)
					{
						var nextRegion = this.ProcessedRegions[i + 1];
						if (region.Name == nextRegion.Name && ((ElementaryFileRegion)nextRegion).signature != null)
						{
							i++;
							continue;
						}
					}

					throw new InvalidSignatureException(string.Format("No signature present for {0}!", region.Name));
				}
			}

			Validator.CheckIfValidated(SignatureRegion.newestDateTime);
			WriteLine(LogLevel.DEBUG, "Processing done.");
		}

		/// This defines what children we can have from the XML config
		[XmlElement("IdentifiedObject", typeof(IdentifiedObjectRegion)),
		XmlElement("ElementaryFile", typeof(ElementaryFileRegion))]
		public ArrayList Regions
		{
			get { return regions; }
			set { regions = value; }
		}

		protected override void InternalToXML(XmlWriter writer)
		{
			foreach (var region in this.ProcessedRegions)
			{
				region.ToXML(writer);
			};
		}
	}
}
