using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class Context
	{
		public string Namespace { get; set; }

		public string FileName { get; set; }

		public bool SplitFiles { get; set; }

		public bool UseDisplayNames { get; set; }

		public bool IsUseCustomDictionary { get; set; }
		public bool IsGenerateLoadPerRelation { get; set; }

		public List<int> Languages { get; set; }

		public bool GenerateLookupLabelsInEntity { get; set; }
		public bool GenerateOptionSetLabelsInEntity { get; set; }
		public bool GenerateLookupLabelsInContract { get; set; }
		public bool GenerateOptionSetLabelsInContract { get; set; }
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
		public List<string> ActionEntities { get; set; }

		public MappingEntity[] Entities { get; set; }

		public MappingAction[] GlobalActions { get; set; }
		public ClearModeEnum ClearMode { get; set; }
	}
}
