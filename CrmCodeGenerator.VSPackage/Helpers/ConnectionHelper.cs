#region Imports

using System.Text.RegularExpressions;
using CrmCodeGenerator.VSPackage.Model;
using Microsoft.Xrm.Sdk;
using Yagasoft.Libraries.EnhancedOrgService.Helpers;
using Yagasoft.Libraries.EnhancedOrgService.Pools;
using Yagasoft.Libraries.EnhancedOrgService.Services;

#endregion

namespace CrmCodeGenerator.VSPackage.Helpers
{
	public class ConnectionHelper
	{
		private static readonly object lockObj = new object();
		private static IEnhancedServicePool<EnhancedOrgService> connectionPool;

		public static IEnhancedOrgService GetConnection(SettingsNew settings)
		{
			lock (lockObj)
			{
				if (connectionPool == null)
				{
					Status.Update($"Creating connection pool to CRM ... ");
					Status.Update($"Connection String: '{SecureConnectionString(settings.ConnectionString)}'.");

					connectionPool = EnhancedServiceHelper.GetPool(settings.ConnectionString, 10);

					Status.Update($"Created connection pool.");
				} 
			}

			var service = connectionPool.GetService();

			return service;
		}

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
