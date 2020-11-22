#region File header

// Project / File: Yagasoft.CrmCodeGenerator / Binder.cs
//         Author: Ahmed Elsawalhy
//   Contributors:
//        Created: 2020 / 08 / 29
//       Modified: 2020 / 08 / 29

#endregion

using System;
using System.Runtime.Serialization;

namespace Yagasoft.CrmCodeGenerator.Helpers.Assembly
{
	public class Binder : SerializationBinder
	{
		// credit: http://stackoverflow.com/a/18856352/1919456
		public override Type BindToType(string assemblyName, string typeName)
		{
			var currentAssemblyInfo = System.Reflection.Assembly.GetExecutingAssembly().FullName;

			var currentAssemblyName = currentAssemblyInfo.Split(',')[0];

			if (assemblyName.StartsWith(currentAssemblyName))
			{
				assemblyName = currentAssemblyInfo;
			}

			const string crmCodeGeneratorAssembly = "CrmCodeGenerator, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc04d039f850a1ff";

			assemblyName = assemblyName
				.Replace(crmCodeGeneratorAssembly, "CrmCodeGenerator");

			typeName = typeName
				.Replace(crmCodeGeneratorAssembly, "CrmCodeGenerator");

			return Type.GetType($"{typeName}, {assemblyName}");
		}
	}
}
