#region Imports

using System.Linq;
using System.Text.RegularExpressions;
using Yagasoft.CrmCodeGenerator.Connection;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class ConnectionHelpers
	{
		private static readonly object lockObj = new object();

		public static string SecureConnectionString(string connectionString)
		{
			return Regex
				.Replace(Regex
					.Replace(connectionString, @"Password\s*?=.*?(?:;{0,1}$|;)", "Password=********;")
					.Replace("\r\n", " "),
					@"\s+", " ")
				.Replace(" = ", "=");
		}
	}
}
