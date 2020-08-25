#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CheckBox = System.Windows.Controls.CheckBox;
using UserControl = System.Windows.Controls.UserControl;

#endregion

namespace CrmCodeGenerator.Controls
{
	/// <summary>
	///     Interaction logic for MultiSelectComboBox.xaml
	///     From Original Source from http://www.codeproject.com/Articles/563862/Multi-Select-ComboBox-in-WPF  modified to use
	///     a simple collection
	/// </summary>
	public partial class MultiSelectComboBox : UserControl
	{
		private readonly ObservableCollection<Node> nodeList;

		public MultiSelectComboBox()
		{
			InitializeComponent();
			nodeList = new ObservableCollection<Node>();
		}

		#region Dependency Properties

		public static readonly DependencyProperty ItemsSourceProperty =
			DependencyProperty.Register("ItemsSource", typeof (Collection<string>), typeof (MultiSelectComboBox),
				new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

		public static readonly DependencyProperty ItemsAppendTextProperty =
			DependencyProperty.Register("ItemsAppendText", typeof (IDictionary<string, string>), typeof (MultiSelectComboBox),
				new FrameworkPropertyMetadata(null, OnItemsAppendTextChanged));

		public static readonly DependencyProperty SelectedItemsProperty =
			DependencyProperty.Register("SelectedItems", typeof (Collection<string>), typeof (MultiSelectComboBox),
				new FrameworkPropertyMetadata(null, OnSelectedItemsChanged));

		public static readonly DependencyProperty FilteredItemsProperty =
			DependencyProperty.Register("FilteredItems", typeof (Collection<string>), typeof (MultiSelectComboBox),
				new FrameworkPropertyMetadata(null, OnFilteredItemsChanged));

		public static readonly DependencyProperty IsSortByAppendedProperty =
			DependencyProperty.Register("IsSortByAppended", typeof (bool), typeof (MultiSelectComboBox),
				new FrameworkPropertyMetadata(false, OnIsSortByAppendedChanged));

		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register("Text", typeof (string), typeof (MultiSelectComboBox),
				new UIPropertyMetadata(string.Empty));

		public static readonly DependencyProperty DefaultTextProperty =
			DependencyProperty.Register("DefaultText", typeof (string), typeof (MultiSelectComboBox),
				new UIPropertyMetadata(string.Empty));


		public Collection<string> ItemsSource
		{
			get { return (Collection<string>)GetValue(ItemsSourceProperty); }
			set { SetValue(ItemsSourceProperty, value); }
		}

		public IDictionary<string, string> ItemsAppendText
		{
			get { return (IDictionary<string, string>)GetValue(ItemsAppendTextProperty); }
			set { SetValue(ItemsAppendTextProperty, value); }
		}

		public Collection<string> SelectedItems
		{
			get { return (Collection<string>)GetValue(SelectedItemsProperty); }
			set { SetValue(SelectedItemsProperty, value); }
		}

		public Collection<string> FilteredItems
		{
			get { return (Collection<string>)GetValue(FilteredItemsProperty); }
			set { SetValue(FilteredItemsProperty, value); }
		}

		public bool IsSortByAppended
		{
			get { return (bool)GetValue(IsSortByAppendedProperty); }
			set { SetValue(IsSortByAppendedProperty, value); }
		}

		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}

		public string DefaultText
		{
			get { return (string)GetValue(DefaultTextProperty); }
			set { SetValue(DefaultTextProperty, value); }
		}

		#endregion

		#region Events

		private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var control = (MultiSelectComboBox) d;
			control.DisplayInControl();
			control.SetText();
		}

		private static void OnItemsAppendTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Dispatcher.CurrentDispatcher
				.BeginInvoke(DispatcherPriority.Background,
					(MethodInvoker) delegate
					                {
						                var control = (MultiSelectComboBox) d;
						                control.UpdateAppendedText();
						                control.SetText();
					                });
		}

		private static void OnIsSortByAppendedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Dispatcher.CurrentDispatcher
				.BeginInvoke(DispatcherPriority.Background,
					(MethodInvoker) delegate
					                {
						                var control = (MultiSelectComboBox) d;
						                control.SetText();
					                });
		}

		private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Dispatcher.CurrentDispatcher
				.BeginInvoke(DispatcherPriority.Background,
					(MethodInvoker) delegate
					                {
						                var control = (MultiSelectComboBox) d;
						                control.SelectNodes();
						                control.SetText();
					                });
		}

		private static void OnFilteredItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			Dispatcher.CurrentDispatcher
				.BeginInvoke(DispatcherPriority.Background,
					(MethodInvoker) delegate
					                {
						                var control = (MultiSelectComboBox) d;
						                control.FilterNodes();
					                });
		}

		private void CheckBox_Click(object sender, RoutedEventArgs e)
		{
			var clickedBox = (CheckBox) sender;

			if ((string)clickedBox.Content == "All")
			{
				if (clickedBox.IsChecked == true)
				{
					foreach (var node in nodeList)
					{
						node.IsSelected = true;
					}
				}
				else
				{
					foreach (var node in nodeList)
					{
						node.IsSelected = false;
					}
				}
			}
			else
			{
				var selectedCount = nodeList.Count(s => s.IsSelected && s.Title != "All");
				var allNode = nodeList.FirstOrDefault(i => i.Title == "All");

				if (allNode != null)
				{
					allNode.IsSelected = selectedCount == nodeList.Count - 1;
				}
			}

			Dispatcher.CurrentDispatcher
				.BeginInvoke(DispatcherPriority.Background,
					(MethodInvoker) delegate
					                {
						                SetSelectedItems();
						                SetText();
					                });
		}

		private void MultiSelectCombo_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var types = new[] { typeof (ScrollViewer) };
			var d = (DependencyObject) e.OriginalSource;

			if (d != null
				&& (d is Run || IsTypeClickedParentCheck(d, types) || IsTypeClickedChildrenCheck(d, types)))
			{
				return;
			}

			SortNodes();
		}

		#endregion

		#region Methods

		private void SelectNodes()
		{
			// unselect obsolete
			foreach (var node in nodeList
				.Where(node => node.Title != "All"
				               && !SelectedItems.Contains(node.Title.Replace("__", "_")))
				.ToArray())
			{
				node.IsSelected = false;
			}

			// select new
			foreach (var node in nodeList
				.Where(node => node.Title != "All"
				               && SelectedItems.Contains(node.Title.Replace("__", "_")))
				.ToArray())
			{
				node.IsSelected = true;
			}

			// set 'all' status
			if (SelectedItems.Count + 1 < nodeList.Count)
			{
				return;
			}

			var allNode = nodeList.FirstOrDefault(i => i.Title == "All");

			if (allNode != null)
			{
				allNode.IsSelected = true;
			}
		}

		private void FilterNodes()
		{
			var nodeAsync = nodeList.ToArray();

			// unfilter obsolete
			foreach (var node in nodeAsync
				.Where(node => !FilteredItems.Contains(node.Title
					                .Replace("__", "_"))))
			{
				node.IsFiltered = false;
			}

			// filter new
			foreach (var node in nodeAsync
				.Where(node => !node.IsFiltered && FilteredItems.Contains(node.Title.Replace("__", "_"))))
			{
				node.IsFiltered = true;
			}
		}

		private void SetSelectedItems()
		{
			if (SelectedItems == null)
			{
				SelectedItems = new Collection<string>();
			}

			foreach (var node in nodeList.ToArray())
			{
				if (node.Title == "All" || ItemsSource.Count <= 0)
				{
					continue;
				}

				if (!SelectedItems.Contains(node.Title.Replace("__", "_"))
					&& node.IsSelected)
				{
					SelectedItems.Add(node.Title.Replace("__", "_"));
				}
				else if (SelectedItems.Contains(node.Title.Replace("__", "_"))
				         && !node.IsSelected)
				{
					SelectedItems.Remove(node.Title.Replace("__", "_"));
				}
			}
		}

		private void DisplayInControl()
		{
			if (ItemsSource.Count > 0 && nodeList.All(node => node.Title != "All"))
			{
				nodeList.Add(new Node("All", ""));
			}

			// remove obsolete nodes
			foreach (var node in nodeList
				.Where(node => !ItemsSource.Contains(node.Title.Replace("__", "_"))
					&& node.Title != "All").ToArray())
			{
				nodeList.Remove(node);
			}

			// add new nodes and review the values
			foreach (var item in ItemsSource
				.Except(nodeList.Select(node => node.Title.Replace("__", "_"))))
			{
				var node = new Node(item.Replace("_", "__"),
					(FirstNotNullOrEmpty(
						ItemsAppendText ?? new Dictionary<string, string>(),
						item)
					?? "").Replace("_", "__"));
				nodeList.Add(node);
			}
		}

		private void UpdateAppendedText()
		{
			foreach (var node in nodeList.ToArray())
			{
				node.AppendText =
					(FirstNotNullOrEmpty(
						ItemsAppendText ?? new Dictionary<string, string>(),
						node.Title.Replace("__", "_"))
					?? "").Replace("_", "__");
			}
		}

		private void SetText()
		{
			if (SelectedItems != null)
			{
				var displayText = new StringBuilder();
				foreach (var s in nodeList)
				{
					if (s.IsSelected && s.Title == "All")
					{
						displayText = new StringBuilder();
						displayText.Append("All");
						break;
					}

					if (!s.IsSelected || s.Title == "All")
					{
						continue;
					}

					var title = s.Title;
					displayText.Append(((IsSortByAppended
											? FirstNotNullOrEmpty(
												ItemsAppendText ?? new Dictionary<string, string>(),
												title.Replace("__", "_"))
											: null)
									   ?? title)
									   .Replace("__", "_")
									   .Replace("(", "")
									   .Replace(")", ""));
					displayText.Append(", ");
				}
				Text = displayText.ToString().TrimEnd(',', ' ');
			}
			// set DefaultText if nothing else selected
			if (string.IsNullOrEmpty(Text))
			{
				Text = DefaultText;
			}
		}

		private void SortNodes()
		{
			var sorted = new List<Node>(nodeList
				.OrderBy(node => node.Title != "All")
				.ThenByDescending(node => !string.IsNullOrEmpty(node.Title))
				.ThenByDescending(node => node.IsSelected || node.IsFiltered)
				.ThenBy(node => (IsSortByAppended
									? (string.IsNullOrEmpty(node.AppendText) ? node.Title : node.AppendText)
									: node.Title).Replace("__", "_")));

			for (var i = 0; i < sorted.Count; i++)
			{
				// check before assign to avoid triggering an event
				if (nodeList[i] != sorted[i])
				{
					nodeList[i] = sorted[i];
				}
			}

			MultiSelectCombo.ItemsSource = nodeList;
		}

		#endregion

		private static bool IsTypeClickedChildrenCheck(DependencyObject dep, params Type[] types)
		{
			if (dep == null)
			{
				return false;
			}

			if (types.Any(type => type.IsInstanceOfType(dep)))
			{
				return true;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
			{
				if (IsTypeClickedChildrenCheck(VisualTreeHelper.GetChild(dep, i), types))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsTypeClickedParentCheck(DependencyObject dep, params Type[] types)
		{
			while (dep != null)
			{
				if (types.Any(type => type.IsInstanceOfType(dep)))
				{
					return true;
				}

				try
				{
					dep = VisualTreeHelper.GetParent(dep);
				}
				catch (Exception)
				{
					dep = null;
				}
			}

			return false;
		}

		private static TValue FirstNotNullOrDefault<TKey, TValue>(IDictionary<TKey, TValue> dictionary, params TKey[] keys)
		{
			return keys.Where(dictionary.ContainsKey).Select(key => dictionary[key]).FirstOrDefault();
		}

		private static string FirstNotNullOrEmpty<TKey>(IDictionary<TKey, string> dictionary, params TKey[] keys)
		{
			return keys.Where(key =>
			{
				if (dictionary.ContainsKey(key))
					return !string.IsNullOrEmpty(dictionary[key]);
				return false;
			}).Select(key => dictionary[key]).FirstOrDefault();
		}
	}

	public class Node : INotifyPropertyChanged
	{
		private string title;
		private string appendText;
		private bool isSelected;
		private bool isFiltered;

		#region ctor

		public Node(string title, string appendText)
		{
			Title = title;
			AppendText = appendText;
		}

		#endregion

		#region Properties

		public string Title
		{
			get { return title; }
			set
			{
				title = value;
				NotifyPropertyChanged("Title");
			}
		}

		public string AppendText
		{
			get { return appendText; }
			set
			{
				appendText = value;
				NotifyPropertyChanged("AppendText");
			}
		}

		public bool IsSelected
		{
			get { return isSelected; }
			set
			{
				isSelected = value;
				NotifyPropertyChanged("IsSelected");
			}
		}

		public Brush Colour => IsFiltered ? Brushes.Red : Brushes.Black;

		public bool IsFiltered
		{
			get { return isFiltered; }
			set
			{
				isFiltered = value;
				NotifyPropertyChanged("IsFiltered");
				NotifyPropertyChanged("Colour");
			}
		}

		#endregion

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#endregion
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
