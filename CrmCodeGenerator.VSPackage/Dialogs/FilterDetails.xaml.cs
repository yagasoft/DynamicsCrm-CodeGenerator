#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using Microsoft.Xrm.Tooling.Connector;
using Application = System.Windows.Forms.Application;
using MultiSelectComboBoxClass = CrmCodeGenerator.Controls.MultiSelectComboBox;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for FilterDetails.xaml
	/// </summary>
	public partial class FilterDetails : INotifyPropertyChanged
	{
		#region Properties

		public string LogicalName { get; set; }

		public EntityFilterArray EntityFilterList { get; set; }
		public EntityFilter EntityFilter { get; set; }

		public EntityDataFilter EntityDataFilter { get; set; }

		public SettingsNew Settings { get; set; }

		public IDictionary<string, EntityMetadata> AttributeMetadataCache;

		private Style originalProgressBarStyle;

		public bool StillOpen { get; private set; } = true;

		public string WindowTitle { get; set; }

		private bool fieldsSelectAll;

		public bool FieldsSelectAll
		{
			get { return fieldsSelectAll; }
			set
			{
				fieldsSelectAll = value;
				Fields.ToList().ForEach(field => field.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool readOnlySelectAll;

		public bool ReadOnlySelectAll
		{
			get { return readOnlySelectAll; }
			set
			{
				readOnlySelectAll = value;
				Fields.Where(field => field.IsReadOnlyEnabled).ToList()
					.ForEach(field => field.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		private bool clearFlagSelectAll;

		public bool ClearFlagSelectAll
		{
			get { return clearFlagSelectAll; }
			set
			{
				clearFlagSelectAll = value;
				Fields.Where(field => field.IsClearFlagEnabled).ToList()
					.ForEach(field => field.IsClearFlag = value);
				OnPropertyChanged();
			}
		}

		private bool relations1NSelectAll;

		public bool Relations1NSelectAll
		{
			get { return relations1NSelectAll; }
			set
			{
				relations1NSelectAll = value;
				Relations1N.ToList().ForEach(relation => relation.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool relations1NReadOnlyAll;

		public bool Relations1NFReadOnlyAll
		{
			get
			{
				return relations1NReadOnlyAll;
			}
			set
			{
				relations1NReadOnlyAll = value;
				Relations1N.ToList().ForEach(relation => relation.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		private bool relationsN1SelectAll;

		public bool RelationsN1SelectAll
		{
			get { return relationsN1SelectAll; }
			set
			{
				relationsN1SelectAll = value;
				RelationsN1.ToList().ForEach(relation => relation.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool relationsN1FlattenAll;

		public bool RelationsN1FlattenAll
		{
			get { return relationsN1FlattenAll; }
			set
			{
				relationsN1FlattenAll = value;
				RelationsN1.ToList().ForEach(relation => relation.IsFlatten = value);
				OnPropertyChanged();
			}
		}

		private bool relationsN1ReadOnlyAll;

		public bool RelationsN1ReadOnlyAll
		{
			get { return relationsN1ReadOnlyAll; }
			set
			{
				relationsN1ReadOnlyAll = value;
				RelationsN1.ToList().ForEach(relation => relation.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		private bool relationsNNSelectAll;

		public bool RelationsNNSelectAll
		{
			get { return relationsNNSelectAll; }
			set
			{
				relationsNNSelectAll = value;
				RelationsNN.ToList().ForEach(relation => relation.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool relationsNNReadOnlyAll;

		public bool RelationsNNReadOnlyAll
		{
			get
			{
				return relationsNNReadOnlyAll;
			}
			set
			{
				relationsNNReadOnlyAll = value;
				RelationsNN.ToList().ForEach(relation => relation.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		public ObservableCollection<EntityGridRow> Entities { get; set; }
		public ObservableCollection<FieldGridRow> Fields { get; set; }
		public ObservableCollection<Relations1NGridRow> Relations1N { get; set; }
		public ObservableCollection<RelationsN1GridRow> RelationsN1 { get; set; }
		public ObservableCollection<RelationsNNGridRow> RelationsNN { get; set; }

		public bool IsEnglishLabelEnabled
		{
			get { return isEnglishLabelEnabled; }
			set
			{
				isEnglishLabelEnabled = value;
				OnPropertyChanged();
			}
		}

		public bool isEnglishLabelEnabled;

		public bool IsCrmEntities;

		#endregion

		private MetadataCache metadataCache;

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public FilterDetails(Window parentWindow, string logicalName, SettingsNew settings,
			ObservableCollection<EntityGridRow> entities, bool isChecked, bool isCrmEntities = false)
		{
			InitializeComponent();

			Owner = parentWindow;

			LogicalName = logicalName;
			WindowTitle = $"\"{LogicalName}\" Profiling";

			IsEnglishLabelEnabled = isChecked;
			IsCrmEntities = isCrmEntities;

			Entities = entities;
			Fields = new ObservableCollection<FieldGridRow>();
			Relations1N = new ObservableCollection<Relations1NGridRow>();
			RelationsN1 = new ObservableCollection<RelationsN1GridRow>();
			RelationsNN = new ObservableCollection<RelationsNNGridRow>();

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

					           ShowBusy("Initialising ...");

					           Dispatcher.Invoke(() =>
					                             {
						                             DataContext = this;
						                             CheckBoxFieldsSelectAll.DataContext = this;
						                             CheckBoxReadOnlySelectAll.DataContext = this;
						                             CheckBoxClearFlagSelectAll.DataContext = this;
						                             CheckBoxRelations1NSelectAll.DataContext = this;
						                             CheckBoxRelationsN1SelectAll.DataContext = this;
						                             CheckBoxNToOneFlattenAll.DataContext = this;
						                             CheckBoxRelationsNNSelectAll.DataContext = this;

						                             //TextBoxEnglishLabelField.DataContext = EntityDataFilter;

						                             FieldsGrid.ItemsSource = Fields;
						                             FieldsGrid.Columns[4].Visibility = IsEnglishLabelEnabled
							                                                                ? Visibility.Visible
							                                                                : Visibility.Hidden;
						                             Relations1NGrid.ItemsSource = Relations1N;
						                             RelationsN1Grid.ItemsSource = RelationsN1;
						                             RelationsNNGrid.ItemsSource = RelationsNN;
					                             });

					           Initialise();

					           HideBusy();
				           }
				           catch (Exception ex)
				           {
					           PopException(ex);
					           Dispatcher.InvokeAsync(Close);
				           }
			           }).Start();
		}

		private void Initialise()
		{
			EntityFilter = Settings.EntityDataFilterArray.GetSelectedFilter();
			EntityDataFilter = EntityFilter.EntityFilterList.FirstOrDefault(filter => filter.LogicalName == LogicalName);

			if (EntityDataFilter == null)
			{
				EntityDataFilter = new EntityDataFilter(LogicalName);
				EntityFilter.EntityFilterList.Add(EntityDataFilter);
			}

			//Dispatcher.Invoke(() => TextBoxEnglishLabelField.DataContext = EntityDataFilter);

			new Thread(() =>
			           {
				           try
				           {
					           ShowBusy("Building lists ...");

					           if (AttributeMetadataCache == null)
					           {
						           AttributeMetadataCache = metadataCache.ProfileAttributeMetadataCache;
					           }

					           if (!AttributeMetadataCache.ContainsKey(LogicalName))
					           {
						           AttributeMetadataCache[LogicalName] =
							           GetEntityMetadata().EntityMetadata.FirstOrDefault();
						           metadataCache.ProfileAttributeMetadataCache = AttributeMetadataCache;
					           }

					           Dispatcher.Invoke(GenerateLists);
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

		private void GenerateLists()
		{
			if (AttributeMetadataCache[LogicalName] == null)
			{
				throw new Exception("Failed to load metadata related to this entity.");
			}

			// choose non-primary attributes, and ones that don't represent others (like names of OptionSetValues)
			// sort by IsSelected, and then by name -- bubbles the selected to the top
			var attributes = from attributeQ in AttributeMetadataCache[LogicalName].Attributes
			                 where (!attributeQ.IsPrimaryId.HasValue || !attributeQ.IsPrimaryId.Value)
			                       && string.IsNullOrEmpty(attributeQ.AttributeOf)
			                 orderby EntityDataFilter.Attributes == null
			                         || EntityDataFilter.Attributes.Contains(attributeQ.LogicalName) descending,
				                 attributeQ.LogicalName
			                 select attributeQ;

			// if no filter, select all
			FieldsSelectAll = EntityDataFilter.Attributes != null
			                  && EntityDataFilter.Attributes.Length == attributes.Count();

			ReadOnlySelectAll = EntityDataFilter.ReadOnly != null
			                    && EntityDataFilter.ReadOnly.Length == attributes.Count();

			ClearFlagSelectAll = EntityDataFilter.ClearFlag != null
			                     && EntityDataFilter.ClearFlag.Length == attributes.Count();

			foreach (var attribute in attributes)
			{
				var attributeAsync = attribute;

				Dispatcher.Invoke(() =>
				                  {
					                  Fields.Add(new FieldGridRow
					                             {
						                             IsSelected =
							                             EntityDataFilter.Attributes?.Contains(attributeAsync.LogicalName) == true,
						                             Name = attributeAsync.LogicalName,
						                             DisplayName =
							                             attributeAsync.DisplayName?.UserLocalizedLabel == null || !Settings.UseDisplayNames
								                             ? Naming.GetProperVariableName(attributeAsync, Settings.TitleCaseLogicalNames)
								                             : Naming.Clean(attributeAsync.DisplayName.UserLocalizedLabel.Label),
						                             Rename =
							                             EntityDataFilter.AttributeRenames?.FirstNotNullOrEmpty(attributeAsync.LogicalName),
						                             Language =
							                             EntityDataFilter.AttributeLanguages?.FirstNotNullOrEmpty(attributeAsync.LogicalName),
						                             IsReadOnly = EntityDataFilter.ReadOnly?.Contains(attributeAsync.LogicalName) == true
						                                          || (attributeAsync.IsValidForCreate != true
						                                              && attributeAsync.IsValidForUpdate != true),
						                             IsReadOnlyEnabled = attributeAsync.IsValidForCreate == true
						                                                 || attributeAsync.IsValidForUpdate == true,
						                             IsClearFlag =
							                             EntityDataFilter.ClearFlag?.Contains(attributeAsync.LogicalName) == true
					                             });
				                  });
			}

			var relations1N = from relation1nQ in AttributeMetadataCache[LogicalName].OneToManyRelationships
			                  orderby EntityDataFilter.OneToN == null
			                          || EntityDataFilter.OneToN.Contains(relation1nQ.SchemaName) descending,
				                  relation1nQ.ReferencingEntity, relation1nQ.ReferencingAttribute
			                  select relation1nQ;

			// if no filter, select all
			Relations1NSelectAll = EntityDataFilter.OneToN != null
			                       && EntityDataFilter.OneToN.Length == relations1N.Count();

			foreach (var relation1n in relations1N)
			{
				var relation1nAsync = relation1n;

				Dispatcher.Invoke(() =>
				                  {
					                  var row = new Relations1NGridRow
					                            {
						                            IsSelected = EntityDataFilter.OneToN == null
						                                         || EntityDataFilter.OneToN.Contains(relation1nAsync.SchemaName),
						                            Name = relation1nAsync.SchemaName,
						                            ToEntity = relation1nAsync.ReferencingEntity ?? "",
						                            ToField = relation1nAsync.ReferencingAttribute ?? "",
						                            Rename =
							                            EntityDataFilter.OneToNRenames?.FirstNotNullOrEmpty(relation1nAsync.SchemaName),
										  IsReadOnlyEnabled = true,
										  IsReadOnly = EntityDataFilter.OneToNReadOnly != null
																&& EntityDataFilter.OneToNReadOnly.ContainsKey(
																	relation1nAsync.SchemaName)
																&& EntityDataFilter.OneToNReadOnly[relation1nAsync.SchemaName]
									  };

					                  Relations1N.Add(row);
				                  });
			}

			var relationsN1 = from relationN1Q in AttributeMetadataCache[LogicalName].ManyToOneRelationships
			                  orderby EntityDataFilter.NToOne == null
			                          || EntityDataFilter.NToOne.Contains(relationN1Q.SchemaName) descending,
				                  relationN1Q.ReferencedEntity, relationN1Q.ReferencingAttribute
			                  select relationN1Q;

			// if no filter, select all
			RelationsN1SelectAll = EntityDataFilter.NToOne != null
			                       && EntityDataFilter.NToOne.Length == relationsN1.Count();

			foreach (var relationN1 in relationsN1)
			{
				var relationN1Async = relationN1;

				Dispatcher.Invoke(() =>
				                  {
					                  var row = new RelationsN1GridRow
					                            {
						                            IsSelected = EntityDataFilter.NToOne == null
						                                         || EntityDataFilter.NToOne.Contains(relationN1Async.SchemaName),
						                            Name = relationN1Async.SchemaName,
						                            ToEntity = relationN1Async.ReferencedEntity ?? "",
						                            FromField = relationN1Async.ReferencingAttribute ?? "",
						                            Rename = EntityDataFilter.NToOneRenames?
							                            .FirstNotNullOrEmpty(relationN1Async.SchemaName),
						                            IsFlatten = EntityDataFilter.NToOneFlatten != null
						                                        && EntityDataFilter.NToOneFlatten.ContainsKey(
							                                        relationN1Async.SchemaName)
						                                        && EntityDataFilter.NToOneFlatten[relationN1Async.SchemaName],
										  IsReadOnlyEnabled = true,
										  IsReadOnly = EntityDataFilter.NToOneReadOnly != null
						                                        && EntityDataFilter.NToOneReadOnly.ContainsKey(
							                                        relationN1Async.SchemaName)
						                                        && EntityDataFilter.NToOneReadOnly[relationN1Async.SchemaName]
					                            };

					                  RelationsN1.Add(row);
				                  });
			}

			var relationsNN = from relationNNQ in AttributeMetadataCache[LogicalName].ManyToManyRelationships
			                  orderby EntityDataFilter.NToN == null
			                          || EntityDataFilter.NToN.Contains(relationNNQ.SchemaName) descending,
				                  (relationNNQ.Entity1LogicalName == LogicalName)
					                  ? relationNNQ.Entity2LogicalName
					                  : (relationNNQ.Entity1LogicalName ?? ""),
				                  relationNNQ.IntersectEntityName
			                  select relationNNQ;

			// if no filter, select all
			RelationsNNSelectAll = EntityDataFilter.NToN != null
			                       && EntityDataFilter.NToN.Length == relationsNN.Count();

			foreach (var relationNN in relationsNN)
			{
				var relationNNAsync = relationNN;

				Dispatcher.Invoke(() =>
				                  {
					                  var row = new RelationsNNGridRow
					                            {
						                            IsSelected = EntityDataFilter.NToN == null
						                                         || EntityDataFilter.NToN.Contains(relationNNAsync.SchemaName),
						                            Name = relationNNAsync.SchemaName,
						                            ToEntity = relationNNAsync.Entity1LogicalName == LogicalName
							                                       ? relationNNAsync.Entity2LogicalName
							                                       : (relationNNAsync.Entity1LogicalName ?? ""),
						                            IntersectEntity = relationNNAsync.IntersectEntityName ?? "",
						                            Rename = EntityDataFilter.NToNRenames?.FirstNotNullOrEmpty(relationNNAsync.SchemaName),
										  IsReadOnlyEnabled = true,
										  IsReadOnly = EntityDataFilter.NToNReadOnly != null
																&& EntityDataFilter.NToNReadOnly.ContainsKey(
																	relationNNAsync.SchemaName)
																&& EntityDataFilter.NToNReadOnly[relationNNAsync.SchemaName]
									  };

					                  RelationsNN.Add(row);
				                  });
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
			if (EntityDataFilter == null)
			{
				return;
			}

			//EntityDataFilter.EnglishLabelField = TextBoxEnglishLabelField.Text;

			EntityDataFilter.Attributes = Fields.Where(field => field.IsSelected).Select(field => field.Name).ToArray();
			EntityDataFilter.AttributeRenames = Fields.Where(field => !string.IsNullOrWhiteSpace(field.Rename))
				.ToDictionary(field => field.Name, field => field.Rename);
			EntityDataFilter.AttributeLanguages = Fields.Where(field => !string.IsNullOrWhiteSpace(field.Language))
				.ToDictionary(field => field.Name, field => field.Language);
			EntityDataFilter.ReadOnly = Fields.Where(field => field.IsReadOnly).Select(field => field.Name).ToArray();
			EntityDataFilter.ClearFlag = Fields.Where(field => field.IsClearFlag).Select(field => field.Name).ToArray();

			EntityDataFilter.OneToN =
				Relations1N.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityDataFilter.OneToNRenames = Relations1N.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityDataFilter.OneToNReadOnly = Relations1N.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			EntityDataFilter.NToOne =
				RelationsN1.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityDataFilter.NToOneRenames = RelationsN1.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityDataFilter.NToOneFlatten = RelationsN1.ToDictionary(relation => relation.Name, relation => relation.IsFlatten);
			EntityDataFilter.NToOneReadOnly = RelationsN1.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			EntityDataFilter.NToN =
				RelationsNN.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityDataFilter.NToNRenames = RelationsNN.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityDataFilter.NToNReadOnly = RelationsNN.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			var toSelect = Relations1N.Where(relation => relation.IsSelected).Select(relation => relation.ToEntity)
				.Union(RelationsN1.Where(relation => relation.IsSelected).Select(relation => relation.ToEntity)
					.Union(RelationsNN.Where(relation => relation.IsSelected)
						.SelectMany(relation => new[] {relation.ToEntity, relation.IntersectEntity})))
				.Distinct();

			foreach (var entityRow in Entities.Where(entity => toSelect.Contains(entity.Name) && !entity.IsSelected))
			{
				entityRow.IsSelected = true;
			}
		}

		#region CRM

		private RetrieveMetadataChangesResponse GetEntityMetadata()
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(
				new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals,
					LogicalName));

			var entityProperties = new MetadataPropertiesExpression
			                       {
				                       AllProperties = false
			                       };
			entityProperties.PropertyNames.AddRange("DisplayName", "Attributes", "OneToManyRelationships"
				, "ManyToOneRelationships", "ManyToManyRelationships");

			var attributeProperties = new MetadataPropertiesExpression
			                          {
				                          AllProperties = false
			                          };
			attributeProperties.PropertyNames
				.AddRange("IsPrimaryId", "LogicalName", "SchemaName", "DisplayName", "AttributeOf", "IsValidForCreate", "IsValidForUpdate");

			var relationshipProperties = new MetadataPropertiesExpression
			                             {
				                             AllProperties = false
			                             };
			relationshipProperties.PropertyNames.AddRange("ReferencedAttribute", "ReferencedEntity",
				"ReferencingEntity", "ReferencingAttribute", "SchemaName",
				"Entity1LogicalName", "Entity2LogicalName", "IntersectEntityName");

			var entityQueryExpression = new EntityQueryExpression
			                            {
				                            Criteria = entityFilter,
				                            Properties = entityProperties,
				                            AttributeQuery = new AttributeQueryExpression
				                                             {
					                                             Properties = attributeProperties
				                                             },
				                            RelationshipQuery = new RelationshipQueryExpression
				                                                {
					                                                Properties = relationshipProperties
				                                                }
			                            };

			var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
			                                     {
				                                     Query = entityQueryExpression,
			                                     };

			using (var service = ConnectionHelper.GetConnection(Settings))
			{
				return (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
			}
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

			if (cell != null && !cell.IsEditing)
			{
				// enables editing on single click
				if (!cell.IsFocused)
				{
					cell.Focus();
				}

				//if (!cell.IsSelected)
				//{
				//	cell.IsSelected = true;
				//}
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
				}

				// select current row
				if (!row.IsSelected)
				{
					row.IsSelected = true;
				}

				return;
			}

			var d = (DependencyObject) e.OriginalSource;

			if (d != null && (IsCheckboxClickedParentCheck(d, "Flatten") || IsCheckboxClickedChildrenCheck(d, "Flatten")))
			{
				// clicked on flatten
				var rowData = (RelationsN1GridRow) row.Item;
				rowData.IsFlatten = !rowData.IsFlatten;

				// selectAll value to false
				var flattenAllField = GetType().GetField(gridName + "FlattenAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				flattenAllField?.SetValue(this, false);

				OnPropertyChanged(gridName + "FlattenAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "ReadOnly")
			                       || IsCheckboxClickedChildrenCheck(d, "ReadOnly")))
			{
				// clicked on meta
				var rowDataCast = (GridRow) row.Item;

				if (rowDataCast.IsReadOnlyEnabled)
				{
					rowDataCast.IsReadOnly = !rowDataCast.IsReadOnly;
				}

				// selectAll value to false
				var field = GetType().GetField("ReadOnlySelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("ReadOnlySelectAll");
			}
			else if (d != null && (IsCheckboxClickedParentCheck(d, "ClearFlag")
			                       || IsCheckboxClickedChildrenCheck(d, "ClearFlag")))
			{
				// clicked on meta
				var rowDataCast = (FieldGridRow) row.Item;

				if (rowDataCast.IsClearFlagEnabled)
				{
					rowDataCast.IsClearFlag = !rowDataCast.IsClearFlag;
				}

				// selectAll value to false
				var field = GetType().GetField("ClearFlagSelectAll",
					BindingFlags.NonPublic | BindingFlags.IgnoreCase | BindingFlags.Instance);

				field?.SetValue(this, false);

				OnPropertyChanged("ClearFlagSelectAll");
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

		#region Details bottom bar stuff

		private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
		{
			if (!Fields.Any() && !Relations1N.Any() && !RelationsN1.Any() && !RelationsNN.Any())
			{
				return;
			}

			FieldsSelectAll = true;
			Relations1NSelectAll = true;
			RelationsN1SelectAll = true;
			RelationsNNSelectAll = true;
		}

		private void ButtonDeselectAll_Click(object sender, RoutedEventArgs e)
		{
			if (!Fields.Any() && !Relations1N.Any() && !RelationsN1.Any() && !RelationsNN.Any())
			{
				return;
			}

			FieldsSelectAll = false;
			Relations1NSelectAll = false;
			RelationsN1SelectAll = false;
			RelationsNNSelectAll = false;
		}

		private void ButtonClearNames_Click(object sender, RoutedEventArgs e)
		{
			foreach (var field in Fields.Where(field => !string.IsNullOrEmpty(field.Rename)))
			{
				field.Rename = "";
			}

			foreach (var relation in Relations1N.Where(relation => !string.IsNullOrEmpty(relation.Rename)))
			{
				relation.Rename = "";
			}

			foreach (var relation in RelationsN1.Where(relation => !string.IsNullOrEmpty(relation.Rename)))
			{
				relation.Rename = "";
			}

			foreach (var relation in RelationsNN.Where(relation => !string.IsNullOrEmpty(relation.Rename)))
			{
				relation.Rename = "";
			}
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

		#endregion

		#endregion
	}
}
