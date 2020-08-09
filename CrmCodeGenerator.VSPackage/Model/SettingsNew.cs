#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.ServiceModel.Description;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Metadata;
using Newtonsoft.Json;
using Yagasoft.Libraries.Common;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	public enum ClearModeEnum
	{
		Disabled,
		Empty,
		Convention,
		Flag
	}

	[Serializable]
	public class EntityDataFilter : INotifyPropertyChanged
	{
		private string entityRename;
		private bool isExcluded = true;
		private bool isGenerateMeta;
		private bool isOptionsetLabels;
		private bool isLookupLabels;
		private ClearModeEnum? valueClearMode;
		private string englishLabelField;

		public string LogicalName { get; set; }

		public string EntityRename
		{
			get => entityRename;
			set
			{
				entityRename = string.IsNullOrEmpty(value) ? null : value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateMeta
		{
			get => isGenerateMeta;
			set
			{
				isGenerateMeta = value;
				OnPropertyChanged();
			}
		}

		public bool IsOptionsetLabels
		{
			get => isOptionsetLabels;
			set
			{
				isOptionsetLabels = value;
				OnPropertyChanged();
			}
		}

		public bool IsLookupLabels
		{
			get => isLookupLabels;
			set
			{
				isLookupLabels = value;
				OnPropertyChanged();
			}
		}

		public ClearModeEnum? ValueClearMode
		{
			get => valueClearMode;
			set
			{
				valueClearMode = value;
				OnPropertyChanged();
			}
		}

		public bool IsExcluded
		{
			get => isExcluded;
			set
			{
				isExcluded = value;
				OnPropertyChanged();
			}
		}

		public string EnglishLabelField
		{
			get => englishLabelField;
			set
			{
				englishLabelField = value;
				OnPropertyChanged();
			}
		}

		public bool IsFiltered => !string.IsNullOrEmpty(EntityRename)
			|| Attributes.Length > 0 || OneToN.Length > 0 || NToOne.Length > 0 || NToN.Length > 0;

		public string[] Attributes { get; set; } = new string[0];
		public IDictionary<string, string> AttributeRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, string> AttributeLanguages { get; set; } = new Dictionary<string, string>();
		public string[] ReadOnly { get; set; } = new string[0];
		public string[] ClearFlag { get; set; } = new string[0];

		public string[] OneToN { get; set; } = new string[0];
		public IDictionary<string, string> OneToNRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> OneToNReadOnly { get; set; } = new Dictionary<string, bool>();

		public string[] NToOne { get; set; } = new string[0];
		public IDictionary<string, string> NToOneRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> NToOneFlatten { get; set; } = new Dictionary<string, bool>();
		public IDictionary<string, bool> NToOneReadOnly { get; set; } = new Dictionary<string, bool>();

		public string[] NToN { get; set; } = new string[0];
		public IDictionary<string, string> NToNRenames { get; set; } = new Dictionary<string, string>();
		public IDictionary<string, bool> NToNReadOnly { get; set; } = new Dictionary<string, bool>();

		public EntityDataFilter(string logicalName)
		{
			LogicalName = logicalName;
		}

		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class SettingsNew : INotifyPropertyChanged
	{
		public Guid Id = Guid.NewGuid();

		public EntityFilterArray EntityDataFilterArray = new EntityFilterArray();
		public int Threads = 2;
		public int EntitiesPerThread = 5;
		public ClearModeEnum ClearMode = ClearModeEnum.Disabled;

		private string connectionString;
		private bool isCleanSave;
		private string _EntitiesToIncludeString;
		private ObservableCollection<string> _EntitiesSelected = new ObservableCollection<string>();
		private bool isSortByDisplayName;
		private ObservableCollection<string> pluginMetadataEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> jsEarlyBoundEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> actionEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> optionsetLabelsEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> lookupLabelsEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> _EntityList = new ObservableCollection<string>();
		private bool _IncludeNonStandard;
		private string _EntitiesString;
		private string _SelectPrefixes = "";
		private bool _SplitFiles;
		private bool _UseDisplayNames = true;
		private bool _IsUseCustomDictionary;
		private bool _IsGenerateLoadPerRelation;
		private bool generateLookupLabelsInEntity;
		private bool generateOptionSetLabelsInEntity;
		private bool generateLookupLabelsInContract;
		private bool generateOptionSetLabelsInContract;
		private bool generateGlobalActions;
		private bool lockNamesOnGenerate;
		private bool titleCaseLogicalNames;

		public string ConnectionString
		{
			get => connectionString;
			set
			{
				var clauses = value?.Split(';').Select(e => e.Trim()).Where(e => e.Contains("=")).ToArray();
				var subclauses = clauses?.Select(e => e.Split('=').Select(s => s.Trim())).ToArray();
				var longestKeyLength = subclauses?.Select(e => e.FirstOrDefault()?.Length ?? 0).Max() ?? 0;
				clauses = subclauses?
					.Select(e => e.StringAggregate(" = ".PadLeft(longestKeyLength + 3 - e.FirstOrDefault()?.Length ?? 0)))
					.ToArray();
				var formattedString = clauses.StringAggregate(";\r\n").Trim() + ";";

				SetField(ref connectionString, formattedString);
				OnPropertyChanged("ConnectionString");
			}
		}

		public bool IsCleanSave
		{
			get => isCleanSave;
			set
			{
				SetField(ref isCleanSave, value);
				OnPropertyChanged("IsCleanSave");
			}
		}

		public string EntitiesToIncludeString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in _EntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();
				var split = value.Split(',').Select(p => p.Trim()).ToList();
				foreach (var s in split)
				{
					newList.Add(s);
					if (!_EntityList.Contains(s))
					{
						_EntityList.Add(s);
					}
				}
				EntitiesSelected = newList;
				SetField(ref _EntitiesToIncludeString, value);
				OnPropertyChanged("EnableExclude");
			}
		}

		public string EntitiesToSelectString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in _EntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				EntitiesSelected = newList;
				OnPropertyChanged("EnableExclude");
			}
		}

		public bool IsSortByDisplayName
		{
			get => isSortByDisplayName;
			set
			{
				isSortByDisplayName = value;
				OnPropertyChanged();
			}
		}

		public string PluginMetadataEntitiesSelectedString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in pluginMetadataEntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				PluginMetadataEntitiesSelected = newList;
				OnPropertyChanged("PluginMetadataEntitiesSelected");
			}
		}

		public string OptionsetLabelsEntitiesSelectedString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in optionsetLabelsEntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				OptionsetLabelsEntitiesSelected = newList;
				OnPropertyChanged("OptionsetLabelsEntitiesSelected");
			}
		}

		public string LookupLabelsEntitiesSelectedString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in lookupLabelsEntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				LookupLabelsEntitiesSelected = newList;
				OnPropertyChanged("LookupLabelsEntitiesSelected");
			}
		}

		public string JsEarlyBoundEntitiesSelectedString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in jsEarlyBoundEntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				jsEarlyBoundEntitiesSelected = newList;
				OnPropertyChanged("JsEarlyBoundEntitiesSelected");
			}
		}

		public string ActionEntitiesSelectedString
		{
			get
			{
				var sb = new StringBuilder();
				foreach (var value in actionEntitiesSelected)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}
				return sb.ToString();
			}
			set
			{
				var newList = new ObservableCollection<string>();

				if (!string.IsNullOrEmpty(value))
				{
					var split = value.Split(',').Select(p => p.Trim()).ToList();
					foreach (var s in split)
					{
						newList.Add(s);
					}
				}

				actionEntitiesSelected = newList;
				OnPropertyChanged("ActionEntitiesSelected");
			}
		}

		public bool IncludeNonStandard
		{
			get => _IncludeNonStandard;
			set => SetField(ref _IncludeNonStandard, value);
		}

		public string EntitiesString
		{
			get
			{
				var sb = new StringBuilder();

				foreach (var value in _EntityList)
				{
					if (sb.Length != 0)
					{
						sb.Append(',');
					}
					sb.Append(value);
				}

				_EntitiesString = sb.ToString();

				return _EntitiesString;
			}
			set
			{
				var split = value.Split(',').Select(p => p.Trim()).ToList();

				foreach (var s in split.Where(s => !_EntityList.Contains(s)))
				{
					_EntityList.Add(s);
				}

				_EntitiesString = value;
			}
		}

		public string SelectPrefixes
		{
			get => _SelectPrefixes;
			set => _SelectPrefixes = value;
		}

		public bool SplitFiles
		{
			get => _SplitFiles;
			set
			{
				_SplitFiles = value;
				OnPropertyChanged();
			}
		}

		public bool UseDisplayNames
		{
			get => _UseDisplayNames;
			set
			{
				_UseDisplayNames = value;
				OnPropertyChanged();
				OnPropertyChanged("TitleCaseLogicalNamesEnabled");
			}
		}

		public bool IsUseCustomDictionary
		{
			get => _IsUseCustomDictionary;
			set
			{
				_IsUseCustomDictionary = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateLoadPerRelation
		{
			get => _IsGenerateLoadPerRelation;
			set
			{
				_IsGenerateLoadPerRelation = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateOptionSetLabelsInEntity
		{
			get => generateOptionSetLabelsInEntity;
			set
			{
				generateOptionSetLabelsInEntity = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateLookupLabelsInEntity
		{
			get => generateLookupLabelsInEntity;
			set
			{
				generateLookupLabelsInEntity = value;
				GenerateLookupLabelsInContract = GenerateLookupLabelsInContract && value;
				OnPropertyChanged();
			}
		}

		public bool GenerateOptionSetLabelsInContract
		{
			get => generateOptionSetLabelsInContract;
			set
			{
				generateOptionSetLabelsInContract = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateLookupLabelsInContract
		{
			get => generateLookupLabelsInContract;
			set
			{
				generateLookupLabelsInContract = value && GenerateLookupLabelsInEntity;
				OnPropertyChanged();
			}
		}

		public bool GenerateGlobalActions
		{
			get => generateGlobalActions;
			set
			{
				generateGlobalActions = value;
				OnPropertyChanged();
			}
		}

		public bool LockNamesOnGenerate
		{
			get => lockNamesOnGenerate;
			set
			{
				lockNamesOnGenerate = value;
				OnPropertyChanged();
			}
		}

		public bool TitleCaseLogicalNames
		{
			get => titleCaseLogicalNames;
			set
			{
				titleCaseLogicalNames = value;
				OnPropertyChanged();
			}
		}

		public ClearModeEnum SelectedClearMode
		{
			get => ClearMode;
			set
			{
				ClearMode = value;
				OnPropertyChanged();
			}
		}

		 private string _Template = "";
		 private string _Folder = "";
		 private bool _NewTemplate;
		 private ObservableCollection<string> _TemplateList = new ObservableCollection<string>();

		public string Template
		{
			get => _Template;
			set
			{
				SetField(ref _Template, value);
				NewTemplate = !File.Exists(Path.Combine(_Folder, _Template));
			}
		}

		public string Folder
		{
			get => _Folder;
			set => SetField(ref _Folder, value);
		}

		public bool NewTemplate
		{
			get => _NewTemplate;
			set => SetField(ref _NewTemplate, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> TemplateList
		{
			get => _TemplateList;
			set => SetField(ref _TemplateList, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EntityList
		{
			get => _EntityList;
			set => SetField(ref _EntityList, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EntitiesSelected
		{
			get => _EntitiesSelected;
			set => SetField(ref _EntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> PluginMetadataEntitiesSelected
		{
			get => pluginMetadataEntitiesSelected;
			set => SetField(ref pluginMetadataEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> OptionsetLabelsEntitiesSelected
		{
			get => optionsetLabelsEntitiesSelected;
			set => SetField(ref optionsetLabelsEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> LookupLabelsEntitiesSelected
		{
			get => lookupLabelsEntitiesSelected;
			set => SetField(ref lookupLabelsEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> JsEarlyBoundEntitiesSelected
		{
			get => jsEarlyBoundEntitiesSelected;
			set => SetField(ref jsEarlyBoundEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> ActionEntitiesSelected
		{
			get => actionEntitiesSelected;
			set => SetField(ref actionEntitiesSelected, value);
		}

		public IOrganizationService CrmConnection { get; set; }

		public bool Dirty { get; set; }

		public bool TitleCaseLogicalNamesEnabled => !UseDisplayNames;

		public IEnumerable<string> FilteredEntities =>
			EntityDataFilterArray.EntityFilters
				.SelectMany(filter => filter.EntityFilterList)
				.Where(filterList => !filterList.IsExcluded || filterList.IsGenerateMeta)
				.Select(filterList => filterList.LogicalName).Distinct();

		#region boiler-plate INotifyPropertyChanged
		
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return false;
			}
			field = value;
			Dirty = true;
			OnPropertyChanged(propertyName);
			return true;
		}

		#endregion

		public void FiltersChanged()
		{
			OnPropertyChanged("FilteredEntities");
		}
	}
}
