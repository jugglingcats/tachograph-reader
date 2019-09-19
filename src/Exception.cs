using System;

namespace DataFileReader
{
	public class ReaderException : Exception
	{
		public ReaderException()
		{
		}

		public ReaderException(string message) : base(message)
		{
		}

		public ReaderException(string message, Exception inner) : base(message, inner)
		{
		}
	}

	public class InvalidSignatureException : ReaderException
	{
		public InvalidSignatureException(string message) : base(message)
		{
		}
	}

	public class ExpiredCertificateException : InvalidSignatureException
	{
		public ExpiredCertificateException(string message) : base(message)
		{
		}
	}

}
