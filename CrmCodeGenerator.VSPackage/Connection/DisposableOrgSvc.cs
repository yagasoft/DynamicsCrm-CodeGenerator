#region Imports

using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.Libraries.EnhancedOrgService.Services.Enhanced;

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
