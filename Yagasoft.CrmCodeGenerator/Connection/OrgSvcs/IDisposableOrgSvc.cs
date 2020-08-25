#region Imports

using System;
using Microsoft.Xrm.Sdk;

#endregion

namespace Yagasoft.CrmCodeGenerator.Connection.OrgSvcs
{
	public interface IDisposableOrgSvc : IOrganizationService, IDisposable
	{ }
}
