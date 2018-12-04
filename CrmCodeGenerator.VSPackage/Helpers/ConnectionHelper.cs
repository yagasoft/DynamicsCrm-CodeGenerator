using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrmCodeGenerator.VSPackage.Model;
using Microsoft.Xrm.Sdk.Discovery;
using System.Collections.ObjectModel;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using LinkDev.Libraries.Common;
using LinkDev.Libraries.EnhancedOrgService.Builders;
using LinkDev.Libraries.EnhancedOrgService.Factories;
using LinkDev.Libraries.EnhancedOrgService.Pools;
using LinkDev.Libraries.EnhancedOrgService.Services;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Tooling.Connector;

namespace CrmCodeGenerator.VSPackage.Helpers
{
	public class ConnectionHelper
    {
		public static IOrganizationService GetConnection(Settings settings, ref int connectionsCreated, int total = 1)
		{
			Status.Update($"Creating connection {connectionsCreated + 1} / {total} to CRM ... ");

			var connectionString = settings.GetOrganizationCrmConnectionString();
			var service = CreateCrmService(connectionString);

			Status.Update($"Created connection {++connectionsCreated} / {total}.");

			return service;
		}

		public static IOrganizationService GetConnection(Settings settings)
		{
			var temp = 0;
			return GetConnection(settings, ref temp);
		}

		private static string latestStringUsed = "";
		private static readonly object lockObject = new object();

		public static IOrganizationService CreateCrmService(string connectionString)
		{
			CrmServiceClient service;

			lock (lockObject)
			{
				if (latestStringUsed != connectionString
					&& !connectionString.ToLower().Contains("requirenewinstance"))
				{
					latestStringUsed = connectionString;
					connectionString = connectionString.Trim(';', ' ');
					connectionString += ";RequireNewInstance=true";
				}

				service = new CrmServiceClient(connectionString);
			}

			var escapedString = Regex.Replace(connectionString, @"Password\s*?=.*?(?:;{0,1}$|;)",
				"Password=********;");

			try
			{
				if (!string.IsNullOrEmpty(service.LastCrmError) || service.LastCrmException != null)
				{
					throw new ServiceActivationException(
						$"Can't create connection to: \"{escapedString}\" due to \"{service.LastCrmError}\"");
				}

				return service;
			}
			catch (Exception ex)
			{
				var errorMessage = service.LastCrmError
					?? (service.LastCrmException != null ? CrmHelpers.BuildExceptionMessage(service.LastCrmException) : null)
						?? CrmHelpers.BuildExceptionMessage(ex);
				throw new ServiceActivationException($"Can't create connection to: \"{escapedString}\" due to\r\n{errorMessage}");
			}
		}

		public static OrganizationDetail GetOrganizationDetails(Settings settings)
        {
            var orgs = GetOrganizations(settings);
            var details = orgs.FirstOrDefault(d => d.UrlName == settings.CrmOrg);
            return details;
        }

        public static ObservableCollection<string> GetOrgList(Settings settings)
        {
            var orgs = GetOrganizations(settings);
            var newOrgs = new ObservableCollection<string>(orgs.Select(d => d.UrlName).ToList());
            return newOrgs;
        }

        public static OrganizationDetailCollection GetOrganizations(Settings settings)
        {
			var credentials = new ClientCredentials();

			if (settings.UseIFD || settings.UseOffice365 || settings.UseOnline)
			{
				credentials.UserName.UserName = settings.Username;
				credentials.UserName.Password = settings.Password;
			}
			else
			{
				credentials.Windows.ClientCredential =
					new System.Net.NetworkCredential(settings.Username, settings.Password, settings.Domain);
			}

			using (var discoveryProxy = new DiscoveryServiceProxy(settings.GetDiscoveryUri(), null, credentials, null))
			{
				discoveryProxy.Authenticate();

				var retrieveOrganizationsRequest = new RetrieveOrganizationsRequest
												   {
													   AccessType = EndpointAccessType.Default,
													   Release = OrganizationRelease.Current
												   };

				var retrieveOrganizationsResponse = (RetrieveOrganizationsResponse) discoveryProxy
					.Execute(retrieveOrganizationsRequest);

				return retrieveOrganizationsResponse.Details;
			}
        }
	}
}
