using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrmCodeGenerator.VSPackage.Model
{
	[Obsolete("Old Settings class used only for migration.", false)]
	[Serializable]
	public class Context
	{
		public string Namespace { get; set; }

		public string FileName { get; set; }

		public bool SplitFiles { get; set; }

		public bool UseDisplayNames { get; set; }

		public bool IsAddEntityAnnotations { get; set; }
		public bool IsAddContractAnnotations { get; set; }

		public bool IsUseCustomDictionary { get; set; }
		public bool IsGenerateLoadPerRelation { get; set; }

		public bool IsUseCustomEntityReference { get; set; }
		public bool IsGenerateAlternateKeys { get; set; }
		public bool IsUseCustomTypeForAltKeys { get; set; }

		public bool IsMakeCrmEntitiesJsonFriendly { get; set; }

		public bool IsGenerateEnumNames { get; set; }
		public bool IsGenerateEnumLabels { get; set; }
		public bool IsGenerateFieldSchemaNames { get; set; }
		public bool IsGenerateFieldLabels { get; set; }
		public bool IsGenerateRelationNames { get; set; }

		public List<int> Languages { get; set; }

		public bool GenerateGlobalActions { get; set; }

		public EntityFilterArray EntityDataFilterArray { get; set; }

		public List<EntityDataFilter> EntityDataFilterList { get; set; }

		public List<string> PluginMetadataEntities { get; set; }

		public List<string> OptionsetLabelsEntities
		{
			get; set;
		}
		public List<string> LookupLabelsEntities
		{
			get; set;
		}

		public List<string> JsEarlyBoundEntities { get; set; }

		public IDictionary<string, string[]> SelectedActions { get; set; }

		public MappingEntity[] Entities { get; set; }
		public string[] SelectedEntities { get; set; }

		public MappingAction[] GlobalActions { get; set; }
		public ClearModeEnum ClearMode { get; set; }
	}
}
