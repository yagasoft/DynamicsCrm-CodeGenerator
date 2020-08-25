using System;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;

namespace Yagasoft.CrmCodeGenerator.Connection
{
	public interface IConnectionManager<out TConnection> where TConnection : IDisposableOrgSvc
	{
		TConnection Get(string connectionString = null);
	}
}
