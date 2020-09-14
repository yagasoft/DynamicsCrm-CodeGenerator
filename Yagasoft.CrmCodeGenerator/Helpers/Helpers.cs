#region Imports

using System;
using System.Collections.Generic;
using System.Reflection;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class ObjectExtensions
	{
		public static bool IsPrimitive(this Type type)
		{
			if (type == typeof(string))
			{
				return true;
			}

			return type.IsValueType & type.IsPrimitive;
		}
	}
}
