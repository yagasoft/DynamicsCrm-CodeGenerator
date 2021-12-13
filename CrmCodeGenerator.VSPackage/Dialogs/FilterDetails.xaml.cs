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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.ViewModels;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using Yagasoft.Libraries.EnhancedOrgService.Services.Enhanced;

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

		public EntityProfile EntityProfile { get; set; }

		public Settings Settings { get; set; }

		public IDictionary<string, EntityMetadata> AttributeMetadataCache;

		public bool StillOpen { get; private set; } = true;

		public string WindowTitle { get; set; }

		private bool fieldsSelectAll;

		public bool FieldsSelectAll
		{
			get => fieldsSelectAll;
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
			get => readOnlySelectAll;
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
			get => clearFlagSelectAll;
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
			get => relations1NSelectAll;
			set
			{
				relations1NSelectAll = value;
				Relations1N.ToList().ForEach(relation => relation.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool relations1NReadOnlyAll;

		public bool Relations1NReadOnlyAll
		{
			get => relations1NReadOnlyAll;
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
			get => relationsN1SelectAll;
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
			get => relationsN1FlattenAll;
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
			get => relationsN1ReadOnlyAll;
			set
			{
				relationsN1ReadOnlyAll = value;
				RelationsN1.ToList().ForEach(relation => relation.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		private bool relationsNnSelectAll;

		public bool RelationsNnSelectAll
		{
			get => relationsNnSelectAll;
			set
			{
				relationsNnSelectAll = value;
				RelationsNn.ToList().ForEach(relation => relation.IsSelected = value);
				OnPropertyChanged();
			}
		}

		private bool relationsNnReadOnlyAll;

		public bool RelationsNnReadOnlyAll
		{
			get => relationsNnReadOnlyAll;
			set
			{
				relationsNnReadOnlyAll = value;
				RelationsNn.ToList().ForEach(relation => relation.IsReadOnly = value);
				OnPropertyChanged();
			}
		}

		public ObservableCollection<GridRow> Entities { get; set; }
		public ObservableCollection<EntityFilterGridRow> Fields { get; set; }
		public ObservableCollection<Relations1NGridRow> Relations1N { get; set; }
		public ObservableCollection<RelationsN1GridRow> RelationsN1 { get; set; }
		public ObservableCollection<RelationsNnGridRow> RelationsNn { get; set; }

		public bool IsEnglishLabelEnabled
		{
			get => isEnglishLabelEnabled;
			set
			{
				isEnglishLabelEnabled = value;
				OnPropertyChanged();
			}
		}

		public bool isEnglishLabelEnabled;

		public bool IsCrmEntities;

		#endregion

		private readonly MetadataCache metadataCache;
		private readonly IConnectionManager connectionManager;
		private EntityMetadata metadata;

		private readonly ConcurrentBag<EntityFilterGridRow> rowListAttrSource = new ConcurrentBag<EntityFilterGridRow>();
		private readonly ConcurrentBag<Relations1NGridRow> rowList1NSource = new ConcurrentBag<Relations1NGridRow>();
		private readonly ConcurrentBag<RelationsN1GridRow> rowListN1Source = new ConcurrentBag<RelationsN1GridRow>();

		private List<string> attributesFilter;
		private List<string> oneToNFilter;
		private List<string> nToOneFilter;

		#region Property events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

		#region Init

		public FilterDetails(Window parentWindow, string logicalName, Settings settings,
			EntityProfile entityProfile, ObservableCollection<GridRow> entities,
			IConnectionManager connectionManager, MetadataCache metadataCache,
			bool isCrmEntities = false)
		{
			InitializeComponent();

			Owner = parentWindow;

			LogicalName = logicalName;
			WindowTitle = $"\"{LogicalName}\" Profiling";

			IsEnglishLabelEnabled = true;
			this.connectionManager = connectionManager;
			IsCrmEntities = isCrmEntities;

			EntityProfile = entityProfile;

			Entities = entities;
			Fields = new ObservableCollection<EntityFilterGridRow>();
			Relations1N = new ObservableCollection<Relations1NGridRow>();
			RelationsN1 = new ObservableCollection<RelationsN1GridRow>();
			RelationsNn = new ObservableCollection<RelationsNnGridRow>();

			Settings = settings;
			this.metadataCache = metadataCache;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Initialising ...");

						Dispatcher.Invoke(
							() =>
							{
								DataContext = this;
								CheckBoxFieldsSelectAll.DataContext = this;
								CheckBoxReadOnlySelectAll.DataContext = this;
								CheckBoxClearFlagSelectAll.DataContext = this;
								CheckBoxRelations1NSelectAll.DataContext = this;
								CheckBoxRelationsN1SelectAll.DataContext = this;
								CheckBoxNToOneFlattenAll.DataContext = this;
								CheckBoxRelationsNnSelectAll.DataContext = this;

								//TextBoxEnglishLabelField.DataContext = EntityProfile;

								FieldsGrid.ItemsSource = Fields;
								FieldsGrid.Columns[4].Visibility = IsEnglishLabelEnabled
									? Visibility.Visible
									: Visibility.Hidden;
								Relations1NGrid.ItemsSource = Relations1N;
								RelationsN1Grid.ItemsSource = RelationsN1;
								RelationsNnGrid.ItemsSource = RelationsNn;
							});

						Initialise();

						Status.HideBusy(Dispatcher, BusyIndicator);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
						Dispatcher.Invoke(Close);
					}
				}).Start();
		}

		private void Initialise()
		{
			if (EntityProfile == null)
			{
				throw new Exception("Entity Profile not provided to this window.");
			}

			//Dispatcher.Invoke(() => TextBoxEnglishLabelField.DataContext = EntityProfile);

			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Building lists ...");

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

						AttributeMetadataCache.TryGetValue(LogicalName, out metadata);

						Dispatcher.Invoke(() => GenerateLists());
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

		private void GenerateLists(string controlName = null)
		{
			if (metadata == null)
			{
				throw new Exception("Failed to load metadata related to this entity.");
			}

			if (controlName == null || controlName == "Attributes")
			{
				Dispatcher.Invoke(Fields.Clear);

				// choose non-primary attributes, and ones that don't represent others (like names of OptionSetValues)
				// sort by IsSelected, and then by name -- bubbles the selected to the top
				var attributes =
					(from attributeQ in metadata.Attributes
					 where (attributeQ.IsPrimaryId != true) && (attributeQ.AttributeOf == null || attributeQ is ImageAttributeMetadata)
						 && (attributesFilter == null || attributesFilter.Contains(attributeQ.LogicalName))
					 orderby EntityProfile.Attributes == null || EntityProfile.Attributes.Contains(attributeQ.LogicalName) descending,
						 attributeQ.LogicalName
					 select attributeQ).ToArray();

				// if no filter, select all
				FieldsSelectAll = EntityProfile.Attributes != null && EntityProfile.Attributes.Length == attributes.Length;
				ReadOnlySelectAll = EntityProfile.ReadOnly != null && EntityProfile.ReadOnly.Length == attributes.Length;
				ClearFlagSelectAll = EntityProfile.ClearFlag != null && EntityProfile.ClearFlag.Length == attributes.Length;

				foreach (var attribute in attributes)
				{
					var attributeAsync = attribute;

					Dispatcher.Invoke(
						() =>
						{
							var row = rowListAttrSource.FirstOrDefault(r => r.Name == attributeAsync.LogicalName)
								?? new EntityFilterGridRow
								   {
									   IsSelected = EntityProfile.Attributes?.Contains(attributeAsync.LogicalName) == true,
									   Name = attributeAsync.LogicalName,
									   DisplayName = attributeAsync.DisplayName?.UserLocalizedLabel == null 
										   || Settings.UseDisplayNames != true
										   ? Naming.GetProperVariableName(attributeAsync, Settings.TitleCaseLogicalNames)
										   : Naming.Clean(attributeAsync.DisplayName.UserLocalizedLabel.Label),
									   Rename = EntityProfile.AttributeRenames?.FirstNotNullOrEmpty(attributeAsync.LogicalName),
									   Language = EntityProfile.AttributeLanguages?.FirstNotNullOrEmpty(attributeAsync.LogicalName),
									   Annotations = EntityProfile.AttributeAnnotations?.FirstNotNullOrEmpty(attributeAsync.LogicalName),
									   IsReadOnly = EntityProfile.ReadOnly?.Contains(attributeAsync.LogicalName) == true
										   || (attributeAsync.IsValidForCreate != true && attributeAsync.IsValidForUpdate != true),
									   IsReadOnlyEnabled = attributeAsync.IsValidForCreate == true || attributeAsync.IsValidForUpdate == true,
									   IsClearFlag = EntityProfile.ClearFlag?.Contains(attributeAsync.LogicalName) == true
								   };

							Fields.Add(row);

							if (rowListAttrSource.All(r => r.Name != row.Name))
							{
								rowListAttrSource.Add(row);
							}
						});
				}
			}

			if (controlName == null || controlName == "1N")
			{
				Dispatcher.Invoke(Relations1N.Clear);

				var relations1N =
					(from relation1Nq in metadata.OneToManyRelationships
					 where oneToNFilter == null || oneToNFilter.Contains(relation1Nq.SchemaName)
					 orderby EntityProfile.OneToN == null || EntityProfile.OneToN.Contains(relation1Nq.SchemaName) descending,
						 relation1Nq.ReferencingEntity, relation1Nq.ReferencingAttribute
					 select relation1Nq).ToArray();

				// if no filter, select all
				Relations1NSelectAll = EntityProfile.OneToN != null && EntityProfile.OneToN.Length == relations1N.Length;

				foreach (var relation1N in relations1N)
				{
					var relation1NAsync = relation1N;

					Dispatcher.Invoke(
						() =>
						{
							var row = rowList1NSource.FirstOrDefault(r => r.Name == relation1NAsync.SchemaName)
								?? new Relations1NGridRow
								   {
									   IsSelected = EntityProfile.OneToN?.Contains(relation1NAsync.SchemaName) == true,
									   Name = relation1NAsync.SchemaName,
									   ToEntity = relation1NAsync.ReferencingEntity ?? "",
									   ToField = relation1NAsync.ReferencingAttribute ?? "",
									   Rename = EntityProfile.OneToNRenames?.FirstNotNullOrEmpty(relation1NAsync.SchemaName),
									   IsReadOnlyEnabled = true,
									   IsReadOnly = EntityProfile.OneToNReadOnly != null
										   && EntityProfile.OneToNReadOnly.ContainsKey(relation1NAsync.SchemaName)
										   && EntityProfile.OneToNReadOnly[relation1NAsync.SchemaName]
								   };

							Relations1N.Add(row);

							if (rowList1NSource.All(r => r.Name != row.Name))
							{
								rowList1NSource.Add(row);
							}
						});
				}
			}

			if (controlName == null || controlName == "N1")
			{
				Dispatcher.Invoke(RelationsN1.Clear);

				var relationsN1 =
					(from relationN1Q in metadata.ManyToOneRelationships
					 where nToOneFilter == null || nToOneFilter.Contains(relationN1Q.SchemaName)
					 orderby EntityProfile.NToOne == null || EntityProfile.NToOne.Contains(relationN1Q.SchemaName) descending,
						 relationN1Q.ReferencedEntity, relationN1Q.ReferencingAttribute
					 select relationN1Q).ToArray();

				// if no filter, select all
				RelationsN1SelectAll = EntityProfile.NToOne != null && EntityProfile.NToOne.Length == relationsN1.Length;

				foreach (var relationN1 in relationsN1)
				{
					var relationN1Async = relationN1;

					Dispatcher.Invoke(
						() =>
						{
							var row = rowListN1Source.FirstOrDefault(r => r.Name == relationN1Async.SchemaName)
								?? new RelationsN1GridRow
								   {
									   IsSelected = EntityProfile.NToOne?.Contains(relationN1Async.SchemaName) == true,
									   Name = relationN1Async.SchemaName,
									   ToEntity = relationN1Async.ReferencedEntity ?? "",
									   FromField = relationN1Async.ReferencingAttribute ?? "",
									   Rename = EntityProfile.NToOneRenames?.FirstNotNullOrEmpty(relationN1Async.SchemaName),
									   IsFlatten = EntityProfile.NToOneFlatten != null
										   && EntityProfile.NToOneFlatten.ContainsKey(relationN1Async.SchemaName)
										   && EntityProfile.NToOneFlatten[relationN1Async.SchemaName],
									   IsReadOnlyEnabled = true,
									   IsReadOnly = EntityProfile.NToOneReadOnly != null
										   && EntityProfile.NToOneReadOnly.ContainsKey(relationN1Async.SchemaName)
										   && EntityProfile.NToOneReadOnly[relationN1Async.SchemaName]
								   };

							RelationsN1.Add(row);

							if (rowListN1Source.All(r => r.Name != row.Name))
							{
								rowListN1Source.Add(row);
							}
						});
				}
			}

			if (controlName == null)
			{
				Dispatcher.Invoke(RelationsNn.Clear);

				var relationsNn =
					(from relationNnq in metadata.ManyToManyRelationships
					 orderby EntityProfile.NToN == null || EntityProfile.NToN.Contains(relationNnq.SchemaName) descending,
						 (relationNnq.Entity1LogicalName == LogicalName)
							 ? relationNnq.Entity2LogicalName
							 : (relationNnq.Entity1LogicalName ?? ""),
						 relationNnq.IntersectEntityName
					 select relationNnq).ToArray();

				// if no filter, select all
				RelationsNnSelectAll = EntityProfile.NToN != null && EntityProfile.NToN.Length == relationsNn.Length;

				foreach (var relationNn in relationsNn)
				{
					var relationNnAsync = relationNn;

					Dispatcher.Invoke(
						() =>
						{
							var row =
								new RelationsNnGridRow
								{
									IsSelected = EntityProfile.NToN?.Contains(relationNnAsync.SchemaName) == true,
									Name = relationNnAsync.SchemaName,
									ToEntity = relationNnAsync.Entity1LogicalName == LogicalName
										? relationNnAsync.Entity2LogicalName
										: (relationNnAsync.Entity1LogicalName ?? ""),
									IntersectEntity = relationNnAsync.IntersectEntityName ?? "",
									Rename = EntityProfile.NToNRenames?.FirstNotNullOrEmpty(relationNnAsync.SchemaName),
									IsReadOnlyEnabled = true,
									IsReadOnly = EntityProfile.NToNReadOnly != null
										&& EntityProfile.NToNReadOnly.ContainsKey(relationNnAsync.SchemaName)
										&& EntityProfile.NToNReadOnly[relationNnAsync.SchemaName]
								};

							RelationsNn.Add(row);
						});
				}
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
			if (EntityProfile == null)
			{
				return;
			}

			//EntityProfile.EnglishLabelField = TextBoxEnglishLabelField.Text;

			EntityProfile.Attributes = rowListAttrSource.Where(field => field.IsSelected).Select(field => field.Name).ToArray();
			EntityProfile.AttributeRenames = rowListAttrSource.Where(field => !string.IsNullOrWhiteSpace(field.Rename))
				.ToDictionary(field => field.Name, field => field.Rename);
			EntityProfile.AttributeLanguages = rowListAttrSource.Where(field => !string.IsNullOrWhiteSpace(field.Language))
				.ToDictionary(field => field.Name, field => field.Language);
			EntityProfile.AttributeAnnotations = rowListAttrSource.Where(field => !string.IsNullOrWhiteSpace(field.Annotations))
				.ToDictionary(field => field.Name, field => field.Annotations);
			EntityProfile.ReadOnly = rowListAttrSource.Where(field => field.IsReadOnly).Select(field => field.Name).ToArray();
			EntityProfile.ClearFlag = rowListAttrSource.Where(field => field.IsClearFlag).Select(field => field.Name).ToArray();

			EntityProfile.OneToN =
				rowList1NSource.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityProfile.OneToNRenames = rowList1NSource.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityProfile.OneToNReadOnly = rowList1NSource.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			EntityProfile.NToOne =
				rowListN1Source.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityProfile.NToOneRenames = rowListN1Source.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityProfile.NToOneFlatten = rowListN1Source.ToDictionary(relation => relation.Name, relation => relation.IsFlatten);
			EntityProfile.NToOneReadOnly = rowListN1Source.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			EntityProfile.NToN =
				RelationsNn.Where(relation => relation.IsSelected).Select(relation => relation.Name).ToArray();
			EntityProfile.NToNRenames = RelationsNn.Where(relation => !string.IsNullOrWhiteSpace(relation.Rename))
				.ToDictionary(relation => relation.Name, relation => relation.Rename);
			EntityProfile.NToNReadOnly = RelationsNn.ToDictionary(relation => relation.Name, relation => relation.IsReadOnly);

			var toSelect = Relations1N.Where(relation => relation.IsSelected).Select(relation => relation.ToEntity)
				.Union(RelationsN1.Where(relation => relation.IsSelected).Select(relation => relation.ToEntity)
					.Union(RelationsNn.Where(relation => relation.IsSelected)
						.SelectMany(relation => new[] { relation.ToEntity, relation.IntersectEntity })))
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
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.Equals, LogicalName));

			var entityProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			entityProperties.PropertyNames.AddRange("DisplayName", "Attributes", "OneToManyRelationships"
				, "ManyToOneRelationships", "ManyToManyRelationships");

			var attributeProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			attributeProperties.PropertyNames
				.AddRange("IsPrimaryId", "LogicalName", "SchemaName", "DisplayName", "AttributeOf",
					"IsValidForCreate", "IsValidForUpdate");

			var relationshipProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			relationshipProperties.PropertyNames.AddRange("ReferencedAttribute", "ReferencedEntity",
				"ReferencingEntity", "ReferencingAttribute", "SchemaName",
				"Entity1LogicalName", "Entity2LogicalName", "IntersectEntityName");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = entityProperties,
					AttributeQuery =
						new AttributeQueryExpression
						{
							Properties = attributeProperties
						},
					RelationshipQuery =
						new RelationshipQueryExpression
						{
							Properties = relationshipProperties
						}
				};

			var retrieveMetadataChangesRequest =
				new RetrieveMetadataChangesRequest
				{
					Query = entityQueryExpression,
				};

			return (RetrieveMetadataChangesResponse)connectionManager.Get().Execute(retrieveMetadataChangesRequest);
		}

		#endregion

		#region UI events

		#region Top bar stuff

		private void ButtonFilter_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;

			if (button == null)
			{
				return;
			}

			SelectByRegex(button.Name.Replace("ButtonFilter", ""));
		}

		private void ButtonFilterClear_Click(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;

			if (button == null)
			{
				return;
			}

			var name = button.Name.Replace("ButtonFilterClear", "");

			switch (name)
			{
				case "Attributes":
					TextBoxFilterAttributes.Text = string.Empty;
					break;
					
				case "1N":
					TextBoxFilter1N.Text = string.Empty;
					break;
					
				case "N1":
					TextBoxFilterN1.Text = string.Empty;
					break;
			}

			SelectByRegex(name);
		}

		private void TextBoxFilter_OnKeyDown(object sender, KeyEventArgs e)
		{
			var textBox = sender as TextBox;

			if (textBox == null)
			{
				return;
			}

			if (e.Key == Key.Enter)
			{
				SelectByRegex(textBox.Name.Replace("TextBoxFilter", ""));
			}
		}

		private void SelectByRegex(string controlName)
		{
			switch (controlName)
			{
				case "Attributes":
					attributesFilter = FilterSource(TextBoxFilterAttributes, rowListAttrSource);
					break;
					
				case "1N":
					oneToNFilter = FilterSource(TextBoxFilter1N, rowList1NSource);
					break;
					
				case "N1":
					nToOneFilter = FilterSource(TextBoxFilterN1, rowListN1Source);
					break;
			}

			// filter entities
			new Thread(
				() =>
				{
					try
					{
						Status.ShowBusy(Dispatcher, BusyIndicator, "Filtering ...");
						GenerateLists(controlName);
						Status.HideBusy(Dispatcher, BusyIndicator);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
						Dispatcher.Invoke(Close);
					}
				}).Start();
		}

		private List<string> FilterSource<T>(TextBox filterBox, ConcurrentBag<T> source) where T : EntityFilterGridRow
		{
			if (filterBox.Text.IsFilled())
			{
				var filters = filterBox.Text.ToLower()
					.Split(',').Select(t => t.Trim())
					.Where(t => t.IsFilled())
					.Distinct();

				return source
					.Where(e => filters.Any(f => Regex.IsMatch(e.Name, f)
						|| (e.DisplayName.IsFilled() && Regex.IsMatch(e.DisplayName.ToLower(), f))
						|| (e.Rename.IsFilled() && Regex.IsMatch(e.Rename.ToLower(), f))
						|| (e is Relations1NGridRow oneN && ((oneN.ToEntity.IsFilled() && Regex.IsMatch(oneN.ToEntity.ToLower(), f))
							|| (oneN.ToField.IsFilled() && Regex.IsMatch(oneN.ToField.ToLower(), f))))
						|| (e is RelationsN1GridRow nOne && ((nOne.ToEntity.IsFilled() && Regex.IsMatch(nOne.ToEntity.ToLower(), f))
							|| (nOne.FromField.IsFilled() && Regex.IsMatch(nOne.FromField.ToLower(), f))))))
					.Select(e => e.Name).Distinct().ToList();
			}

			return null;
		}

		#endregion

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

					var rowQ = (DataGridRow)container;

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

			var d = (DependencyObject)e.OriginalSource;

			if (d != null && (IsCheckboxClickedParentCheck(d, "Flatten") || IsCheckboxClickedChildrenCheck(d, "Flatten")))
			{
				// clicked on flatten
				var rowData = (RelationsN1GridRow)row.Item;
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
				var rowDataCast = (EntityFilterGridRow)row.Item;

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
				var rowDataCast = (EntityFilterGridRow)row.Item;

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
				var rowData = (GridRow)row.Item;
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
				var item = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(i);

				if (item != null && item.IsEditing)
				{
					return;
				}
			}

			switch (e.Key)
			{
				case Key.Space:
					var isFirstItemSelected = ((GridRow)grid.SelectedItem).IsSelected;

					foreach (var item in grid.SelectedItems.Cast<GridRow>()
						.Where(item => item.IsSelected == isFirstItemSelected))
					{
						item.IsSelected = !isFirstItemSelected;
					}

					break;

				case Key.Delete:
					foreach (var item in grid.SelectedItems.Cast<EntityFilterGridRow>()
						.Where(item => !string.IsNullOrEmpty(item.Rename)))
					{
						item.Rename = "";
					}

					break;
			}
		}

		private static DataGridCell GetCellClicked(MouseButtonEventArgs e)
		{
			var dep = (DependencyObject)e.OriginalSource;

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

			if (dep is CheckBox && ((CheckBox)dep).Name == name)
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
				if (dep is CheckBox && ((CheckBox)dep).Name == name)
				{
					return true;
				}

				dep = VisualTreeHelper.GetParent(dep);
			}

			return false;
		}

		private static bool IsCheckBoxCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] { typeof(CheckBox), typeof(DataGridCheckBoxColumn) };
			var d = (DependencyObject)e.OriginalSource;
			return d != null && (IsCellClickedParentCheck(d, types) || IsCellClickedChildrenCheck(d, types));
		}

		private static bool IsTextCellClicked(MouseButtonEventArgs e)
		{
			var types = new[] { typeof(TextBlock), typeof(TextBox), typeof(DataGridTextColumn), typeof(RichTextBox) };
			var d = (DependencyObject)e.OriginalSource;
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
			if (!Fields.Any() && !Relations1N.Any() && !RelationsN1.Any() && !RelationsNn.Any())
			{
				return;
			}

			FieldsSelectAll = true;
			Relations1NSelectAll = true;
			RelationsN1SelectAll = true;
			RelationsNnSelectAll = true;
		}

		private void ButtonDeselectAll_Click(object sender, RoutedEventArgs e)
		{
			if (!Fields.Any() && !Relations1N.Any() && !RelationsN1.Any() && !RelationsNn.Any())
			{
				return;
			}

			FieldsSelectAll = false;
			Relations1NSelectAll = false;
			RelationsN1SelectAll = false;
			RelationsNnSelectAll = false;
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

			foreach (var relation in RelationsNn.Where(relation => !string.IsNullOrEmpty(relation.Rename)))
			{
				relation.Rename = "";
			}
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
