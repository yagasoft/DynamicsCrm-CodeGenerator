#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Client.Collections.Generic;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Cache.Metadata;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Application = System.Windows.Forms.Application;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	public class EntitiesSelectionGridRow : EntityGridRow
	{
		private bool isJsEarly;

		public bool IsJsEarly
		{
			get => isJsEarly;
			set
			{
				isJsEarly = value;
				OnPropertyChanged();
			}
		}

		public IEnumerable<string> ActionNames
		{
			get => actionNames;
			set
			{
				actionNames = value;
				OnPropertyChanged();
			}
		}

		public IEnumerable<string> SelectedActions
		{
			get => selectedActions;
			set
			{
				selectedActions = value;
				OnPropertyChanged();
				OnPropertyChanged("ActionColour");
				OnPropertyChanged("ActionCount");
			}
		}

		private IEnumerable<string> actionNames;
		private IEnumerable<string> selectedActions;

		public Brush ActionColour => SelectedActions?.Any() == true ? Brushes.Red : Brushes.Black;

		public string ActionCount
		{
			get
			{
				var count = SelectedActions?.Count();
				return count > 0 ? count.ToString() : "-";
			}
		}

		public Brush Colour => IsFiltered ? Brushes.Red : Brushes.Black;

		private bool isFiltered;

		public bool IsFiltered
		{
			get => isFiltered;
			set
			{
				isFiltered = value;
				OnPropertyChanged();
				OnPropertyChanged("Colour");
			}
		}

	}

	/// <summary>
	///     Interaction logic for Filter.xaml
	/// </summary>
	public partial class EntitySelection : INotifyPropertyChanged
	{
		private readonly IConnectionManager<IDisposableOrgSvc> connectionManager;
		private readonly MetadataCacheManagerBase metadataCacheManager;

		#region Properties

		public Settings Settings { get; set; }

		public List<EntityMetadata> EntityMetadataCache;

		private Style originalProgressBarStyle;

		public bool StillOpen { get; } = true;

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


		private bool isMetadataSelectAll;

		public bool IsMetadataSelectAll
		{
			get => isMetadataSelectAll;
			set
			{
				isMetadataSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsGenerateMeta = value);
				OnPropertyChanged();
			}
		}

		private bool isJsEarlySelectAll;

		public bool IsJsEarlySelectAll
		{
			get => isJsEarlySelectAll;
			set
			{
				isJsEarlySelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsJsEarly = value);
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

		public ObservableCollection<EntitiesSelectionGridRow> Entities { get; set; }

		#endregion

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		private MetadataCache metadataCache;

		#region Init

		public EntitySelection(Window parentWindow, Settings settings,
			IConnectionManager<IDisposableOrgSvc> connectionManager, MetadataCacheManagerBase metadataCacheManager)
		{
			InitializeComponent();

			this.connectionManager = connectionManager;
			this.metadataCacheManager = metadataCacheManager;

			Owner = parentWindow;

			Entities = new ObservableCollection<EntitiesSelectionGridRow>();

			Settings = settings;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			originalProgressBarStyle = BusyIndicator.ProgressBarStyle;

			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Fetching entity metadata ...",
							HeightProperty, originalProgressBarStyle);

						metadataCache = metadataCacheManager.GetCache(Settings.ConnectionString);

						if (metadataCache.ProfileEntityMetadataCache.Any())
						{
							EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
						}
						else
						{
							RefreshEntityMetadata();
						}

						Status.ShowBusy(Dispatcher, BusyIndicator, "Initialising ...",
							HeightProperty, originalProgressBarStyle);
						InitEntityList();

						Dispatcher.Invoke(
							() =>
							{
								DataContext = this;
								CheckBoxEntitiesSelectAll.DataContext = this;
								CheckBoxMetadataSelectAll.DataContext = this;
								CheckBoxJsEarlySelectAll.DataContext = this;
								CheckBoxOptionsetLabelsSelectAll.DataContext = this;
								CheckBoxLookupLabelsSelectAll.DataContext = this;
							});

						Status.HideBusy(Dispatcher, BusyIndicator);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
						Dispatcher.InvokeAsync(Close);
					}
				}).Start();
		}

		private void InitEntityList(List<string> filter = null)
		{
			Dispatcher.Invoke(Entities.Clear);

			var rowList = new List<EntitiesSelectionGridRow>();

			var filteredEntities = EntityMetadataCache
				.Where(entity => filter == null || filter.Contains(entity.LogicalName)).ToArray();

			foreach (var entity in filteredEntities)
			{
				var entityAsync = entity;

				Dispatcher.Invoke(
					() =>
					{
						var row =
							new EntitiesSelectionGridRow
							{
								IsSelected = Settings.EntitiesSelected.Contains(entity.LogicalName),
								Name = entityAsync.LogicalName,
								DisplayName =
									entity.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
										? Naming.GetProperHybridName(entity.SchemaName, entity.LogicalName)
										: Naming.Clean(entity.DisplayName.UserLocalizedLabel.Label),
								IsFiltered = Settings.FilteredEntities.Contains(entity.LogicalName),
								IsGenerateMeta = Settings.PluginMetadataEntitiesSelected.Contains(entity.LogicalName),
								IsJsEarly = Settings.JsEarlyBoundEntitiesSelected.Contains(entity.LogicalName),
								IsOptionsetLabels =
									Settings.OptionsetLabelsEntitiesSelected.Contains(entity.LogicalName),
								IsLookupLabels = Settings.LookupLabelsEntitiesSelected.Contains(entity.LogicalName),
								SelectedActions = Settings.SelectedActions?.FirstNotNullOrDefault(entityAsync.LogicalName)
							};

						row.PropertyChanged +=
							(sender, args) =>
							{
								if (args.PropertyName == "IsSelected")
								{
									if (row.IsSelected && !Settings.EntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.EntitiesSelected.Add(entity.LogicalName);
									}
									else if (!row.IsSelected && Settings.EntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.EntitiesSelected.Remove(entity.LogicalName);
									}
								}
								else if (args.PropertyName == "IsGenerateMeta")
								{
									if (row.IsGenerateMeta && !Settings.PluginMetadataEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.PluginMetadataEntitiesSelected.Add(entity.LogicalName);
									}
									else if (!row.IsGenerateMeta && Settings.PluginMetadataEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.PluginMetadataEntitiesSelected.Remove(entity.LogicalName);
									}
								}
								else if (args.PropertyName == "IsJsEarly")
								{
									if (row.IsJsEarly && !Settings.JsEarlyBoundEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.JsEarlyBoundEntitiesSelected.Add(entity.LogicalName);
									}
									else if (!row.IsJsEarly && Settings.JsEarlyBoundEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.JsEarlyBoundEntitiesSelected.Remove(entity.LogicalName);
									}
								}
								else if (args.PropertyName == "IsOptionsetLabels")
								{
									if (row.IsOptionsetLabels && !Settings.OptionsetLabelsEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.OptionsetLabelsEntitiesSelected.Add(entity.LogicalName);
									}
									else if (!row.IsOptionsetLabels && Settings.OptionsetLabelsEntitiesSelected.Contains(entity.LogicalName))
									{
										if (Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
											.SelectMany(filterQ => filterQ.EntityProfiles)
											.Any(dataFilter => dataFilter.LogicalName == entity.LogicalName
												&& dataFilter.IsOptionsetLabels))
										{
											row.IsOptionsetLabels = true;
										}
										else
										{
											Settings.OptionsetLabelsEntitiesSelected.Remove(entity.LogicalName);
										}
									}
								}
								else if (args.PropertyName == "IsLookupLabels")
								{
									if (row.IsLookupLabels && !Settings.LookupLabelsEntitiesSelected.Contains(entity.LogicalName))
									{
										Settings.LookupLabelsEntitiesSelected.Add(entity.LogicalName);
									}
									else if (!row.IsLookupLabels && Settings.LookupLabelsEntitiesSelected.Contains(entity.LogicalName))
									{
										if (Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
											.SelectMany(filterQ => filterQ.EntityProfiles)
											.Any(dataFilter => dataFilter.LogicalName == entity.LogicalName
												&& dataFilter.IsLookupLabels))
										{
											row.IsLookupLabels = true;
										}
										else
										{
											Settings.LookupLabelsEntitiesSelected.Remove(entity.LogicalName);
										}
									}
								}
								else if (args.PropertyName == "SelectedActions")
								{
									if (row.SelectedActions?.Any() == true)
									{
										Settings.SelectedActions[entity.LogicalName] = row.SelectedActions.Intersect(row.ActionNames).ToArray();
									}
									else
									{
										Settings.SelectedActions.Remove(entity.LogicalName);
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

			// selectAll status based on selection count
			if (filteredEntities.All(entity => rowList.Where(row => row.IsSelected).Select(row => row.Name)
				                                   .Contains(entity.LogicalName)))
			{
				Dispatcher.Invoke(() => EntitiesSelectAll = true);
			}

			if (filteredEntities.All(entity => rowList.Where(row => row.IsGenerateMeta).Select(row => row.Name)
				                                   .Contains(entity.LogicalName)))
			{
				Dispatcher.Invoke(() => IsMetadataSelectAll = true);
			}

			if (filteredEntities.All(entity => rowList.Where(row => row.IsJsEarly).Select(row => row.Name)
				                                   .Contains(entity.LogicalName)))
			{
				Dispatcher.Invoke(() => IsJsEarlySelectAll = true);
			}

			if (filteredEntities.All(entity => rowList.Where(row => row.IsOptionsetLabels).Select(row => row.Name)
				                                   .Contains(entity.LogicalName)))
			{
				Dispatcher.Invoke(() => IsOptionsetLabelsSelectAll = true);
			}

			if (filteredEntities.All(entity => rowList.Where(row => row.IsLookupLabels).Select(row => row.Name)
				                                   .Contains(entity.LogicalName)))
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
		}

		#region CRM

		private void RefreshEntityMetadata()
		{
			MetadataHelpers.RefreshSettingsEntityMetadata(Settings, connectionManager, metadataCacheManager);
			EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
		}

		#endregion

		#region UI events

		#region Grid stuff

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

			var d = (DependencyObject)e.OriginalSource;

			if (d != null && (IsControlClickedParentCheck<CheckBox>(d, "IsGenerateMeta")
				|| IsControlClickedChildrenCheck<CheckBox>(d, "IsGenerateMeta")))
			{
				// clicked on meta
				var rowDataCast = (EntitiesSelectionGridRow)row.Item;
				rowDataCast.IsGenerateMeta = !rowDataCast.IsGenerateMeta;

				// selectAll value to false
				var field = GetType().GetField("IsMetadataSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsMetadataSelectAll");
			}
			else if (d != null && (IsControlClickedParentCheck<CheckBox>(d, "IsJsEarly")
				|| IsControlClickedChildrenCheck<CheckBox>(d, "IsJsEarly")))
			{
				// clicked on meta
				var rowDataCast = (EntitiesSelectionGridRow)row.Item;
				rowDataCast.IsJsEarly = !rowDataCast.IsJsEarly;

				// selectAll value to false
				var field = GetType().GetField("IsJsEarlySelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsJsEarlySelectAll");
			}
			else if (d != null && (IsControlClickedParentCheck<CheckBox>(d, "IsOptionsetLabels")
				|| IsControlClickedChildrenCheck<CheckBox>(d, "IsOptionsetLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntitiesSelectionGridRow)row.Item;
				rowDataCast.IsOptionsetLabels = !rowDataCast.IsOptionsetLabels;

				// selectAll value to false
				var field = GetType().GetField("IsOptionsetLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsOptionsetLabelsSelectAll");
			}
			else if (d != null && (IsControlClickedParentCheck<CheckBox>(d, "IsLookupLabels")
				|| IsControlClickedChildrenCheck<CheckBox>(d, "IsLookupLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntitiesSelectionGridRow)row.Item;
				rowDataCast.IsLookupLabels = !rowDataCast.IsLookupLabels;

				// selectAll value to false
				var field = GetType().GetField("IsLookupLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsLookupLabelsSelectAll");
			}
			else if (d != null && (IsControlClickedParentCheck<Button>(d, "LoadActions")
				|| IsControlClickedChildrenCheck<Button>(d, "LoadActions")))
			{
				// clicked on meta
				var rowDataCast = (EntitiesSelectionGridRow)row.Item;
				var location = PointToScreen(Mouse.GetPosition(this));
				LoadActions(rowDataCast,
					() => new PopupSelector(this, rowDataCast.ActionNames, rowDataCast.SelectedActions,
						selectedActions => Dispatcher.InvokeAsync(() => rowDataCast.SelectedActions = selectedActions),
						location.X, location.Y).ShowDialog());
			}
			else
			{
				// clicked select
				var rowData = (GridRow)row.Item;
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
				// enables editing on single click
				if (!row.IsFocused)
				{
					row.Focus();
				}

				var rowData = (GridRow)row.Item;

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

						var rowQ = (DataGridRow)container;

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
						var logicalName = rowData.Name;
						new FilterDetails(this, logicalName, Settings,
							new ObservableCollection<EntityGridRow>(Entities),
							connectionManager, metadataCacheManager, true).ShowDialog();
					}
				}
			}
		}

		private static bool IsTextCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] { typeof(TextBlock), typeof(TextBox), typeof(DataGridTextColumn), typeof(RichTextBox) };
			var d = (DependencyObject)e.OriginalSource;
			return d != null && (IsCellClickedParentCheck(d, types) || IsCellClickedChildrenCheck(d, types));
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

		private static bool IsControlClickedChildrenCheck<TControl>(DependencyObject dep, string name)
			where TControl : ContentControl
		{
			switch (dep)
			{
				case TControl control when control.Name == name:
					return true;

				case null:
					return false;
			}

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
			{
				if (IsControlClickedChildrenCheck<TControl>(VisualTreeHelper.GetChild(dep, i), name))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsControlClickedParentCheck<TControl>(DependencyObject dep, string name)
			where TControl : ContentControl
		{
			while (dep != null)
			{
				if (dep is TControl control && control.Name == name)
				{
					return true;
				}

				dep = VisualTreeHelper.GetParent(dep);
			}

			return false;
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

		private void LoadActions(EntitiesSelectionGridRow row, Action action)
		{
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, $"Loading {row.Name} Actions ...",
							HeightProperty, originalProgressBarStyle);
						var actions = MetadataHelpers.RetrieveActionNames(Settings,
							connectionManager, metadataCacheManager, row.Name);
						Dispatcher.InvokeAsync(
							() =>
							{
								row.ActionNames = new ObservableCollection<string>(actions);
								row.SelectedActions = new ObservableCollection<string>(Settings
									.SelectedActions.FirstNotNullOrDefault(row.Name) ?? Array.Empty<string>());
								action();
							});
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
					}
					finally
					{
						Status.HideBusy(Dispatcher, BusyIndicator);
					}
				}).Start();
		}

		#region Top bar stuff

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
					var defaultFiltered = Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
						.SelectMany(e => e.EntityProfiles)
						.Where(e => e.IsApplyToCrm).ToArray();

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
					          	Status.ShowBusy(Dispatcher, BusyIndicator, "Filtering ...",
					          		HeightProperty, originalProgressBarStyle);

					           InitEntityList(customEntities?.ToList());

					           //Dispatcher.Invoke(() => { DataContext = this; });
					           Dispatcher.Invoke(() => TextBoxFilter.Focus());
					           
					          	Status.HideBusy(Dispatcher, BusyIndicator);
				           }
				           catch (Exception ex)
				           {
					          	Status.PopException(Dispatcher, ex);
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
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Saving ...",
							HeightProperty, originalProgressBarStyle);
						SaveFilter();

						Status.ShowBusy(Dispatcher, BusyIndicator, "Fetching entity metadata ...",
							HeightProperty, originalProgressBarStyle);
						RefreshEntityMetadata();

						Status.ShowBusy(Dispatcher, BusyIndicator, "Initialising ...",
							HeightProperty, originalProgressBarStyle);
						InitEntityList();
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
					}
					finally
					{
						Status.HideBusy(Dispatcher, BusyIndicator);
					}
				}).Start();
		}

		#endregion

		#endregion
	}
}
