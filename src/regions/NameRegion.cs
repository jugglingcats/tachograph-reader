using System;
using DataFileReader;

namespace DataFileReader
{
	// A name is a string with codepage with fixed length = 35
	public class NameRegion : CodePageStringRegion
	{
		private static readonly int SIZE=35;

		public NameRegion() : base(SIZE)
		{
		}
	}
}
