using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using DataFileReader;

namespace DataFileReader
{
	// A string that is prefixed by a code page byte
	public class CodePageStringRegion : SimpleStringRegion
	{
		static Dictionary<string, Encoding> encodingCache = new Dictionary<string, Encoding>();
		static Dictionary<byte, string> charsetMapping = new Dictionary<byte, string>();

		// private int codepage;

		static CodePageStringRegion() {
			foreach ( var i in Encoding.GetEncodings() ) {
				if (!encodingCache.ContainsKey(i.Name.ToUpper()))
					encodingCache.Add(i.Name.ToUpper(), i.GetEncoding());
			}

			charsetMapping[0] = "ASCII";
			charsetMapping[1] = "ISO-8859-1";
			charsetMapping[2] = "ISO-8859-2";
			charsetMapping[3] = "ISO-8859-3";
			charsetMapping[5] = "ISO-8859-5";
			charsetMapping[7] = "ISO-8859-7";
			charsetMapping[9] = "ISO-8859-9";
			charsetMapping[13] = "ISO-8859-13";
			charsetMapping[15] = "ISO-8859-15";
			charsetMapping[16] = "ISO-8859-16";
			charsetMapping[80] = "KOI8-R";
			charsetMapping[85] = "KOI8-U";

			// CodePagesEncodingProvider (System.Text.Encoding.CodePages package) on .NET Core by default (GetEncodings() method) supports only few encodings
			// https://msdn.microsoft.com/en-us/library/system.text.codepagesencodingprovider.aspx#Anchor_4
			// but if you call GetEncoding directly by name you can get other encodings too
			// so here we add those too to our cache
			foreach (var encodingName in charsetMapping.Values) {
				if (!encodingCache.ContainsKey(encodingName)) {
					try {
						var encoding = Encoding.GetEncoding(encodingName);
						encodingCache.Add(encodingName, encoding);
					} catch (ArgumentException e) {
						Console.Error.WriteLine("Warning! Current platform doesn't support encoding with name {0}\n{1}", encodingName, e.Message);
					}
				}
			}
		}

		public CodePageStringRegion()
		{
		}

		public CodePageStringRegion(int size) : base(size)
		{
		}

		protected override void ProcessInternal(CustomBinaryReader reader)
		{
			// get the codepage
			var codepage = reader.ReadByte();
			// codePage specifies the part of the ISO/IEC 8859 used to code this string

			string encodingName = "UNKNOWN";
			if (charsetMapping.ContainsKey(codepage)) {
				encodingName = charsetMapping[codepage];
			}

			Encoding enc = null;
			if (encodingCache.ContainsKey(encodingName)) {
				enc = encodingCache[encodingName];
			}

			if (enc == null) {
				// we want to warn if we didn't recognize codepage because using wrong codepage will cause use of wrong codepoints and thus incorrect data
				WriteLine(LogLevel.WARN, "Unknown codepage {0}", codepage);
				enc = Encoding.ASCII;
			}

			// read string using encoding
			base.ProcessInternal(reader, enc);
		}
	}
}
