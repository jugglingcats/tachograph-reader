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

		static CodePageStringRegion() {

			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

			foreach ( var i in Encoding.GetEncodings() ) {
				if (!encodingCache.ContainsKey(i.Name.ToUpper()))
					encodingCache.Add(i.Name.ToUpper(), i.GetEncoding());
			}

			charsetMapping[0] = "ASCII";
			charsetMapping[1] = "ISO-8859-1";
			charsetMapping[2] = "ISO-8859-2";
			charsetMapping[7] = "ISO-8859-7";

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
#if DEBUG
						// this message only makes sense in the testing or debugging context
						Console.Error.WriteLine("Unupported encoding {0}\n{1}", encodingName, e.Message);
#endif
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
