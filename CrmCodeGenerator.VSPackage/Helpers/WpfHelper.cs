using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Yagasoft.Libraries.Common;

namespace CrmCodeGenerator.VSPackage.Helpers
{
	class WpfHelper
	{
	}

	public static class Extensions
	{
		public static T GetChild<T>(this DependencyObject depObj) where T : DependencyObject
		{
			if (depObj == null)
			{
				return null;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				var child = VisualTreeHelper.GetChild(depObj, i);

				var result = (child as T) ?? GetChild<T>(child);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public static T GetParent<T>(this DependencyObject child) where T : DependencyObject
		{
			while (true)
			{
				//get parent item
				var parentObject = VisualTreeHelper.GetParent(child);

				//we've reached the end of the tree
				if (parentObject == null)
				{
					return null;
				}

				//check if the parent matches the type we're looking for
				var parent = parentObject as T;

				if (parent != null)
				{
					return parent;
				}

				child = parentObject;
			}
		}

		public static IDictionary<TU, TV> RemoveDefaultValued<TU, TV>(this IDictionary<TU, TV> dictionary)
		{
			var keys = dictionary
				.Where(p => p.Value.Equals(default(TV)))
				.Select(p => p.Key).ToArray();

			foreach (var key in keys)
			{
				dictionary.Remove(key);
			}

			return dictionary;
		}

		public static ICollection<TV> RemoveDefaultValued<TV>(this ICollection<TV> collection)
		{
			var emptyValues = collection.Where(e => e.Equals(default(TV))).ToArray();

			foreach (var value in emptyValues)
			{
				collection.Remove(value);
			}

			return collection;
		}

		public static ICollection<TV> RemoveEmpty<TV>(this ICollection<TV> collection)
		{
			var values = collection.RemoveDefaultValued()
				.Where(e => (e is string stringValue) && stringValue.IsEmpty()).ToArray();

			foreach (var value in values)
			{
				collection.Remove(value);
			}

			return collection;
		}

		public static IDictionary<TU, TV> RemoveEmpty<TU, TV>(this IDictionary<TU, TV> dictionary)
		{
			var keys = dictionary.RemoveDefaultValued()
				.Where(p => (p.Value is string stringValue) && stringValue.IsEmpty())
				.Select(p => p.Key).ToArray();

			foreach (var key in keys)
			{
				dictionary.Remove(key);
			}

			return dictionary;
		}

		public static ICollection<ICollection<TV>> RemoveEmpty<TV>(this ICollection<ICollection<TV>> collection)
		{
			var emptyValues = collection.Where(e => e.Equals(default(TV))).ToArray();

			foreach (var value in emptyValues)
			{
				collection.Remove(value);
			}

			foreach (var value in collection.ToArray())
			{
				value.RemoveEmpty();
			}

			return collection;
		}

		public static IDictionary<TU, ICollection<TV>> RemoveEmpty<TU, TV>(this IDictionary<TU, ICollection<TV>> dictionary)
		{
			var keys = dictionary.RemoveDefaultValued()
				.Where(p => p.Value.Count <= 0)
				.Select(p => p.Key).ToArray();

			foreach (var key in keys)
			{
				dictionary.Remove(key);
			}

			foreach (var value in dictionary.Select(p => p.Value).ToArray())
			{
				value.RemoveEmpty();
			}

			return dictionary;
		}
	}
}
