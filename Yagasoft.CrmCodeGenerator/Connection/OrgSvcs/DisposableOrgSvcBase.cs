#region Imports

using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace Yagasoft.CrmCodeGenerator.Connection.OrgSvcs
{
	public abstract class DisposableOrgSvcBase : IDisposableOrgSvc
	{
		protected readonly IOrganizationService InnerService;

		protected DisposableOrgSvcBase(IOrganizationService innerService)
		{
			this.InnerService = innerService;
		}

		public virtual Guid Create(Entity entity)
		{
			return InnerService.Create(entity);
		}

		public virtual Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
		{
			return InnerService.Retrieve(entityName, id, columnSet);
		}

		public virtual void Update(Entity entity)
		{
			InnerService.Update(entity);
		}

		public virtual void Delete(string entityName, Guid id)
		{
			InnerService.Delete(entityName, id);
		}

		public virtual OrganizationResponse Execute(OrganizationRequest request)
		{
			return InnerService.Execute(request);
		}

		public virtual void Associate(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			InnerService.Associate(entityName, entityId, relationship, relatedEntities);
		}

		public virtual void Disassociate(string entityName, Guid entityId, Relationship relationship,
			EntityReferenceCollection relatedEntities)
		{
			InnerService.Disassociate(entityName, entityId, relationship, relatedEntities);
		}

		public virtual EntityCollection RetrieveMultiple(QueryBase query)
		{
			return InnerService.RetrieveMultiple(query);
		}

		public abstract void Dispose();
	}
}
