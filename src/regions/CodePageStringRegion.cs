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

		private byte codePage;
		private byte[] undecoded;

		static CodePageStringRegion() {
#if NET9_0_OR_GREATER
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
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
					} catch (ArgumentException) {
						// Fail only if this encoding is actually used by the input.
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
			codePage = codepage;
			undecoded = null;
			if (codepage == 16)
			{
				// ISO/IEC 8859-16:2001; Unicode mapping: unicode.org/Public/MAPPINGS/ISO8859/8859-16.TXT
				const string high = "\u00A0\u0104\u0105\u0141\u20AC\u201E\u0160\u00A7\u0161\u00A9\u0218\u00AB\u0179\u00AD\u017A\u017B\u00B0\u00B1\u010C\u0142\u017D\u201D\u00B6\u00B7\u017E\u010D\u0219\u00BB\u0152\u0153\u0178\u017C\u00C0\u00C1\u00C2\u0102\u00C4\u0106\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF\u0110\u0143\u00D2\u00D3\u00D4\u0150\u00D6\u015A\u0170\u00D9\u00DA\u00DB\u00DC\u0118\u021A\u00DF\u00E0\u00E1\u00E2\u0103\u00E4\u0107\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF\u0111\u0144\u00F2\u00F3\u00F4\u0151\u00F6\u015B\u0171\u00F9\u00FA\u00FB\u00FC\u0119\u021B\u00FF";
				var bytes = reader.ReadExactlyBytes(Length);
				int end = Array.IndexOf(bytes, (byte)0);
				if (end < 0) end = bytes.Length;
				while (end > 0 && bytes[end - 1] == 255) end--;
				var chars = new char[end];
				for (int i = 0; i < end; i++) chars[i] = bytes[i] < 160 ? (char)bytes[i] : high[bytes[i] - 160];
				text = new string(chars).Trim();
				return;
			}
			if (codepage == 255)
			{
				var bytes = reader.ReadExactlyBytes(Length);
				bool empty = true;
				foreach (byte value in bytes) empty &= value == 0 || value == 255 || value == 32;
				if (!empty) undecoded = bytes;
				text = string.Empty;
				return;
			}
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
				if (charsetMapping.ContainsKey(codepage))
					throw new ReaderException("Unsupported code page " + codepage);
				undecoded = reader.ReadExactlyBytes(Length);
				text = string.Empty;
				return;
			}

			// read string using encoding
			base.ProcessInternal(reader, enc);
		}
		protected override void InternalToXML(XmlWriter writer)
		{
			writer.WriteAttributeString("CodePage", codePage.ToString());
			if (undecoded != null)
			{
				writer.WriteAttributeString("EncodingKnown", "false");
				writer.WriteAttributeString("RawValue", HexValueRegion.ToHexString(undecoded));
			}
			base.InternalToXML(writer);
		}
	}
}
