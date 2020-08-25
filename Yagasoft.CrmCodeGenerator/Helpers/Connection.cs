#region Imports

using System.Text.RegularExpressions;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class ConnectionHelpers
	{
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
