using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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
	}
}
