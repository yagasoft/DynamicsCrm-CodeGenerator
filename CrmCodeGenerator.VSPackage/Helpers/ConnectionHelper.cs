#region Imports

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
		private static IEnhancedServicePool<EnhancedOrgService> connectionPool;

		public static IEnhancedOrgService GetConnection(SettingsNew settings)
		{
			Status.Update($"Creating connection to CRM ... ");

			if (connectionPool == null)
			{
				connectionPool = EnhancedServiceHelper.GetPool(settings.ConnectionString);
			}

			var service = connectionPool.GetService();

			Status.Update($"Created connection.");

			return service;
		}
	}
}
