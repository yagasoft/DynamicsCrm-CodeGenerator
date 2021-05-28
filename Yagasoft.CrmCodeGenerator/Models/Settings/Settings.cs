#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Settings
{
	public enum ClearModeEnum
	{
		Disabled,
		Empty,
		Convention,
		Flag
	}

	public class Settings : INotifyPropertyChanged
	{
		public Guid Id = Guid.NewGuid();

		public string AppId { get; set; }
		public string AppVersion { get; set; }
		public string SettingsVersion { get; set; }

		public string DetectedTemplateVersion { get; set; }
		public string DetectedMinAppVersion { get; set; }

		public string BaseFileName = "CrmSchema";

		public EntityProfilesHeaderSelector EntityProfilesHeaderSelector = new EntityProfilesHeaderSelector();
		public int EntitiesPerThread = 5;
		public ClearModeEnum ClearMode = ClearModeEnum.Disabled;

		[JsonIgnore]
		public string ConnectionString
		{
			get => connectionString;
			set
			{
				var clauses = value?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(e => e.Trim()).Where(e => e.Contains("=")).ToArray();

				if (clauses?.Any() == true)
				{
					var subclauses = clauses.Select(e => e.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())).ToArray();
					var longestKeyLength = subclauses.Select(e => e.FirstOrDefault()?.Length ?? 0).Max();
					clauses = subclauses?
						.Select(e => e.StringAggregate(" = ".PadLeft(longestKeyLength + 3 - e.FirstOrDefault()?.Length ?? 0)))
						.ToArray();
				}

				var formattedString = clauses.StringAggregate(";\r\n").Trim();

				SetField(ref connectionString, formattedString);
				OnPropertyChanged();
			}
		}

		public bool IsCleanSave
		{
			get => isCleanSave;
			set
			{
				SetField(ref isCleanSave, value);
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsRemoveUnselectedProfilesEnabled));
			}
		}

		public bool IsRemoveUnselectedProfiles
		{
			get => isRemoveUnselectedProfiles;
			set
			{
				SetField(ref isRemoveUnselectedProfiles, value);
				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		public bool IsRemoveUnselectedProfilesEnabled => IsCleanSave;

		public bool IncludeNonStandard
		{
			get => includeNonStandard;
			set => SetField(ref includeNonStandard, value);
		}

		public string[] SelectedGlobalActions { get; set; }

		public IDictionary<string, string[]> SelectedActions
		{
			get => selectedActions ?? (selectedActions = new ConcurrentDictionary<string, string[]>());
			set => selectedActions = value;
		}

		public bool SplitFiles
		{
			get => splitFiles;
			set
			{
				splitFiles = value;
				OnPropertyChanged();
			}
		}

		public bool SplitContractFiles
		{
			get => splitContractFiles;
			set
			{
				splitContractFiles = value;
				OnPropertyChanged();
			}
		}

		public bool? UseDisplayNames
		{
			get => useDisplayNames ?? true;
			set
			{
				useDisplayNames = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TitleCaseLogicalNamesEnabled));
			}
		}

		public bool IsUseCustomDictionary
		{
			get => isUseCustomDictionary;
			set
			{
				isUseCustomDictionary = value;
				OnPropertyChanged();
			}
		}

		public bool IsUseCustomEntityReference
		{
			get => isUseCustomEntityReference;
			set
			{
				isUseCustomEntityReference = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateAlternateKeys
		{
			get => isGenerateAlternateKeys;
			set
			{
				isGenerateAlternateKeys = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsUseCustomTypeForAltKeysEnabled));
			}
		}

		public bool IsUseCustomTypeForAltKeys
		{
			get => isUseCustomTypeForAltKeys;
			set
			{
				isUseCustomTypeForAltKeys = value;
				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		public bool IsUseCustomTypeForAltKeysEnabled => IsGenerateAlternateKeys;

		public bool IsMakeCrmEntitiesJsonFriendly
		{
			get => isMakeCrmEntitiesJsonFriendly;
			set
			{
				isMakeCrmEntitiesJsonFriendly = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateLoadPerRelation
		{
			get => isGenerateLoadPerRelation;
			set
			{
				isGenerateLoadPerRelation = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateEnumNames
		{
			get => isGenerateEnumNames;
			set
			{
				isGenerateEnumNames = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateEnumLabels
		{
			get => isGenerateEnumLabels;
			set
			{
				isGenerateEnumLabels = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateFieldSchemaNames
		{
			get => isGenerateFieldSchemaNames;
			set
			{
				isGenerateFieldSchemaNames = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateFieldLabels
		{
			get => isGenerateFieldLabels;
			set
			{
				isGenerateFieldLabels = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateRelationNames
		{
			get => isGenerateRelationNames;
			set
			{
				isGenerateRelationNames = value;
				OnPropertyChanged();
			}
		}

		public bool IsImplementINotifyProperty
		{
			get => isImplementINotifyProperty;
			set
			{
				isImplementINotifyProperty = value;
				OnPropertyChanged();
			}
		}

		public bool IsAddEntityAnnotations
		{
			get => isAddEntityAnnotations;
			set
			{
				isAddEntityAnnotations = value;
				OnPropertyChanged();
			}
		}

		public bool IsAddContractAnnotations
		{
			get => isAddContractAnnotations;
			set
			{
				isAddContractAnnotations = value;
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

		public string Template
		{
			get => template;
			set
			{
				SetField(ref template, value);
				NewTemplate = !File.Exists(Path.Combine(folder, template));
			}
		}

		public string Folder
		{
			get => folder;
			set => SetField(ref folder, value);
		}

		public bool NewTemplate
		{
			get => newTemplate;
			set => SetField(ref newTemplate, value);
		}

		public List<EntityProfile> CrmEntityProfiles = new List<EntityProfile>();

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> TemplateList
		{
			get => templateList = templateList ?? new ObservableCollection<string>();
			set => SetField(ref templateList, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EntityList
		{
			get => entityList = entityList ?? new ObservableCollection<string>();
			set => SetField(ref entityList, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EntitiesSelected
		{
			get => entitiesSelected = entitiesSelected ?? new ObservableCollection<string>();
			set => SetField(ref entitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> PluginMetadataEntitiesSelected
		{
			get => pluginMetadataEntitiesSelected = pluginMetadataEntitiesSelected ?? new ObservableCollection<string>();
			set => SetField(ref pluginMetadataEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> OptionsetLabelsEntitiesSelected
		{
			get => optionsetLabelsEntitiesSelected = optionsetLabelsEntitiesSelected ?? new ObservableCollection<string>();
			set => SetField(ref optionsetLabelsEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> LookupLabelsEntitiesSelected
		{
			get => lookupLabelsEntitiesSelected = lookupLabelsEntitiesSelected ?? new ObservableCollection<string>();
			set => SetField(ref lookupLabelsEntitiesSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EarlyBoundFilteredSelected
		{
			get => earlyBoundFilteredSelected = earlyBoundFilteredSelected ?? new ObservableCollection<string>();
			set => SetField(ref earlyBoundFilteredSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> EarlyBoundLinkedSelected
		{
			get => earlyBoundLinkedSelected = earlyBoundLinkedSelected ?? new ObservableCollection<string>();
			set => SetField(ref earlyBoundLinkedSelected, value);
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<string> JsEarlyBoundEntitiesSelected
		{
			get => jsEarlyBoundEntitiesSelected = jsEarlyBoundEntitiesSelected ?? new ObservableCollection<string>();
			set => SetField(ref jsEarlyBoundEntitiesSelected, value);
		}

		public string Namespace { get; set; }

		[JsonIgnore]
		public bool TitleCaseLogicalNamesEnabled => UseDisplayNames != true;

		public int Threads
		{
			get => threads;
			set
			{
				threads = value;
				OnPropertyChanged();
			}
		}

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
			OnPropertyChanged(propertyName);

			return true;
		}

		#endregion

		private string connectionString;
		private int threads = 2;
		private bool isCleanSave;
		private bool isRemoveUnselectedProfiles;
		private ObservableCollection<string> entitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> pluginMetadataEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> jsEarlyBoundEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> earlyBoundFilteredSelected = new ObservableCollection<string>();
		private ObservableCollection<string> earlyBoundLinkedSelected = new ObservableCollection<string>();
		private ObservableCollection<string> optionsetLabelsEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> lookupLabelsEntitiesSelected = new ObservableCollection<string>();
		private ObservableCollection<string> entityList = new ObservableCollection<string>();
		private IDictionary<string, string[]> selectedActions;
		private bool includeNonStandard;
		private bool splitFiles;
		private bool splitContractFiles;
		private bool? useDisplayNames = true;
		private bool isUseCustomDictionary;
		private bool isUseCustomEntityReference;
		private bool isGenerateAlternateKeys;
		private bool isUseCustomTypeForAltKeys;
		private bool isMakeCrmEntitiesJsonFriendly;
		private bool isGenerateLoadPerRelation;
		private bool isGenerateEnumNames;
		private bool isGenerateEnumLabels;
		private bool isGenerateFieldSchemaNames;
		private bool isGenerateFieldLabels;
		private bool isGenerateRelationNames;
		private bool isImplementINotifyProperty;
		private bool isAddEntityAnnotations;
		private bool isAddContractAnnotations;
		private bool generateGlobalActions;
		private bool lockNamesOnGenerate;
		private bool titleCaseLogicalNames;

		private string template = "";
		private string folder = "";
		private bool newTemplate;
		private ObservableCollection<string> templateList = new ObservableCollection<string>();
	}
}
