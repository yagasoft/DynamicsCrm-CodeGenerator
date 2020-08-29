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

			return Type.GetType($"{typeName}, {assemblyName}");
		}
	}
}
