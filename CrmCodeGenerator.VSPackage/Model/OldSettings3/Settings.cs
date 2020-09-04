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
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Metadata;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Obsolete("Old Settings class used only for migration.", false)]
	public enum ClearModeEnum
	{
		Disabled,
		Empty,
		Convention,
		Flag
	}

	[Obsolete("Old Settings class used only for migration.", false)]
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

	[Obsolete("Old Settings class used only for migration.", false)]
	[Serializable]
	public class Settings : INotifyPropertyChanged, IDeserializationCallback
	{
		#region Properties

		public IDictionary<Guid, MappingEntity> EntityMetadataCache;

		public IDictionary<string, LookupMetadata> LookupEntitiesMetadataCache
		{
			get
			{
				return new Dictionary<string, LookupMetadata>();
			}
			set
			{
			}
		}

		public byte[] LookupEntitiesMetadataCacheSerialised;

		// credit: http://stackoverflow.com/a/12845153/1919456
		public List<EntityMetadata> ProfileEntityMetadataCache
		{
			get
			{
				return new List<EntityMetadata>();
			}
			set
			{
			}
		}

		public IDictionary<string, EntityMetadata> ProfileAttributeMetadataCache
		{
			get
			{
				return new Dictionary<string, EntityMetadata>();
			}
			set
			{
			}
		}

		public byte[] ProfileEntityMetadataCacheSerialised;
		public byte[] ProfileAttributeMetadataCacheSerialised;

		// for backwards compatibility
		public List<EntityDataFilter> EntityDataFilterList;

		public EntityFilterArray EntityDataFilterArray;

		#region Backing
		private bool _UseSSL;
		private bool _UseIFD;
		private bool _UseOnline;
		private bool _UseOffice365;
		private string _EntitiesToIncludeString;
		public ObservableCollection<string> _EntitiesSelected;
		public bool isSortByDisplayName;
		public ObservableCollection<string> pluginMetadataEntitiesSelected;
		public ObservableCollection<string> jsEarlyBoundEntitiesSelected;
		public ObservableCollection<string> actionEntitiesSelected;
		public ObservableCollection<string> optionsetLabelsEntitiesSelected;
		public ObservableCollection<string> lookupLabelsEntitiesSelected;
		public ObservableCollection<string> _EntityList;
		private string _CrmOrg;
		private string _Password;
		private string _Username;
		private string _Domain;
		private bool _IncludeNonStandard;
		private string _Namespace;
		private string _ProjectName;
		private string _ProfileName = "";
		private string _ServerName = "";
		private string _ServerPort = "";
		private string _HomeRealm = "";
		private bool _UseWindowsAuth;
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
		private Context _Context;
		public int Threads = 2;
		public int EntitiesPerThread = 5;
		public ClearModeEnum ClearMode = ClearModeEnum.Disabled;

		#endregion

		#region Serialisable
		public bool UseSSL
		{
			get { return _UseSSL; }
			set
			{
				SetField(ref _UseSSL, value);
				ReEvalReadOnly();
				//if (SetField(ref _UseSSL, value))
				//{
				//	ReEvalReadOnly();
				//}
			}
		}

		public bool UseIFD
		{
			get { return _UseIFD; }
			set
			{
				if (SetField(ref _UseIFD, value))
				{
					if (value)
					{
						UseOnline = false;
						UseOffice365 = false;
						UseSSL = true;
					}
					//ReEvalReadOnly();
				}

				ReEvalReadOnly();
			}
		}

		public bool UseOnline
		{
			get { return _UseOnline; }
			set
			{
				if (SetField(ref _UseOnline, value))
				{
					if (value)
					{
						UseIFD = false;
						UseOffice365 = true;
						UseSSL = true;
					}
					else
					{
						UseOffice365 = false;
					}
					//ReEvalReadOnly();
				}

				ReEvalReadOnly();
			}
		}

		public bool UseOffice365
		{
			get { return _UseOffice365; }
			set
			{
				if (SetField(ref _UseOffice365, value))
				{
					if (value)
					{
						UseIFD = false;
						UseOnline = true;
						UseSSL = true;
						UseWindowsAuth = false;
					}
					//ReEvalReadOnly();
				}

				ReEvalReadOnly();
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
			get { return isSortByDisplayName; }
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

		public string CrmOrg
		{
			get { return _CrmOrg; }
			set
			{
				SetField(ref _CrmOrg, value);
				OnPropertyChanged("DisplayName");
			}
		}

		public string Password
		{
			get { return _Password; }
			set { SetField(ref _Password, value); }
		}

		public string Username
		{
			get { return _Username; }
			set
			{
				SetField(ref _Username, value);
				OnPropertyChanged("DisplayName");
			}
		}

		public string Domain
		{
			get { return _Domain; }
			set { SetField(ref _Domain, value); }
		}

		public bool IncludeNonStandard
		{
			get { return _IncludeNonStandard; }
			set { SetField(ref _IncludeNonStandard, value); }
		}

		public string Namespace
		{
			get { return _Namespace; }
			set { SetField(ref _Namespace, value); }
		}

		public string ProjectName
		{
			get { return _ProjectName; }
			set { SetField(ref _ProjectName, value); }
		}

		public string ProfileName
		{
			get { return _ProfileName; }
			set
			{
				SetField(ref _ProfileName, value);
				OnPropertyChanged("DisplayName");
			}
		}

		public string ServerName
		{
			get { return _ServerName; }
			set
			{
				SetField(ref _ServerName, value);
				OnPropertyChanged("DisplayName");
			}
		}

		public string ServerPort
		{
			get => _ServerPort;
			set
			{
				SetField(ref _ServerPort, value);
				OnPropertyChanged("DisplayName");
			}
		}

		public string HomeRealm
		{
			get { return _HomeRealm; }
			set { SetField(ref _HomeRealm, value); }
		}

		public bool UseWindowsAuth
		{
			get { return _UseWindowsAuth; }
			set
			{
				SetField(ref _UseWindowsAuth, value);
				ReEvalReadOnly();
			}
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
			get { return _SelectPrefixes; }
			set { _SelectPrefixes = value; }
		}

		public bool SplitFiles
		{
			get { return _SplitFiles; }
			set
			{
				_SplitFiles = value;
				OnPropertyChanged();
			}
		}

		public bool UseDisplayNames
		{
			get { return _UseDisplayNames; }
			set
			{
				_UseDisplayNames = value;
				OnPropertyChanged();
				OnPropertyChanged("TitleCaseLogicalNamesEnabled");
			}
		}

		public bool IsUseCustomDictionary
		{
			get { return _IsUseCustomDictionary; }
			set
			{
				_IsUseCustomDictionary = value;
				OnPropertyChanged();
			}
		}

		public bool IsGenerateLoadPerRelation
		{
			get
			{
				return _IsGenerateLoadPerRelation;
			}
			set
			{
				_IsGenerateLoadPerRelation = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateOptionSetLabelsInEntity
		{
			get { return generateOptionSetLabelsInEntity; }
			set
			{
				generateOptionSetLabelsInEntity = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateLookupLabelsInEntity
		{
			get { return generateLookupLabelsInEntity; }
			set
			{
				generateLookupLabelsInEntity = value;
				GenerateLookupLabelsInContract = GenerateLookupLabelsInContract && value;
				OnPropertyChanged();
			}
		}

		public bool GenerateOptionSetLabelsInContract
		{
			get { return generateOptionSetLabelsInContract; }
			set
			{
				generateOptionSetLabelsInContract = value;
				OnPropertyChanged();
			}
		}

		public bool GenerateLookupLabelsInContract
		{
			get { return generateLookupLabelsInContract; }
			set
			{
				generateLookupLabelsInContract = value && GenerateLookupLabelsInEntity;
				OnPropertyChanged();
			}
		}

		public bool GenerateGlobalActions
		{
			get { return generateGlobalActions; }
			set
			{
				generateGlobalActions = value;
				OnPropertyChanged();
			}
		}

		public bool LockNamesOnGenerate
		{
			get { return lockNamesOnGenerate; }
			set
			{
				lockNamesOnGenerate = value;
				OnPropertyChanged();
			}
		}

		public bool TitleCaseLogicalNames
		{
			get { return titleCaseLogicalNames; }
			set
			{
				titleCaseLogicalNames = value;
				OnPropertyChanged();
			}
		}

		public ClearModeEnum SelectedClearMode
		{
			get { return ClearMode; }
			set
			{
				ClearMode = value;
				OnPropertyChanged();
			}
		}

		public Context Context
		{
			get { return _Context; }
			set { _Context = value; }
		}

		#endregion
		#region Non serialisable

		[field: NonSerialized] private string _CrmSdkUrl;
		[field: NonSerialized] private string _Template;
		[field: NonSerialized] private string _T4Path;
		[field: NonSerialized] private string _OutputPath;
		[field: NonSerialized] private string _Folder = "";
		[field: NonSerialized] private bool _NewTemplate;
		[field: NonSerialized] private ObservableCollection<string> _OnLineServers;
		[field: NonSerialized] private ObservableCollection<string> _OrgList;
		[field: NonSerialized] private ObservableCollection<string> _TemplateList;

		public string CrmSdkUrl
		{
			get { return _CrmSdkUrl; }
			set { SetField(ref _CrmSdkUrl, value); }
		}

		public string Template
		{
			get { return _Template; }
			set
			{
				SetField(ref _Template, value);
				NewTemplate = !File.Exists(Path.Combine(_Folder, _Template));
			}
		}

		public string T4Path
		{
			get { return _T4Path; }
			set { SetField(ref _T4Path, value); }
		}

		public string OutputPath
		{
			get { return _OutputPath; }
			set { SetField(ref _OutputPath, value); }
		}

		public string Folder
		{
			get { return _Folder; }
			set { SetField(ref _Folder, value); }
		}

		public bool NewTemplate
		{
			get { return _NewTemplate; }
			set { SetField(ref _NewTemplate, value); }
		}


		public ObservableCollection<string> OnLineServers
		{
			get { return _OnLineServers; }
			set { SetField(ref _OnLineServers, value); }
		}


		public ObservableCollection<string> OrgList
		{
			get { return _OrgList; }
			set { SetField(ref _OrgList, value); }
		}


		public ObservableCollection<string> TemplateList
		{
			get { return _TemplateList; }
			set { SetField(ref _TemplateList, value); }
		}

		public ObservableCollection<string> EntityList
		{
			get { return _EntityList; }
			set { SetField(ref _EntityList, value); }
		}

		public ObservableCollection<string> EntitiesSelected
		{
			get { return _EntitiesSelected; }
			set
			{
				SetField(ref _EntitiesSelected, value);
			}
		}

		public ObservableCollection<string> PluginMetadataEntitiesSelected
		{
			get { return pluginMetadataEntitiesSelected; }
			set { SetField(ref pluginMetadataEntitiesSelected, value); }
		}

		public ObservableCollection<string> OptionsetLabelsEntitiesSelected
		{
			get
			{
				return optionsetLabelsEntitiesSelected;
			}
			set
			{
				SetField(ref optionsetLabelsEntitiesSelected, value);
			}
		}

		public ObservableCollection<string> LookupLabelsEntitiesSelected
		{
			get
			{
				return lookupLabelsEntitiesSelected;
			}
			set
			{
				SetField(ref lookupLabelsEntitiesSelected, value);
			}
		}

		public ObservableCollection<string> JsEarlyBoundEntitiesSelected
		{
			get { return jsEarlyBoundEntitiesSelected; }
			set { SetField(ref jsEarlyBoundEntitiesSelected, value); }
		}

		public ObservableCollection<string> ActionEntitiesSelected
		{
			get { return actionEntitiesSelected; }
			set { SetField(ref actionEntitiesSelected, value); }
		}

		public IOrganizationService CrmConnection { get; set; }

		public bool Dirty { get; set; }

		#endregion

		#region Read Only Properties

		public bool TitleCaseLogicalNamesEnabled => !UseDisplayNames;

		public string DisplayName =>
			string.IsNullOrWhiteSpace(ProfileName)
				? (string.IsNullOrWhiteSpace(ServerName)
					? "Unnamed Profile"
					: $"{ServerName.Replace("http://", "").Replace("https://", "")} - {Username}")
				: ProfileName;

		[field: NonSerialized] private ObservableCollection<string> filteredEntities;

		public ObservableCollection<string> FilteredEntities
		{
			get
			{
				if (filteredEntities != null)
				{
					return filteredEntities;
				}

				return filteredEntities =
				       new ObservableCollection<string>(EntityDataFilterArray.EntityFilters
					       .SelectMany(filter => filter.EntityFilterList)
					       .Where(filterList => !filterList.IsExcluded || filterList.IsGenerateMeta)
					       .Select(filterList => filterList.LogicalName).Distinct());
			}
		}

		private void ReEvalReadOnly()
		{
			OnPropertyChanged("NeedServer");
			OnPropertyChanged("NeedOnlineServer");
			OnPropertyChanged("NeedServerPort");
			OnPropertyChanged("NeedHomeRealm");
			OnPropertyChanged("NeedCredentials");
			OnPropertyChanged("NeedDomain");
			OnPropertyChanged("CanUseWindowsAuth");
			OnPropertyChanged("CanUseSSL");
		}

		public bool NeedServer => !(UseOnline || UseOffice365);

		public bool NeedOnlineServer => (UseOnline || UseOffice365);

		public bool NeedServerPort => !(UseOffice365 || UseOnline);
		public bool NotNeedServerPort => !NeedServerPort;

		public bool NeedHomeRealm => !(UseIFD || UseOffice365 || UseOnline);
		public bool NotNeedHomeRealm => !NeedHomeRealm;

		public bool NeedCredentials => !UseWindowsAuth;

		public bool NeedDomain => !(UseOffice365 || UseOnline) && NeedCredentials;
		public bool NotNeedDomain => !NeedDomain;

		public bool CanUseWindowsAuth => !(UseIFD || UseOnline || UseOffice365);

		public bool CanUseSSL => !(UseOnline || UseOffice365 || UseIFD);

		#endregion

		#endregion

		#region boiler-plate INotifyPropertyChanged

		[field: NonSerialized]
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

		#region Obsolete settings

		[Serializable]
		public class SerializableSettings
		{
			public Guid[] _EntityMetadataCacheGuid;
			public MappingEntity[] _EntityMetadataCacheMappingEntity;
			public bool _UseSSL;
			public bool _UseIFD;
			public bool _UseOnline;
			public bool _UseOffice365;
			public string _EntitiesToIncludeString;
			public string _CrmOrg;
			public string _Password;
			public string _Username;
			public string _Domain;
			public string _Namespace;
			public string _ProjectName;
			public string _ServerName;
			public string _ServerPort;
			public string _HomeRealm;
			public bool _UseWindowsAuth;
			public string _EntitiesString;
			public string _SelectPrefixes;
			public bool _SplitFiles;
			public Context _Context;
			public int _Threads = 2;
			public int _EntitiesPerThread = 5;
		}

		public SerializableSettings SerializedSettings
		{
			get
			{
				return null;

				var entityMetadataCacheGuid = new Guid[EntityMetadataCache.Count];
				var entityMetadataCacheMappingEntity = new MappingEntity[EntityMetadataCache.Count];

				var index = 0;
				EntityMetadataCache.Keys.ToList()
					.ForEach(key =>
					         {
						         var value = EntityMetadataCache[key];
						         entityMetadataCacheGuid[index] = key;
						         entityMetadataCacheMappingEntity[index] = value;
						         index++;
					         });

				return new SerializableSettings
				       {
					       _EntityMetadataCacheGuid = entityMetadataCacheGuid,
					       _EntityMetadataCacheMappingEntity = entityMetadataCacheMappingEntity,
					       _UseSSL = UseSSL,
					       _UseIFD = UseIFD,
					       _UseOnline = UseOnline,
					       _UseOffice365 = UseOffice365,
					       _EntitiesToIncludeString = EntitiesToIncludeString,
					       _CrmOrg = CrmOrg,
					       _Password = Password,
					       _Username = Username,
					       _Domain = Domain,
					       _Namespace = Namespace,
					       _ProjectName = ProjectName,
					       _ServerName = ServerName,
					       _ServerPort = ServerPort,
					       _HomeRealm = HomeRealm,
					       _UseWindowsAuth = UseWindowsAuth,
					       _EntitiesString = EntitiesString,
					       _SelectPrefixes = SelectPrefixes,
					       _SplitFiles = SplitFiles,
					       _Context = Context,
					       _Threads = Threads,
					       _EntitiesPerThread = EntitiesPerThread
				       };
			}

			set
			{
				if (value == null)
				{
					return;
				}

				EntityMetadataCache.Clear();

				for (var i = 0; i < value._EntityMetadataCacheGuid.Length; i++)
				{
					EntityMetadataCache.Add(value._EntityMetadataCacheGuid[i], value._EntityMetadataCacheMappingEntity[i]);
				}

				UseSSL = value._UseSSL;
				UseIFD = value._UseIFD;
				UseOnline = value._UseOnline;
				UseOffice365 = value._UseOffice365;
				EntitiesToIncludeString = value._EntitiesToIncludeString ?? EntitiesToIncludeString;
				CrmOrg = value._CrmOrg ?? CrmOrg;
				Password = value._Password ?? Password;
				Username = value._Username ?? Username;
				Domain = value._Domain ?? Domain;
				Namespace = value._Namespace ?? Namespace;
				ProjectName = value._ProjectName ?? ProjectName;
				ServerName = value._ServerName ?? ServerName;
				HomeRealm = value._HomeRealm ?? HomeRealm;
				UseWindowsAuth = value._UseWindowsAuth;
				EntitiesString = value._EntitiesString ?? EntitiesString;
				SelectPrefixes = value._SelectPrefixes ?? SelectPrefixes;
				SplitFiles = value._SplitFiles;
				Context = value._Context;
				Threads = value._Threads <= 0 ? 2 : value._Threads;
				EntitiesPerThread = value._EntitiesPerThread <= 0 ? 5 : value._EntitiesPerThread;
				ServerPort = value._ServerPort ?? ServerPort;

				if (string.IsNullOrWhiteSpace(ServerPort) || Regex.IsMatch(ServerPort, "^\\d+$"))
				{
					ServerName = $"{(UseSSL ? "https" : "http")}://{ServerName}";
					ServerName += string.IsNullOrWhiteSpace(ServerPort) ? "" : $":{ServerPort}";
					ServerName += string.IsNullOrWhiteSpace(CrmOrg) ? "" : $"/{CrmOrg}";
					ServerPort = UseOffice365 ? "Office365" : (UseIFD ? "IFD" : "AD");
				}
			}
		}

		#endregion

		#region Init

		// entity metadata ID mapped to its object
		public Settings()
		{
			InitFields();
		}

		public Settings(SerializableSettings serSettings)
		{
			EntityList = new ObservableCollection<string>();
			EntitiesSelected = new ObservableCollection<string>();

			CrmSdkUrl = @"https://disco.crm.dynamics.com/XRMServices/2011/Discovery.svc";
			ProjectName = "";
			Domain = "";
			T4Path = Path.Combine(DteHelper.AssemblyDirectory(), @"Resources\Templates\CrmSvcUtil.tt");
			Template = "";
			CrmOrg = "";
			EntitiesString = "account,contact,lead,opportunity,systemuser";
			EntitiesToIncludeString = "account,contact,lead,opportunity,systemuser";
			OutputPath = "";
			Username = "[user]@[org].onmicrosoft.com";
			Password = "";
			Namespace = "";
			Dirty = false;

			InitFields();

			SerializedSettings = serSettings;
		}

		public void OnDeserialization(object sender)
		{
			InitFields();
		}

		private void InitFields()
		{
			EntityList = EntityList ?? new ObservableCollection<string>();
			EntitiesSelected = EntitiesSelected ?? new ObservableCollection<string>();
			PluginMetadataEntitiesSelected = PluginMetadataEntitiesSelected ?? new ObservableCollection<string>();
			JsEarlyBoundEntitiesSelected = JsEarlyBoundEntitiesSelected ?? new ObservableCollection<string>();
			ActionEntitiesSelected = ActionEntitiesSelected ?? new ObservableCollection<string>();
			OptionsetLabelsEntitiesSelected = OptionsetLabelsEntitiesSelected ?? new ObservableCollection<string>();
			LookupLabelsEntitiesSelected = LookupLabelsEntitiesSelected ?? new ObservableCollection<string>();

			ProfileEntityMetadataCache = ProfileEntityMetadataCache ?? new List<EntityMetadata>();
			ProfileAttributeMetadataCache = ProfileAttributeMetadataCache ?? new Dictionary<string, EntityMetadata>();
			
			LookupEntitiesMetadataCache = LookupEntitiesMetadataCache ?? new Dictionary<string, LookupMetadata>();

			EntityMetadataCache = EntityMetadataCache ?? new Dictionary<Guid, MappingEntity>();
			EntityDataFilterArray = EntityDataFilterArray ?? new EntityFilterArray();

			OnLineServers = OnLineServers ?? new ObservableCollection<string>();
			OrgList = OrgList ?? new ObservableCollection<string>();
			EntitiesSelected = EntitiesSelected ?? new ObservableCollection<string>();
			EntityList = EntityList ?? new ObservableCollection<string>();
			TemplateList = TemplateList ?? new ObservableCollection<string>();

			CrmSdkUrl = CrmSdkUrl ?? @"https://disco.crm.dynamics.com/XRMServices/2011/Discovery.svc";
			ProjectName = ProjectName ?? "";
			Domain = Domain ?? "";
			CrmOrg = CrmOrg ?? "";
			EntitiesString = EntitiesString ?? "account,contact,lead,opportunity,systemuser";
			EntitiesToIncludeString = EntitiesToIncludeString ?? "account,contact,lead,opportunity,systemuser";
			OutputPath = OutputPath ?? "";
			Username = Username ?? "";
			Password = Password ?? "";
			Namespace = Namespace ?? "";
			
			if (string.IsNullOrWhiteSpace(ServerPort) || Regex.IsMatch(ServerPort, "^\\d+$"))
			{
				ServerName = $"{(UseSSL ? "https" : "http")}://{ServerName}";
				ServerName += string.IsNullOrWhiteSpace(ServerPort) ? "" : $":{ServerPort}";
				ServerName += string.IsNullOrWhiteSpace(CrmOrg) ? "" : $"/{CrmOrg}";
				ServerPort = UseOffice365 ? "Office365" : (UseIFD ? "IFD" : "AD");
			}
		}

		#endregion

		public void FiltersChanged()
		{
			filteredEntities = null;
			OnPropertyChanged("FilteredEntities");
		}

		#region Conntection Strings

		public AuthenticationProviderType AuthType
		{
			get
			{
				if (UseIFD)
				{
					return AuthenticationProviderType.Federation;
				}

				if (UseOffice365)
				{
					return AuthenticationProviderType.OnlineFederation;
				}

				if (UseOnline)
				{
					return AuthenticationProviderType.LiveId;
				}

				return AuthenticationProviderType.ActiveDirectory;
			}
		}

		public Uri GetDiscoveryUri()
		{
			var url =
				$"{(UseSSL ? "https" : "http")}://{(UseIFD ? ServerName : UseOffice365 ? "disco." + ServerName : UseOnline ? "dev." + ServerName : ServerName)}:{(ServerPort.Length == 0 ? (UseSSL ? 443 : 80) : int.Parse(ServerPort))}";
			return new Uri(url + "/XRMServices/2011/Discovery.svc");
		}

		public string GetDiscoveryCrmConnectionString()
		{
			var connectionString =
				$"Url={(UseSSL ? "https" : "http")}://{(UseIFD ? ServerName : UseOffice365 ? "disco." + ServerName : UseOnline ? "dev." + ServerName : ServerName)}:{(ServerPort.Length == 0 ? (UseSSL ? 443 : 80) : int.Parse(ServerPort))};";

			if (!UseWindowsAuth)
			{
				if (!UseIFD)
				{
					if (!string.IsNullOrEmpty(Domain))
					{
						connectionString += $"Domain={Domain};";
					}
				}

				var sUsername = Username;
				if (UseIFD)
				{
					if (!string.IsNullOrEmpty(Domain))
					{
						sUsername = $"{Domain}\\{Username}";
					}
				}

				connectionString += $"Username={sUsername};Password={Password};";
			}

			if (UseOnline && !UseOffice365)
			{
				ClientCredentials deviceCredentials;

				do
				{
					deviceCredentials = new ClientCredentials();
				} while (deviceCredentials.UserName.Password.Contains(";")
				         || deviceCredentials.UserName.Password.Contains("=")
				         || deviceCredentials.UserName.Password.Contains(" ")
				         || deviceCredentials.UserName.UserName.Contains(";")
				         || deviceCredentials.UserName.UserName.Contains("=")
				         || deviceCredentials.UserName.UserName.Contains(" "));

				connectionString +=
					$"DeviceID={deviceCredentials.UserName.UserName};DevicePassword={deviceCredentials.UserName.Password};";
			}

			if (UseIFD && !string.IsNullOrEmpty(HomeRealm))
			{
				connectionString += $"HomeRealmUri={HomeRealm};";
			}

			return connectionString;
		}


		private string currentString = "";
		private string checkString = "";

		private string GetCurrentUniqueConnectionString()
		{
			var connectionString = $"AuthType={ServerPort.Trim(';')};Url={ServerName.Trim(';')};";

			if (!UseWindowsAuth)
			{
				if (!string.IsNullOrWhiteSpace(Domain))
				{
					connectionString += $"Domain={Domain.Trim(';')};";
				}

				connectionString += $"Username={Username.Trim(';')};Password={Password.Trim(';')};";
			}

			if (!string.IsNullOrWhiteSpace(HomeRealm))
			{
				connectionString += $"HomeRealmUri={HomeRealm.Trim(';')};";
			}

			return connectionString;
		}

		public string GetOrganizationCrmConnectionString()
		{
			return GetCurrentUniqueConnectionString();
		}

		#endregion
	}
}
