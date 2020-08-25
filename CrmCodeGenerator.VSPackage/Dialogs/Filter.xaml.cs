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
		protected EntityProfile entityProfile;
		protected bool isSelected;
		protected string rename;

		public bool IsReadOnlyEnabled
		{
			get; set;
		}

		public EntityProfile EntityProfile
		{
			get => entityProfile;
			set
			{
				entityProfile = value;
				OnPropertyChanged();
			}
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
		private bool isApplyToCrm;

		public bool IsApplyToCrm
		{
			get => isApplyToCrm;
			set
			{
				isApplyToCrm = value;
				OnPropertyChanged();
			}
		}

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
		private readonly IConnectionManager<IDisposableOrgSvc> connectionManager;
		private readonly MetadataCacheManagerBase metadataCacheManager;

		#region Properties

		public string LogicalName { get; set; }

		public EntityProfilesHeaderSelector EntityProfilesHeaderSelector { get; set; }

		public EntityProfilesHeader SelectedEntityProfilesHeader { get; set; }

		public EntityProfile EntityProfile { get; set; }

		public Settings Settings { get; set; }

		public List<EntityMetadata> EntityMetadataCache;

		private Style originalProgressBarStyle;

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

		private bool applyToCrmSelectAll;

		public bool ApplyToCrmSelectAll
		{
			get => applyToCrmSelectAll;
			set
			{
				applyToCrmSelectAll = value;
				Entities.ToList().ForEach(entity => entity.IsApplyToCrm = value);
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
		private readonly IDictionary<EntityProfilesHeader, List<EntityGridRow>> rowMap = new Dictionary<EntityProfilesHeader, List<EntityGridRow>>();
		private List<EntityGridRow> rowList;

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public Filter(Window parentWindow, Settings settings,
			IConnectionManager<IDisposableOrgSvc> connectionManager, MetadataCacheManagerBase metadataCacheManager)
		{
			this.connectionManager = connectionManager;
			this.metadataCacheManager = metadataCacheManager;
			InitializeComponent();

			Owner = parentWindow;
			WindowTitle = "Entities Profiling";

			Entities = new ObservableCollection<EntityGridRow>();

			Settings = settings;
			metadataCache = metadataCacheManager.GetCache(settings.ConnectionString);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			originalProgressBarStyle = BusyIndicator.ProgressBarStyle;

			new Thread(
				() =>
				{
					try
					{
						EntityProfilesHeaderSelector = Settings.EntityProfilesHeaderSelector;
						SelectedEntityProfilesHeader = EntityProfilesHeaderSelector.GetSelectedFilter();

						Status.ShowBusy(Dispatcher, BusyIndicator, "Fetching entity metadata ...",
							HeightProperty, originalProgressBarStyle);
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
								CheckBoxApplyToCrmSelectAll.DataContext = this;
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
						Dispatcher.InvokeAsync(Close);
					}
				}).Start();
		}

		private void InitEntityList(List<string> filter = null)
		{
			Dispatcher.Invoke(Entities.Clear);

			if (!rowMap.TryGetValue(SelectedEntityProfilesHeader, out rowList))
			{
				rowList = rowMap[SelectedEntityProfilesHeader] = new List<EntityGridRow>();

				var filteredEntities = EntityMetadataCache
					.Where(entity => filter == null || filter.Contains(entity.LogicalName)).ToArray();

				foreach (var entity in filteredEntities)
				{
					var entityAsync = entity;

					Dispatcher.Invoke(
						() =>
						{
							var entityProfile = SelectedEntityProfilesHeader.EntityProfiles
								.FirstOrDefault(e => e.LogicalName == entityAsync.LogicalName);

							if (entityProfile == null)
							{
								entityProfile = new EntityProfile(entityAsync.LogicalName);
								SelectedEntityProfilesHeader.EntityProfiles.Add(entityProfile);
							}

							var row =
								new EntityGridRow
								{
									EntityProfile = entityProfile,
									IsSelected = !entityProfile.IsExcluded,
									Name = entityAsync.LogicalName,
									DisplayName = entity.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
										? Naming.GetProperHybridName(entity.SchemaName, entity.LogicalName)
										: Naming.Clean(entity.DisplayName.UserLocalizedLabel.Label),
									Rename = entityProfile.EntityRename,
									IsApplyToCrm = entityProfile.IsApplyToCrm,
									IsGenerateMeta = entityProfile.IsGenerateMeta,
									IsOptionsetLabels = entityProfile.IsOptionsetLabels,
									IsLookupLabels = entityProfile.IsLookupLabels,
									ValueClearMode = entityProfile.ValueClearMode == null
										? ClearModeEnumUi.Default
										: (ClearModeEnumUi)entityProfile.ValueClearMode
								};

							row.PropertyChanged +=
								(sender, args) =>
								{
									if (args.PropertyName == nameof(EntityProfile.IsApplyToCrm))
									{
										Dispatcher.InvokeAsync(() => CheckBoxIsDefault.IsChecked = rowList.Any(e => e.IsApplyToCrm));
									}
								};

							rowList.Add(row);
						});
				}
			}

			foreach (var row in rowList.OrderByDescending(row => row.IsSelected).ThenBy(row => row.Name))
			{
				Dispatcher.Invoke(() => Entities.Add(row));
			}

			// if no filter, select all
			if (SelectedEntityProfilesHeader.EntityProfiles.Count(e => !e.IsExcluded) == EntityMetadataCache.Count)
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

			Dispatcher.InvokeAsync(() => CheckBoxIsDefault.IsChecked = rowList.Any(e => e.IsApplyToCrm));
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
			foreach (var row in rowMap.Values.SelectMany(e => e))
			{
				var entityProfile = row.EntityProfile;
				entityProfile.IsExcluded = !row.IsSelected;
				entityProfile.IsApplyToCrm = row.IsApplyToCrm;
				entityProfile.IsGenerateMeta = row.IsGenerateMeta;
				entityProfile.IsOptionsetLabels = row.IsOptionsetLabels;
				entityProfile.IsLookupLabels = row.IsLookupLabels;
				entityProfile.EntityRename = row.Rename;

				switch (row.ValueClearMode)
				{
					case ClearModeEnumUi.Default:
						entityProfile.ValueClearMode = null;
						break;

					default:
						entityProfile.ValueClearMode = (ClearModeEnum?)row.ValueClearMode;
						break;
				}

				if (entityProfile.IsApplyToCrm)
				{
					ConfirmSingleDefault(entityProfile);
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
		}

		private void ConfirmSingleDefault(EntityProfile entityProfile)
		{
			var duplicates =
				EntityProfilesHeaderSelector.EntityProfilesHeaders
					.SelectMany(e => e.EntityProfiles.Where(s => s.LogicalName == entityProfile.LogicalName && s.IsApplyToCrm))
					.GroupBy(e => e.LogicalName)
					.Where(e => e.Count() > 1);

			foreach (var group in duplicates)
			{
				for (var i = 1; i < group.Count(); i++)
				{
					group.ElementAt(i).IsApplyToCrm = false;
				}
			}
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

			if (d != null && (IsCheckboxClickedParentCheck(d, "ApplyToCrm")
			                  || IsCheckboxClickedChildrenCheck(d, "ApplyToCrm")))
			{
				// clicked on ApplyToCrm
				var rowDataCast = (EntityGridRow) row.Item;
				rowDataCast.IsApplyToCrm = !rowDataCast.IsApplyToCrm;

				// selectAll value to false
				var applyToCrmField = GetType().GetField("ApplyToCrmSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				applyToCrmField?.SetValue(this, false);

				OnPropertyChanged("ApplyToCrmSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "GenerateMeta")
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
					new FilterDetails(this, LogicalName, Settings, Entities, connectionManager, metadataCacheManager)
						.ShowDialog();
				}

				return;
			}

			var d = (DependencyObject) e.OriginalSource;

			if (d != null && (IsCheckboxClickedParentCheck(d, "ApplyToCrm")
			                  || IsCheckboxClickedChildrenCheck(d, "ApplyToCrm")))
			{
				// clicked on ApplyToCrm
				var rowDataCast = (EntityGridRow) rowData;
				rowDataCast.IsApplyToCrm = !rowDataCast.IsApplyToCrm;

				// selectAll value to false
				var applyToCrmField = GetType().GetField("ApplyToCrmSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				applyToCrmField?.SetValue(this, false);

				OnPropertyChanged("ApplyToCrmSelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "GenerateMeta")
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

			if (EntityProfile != null && rowDataQ.Name == EntityProfile.LogicalName)
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
			EntityProfile = null;
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
					var defaultFiltered = Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
						.SelectMany(s => s.EntityProfiles.Where(e => e.IsApplyToCrm)).ToArray();

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
			new Thread(
				() =>
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

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			StillOpen = false;
			Dispatcher.InvokeAsync(Close);
		}

		private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
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
