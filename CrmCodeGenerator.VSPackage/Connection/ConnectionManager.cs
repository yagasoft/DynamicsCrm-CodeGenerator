#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.Libraries.Common;
using Yagasoft.Libraries.EnhancedOrgService.Helpers;
using Yagasoft.Libraries.EnhancedOrgService.Params;
using Yagasoft.Libraries.EnhancedOrgService.Pools;
using Yagasoft.Libraries.EnhancedOrgService.Services.Enhanced;
using static Yagasoft.CrmCodeGenerator.Helpers.ConnectionHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage.Connection
{
	public class ConnectionManager : IConnectionManager
	{
		public string ConnectionString
		{
			get => connectionString;
			set
			{
				lock (this)
				{
					if (value != connectionString)
					{
						service = null;
					}

					connectionString = value;
				}
			}
		}

		public int Threads
		{
			get => threads;
			set
			{
				if (value > threads)
				{
					service = null;
				}

				threads = value;
			}
		}

		private IEnhancedOrgService service;
		private string connectionString;
		private int threads;

		public ConnectionManager(int threads = 3)
		{
			Threads = threads;
		}

		public IOrganizationService Get()
		{
			try
			{
				lock (this)
				{
					if (service != null)
					{
						return service;
					}

					Status.Update($"[Connection] Creating connection to CRM ... ");
					Status.Update($"[Connection] Connection String: '{SecureConnectionString(connectionString)}'.");

					var connectionPool = EnhancedServiceHelper.GetPool(connectionString, new PoolParams { DequeueTimeoutInMillis = 20 * 1000 });
					connectionPool.WarmUp();

					service = connectionPool.GetService(threads);

					Status.Update($"[Connection] [DONE] Created connection.");

					return service;
				}
			}
			catch (Exception)
			{
				service = null;
				throw;
			}
		}
	}
}
