using System;
using Microsoft.Xrm.Sdk;

namespace Yagasoft.CrmCodeGenerator.Connection
{
	public interface IConnectionManager
	{
		IOrganizationService Get();
	}
}
