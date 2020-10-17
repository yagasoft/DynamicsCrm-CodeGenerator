#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.ViewModels;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using Application = System.Windows.Forms.Application;
using MetadataHelpers = Yagasoft.CrmCodeGenerator.Helpers.MetadataHelpers;

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

	#endregion
	
	/// <summary>
		///     Interaction logic for Filter.xaml
		/// </summary>
	public partial class Filter : INotifyPropertyChanged
	{
		private readonly IConnectionManager<IDisposableOrgSvc> connectionManager;
		private readonly MetadataCache metadataCache;

		#region Properties

		public string LogicalName { get; set; }

		public EntityProfilesHeaderSelector EntityProfilesHeaderSelector { get; set; }

		public EntityProfilesHeader SelectedEntityProfilesHeader { get; set; }

		public Settings Settings { get; set; }

		public List<EntityMetadata> EntityMetadataCache;

		public bool StillOpen { get; private set; } = true;

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

		public ObservableCollection<EntityProfileGridRow> Entities { get; set; }

		#endregion

		private readonly IDictionary<EntityProfilesHeader, ConcurrentBag<EntityProfileGridRow>> rowSourceMap =
			new Dictionary<EntityProfilesHeader, ConcurrentBag<EntityProfileGridRow>>();
		private ConcurrentBag<EntityProfileGridRow> rowListSource = new ConcurrentBag<EntityProfileGridRow>();

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public Filter(Window parentWindow, Settings settings,
			IConnectionManager<IDisposableOrgSvc> connectionManager, MetadataCache metadataCache)
		{
			this.connectionManager = connectionManager;
			this.metadataCache = metadataCache;

			InitializeComponent();

			Owner = parentWindow;
			WindowTitle = "Entities Profiling";

			Entities = new ObservableCollection<EntityProfileGridRow>();

			Settings = settings;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
						EntityProfilesHeaderSelector = Settings.EntityProfilesHeaderSelector;
						SelectedEntityProfilesHeader = EntityProfilesHeaderSelector.GetSelectedFilter();

						Status.ShowBusy(Dispatcher, BusyIndicator, "Fetching entity metadata ...");
						if (metadataCache.ProfileEntityMetadataCache.Any())
						{
							EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
						}
						else
						{
							RefreshEntityMetadata();
						}

						Status.ShowBusy(Dispatcher, BusyIndicator, "Initialising ...");
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

								TextBoxPrefix.DataContext = SelectedEntityProfilesHeader;
								TextBoxSuffix.DataContext = SelectedEntityProfilesHeader;

								EntitiesGrid.ItemsSource = Entities;

								ComboBoxFilters.DataContext = EntityProfilesHeaderSelector;
								ComboBoxFilters.DisplayMemberPath = "DisplayName";
							});

						Status.HideBusy(Dispatcher, BusyIndicator);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
						Dispatcher.Invoke(Close);
					}
				}).Start();
		}

		private void InitEntityList(List<string> filter = null)
		{
			Dispatcher.Invoke(Entities.Clear);

			var isFoundSource = rowSourceMap.TryGetValue(SelectedEntityProfilesHeader, out rowListSource);
			var rowList = new ConcurrentBag<EntityProfileGridRow>();

			if (!isFoundSource)
			{
				rowListSource = rowSourceMap[SelectedEntityProfilesHeader] = new ConcurrentBag<EntityProfileGridRow>();
			}

			var filteredEntities = EntityMetadataCache
				.Where(entity => filter == null || filter.Contains(entity.LogicalName)).ToArray();

			var missingProfiles = new ConcurrentBag<EntityProfile>();

			Parallel.ForEach(filteredEntities,
				entity =>
				{
					var entityAsync = entity;

					EntityProfile[] entityProfiles;

					lock (this)
					{
						entityProfiles = SelectedEntityProfilesHeader.EntityProfiles
							.Where(e => e.LogicalName == entityAsync.LogicalName).ToArray();
					}

					var  entityProfile = entityProfiles.FirstOrDefault();

					if (entityProfile == null)
					{
						entityProfile = new EntityProfile(entityAsync.LogicalName);
						missingProfiles.Add(entityProfile);
					}

					// clean redundant profiles
					if (entityProfiles.Length > 1)
					{
						foreach (var profile in entityProfiles.Skip(1))
						{
							lock (this)
							{
								SelectedEntityProfilesHeader.EntityProfiles.Remove(profile);
							}
						}
					}

					var row = rowListSource.FirstOrDefault(r => r.Name == entityAsync.LogicalName)
						?? new EntityProfileGridRow
						   {
							   OriginalEntityProfile = entityProfile,
							   EntityProfile = entityProfile.Copy(),
							   IsSelected = entityProfile.IsIncluded,
							   Name = entityAsync.LogicalName,
							   DisplayName = entity.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
								   ? Naming.GetProperHybridName(entity.SchemaName, entity.LogicalName)
								   : Naming.Clean(entity.DisplayName.UserLocalizedLabel.Label),
							   Rename = entityProfile.EntityRename,
							   Annotations = entityProfile.EntityAnnotations,
							   IsGenerateMeta = entityProfile.IsGenerateMeta,
							   IsOptionsetLabels = entityProfile.IsOptionsetLabels,
							   IsLookupLabels = entityProfile.IsLookupLabels,
							   ValueClearMode = entityProfile.ValueClearMode == null
								   ? ClearModeEnumUi.Default
								   : (ClearModeEnumUi)entityProfile.ValueClearMode
						   };

					rowList.Add(row);

					if (rowListSource.All(r => r.Name != row.Name))
					{
						rowListSource.Add(row);
					}
				});

			SelectedEntityProfilesHeader.EntityProfiles.AddRange(missingProfiles);

			foreach (var row in rowList.OrderByDescending(row => row.IsSelected).ThenBy(row => row.Name))
			{
				Dispatcher.Invoke(() => Entities.Add(row));
			}

			// if no filter, select all
			if (SelectedEntityProfilesHeader.EntityProfiles.Count(e => e.IsIncluded) == EntityMetadataCache.Count)
			{
				Dispatcher.Invoke(() => EntitiesSelectAll = true);
			}

			if (SelectedEntityProfilesHeader.EntityProfiles.Count(e => e.IsOptionsetLabels) == EntityMetadataCache.Count)
			{
				Dispatcher.Invoke(() => IsOptionsetLabelsSelectAll = true);
			}

			if (SelectedEntityProfilesHeader.EntityProfiles.Count(e => e.IsLookupLabels) == EntityMetadataCache.Count)
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
			foreach (var pair in rowSourceMap)
			{
				var header = pair.Key;

				foreach (var row in pair.Value)
				{
					var original = row.OriginalEntityProfile;
					var profile = row.EntityProfile;

					profile.IsIncluded = row.IsSelected;
					profile.IsGenerateMeta = row.IsGenerateMeta;
					profile.IsOptionsetLabels = row.IsOptionsetLabels;
					profile.IsLookupLabels = row.IsLookupLabels;
					profile.EntityRename = row.Rename;
					profile.EntityAnnotations = row.Annotations;

					switch (row.ValueClearMode)
					{
						case ClearModeEnumUi.Default:
							profile.ValueClearMode = null;
							break;

						default:
							profile.ValueClearMode = (ClearModeEnum?)row.ValueClearMode;
							break;
					}

					header.EntityProfiles.Remove(original);

					if (profile.IsContainsData)
					{
						header.EntityProfiles.Add(profile);
					}
				}
			}

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

			foreach (var entity in Settings.EntityList)
			{
				var isLinked = Settings.EarlyBoundLinkedSelected.Contains(entity);
				var crmProfile = Settings.CrmEntityProfiles.FirstOrDefault(p => p.LogicalName == entity);

				// copy attributes from contracts to CRM entity
				if (isLinked && crmProfile != null)
				{
					var contracts = Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders.SelectMany(p => p.EntityProfiles)
						.Where(p => p.LogicalName == entity).ToArray();
					crmProfile.Attributes = contracts.SelectMany(p => p.Attributes).Union(crmProfile.Attributes ?? new string[0]).Distinct().OrderBy(a => a).ToArray();
					crmProfile.OneToN = contracts.SelectMany(p => p.OneToN).Union(crmProfile.OneToN ?? new string[0]).Distinct().OrderBy(a => a).ToArray();
					crmProfile.NToOne = contracts.SelectMany(p => p.NToOne).Union(crmProfile.NToOne ?? new string[0]).Distinct().OrderBy(a => a).ToArray();
					crmProfile.NToN = contracts.SelectMany(p => p.NToN).Union(crmProfile.NToN ?? new string[0]).Distinct().OrderBy(a => a).ToArray();
				}
			}
		}

		#region CRM

		private void RefreshEntityMetadata()
		{
			MetadataHelpers.RefreshSettingsEntityMetadata(Settings, connectionManager, metadataCache);
			EntityMetadataCache = metadataCache.ProfileEntityMetadataCache;
		}

		#endregion

		#region UI events

		#region Grid stuff

		private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var cell = sender as DataGridCell;

			if (cell != null && !cell.IsEditing && !e.IsComboBoxCellClicked())
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
			if (e.IsTextCellClicked() || e.IsComboBoxCellClicked())
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

			if (d != null && (d.IsCheckboxClickedParentCheck("GenerateMeta")
			                  || d.IsCheckboxClickedChildrenCheck("GenerateMeta")))
			{
				// clicked on meta
				var rowDataCast = (EntityProfileGridRow) row.Item;
				rowDataCast.IsGenerateMeta = !rowDataCast.IsGenerateMeta;

				// selectAll value to false
				var generateMetaField = GetType().GetField("MetadataSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				generateMetaField?.SetValue(this, false);

				OnPropertyChanged("MetadataSelectAll");
			}
			else if (d != null && (d.IsCheckboxClickedParentCheck("IsOptionsetLabels")
			                       || d.IsCheckboxClickedChildrenCheck("IsOptionsetLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityProfileGridRow) row.Item;
				rowDataCast.IsOptionsetLabels = !rowDataCast.IsOptionsetLabels;

				// selectAll value to false
				var field = GetType().GetField("IsOptionsetLabelsSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("IsOptionsetLabelsSelectAll");
			}
			else if (d != null && (d.IsCheckboxClickedParentCheck("IsLookupLabels")
			                       || d.IsCheckboxClickedChildrenCheck("IsLookupLabels")))
			{
				// clicked on meta
				var rowDataCast = (EntityProfileGridRow) row.Item;
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
				// enables editing on single click
				if (!row.IsFocused)
				{
					row.Focus();
				}

				var rowData = (GridRow)row.Item;

				if (e.IsTextCellClicked())
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

					if (row.IsSelected)
					{
						return;
					}

					// select current row
					Extensions.SetBang(row, true);
					row.IsSelected = true;

					// get logical name and re-init
					LogicalName = rowData.Name;

					var entityProfile = rowData.EntityProfile ??= new EntityProfile(LogicalName);

					new FilterDetails(this, LogicalName, Settings, entityProfile,
						new ObservableCollection<GridRow>(Entities), connectionManager, metadataCache)
						.ShowDialog();
				}
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

					foreach (var item in grid.SelectedItems.Cast<GridRow>()
						.Where(item => !string.IsNullOrEmpty(item.Annotations)))
					{
						item.Annotations = "";
					}

					break;
			}
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
			SelectedEntityProfilesHeader = EntityProfilesHeaderSelector.GetSelectedFilter();

			TextBoxPrefix.DataContext = SelectedEntityProfilesHeader;
			TextBoxSuffix.DataContext = SelectedEntityProfilesHeader;

			InitEntityList();
		}

		private void ButtonNewFilter_Click(object sender, RoutedEventArgs e)
		{
			var newFilter = new EntityProfilesHeader();
			EntityProfilesHeaderSelector.EntityProfilesHeaders.Add(newFilter);
			EntityProfilesHeaderSelector.SelectedFilterIndex = EntityProfilesHeaderSelector.EntityProfilesHeaders.IndexOf(newFilter);
		}

		private void ButtonDuplicateFilter_Click(object sender, RoutedEventArgs e)
		{
			var newFilter = SelectedEntityProfilesHeader.Copy();
			newFilter.Prefix = "";
			newFilter.Suffix = "Contract";

			EntityProfilesHeaderSelector.EntityProfilesHeaders.Add(newFilter);
			DteHelper.ShowInfo("The selected profile has been duplicated, and the new profile has been selected instead.",
				"Profile duplicated.");
			EntityProfilesHeaderSelector.SelectedFilterIndex = EntityProfilesHeaderSelector.EntityProfilesHeaders.Count - 1;
		}

		private void ButtonDeleteFilter_Click(object sender, RoutedEventArgs e)
		{
			if (EntityProfilesHeaderSelector.EntityProfilesHeaders.Count <= 1)
			{
				Status.PopException(Dispatcher, new Exception("Can't delete the last filter profile."));
				return;
			}

			if (DteHelper.IsConfirmed("Are you sure you want to delete this filter profile? This will affect other entities!",
				"Confirm delete action ..."))
			{
				EntityProfilesHeaderSelector.EntityProfilesHeaders.Remove(EntityProfilesHeaderSelector.GetSelectedFilter());
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
					customEntities =
						EntityMetadataCache
							.ToDictionary(key => key.LogicalName,
								value =>
								{
									var rename = SelectedEntityProfilesHeader.EntityProfiles
										.FirstOrDefault(filter => filter.LogicalName == value.LogicalName)?.EntityRename;

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
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Filtering ...");

						InitEntityList(customEntities?.ToList());

						//Dispatcher.Invoke(() => { DataContext = this; });
						Dispatcher.Invoke(() => TextBoxFilter.Focus());

						Status.HideBusy(Dispatcher, BusyIndicator);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
						Dispatcher.Invoke(Close);
					}
				}).Start();
		}

		#endregion

		#region Bottom bar stuff

		private void Logon_Click(object sender, RoutedEventArgs e)
		{
			SaveFilter();
			Dispatcher.Invoke(Close);
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			StillOpen = false;
			DialogResult = false;
			Dispatcher.Invoke(Close);
		}

		#endregion

		#endregion
	}
}
