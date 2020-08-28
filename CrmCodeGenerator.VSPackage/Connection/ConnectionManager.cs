#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.Libraries.Common;
using Yagasoft.Libraries.EnhancedOrgService.Helpers;
using Yagasoft.Libraries.EnhancedOrgService.Pools;
using Yagasoft.Libraries.EnhancedOrgService.Services;
using static Yagasoft.CrmCodeGenerator.Helpers.ConnectionHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage.Connection
{
	public class ConnectionManager : IConnectionManager<IDisposableOrgSvc>
	{
		public int Threads
		{
			get => threads;
			set
			{
				if (value > threads)
				{
					connectionPool = null;
				}

				threads = value;
			}
		}

		private IEnhancedServicePool<EnhancedOrgService> connectionPool;
		private string latestConnectionString;
		private int threads;

		public ConnectionManager(int threads = 3)
		{
			Threads = threads;
		}

		public IDisposableOrgSvc Get(string connectionString = null)
		{
			lock (this)
			{
				if (connectionString.IsFilled() && (connectionPool == null || connectionString != latestConnectionString))
				{
					Status.Update($"Creating connection pool to CRM ... ");
					Status.Update($"Connection String: '{SecureConnectionString(connectionString)}'.");

					connectionPool = EnhancedServiceHelper.GetPool(connectionString, Threads);
					connectionPool.WarmUp();
					latestConnectionString = connectionString;

					Status.Update($"Created connection pool.");
				}
			}

			var service = new DisposableOrgSvc(connectionPool.GetService());

			return service;
		}
	}
}
