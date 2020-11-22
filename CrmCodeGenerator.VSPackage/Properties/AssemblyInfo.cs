using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("CrmCodeGenerator.VSPackage")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Ahmed Elsawalhy (yagasoft.com)")]
[assembly: AssemblyProduct("CrmCodeGenerator.VSPackage")]
[assembly: AssemblyCopyright("Yagasoft 2020")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]   
[assembly: ComVisible(false)]     
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers 
// by using the '*' as shown below:

[assembly: AssemblyVersion("10.7.3.0")]
[assembly: AssemblyFileVersion("10.7.3.0")]


[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Xrm.Sdk",
	CodeBase = @"$PackageFolder$\Microsoft.Xrm.Sdk.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "9.0.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Xrm.Sdk.Deployment",
	CodeBase = @"$PackageFolder$\Microsoft.Xrm.Sdk.Deployment.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "9.0.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Crm.Sdk.Proxy",
	CodeBase = @"$PackageFolder$\Microsoft.Crm.Sdk.Proxy.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "9.0.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Xrm.Tooling.Connector",
	CodeBase = @"$PackageFolder$\Microsoft.Xrm.Tooling.Connector.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.0.0.0")]

[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.IdentityModel.Clients.ActiveDirectory",
	CodeBase = @"$PackageFolder$\Microsoft.IdentityModel.Clients.ActiveDirectory.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "3.19.8.16603")]

[assembly: ProvideBindingRedirection(AssemblyName = "Newtonsoft.Json",
	CodeBase = @"$PackageFolder$\Newtonsoft.Json.dll",
	OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "10.0.0.0")]
