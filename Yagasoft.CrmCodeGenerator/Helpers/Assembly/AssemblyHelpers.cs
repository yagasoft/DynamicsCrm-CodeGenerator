#region File header

// Project / File: Yagasoft.CrmCodeGenerator / AssemblyHelpers.cs
//         Author: Ahmed Elsawalhy
//   Contributors:
//        Created: 2020 / 08 / 29
//       Modified: 2020 / 08 / 29

#endregion

#region Imports

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers.Assembly
{
	public static class AssemblyHelpers
	{
		// credit: http://blog.slaks.net/2013-12-25/redirecting-assembly-loads-at-runtime/
		public static void RedirectAssembly(string shortName, Version targetVersion, string publicKeyToken)
		{
			System.Reflection.Assembly Handler(object sender, ResolveEventArgs args)
			{
				// Use latest strong name & version when trying to load SDK assemblies
				var requestedAssembly = new AssemblyName(args.Name);

				if (requestedAssembly.Name != shortName && !requestedAssembly.FullName.Contains(shortName + ","))
				{
					return null;
				}

				Debug.WriteLine("Redirecting assembly load of " + args.Name + ",\tloaded by "
					+ (args.RequestingAssembly == null ? "(unknown)" : args.RequestingAssembly.FullName));

				requestedAssembly.Version = targetVersion;
				requestedAssembly.SetPublicKeyToken(new AssemblyName("x, PublicKeyToken=" + publicKeyToken).GetPublicKeyToken());
				requestedAssembly.CultureInfo = CultureInfo.InvariantCulture;

				AppDomain.CurrentDomain.AssemblyResolve -= Handler;

				var loadedAssembly = System.Reflection.Assembly.Load(requestedAssembly);
				return loadedAssembly;
			}

			AppDomain.CurrentDomain.AssemblyResolve += Handler;
		}
	}
}
