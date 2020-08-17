#region Imports

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Application = System.Windows.Forms.Application;
using MultiSelectComboBoxClass = CrmCodeGenerator.Controls.MultiSelectComboBox;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	public class Extensions
	{
		public static readonly DependencyProperty BangProperty;

		public static bool GetBang(DependencyObject obj)
		{
			return (bool) obj.GetValue(BangProperty);
		}

		public static void SetBang(DependencyObject obj, bool value)
		{
			obj.SetValue(BangProperty, value);
		}

		static Extensions()
		{
			//register attached dependency property
			var metadata = new FrameworkPropertyMetadata(false);
			BangProperty = DependencyProperty.RegisterAttached("Bang",
				typeof(bool),
				typeof(Extensions), metadata);
		}
	}

	#region Row classes

	public class GridRow : INotifyPropertyChanged
	{
		protected bool isSelected;
		protected string rename;

		public bool IsReadOnlyEnabled
		{
			get; set;
		}

		public bool IsSelected
		{
			get => isSelected;
			set
			{
				isSelected = value;
				OnPropertyChanged();
			}
		}

		public string Name { get; set; }

		public string Rename
		{
			get => rename;
			set
			{
				rename = value;
				OnPropertyChanged();
			}
		}

		private bool isReadOnly;

		public bool IsReadOnly
		{
			get => isReadOnly;
			set
			{
				isReadOnly = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class FieldGridRow : GridRow
	{
		public string DisplayName { get; set; }
		public string Language { get; set; }

		public bool IsClearFlagEnabled => IsReadOnlyEnabled;

		private bool isClearFlag;

		public bool IsClearFlag
		{
			get => isClearFlag;
			set
			{
				isClearFlag = value;
				OnPropertyChanged();
			}
		}
	}

	public class EntityGridRow : FieldGridRow
	{
		private bool isGenerateMeta;

		public bool IsGenerateMeta
		{
			get => isGenerateMeta;
			set
			{
				isGenerateMeta = value;
				OnPropertyChanged();
			}
		}

		private bool isOptionsetLabels;

		public bool IsOptionsetLabels
		{
			get => isOptionsetLabels;
			set
			{
				isOptionsetLabels = value;
				OnPropertyChanged();
			}
		}

		private bool isLookupLabels;

		public bool IsLookupLabels
		{
			get => isLookupLabels;
			set
			{
				isLookupLabels = value;
				OnPropertyChanged();
			}
		}

		private ClearModeEnumUi valueClearMode;

		public ClearModeEnumUi ValueClearMode
		{
			get => valueClearMode;
			set
			{
				valueClearMode = value;
				OnPropertyChanged();
			}
		}

		public IEnumerable<ClearModeEnumUi> ValueClearModes => new[] { ClearModeEnumUi.Default }.Union(
			Enum.GetValues(typeof(ClearModeEnum)).Cast<ClearModeEnumUi>());
	}

	public enum ClearModeEnumUi
	{
		Default = -1,
		Disabled = 0,
		Empty = 1,
		Convention = 2,
		Flag = 3
	}

	public class Relations1NGridRow : GridRow
	{
		public string ToEntity { get; set; }
		public string ToField { get; set; }
	}

	public class RelationsN1GridRow : GridRow
	{
		private bool isFlatten;

		public string ToEntity { get; set; }
		public string FromField { get; set; }

		public bool IsFlatten
		{
			get => isFlatten;
			set
			{
				isFlatten = value;
				OnPropertyChanged();
			}
		}
	}

	public class RelationsNNGridRow : GridRow
	{
		public string ToEntity { get; set; }
		public string IntersectEntity { get; set; }
	}

	#endregion
	
	/// <summary>
		///     Interaction logic for Filter.xaml
		/// </summary>
	public partial class Filter : INotifyPropertyChanged
	{
		#region Properties

		public string LogicalName { get; set; }

		public EntityFilterArray EntityFilterList { get; set; }
		public EntityFilter EntityFilter { get; set; }

		public EntityDataFilter EntityDataFilter { get; set; }

		public SettingsNew Settings { get; set; }

		public List<EntityMetadata> EntityMetadataCache;

		private Style originalProgressBarStyle;

		public bool StillOpen { get; } = true;

		public string WindowTitle { get; set; }

		private bool entitiesSelectAll;

		public bool EntitiesSelectAll
		{
			get => entitiesSelectAll;
			set
			{
				entitiesSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool metadataSelectAll;

		public bool MetadataSelectAll
		{
			get => metadataSelectAll;
			set
			{
				metadataSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsGenerateMeta = value);
				OnPropertyChanged();
			}
		}

		private bool isOptionsetLabelsSelectAll;

		public bool IsOptionsetLabelsSelectAll
		{
			get => isOptionsetLabelsSelectAll;
			set
			{
				isOptionsetLabelsSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsOptionsetLabels = value);
				OnPropertyChanged();
			}
		}

		private bool isLookupLabelsSelectAll;

		public bool IsLookupLabelsSelectAll
		{
			get => isLookupLabelsSelectAll;
			set
			{
				isLookupLabelsSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsLookupLabels = value);
				OnPropertyChanged();
			}
		}


		public IEnumerable<ClearModeEnumUi> ValueClearModes => new[] { ClearModeEnumUi.Default }.Union(
			Enum.GetValues(typeof(ClearModeEnum)).Cast<ClearModeEnumUi>());

		private ClearModeEnumUi valueClearModeAll;

		public ClearModeEnumUi ValueClearModeAll
		{
			get => valueClearModeAll;
			set
			{
				valueClearModeAll = value;
				Entities.ToList().ForEach(entity => entity.ValueClearMode = value);
				OnPropertyChanged();
			}
		}

		private bool displayFilter;

		public bool DisplayFilter
		{
			get => displayFilter;
			set
			{
				displayFilter = value;
				OnPropertyChanged();
			}
		}

		public ObservableCollection<EntityGridRow> Entities { get; set; }

		#endregion

		private readonly MetadataCache metadataCache;

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public Filter(Window parentWindow, SettingsNew settings)
		{
			InitializeComponent();

			Owner = parentWindow;
			WindowTitle = "Entities Profiling";

			Entities = new ObservableCollection<EntityGridRow>();

			Settings = settings;
			metadataCache = MetadataCacheHelpers.GetMetadataCache(settings.ConnectionString);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			new Thread(() =>
					   {
						   try
						   {
							   EntityFilterList = Settings.EntityDataFilterArray;
							   EntityFilter = EntityFilterList.GetSelectedFilter();

							   ShowBusy("Fetching entity metadata ...");
							   if (metadataCache.ProfileEntityMetadataCache.Any())
							   {
								   EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
							   }
							   else
							   {
								   RefreshEntityMetadata();
							   }

							   ShowBusy("Initialising ...");
							   InitEntityList();

							   Dispatcher.Invoke(
								   () =>
								   {
									   DataContext = this;
									   CheckBoxEntitiesSelectAll.DataContext = this;
									   CheckBoxMetadataSelectAll.DataContext = this;
									   CheckBoxOptionsetLabelsSelectAll.DataContext = this;
									   CheckBoxLookupLabelsSelectAll.DataContext = this;
									   ComboBoxClearModeAll.DataContext = this;
									   ComboBoxClearModeAll.SelectedIndex = -1;

									   TextBoxPrefix.DataContext = EntityFilter;
									   TextBoxSuffix.DataContext = EntityFilter;
									   CheckBoxIsDefault.DataContext = EntityFilter;

									   EntitiesGrid.ItemsSource = Entities;

									   ComboBoxFilters.DataContext = EntityFilterList;
									   ComboBoxFilters.DisplayMemberPath = "DisplayName";
								   });

							   HideBusy();
						   }
						   catch (Exception ex)
						   {
							   PopException(ex);
							   Dispatcher.InvokeAsync(Close);
						   }
					   }).Start();
		}

		private void InitEntityList(List<string> filter = null)
		{
			Dispatcher.Invoke(Entities.Clear);

			var rowList = new List<EntityGridRow>();

			var filteredEntities = EntityMetadataCache
				.Where(entity => filter == null || filter.Contains(entity.LogicalName)).ToArray();

			foreach (var entity in filteredEntities)
			{
				var entityAsync = entity;

				Dispatcher.Invoke(() =>
								  {
									  var dataFilter = EntityFilter.EntityFilterList
										  .FirstOrDefault(e => e.LogicalName == entityAsync.LogicalName);

									  if (dataFilter == null)
									  {
										  dataFilter = new EntityDataFilter(entityAsync.LogicalName);
										  EntityFilter.EntityFilterList.Add(dataFilter);
									  }

									  var row = new EntityGridRow
												{
													IsSelected = !dataFilter.IsExcluded,
													Name = entityAsync.LogicalName,
													DisplayName =
														entity.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
															? Naming.GetProperHybridName(entity.SchemaName, entity.LogicalName)
															: Naming.Clean(entity.DisplayName.UserLocalizedLabel.Label),
													Rename = dataFilter.EntityRename,
													IsGenerateMeta = dataFilter.IsGenerateMeta,
													IsOptionsetLabels = dataFilter.IsOptionsetLabels,
													IsLookupLabels = dataFilter.IsLookupLabels,
													ValueClearMode = dataFilter.ValueClearMode == null
														? ClearModeEnumUi.Default
														: (ClearModeEnumUi)dataFilter.ValueClearMode
												};

									  row.PropertyChanged +=
										  (sender, args) =>
										  {
											  if (args.PropertyName == "IsSelected")
											  {
												  dataFilter.IsExcluded = !row.IsSelected;
											  }
											  else if (args.PropertyName == "IsGenerateMeta")
											  {
												  dataFilter.IsGenerateMeta = row.IsGenerateMeta;
											  }
											  else if (args.PropertyName == "IsOptionsetLabels")
											  {
												  dataFilter.IsOptionsetLabels = row.IsOptionsetLabels;
											  }
											  else if (args.PropertyName == "IsLookupLabels")
											  {
												  dataFilter.IsLookupLabels = row.IsLookupLabels;
											  }
											  else if (args.PropertyName == "Rename")
											  {
												  dataFilter.EntityRename = row.Rename;
											  }
											  else if (args.PropertyName == "ValueClearMode")
											  {
												  switch (row.ValueClearMode)
												  {
													  case ClearModeEnumUi.Default:
														  dataFilter.ValueClearMode = null;
														  break;

													  default:
														  dataFilter.ValueClearMode =
															  (ClearModeEnum?)row.ValueClearMode;
														  break;
												  }
											  }
										  };

									  rowList.Add(row);
								  });
			}

			foreach (var row in rowList.OrderByDescending(row => row.IsSelected).ThenBy(row => row.Name))
			{
				Dispatcher.Invoke(() => Entities.Add(row));
			}

			// if no filter, select all
			if (EntityFilter.EntityFilterList.Count(e => !e.IsExcluded) == EntityMetadataCache.Count)
			{
				Dispatcher.Invoke(() => EntitiesSelectAll = true);
			}

			if (EntityFilter.EntityFilterList.Count(e => e.IsOptionsetLabels) == EntityMetadataCache.Count)
			{
				Dispatcher.Invoke(() => IsOptionsetLabelsSelectAll = true);
			}

			if (EntityFilter.EntityFilterList.Count(e => e.IsLookupLabels) == EntityMetadataCache.Count)
			{
				Dispatcher.Invoke(() => IsLookupLabelsSelectAll = true);
			}
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// hide close button
			this.HideCloseButton();
			// hide minimise button
			this.HideMinimizeButton();
		}

		#endregion

		private void SaveFilter()
		{
			ConfirmSingleDefault();

			foreach (var entityRow in Entities.Where(entity => entity.IsOptionsetLabels))
			{
				if (!Settings.OptionsetLabelsEntitiesSelected.Contains(entityRow.Name))
				{
					Settings.OptionsetLabelsEntitiesSelected.Add(entityRow.Name);
				}
			}

			foreach (var entityRow in Entities.Where(entity => entity.IsLookupLabels))
			{
				if (!Settings.LookupLabelsEntitiesSelected.Contains(entityRow.Name))
				{
					Settings.LookupLabelsEntitiesSelected.Add(entityRow.Name);
				}
			}
		}

		private void ConfirmSingleDefault()
		{
			// make sure only one profile is the default
			if (EntityFilter.IsDefault)
			{
				foreach (var entityFilter in EntityFilterList.EntityFilters
					.Where(filter => filter.IsDefault && filter != EntityFilter))
				{
					entityFilter.IsDefault = false;
				}
			}
			else if (EntityFilterList.EntityFilters.Count(filter => filter.IsDefault) <= 0)
			{
				EntityFilter.IsDefault = true;
			}
		}

		#region CRM

		private void RefreshEntityMetadata()
		{
			MetadataHelpers.RefreshSettingsEntityMetadata(Settings);
			EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
		}

		#endregion

		#region Status stuff

		private void PopException(Exception exception)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  var message = exception.Message
				                                + (exception.InnerException != null ? "\n" + exception.InnerException.Message : "");
				                  MessageBox.Show(message, exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);

				                  var error = "[ERROR] " + exception.Message
				                              +
				                              (exception.InnerException != null
					                               ? "\n" + "[ERROR] " + exception.InnerException.Message
					                               : "");
				                  UpdateStatus(error, false);
				                  UpdateStatus(exception.StackTrace, false);
			                  });
		}

		private void ShowBusy(string message, double? progress = null)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = true;
				                  BusyIndicator.BusyContent =
					                  string.IsNullOrEmpty(message) ? "Please wait ..." : message;

				                  if (progress == null)
				                  {
					                  BusyIndicator.ProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;
				                  }
				                  else
				                  {
					                  originalProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;

					                  var style = new Style(typeof(ProgressBar));
					                  style.Setters.Add(new Setter(HeightProperty, 15d));
					                  style.Setters.Add(new Setter(RangeBase.ValueProperty, progress));
					                  style.Setters.Add(new Setter(RangeBase.MaximumProperty, 100d));
					                  BusyIndicator.ProgressBarStyle = style;
				                  }
			                  }, DispatcherPriority.Send);
		}

		private void HideBusy()
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = false;
				                  BusyIndicator.BusyContent = "Please wait ...";
			                  }, DispatcherPriority.Send);
		}

		internal void UpdateStatus(string message, bool working, bool allowBusy = true, bool newLine = true)
		{
			//Dispatcher.Invoke(() => SetEnabledChildren(Inputs, !working, "ButtonCancel"));

			if (allowBusy)
			{
				if (working)
				{
					ShowBusy(message);
				}
				else
				{
					HideBusy();
				}
			}

			if (!string.IsNullOrWhiteSpace(message))
			{
				Dispatcher.BeginInvoke(new Action(() => { Status.Update(message, newLine); }));
			}

			Application.DoEvents();
		}

		#endregion

		#region UI events

		#region Grid stuff

		private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var cell = sender as DataGridCell;

			if (cell != null && !cell.IsEditing && !IsComboBoxCellClicked(e))
			{
				// enables editing on single click
				if (!cell.IsFocused)
				{
					cell.Focus();
				}
			}
		}

		/// <summary>
		///     Credit: http://stackoverflow.com/a/3833742/1919456
		/// </summary>
		private void DataGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var row = sender as DataGridRow;

			if (row == null)
			{
				return;
			}

			var grid = row.GetParent<DataGrid>();
			var gridName = grid.Name.Replace("Grid", "");

			// enables editing on single click
			if (!row.IsFocused)
			{
				row.Focus();
			}

			// skip if an editable cell is clicked
			if (IsTextCellClicked(e) || IsComboBoxCellClicked(e))
			{
				// unselect all rows
				for (var i = 0; i < grid.Items.Count; i++)
				{
					var container = grid.ItemContainerGenerator.ContainerFromIndex(i);

					if (container == null)
					{
						continue;
					}

					var rowQ = (DataGridRow) container;

					if (rowQ.IsSelected)
					{
						rowQ.IsSelected = false;
					}
				}

				// select current row
				if (!row.IsSelected)
				{
					row.IsSelected = true;
				}

				return;
			}

			var d = (DependencyObject) e.OriginalSource;

			if (d != null && (IsCheckboxClickedParentCheck(d, "GenerateMeta")
			                  || IsCheckboxClickedChildrenCheck(d, "GenerateMeta")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsGenerateMeta = !rowDataCast.IsGenerateMeta;

				// selectAll value to false
				var generateMetaField = GetType().GetField("MetadataSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				generateMetaField?.SetValue(this, false);

				OnPropertyChanged("MetadataSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "IsOptionsetLabels")
			                       || IsCheckboxClickedChildrenCheck(d, "IsOptionsetLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsOptionsetLabels = !rowDataCast.IsOptionsetLabels;

				// selectAll value to false
				var field = GetType().GetField("IsOptionsetLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsOptionsetLabelsSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "IsLookupLabels")
			                       || IsCheckboxClickedChildrenCheck(d, "IsLookupLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsLookupLabels = !rowDataCast.IsLookupLabels;

				// selectAll value to false
				var field = GetType().GetField("IsLookupLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsLookupLabelsSelectAll");
			}
			else
			{
				// clicked select
				var rowData = (GridRow) row.Item;
				rowData.IsSelected = !rowData.IsSelected;

				// selectAll value to false
				var selectAllField = GetType().GetField(gridName + "SelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				selectAllField?.SetValue(this, false);

				OnPropertyChanged(gridName + "SelectAll");
			}
		}

		private void DataGridRow_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var row = sender as DataGridRow;

			if (row == null)
			{
				return;
			}

			var grid = row.GetParent<DataGrid>();
			var gridName = grid.Name.Replace("Grid", "");

			if (gridName.Contains("Entities"))
			{
				EntitiesDataGridRow_PreviewMouseLeftButtonDown(sender, e);
			}
		}

		// double click!
		private void EntitiesDataGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var row = sender as DataGridRow;

			if (row == null)
			{
				return;
			}

			var grid = row.GetParent<DataGrid>();
			var gridName = grid.Name.Replace("Grid", "");

			// enables editing on single click
			if (!row.IsFocused)
			{
				row.Focus();
			}

			var rowData = (GridRow) row.Item;

			if (IsComboBoxCellClicked(e))
			{
				// unselect all rows
				for (var i = 0; i < grid.Items.Count; i++)
				{
					var container = grid.ItemContainerGenerator.ContainerFromIndex(i);

					if (container == null)
					{
						continue;
					}

					var rowQ = (DataGridRow) container;

					if (rowQ.IsSelected)
					{
						rowQ.IsSelected = false;
					}
				}

				return;
			}

			if (IsTextCellClicked(e))
			{
				// unselect all rows
				for (var i = 0; i < grid.Items.Count; i++)
				{
					var container = grid.ItemContainerGenerator.ContainerFromIndex(i);

					if (container == null)
					{
						continue;
					}

					var rowQ = (DataGridRow) container;

					if (rowQ.IsSelected)
					{
						rowQ.IsSelected = false;
					}

					if (Extensions.GetBang(rowQ))
					{
						Extensions.SetBang(rowQ, false);
					}
				}

				// select current row
				if (!row.IsSelected)
				{
					Extensions.SetBang(row, true);
					row.IsSelected = true;

					// get logical name and re-init
					LogicalName = rowData.Name;
					new FilterDetails(this, LogicalName, Settings, Entities, CheckBoxIsDefault.IsChecked == true)
						.ShowDialog();
				}

				return;
			}

			var d = (DependencyObject) e.OriginalSource;

			if (d != null && (IsCheckboxClickedParentCheck(d, "GenerateMeta")
			                  || IsCheckboxClickedChildrenCheck(d, "GenerateMeta")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) rowData;
				rowDataCast.IsGenerateMeta = !rowDataCast.IsGenerateMeta;

				// selectAll value to false
				var generateMetaField = GetType().GetField("MetadataSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				generateMetaField?.SetValue(this, false);

				OnPropertyChanged("MetadataSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "IsOptionsetLabels")
			                       || IsCheckboxClickedChildrenCheck(d, "IsOptionsetLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsOptionsetLabels = !rowDataCast.IsOptionsetLabels;

				// selectAll value to false
				var field = GetType().GetField("IsOptionsetLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsOptionsetLabelsSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "IsLookupLabels")
			                       || IsCheckboxClickedChildrenCheck(d, "IsLookupLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsLookupLabels = !rowDataCast.IsLookupLabels;

				// selectAll value to false
				var field = GetType().GetField("IsLookupLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsLookupLabelsSelectAll");
			}
			else
			{
				// clicked select
				rowData.IsSelected = !rowData.IsSelected;

				// selectAll value to false
				var selectAllField = GetType().GetField(gridName + "SelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				selectAllField?.SetValue(this, false);

				OnPropertyChanged(gridName + "SelectAll");
			}
		}

		// Credit: http://blog.scottlogic.com/2008/12/02/wpf-datagrid-detecting-clicked-cell-and-row.html
		private void EntitiesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
		{
			var rowQ = e.Row;
			var rowDataQ = (GridRow) rowQ.Item;

			if (EntityDataFilter != null && rowDataQ.Name == EntityDataFilter.LogicalName)
			{
				Extensions.SetBang(rowQ, true);
			}
			else
			{
				Extensions.SetBang(rowQ, false);
			}
		}

		private void Grid_KeyUp(object sender, KeyEventArgs e)
		{
			ProcessGridKeyUp(sender, e);
		}

		private static void ProcessGridKeyUp(object sender, KeyEventArgs e)
		{
			var grid = sender as DataGrid;

			if (grid == null)
			{
				return;
			}

			for (var i = 0; i < grid.Items.Count; i++)
			{
				var item = (DataGridRow) grid.ItemContainerGenerator.ContainerFromIndex(i);

				if (item != null && item.IsEditing)
				{
					return;
				}
			}

			switch (e.Key)
			{
				case Key.Space:
					var isFirstItemSelected = ((GridRow) grid.SelectedItem).IsSelected;

					foreach (var item in grid.SelectedItems.Cast<GridRow>()
						.Where(item => item.IsSelected == isFirstItemSelected))
					{
						item.IsSelected = !isFirstItemSelected;
					}

					break;

				case Key.Delete:
					foreach (var item in grid.SelectedItems.Cast<GridRow>()
						.Where(item => !string.IsNullOrEmpty(item.Rename)))
					{
						item.Rename = "";
					}

					break;
			}
		}

		private static DataGridCell GetCellClicked(MouseButtonEventArgs e)
		{
			var dep = (DependencyObject) e.OriginalSource;

			// iteratively traverse the visual tree
			while (dep != null && !(dep is DataGridCell))
			{
				dep = VisualTreeHelper.GetParent(dep);
			}

			return GetCellClickedChildren(dep);
		}

		private static DataGridCell GetCellClickedChildren(DependencyObject dep)
		{
			if (dep == null)
			{
				return null;
			}

			var cell = dep as DataGridCell;

			if (cell != null)
			{
				return cell;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
			{
				cell = GetCellClickedChildren(VisualTreeHelper.GetChild(dep, i));

				if (cell != null)
				{
					return cell;
				}
			}

			return null;
		}

		private static bool IsCellClickedChildrenCheck(DependencyObject dep, params Type[] types)
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
				if (IsCellClickedChildrenCheck(VisualTreeHelper.GetChild(dep, i), types))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsCellClickedParentCheck(DependencyObject dep, params Type[] types)
		{
			while (dep != null)
			{
				if (types.Any(type => type.IsInstanceOfType(dep)))
				{
					return true;
				}

				dep = VisualTreeHelper.GetParent(dep);
			}

			return false;
		}

		private static bool IsCheckboxClickedChildrenCheck(DependencyObject dep, string name)
		{
			if (dep == null)
			{
				return false;
			}

			if (dep is CheckBox && ((CheckBox) dep).Name == name)
			{
				return true;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
			{
				if (IsCheckboxClickedChildrenCheck(VisualTreeHelper.GetChild(dep, i), name))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsCheckboxClickedParentCheck(DependencyObject dep, string name)
		{
			while (dep != null)
			{
				if (dep is CheckBox && ((CheckBox) dep).Name == name)
				{
					return true;
				}

				dep = VisualTreeHelper.GetParent(dep);
			}

			return false;
		}

		private static bool IsCheckBoxCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] {typeof(CheckBox), typeof(DataGridCheckBoxColumn)};
			var d = (DependencyObject) e.OriginalSource;
			return d != null && (IsCellClickedParentCheck(d, types) || IsCellClickedChildrenCheck(d, types));
		}

		private static bool IsComboBoxCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] {typeof(ComboBox), typeof(ComboBoxItem)};
			var d = (DependencyObject) e.OriginalSource;
			return d != null && (IsCellClickedParentCheck(d, types) || IsCellClickedChildrenCheck(d, types));
		}

		private static bool IsTextCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] {typeof(TextBlock), typeof(TextBox), typeof(DataGridTextColumn), typeof(RichTextBox)};
			var d = (DependencyObject) e.OriginalSource;
			return d != null && (IsCellClickedParentCheck(d, types) || IsCellClickedChildrenCheck(d, types));
		}

		private void CheckBoxIsSelected_OnClick(object sender, RoutedEventArgs e)
		{
			// ignore check-box clicks
			var checkBox = sender as CheckBox;

			if (checkBox != null)
			{
				checkBox.IsChecked = !checkBox.IsChecked;
			}
		}

		#endregion

		#region Top bar stuff

		private void ComboBoxFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SaveFilter();

			EntityDataFilter = null;
			EntityFilter = EntityFilterList.GetSelectedFilter();

			TextBoxPrefix.DataContext = EntityFilter;
			TextBoxSuffix.DataContext = EntityFilter;
			CheckBoxIsDefault.DataContext = EntityFilter;

			InitEntityList();
		}

		private void ButtonNewFilter_Click(object sender, RoutedEventArgs e)
		{
			var newFilter = new EntityFilter();
			EntityFilterList.EntityFilters.Add(newFilter);
			EntityFilterList.SelectedFilterIndex = EntityFilterList.EntityFilters.IndexOf(newFilter);
		}

		private void ButtonDuplicateFilter_Click(object sender, RoutedEventArgs e)
		{
			var newFilter = EntityFilter.Copy();
			newFilter.Prefix = "";
			newFilter.Suffix = "Contract";

			EntityFilterList.EntityFilters.Add(newFilter);
			DteHelper.ShowInfo("The selected profile has been duplicated, and the new profile has been selected instead.",
				"Profile duplicated.");
			EntityFilterList.SelectedFilterIndex = EntityFilterList.EntityFilters.Count - 1;
		}

		private void ButtonDeleteFilter_Click(object sender, RoutedEventArgs e)
		{
			if (EntityFilterList.EntityFilters.Count <= 1)
			{
				PopException(new Exception("Can't delete the last filter profile."));
				return;
			}

			if (DteHelper.IsConfirmed("Are you sure you want to delete this filter profile? This will affect other entities!",
				"Confirm delete action ..."))
			{
				EntityFilterList.EntityFilters.Remove(EntityFilterList.GetSelectedFilter());
			}
		}

		private void ButtonFilter_Click(object sender, RoutedEventArgs e)
		{
			SelectEntitiesByRegex();
		}

		private void ButtonFilterClear_Click(object sender, RoutedEventArgs e)
		{
			TextBoxFilter.Text = string.Empty;
			SelectEntitiesByRegex();
		}

		private void TextBoxFilter_OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				SelectEntitiesByRegex();
			}
		}

		private void SelectEntitiesByRegex()
		{
			IEnumerable<string> customEntities = null;

			if (!string.IsNullOrEmpty(TextBoxFilter.Text))
			{

				// get all regex
				var prefixes = TextBoxFilter.Text.ToLower()
					.Split(',').Select(prefix => prefix.Trim())
					.Where(prefix => !string.IsNullOrEmpty(prefix))
					.Distinct();

				// get entity names that match any regex from the fetched list
				if (DisplayFilter)
				{
					var defaultFiltered = Settings.EntityDataFilterArray.EntityFilters
						.Where(filter => filter.IsDefault)
						.SelectMany(filter => filter.EntityFilterList).ToArray();

					customEntities =
						EntityMetadataCache
							.ToDictionary(key => key.LogicalName,
								value =>
								{
									var rename =
										defaultFiltered.FirstOrDefault(filter => filter.LogicalName == value.LogicalName)?.EntityRename;

									return "("
										+ (string.IsNullOrEmpty(rename)
											? value.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
												? Naming.GetProperHybridName(value.SchemaName, value.LogicalName)
												: Naming.Clean(value.DisplayName.UserLocalizedLabel.Label)
											: rename)
										+ ")";
								})
							.Where(keyValue => prefixes.Any(
								prefix => Regex.IsMatch(keyValue.Value.ToLower().Replace("(", "").Replace(")", ""), prefix)))
							.Select(keyValue => keyValue.Key)
							.Distinct();
				}
				else
				{
					customEntities = Settings.EntityList
						.Where(entity => prefixes.Any(prefix => Regex.IsMatch(entity, prefix)))
						.Distinct();
				}
			}

			// filter entities
			new Thread(() =>
			           {
				           try
				           {
					           ShowBusy("Filtering ...");

					           InitEntityList(customEntities?.ToList());

					           //Dispatcher.Invoke(() => { DataContext = this; });
					           Dispatcher.Invoke(() => TextBoxFilter.Focus());
					           
					           HideBusy();
				           }
				           catch (Exception ex)
				           {
					           PopException(ex);
					           Dispatcher.InvokeAsync(Close);
				           }
			           }).Start();
		}

		#endregion

		#region Bottom bar stuff

		private void Logon_Click(object sender, RoutedEventArgs e)
		{
			SaveFilter();
			Settings.FiltersChanged();
			Dispatcher.InvokeAsync(Close);
		}

		//private void Cancel_Click(object sender, RoutedEventArgs e)
		//{
		//	stillOpen = false;
		//	Dispatcher.InvokeAsync(Close);
		//}

		private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
		{
			new Thread(() =>
			           {
				           try
				           {
					           ShowBusy("Saving ...");
					           SaveFilter();

					           ShowBusy("Fetching entity metadata ...");
					           RefreshEntityMetadata();

					           ShowBusy("Initialising ...");
					           InitEntityList();
				           }
				           catch (Exception ex)
				           {
					           PopException(ex);
				           }
				           finally
				           {
					           HideBusy();
				           }
			           }).Start();
		}

		#endregion

		#endregion
	}
}
