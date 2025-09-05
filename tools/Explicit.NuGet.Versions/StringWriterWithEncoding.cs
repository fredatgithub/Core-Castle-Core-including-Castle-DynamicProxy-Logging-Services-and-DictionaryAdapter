using System.IO;
using System.Text;

namespace Explicit.NuGet.Versions
{
	public sealed class StringWriterWithEncoding : StringWriter
	{
		private readonly Encoding encoding;

		public StringWriterWithEncoding(Encoding encoding)
		{
			this.encoding = encoding;
		}

		public override Encoding Encoding
		{
			get { return encoding; }
		}
	}
}
