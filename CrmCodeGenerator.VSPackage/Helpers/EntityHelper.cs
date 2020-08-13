#region Imports

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CrmCodeGenerator.VSPackage.Model;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using static CrmCodeGenerator.VSPackage.Helpers.MetadataCacheHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage.Helpers
{
	public class EntityHelper
	{
		public static string[] NonStandard =
		{
			"applicationfile"
			, "attachment" // Not included with CrmSvcUtil 6.0.0001.0061
			, "authorizationserver" // Not included with CrmSvcUtil 6.0.0001.0061
			, "businessprocessflowinstance"
			// Not included with CrmSvcUtil 2013  http://community.dynamics.com/crm/f/117/t/117642.aspx
			, "businessunitmap" // Not included with CrmSvcUtil 2013
			, "clientupdate" // Not included with CrmSvcUtil 2013
			, "commitment" // Not included with CrmSvcUtil 2013
			, "competitoraddress" //isn't include in CrmSvcUtil but it shows in the default solution
			, "complexcontrol" //Not Included with CrmSvcUtil 2013
			, "dependencynode" //Not Included with CrmSvcUtil 2013
			, "displaystringmap" // Not Included with CrmSvcUtil 2013
			, "documentindex" // Not Included with CrmSvcUtil 2013
			, "emailhash" // Not Included with CrmSvcUtil 2013
			, "emailsearch" // Not Included with CrmSvcUtil 2013
			, "filtertemplate" // Not Included with CrmSvcUtil 2013
			, "imagedescriptor" // Not included with CrmSvcUtil 2013
			, "importdata" // Not included with CrmSvcUtil 6.0.0001.0061
			, "integrationstatus" // Not included with CrmSvcUtil 6.0.0001.0061
			, "interprocesslock" // Not included with CrmSvcUtil 6.0.0001.0061
			, "multientitysearchentities" // Not included with CrmSvcUtil 6.0.0001.0061
			, "multientitysearch" // Not included with CrmSvcUtil 6.0.0001.0061
			, "notification" // Not included with CrmSvcUtil 6.0.0001.0061
			, "organizationstatistic" // Not included with CrmSvcUtil 6.0.0001.0061
			, "owner" // Not included with CrmSvcUtil 2013
			, "partnerapplication" // Not included with CrmSvcUtil 6.0.0001.0061
			, "principalattributeaccessmap" // Not included with CrmSvcUtil 6.0.0001.0061
			, "principalobjectaccessreadsnapshot" // Not included with CrmSvcUtil 6.0.0001.0061
			, "principalobjectaccess" // Not included with CrmSvcUtil 6.0.0001.0061
			, "privilegeobjecttypecodes" // Not included with CrmSvcUtil 6.0.0001.0061
			, "postregarding" // Not included with CrmSvcUtil 2013
			, "postrole" // Not included with CrmSvcUtil 2013
			, "subscriptionclients" // Not included with CrmSvcUtil 6.0.0001.0061
			, "salesprocessinstance" // Not included with CrmSvcUtil 6.0.0001.0061
			, "recordcountsnapshot" // Not included with CrmSvcUtil 6.0.0001.0061
			, "replicationbacklog" // Not included with CrmSvcUtil 6.0.0001.0061
			, "resourcegroupexpansion" // Not included with CrmSvcUtil 6.0.0001.0061
			, "ribboncommand" // Not included with CrmSvcUtil 6.0.0001.0061
			, "ribboncontextgroup" // Not included with CrmSvcUtil 6.0.0001.0061
			, "ribbondiff" // Not included with CrmSvcUtil 6.0.0001.0061
			, "ribbonrule" // Not included with CrmSvcUtil 6.0.0001.0061
			, "ribbontabtocommandmap" // Not included with CrmSvcUtil 6.0.0001.0061
			, "roletemplate" // Not included with CrmSvcUtil 6.0.0001.0061
			, "statusmap" // Not included with CrmSvcUtil 6.0.0001.0061
			, "stringmap" // Not included with CrmSvcUtil 6.0.0001.0061
			, "sqlencryptionaudit"
			, "subscriptionsyncinfo"
			, "subscription" // Not included with CrmSvcUtil 6.0.0001.0061
			, "subscriptiontrackingdeletedobject"
			, "systemapplicationmetadata" // Not included with CrmSvcUtil 6.0.0001.0061
			, "systemuserbusinessunitentitymap" // Not included with CrmSvcUtil 6.0.0001.0061
			, "systemuserprincipals" // Not included with CrmSvcUtil 6.0.0001.0061
			, "traceassociation" // Not included with CrmSvcUtil 6.0.0001.0061
			, "traceregarding" // Not included with CrmSvcUtil 6.0.0001.0061
			, "unresolvedaddress" // Not included with CrmSvcUtil 6.0.0001.0061
			, "userapplicationmetadata" // Not included with CrmSvcUtil 6.0.0001.0061
			, "userfiscalcalendar" // Not included with CrmSvcUtil 6.0.0001.0061
			, "webwizard" // Not included with CrmSvcUtil 6.0.0001.0061
			, "wizardaccessprivilege" // Not included with CrmSvcUtil 6.0.0001.0061
			, "wizardpage" // Not included with CrmSvcUtil 6.0.0001.0061
			, "workflowwaitsubscription" // Not included with CrmSvcUtil 6.0.0001.0061
			// the following cause duplicate errors in generated code
			, "bulkdeleteoperation"
			, "reportlink"
			, "rollupjob"
		};

		public static List<EntityMetadata> RefreshSettingsEntityMetadata(SettingsNew settings)
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);

			var entityProperties = new MetadataPropertiesExpression
								   {
									   AllProperties = false
								   };
			entityProperties.PropertyNames.AddRange("DisplayName", "SchemaName");

			var entityQueryExpression = new EntityQueryExpression
										{
											Criteria = entityFilter,
											Properties = entityProperties,
										};

			var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
												 {
													 Query = entityQueryExpression,
													 ClientVersionStamp = null
												 };

			EntityMetadataCollection result;

			using (var service = ConnectionHelper.GetConnection(settings))
			{
				result = ((RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest)).EntityMetadata;
			}

			var cache = GetMetadataCache(settings.ConnectionString);

			// cache the result
			var resultFiltered =
				cache.ProfileEntityMetadataCache =
					result.Where(entity =>
								 {
									 if (settings.IncludeNonStandard)
									 {
										 return true;
									 }

									 if (entity.SchemaName == null || entity.LogicalName == null)
									 {
										 return false;
									 }

									 return !NonStandard.Contains(entity.LogicalName);
								 }).ToList();

			// reset attributes cache as well
			cache.ProfileAttributeMetadataCache = new Dictionary<string, EntityMetadata>();

			var origSelection = settings.EntitiesToIncludeString;
			var newList = new ObservableCollection<string>();

			foreach (var entity in resultFiltered.OrderBy(e => e.LogicalName))
			{
				newList.Add(entity.LogicalName);
			}

			settings.EntityList = newList;
			settings.EntitiesToIncludeString = string.Join(",",
				origSelection.Split(',').Where(selection => settings.EntityList.Contains(selection)));

			// remove obsolete entities in the filters
			foreach (var filter in settings.EntityDataFilterArray.EntityFilters)
			{
				filter.EntityFilterList.RemoveAll(entity => !settings.EntityList.Contains(entity.LogicalName));
			}

			settings.FiltersChanged();

			return resultFiltered;
		}


		public static IDictionary<string, int> GetEntityCodes(SettingsNew settings)
		{
			var metadataCache = GetMetadataCache(settings.ConnectionString);

			if (metadataCache.EntityCodesCache != null)
			{
				return metadataCache.EntityCodesCache;
			}

			var entityProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			entityProperties.PropertyNames.AddRange("LogicalName", "ObjectTypeCode");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Properties = entityProperties
				};

			var retrieveMetadataChangesRequest =
				new RetrieveMetadataChangesRequest
				{
					Query = entityQueryExpression
				};

			using (var service = ConnectionHelper.GetConnection(settings))
			{
				return metadataCache.EntityCodesCache =
					((RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest)).EntityMetadata
						.ToDictionary(e => e.LogicalName, e => e.ObjectTypeCode.GetValueOrDefault());
			}
		}

		public static IEnumerable<string> RetrieveActionNames(SettingsNew settings, string logicalName = "none")
		{
			var fetchXml =
				$@"
<fetch no-lock='true' >
  <entity name='sdkmessage' >
    <attribute name='name' />
    <filter>
      <condition entityname='workflow' attribute='category' operator='eq' value='3' />
      <condition entityname='workflow' attribute='type' operator='neq' value='3' />
      <condition entityname='sdkmessagerequest' attribute='primaryobjecttypecode'
		operator='eq' value='{(logicalName == "none" ? 0 : GetEntityCodes(settings)[logicalName])}' />
    </filter>
    <link-entity name='sdkmessagepair' from='sdkmessageid' to='sdkmessageid'>
      <link-entity name='sdkmessagerequest' from='sdkmessagepairid' to='sdkmessagepairid'>
        <link-entity name='sdkmessageresponse' from='sdkmessagerequestid' to='sdkmessagerequestid'>
          <link-entity name='sdkmessageresponsefield' from='sdkmessageresponseid' to='sdkmessageresponseid' link-type='outer'>
          </link-entity>
        </link-entity>
        <link-entity name='sdkmessagerequestfield' from='sdkmessagerequestid' to='sdkmessagerequestid'>
          <filter>
            <condition attribute='fieldmask' operator='neq' value='4' />
          </filter>
        </link-entity>
      </link-entity>
    </link-entity>
    <link-entity name='workflowdependency' from='sdkmessageid' to='sdkmessageid' >
      <link-entity name='workflow' from='workflowid' to='workflowid' >
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

			using (var service = ConnectionHelper.GetConnection(settings))
			{
				return service.RetrieveMultiple(new FetchExpression(fetchXml))
					.Entities
					.Select(e => e.GetAttributeValue<string>("name"))
					.Distinct()
					.OrderBy(e => e);
			}
		}
	}
}
