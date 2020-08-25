#region Imports

using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.Libraries.EnhancedOrgService.Services;

#endregion

namespace CrmCodeGenerator.VSPackage.Connection
{
	public class DisposableOrgSvc : DisposableOrgSvcBase
	{
		public DisposableOrgSvc(IEnhancedOrgService innerService) : base(innerService)
		{ }

		public override void Dispose()
		{
			((IEnhancedOrgService)InnerService).Dispose();
		}
	}
}
