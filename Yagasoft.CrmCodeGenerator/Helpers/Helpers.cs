#region Imports

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#endregion

namespace Yagasoft.CrmCodeGenerator.Helpers
{
	public static class Extensions
	{
		public static bool IsPrimitive(this Type type)
		{
			if (type == typeof(string))
			{
				return true;
			}

			return type.IsValueType & type.IsPrimitive;
		}

		public static bool IsMembersEquals(this IEnumerable enumerable, IEnumerable otherEnumerable)
		{
			var otherCast = otherEnumerable?.Cast<object>().ToArray();
			return enumerable?.Cast<object>().Intersect(otherCast ?? new object[0]).Count() == otherCast?.Length;
		}
	}
}
