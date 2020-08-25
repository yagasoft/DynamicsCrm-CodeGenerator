#region Imports

using CrmCodeGenerator.VSPackage.Helpers;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.Libraries.EnhancedOrgService.Helpers;
using Yagasoft.Libraries.EnhancedOrgService.Pools;
using Yagasoft.Libraries.EnhancedOrgService.Services;
using static Yagasoft.CrmCodeGenerator.Helpers.ConnectionHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage.Connection
{
	public class ConnectionManager : IConnectionManager<IDisposableOrgSvc>
	{
		private static readonly object lockObj = new object();
		private IEnhancedServicePool<EnhancedOrgService> connectionPool;
		private string latestConnectionString;

		public IDisposableOrgSvc Get(string connectionString = null)
		{
			lock (lockObj)
			{
				if (connectionPool == null || connectionString != latestConnectionString)
				{
					Status.Update($"Creating connection pool to CRM ... ");
					Status.Update($"Connection String: '{SecureConnectionString(connectionString)}'.");

					connectionPool = EnhancedServiceHelper.GetPool(connectionString, 10);
					latestConnectionString = connectionString;

					Status.Update($"Created connection pool.");
				} 
			}

			var service = new DisposableOrgSvc(connectionPool.GetService());

			return service;
		}
	}
}
