#region Imports

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

#endregion

namespace CrmCodeGenerator.VSPackage.Helpers
{
	/// <summary>
	/// Credit: https://stackoverflow.com/a/3702183/1919456 (ASanch)
	/// </summary>
	public class DataGridBehavior : DependencyObject
	{
		public static readonly Brush HighlightColour = new SolidColorBrush(Color.FromRgb(235, 249, 255));

		public static bool GetHighlightColumn(DependencyObject obj)
		{
			return (bool)obj.GetValue(HighlightColumnProperty);
		}

		public static void SetHighlightColumn(DependencyObject obj, bool value)
		{
			obj.SetValue(HighlightColumnProperty, value);
		}

		// Using a DependencyProperty as the backing store for HighlightColumn.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty HighlightColumnProperty =
			DependencyProperty.RegisterAttached("HighlightColumn", typeof(bool),
				typeof(DataGridBehavior), new FrameworkPropertyMetadata(false, OnHighlightColumnPropertyChanged));

		public static bool GetIsCellHighlighted(DependencyObject obj)
		{
			return (bool)obj.GetValue(IsCellHighlightedProperty);
		}

		public static void SetIsCellHighlighted(DependencyObject obj, bool value)
		{
			obj.SetValue(IsCellHighlightedProperty, value);
		}

		// Using a DependencyProperty as the backing store for IsCellHighlighted.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty IsCellHighlightedProperty =
			DependencyProperty.RegisterAttached("IsCellHighlighted", typeof(bool), typeof(DataGridBehavior),
				new UIPropertyMetadata(false));

		private static void OnHighlightColumnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not DataGridCell cell)
			{
				return;
			}

			cell.SetValue(IsCellHighlightedProperty, e.NewValue);
			cell.GetParent<DataGrid>().GetChild<DataGridColumnHeader>(cell.Column.DisplayIndex).SetValue(IsCellHighlightedProperty, e.NewValue);
			cell.GetParent<DataGridRow>().GetChild<DataGridCell>(1).SetValue(IsCellHighlightedProperty, e.NewValue);
		}
	}
}
