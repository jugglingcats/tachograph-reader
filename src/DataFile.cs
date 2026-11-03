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
			using (var stream = File.OpenRead(configFile))
			using (var xtr = XmlReader.Create(stream))
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
			using (Stream s = File.OpenRead(dataFile))
				Process(s, writer);
		}

		public void Process(Stream s)
		{
			using (var r = new CustomBinaryReader(s, true))
				Process(r);
		}

		public void Process(Stream s, XmlWriter writer)
		{
			this.Process(s);
			this.ToXML(writer);
		}

		private static readonly object processingLock = new object();

		public override void Process(CustomBinaryReader reader)
		{
			if (!reader.BaseStream.CanSeek) throw new NotSupportedException("A seekable stream is required");
			lock (processingLock)
			{
				ProcessedRegions.Clear();
				globalValues.Clear();
				try { base.Process(reader); }
				catch { ProcessedRegions.Clear(); throw; }
			}
		}

		/// This is the core method overridden by all subclasses of Region
		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			WriteLine(LogLevel.DEBUG, "Processing...");
			SignatureRegion.Reset();
			Validator.Reset();
			Gen2CardContext cardContext = null;
			foreach (Region configured in regions)
			{
				if (!(configured is ElementaryFileRegion) && !(configured is Gen2CardRegion)) continue;
				cardContext = Gen2CardContext.ReadApplications(reader);
				break;
			}

			// in this case we read a magic and try to process it
			while ( true )
			{
				byte[] magic=new byte[2];
				int bytesRead=reader.Read(magic, 0, 1);
				if (bytesRead == 1) bytesRead += reader.Read(magic, 1, 1);
				long magicPos = reader.BaseStream.Position - 2;

				if ( bytesRead == 0 )
					// end of file - nothing more to read
					break;

				if ( bytesRead == 1 )
				{
					throw new EndOfStreamException("Truncated block identifier");
				}

				// test whether the magic matches one of our child objects
				string magicString=string.Format("0x{0:X2}{1:X2}", magic[0], magic[1]);
				bool matched = false;
				foreach ( IdentifiedObjectRegion r in regions )
				{
					if ( r.Matches(magicString) )
					{
						WriteLine(LogLevel.DEBUG, "Identified region: {0} with magic {1} at 0x{2:X4}", r.Name, magicString, magicPos);
						Region newRegion;
						long payloadPosition = reader.BaseStream.Position;
						int blockType = cardContext != null ? reader.ReadByte() : -1;
						reader.BaseStream.Position = payloadPosition;
						if (r is ElementaryFileRegion && (blockType == 2 || blockType == 3))
							newRegion = new Gen2CardRegion { Identifier = magicString, LogLevel = r.LogLevel };
						else newRegion = r.Copy();
						var gen2Card = newRegion as Gen2CardRegion;
						if (gen2Card != null) gen2Card.Context = cardContext;
						newRegion.Process(reader);
						if (gen2Card != null && gen2Card.BlockType == 3)
						{
							var previous = ProcessedRegions.Count == 0 ? null : ProcessedRegions[ProcessedRegions.Count - 1] as Gen2CardRegion;
							if (previous == null || previous.BlockType != 2 || previous.Identifier != gen2Card.Identifier)
								throw new ReaderException("Gen2 signature must immediately follow its matching data EF");
						}
						this.ProcessedRegions.Add(newRegion);
						matched = true;
						break;
					}
				}

				if (!matched)
				{
					WriteLine(LogLevel.WARN, "Unrecognized magic=0x{0:X2}{1:X2} at offset 0x{2:X4}  ", magic[0], magic[1], magicPos);
					throw new ReaderException(string.Format("Unrecognized block {0} at 0x{1:X}", magicString, magicPos));
				}
			}

			if (ProcessedRegions.Count == 0) throw new ReaderException("No tachograph blocks found");

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
						if (region.Name == nextRegion.Name && nextRegion is ElementaryFileRegion && ((ElementaryFileRegion)nextRegion).signature != null)
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
		XmlElement("ElementaryFile", typeof(ElementaryFileRegion)),
		XmlElement("Gen2VehicleUnit", typeof(Gen2VehicleUnitRegion)),
		XmlElement("Gen2Card", typeof(Gen2CardRegion))]
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
