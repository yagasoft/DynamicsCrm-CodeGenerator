#region Imports

using System;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Mapping
{
	[Serializable]
	public class MappingLookup
	{
		public string LookupSingleType { get; set; }
		public string[] LookupTypes { get; set; }
		public LookupLabel LookupLabel { get; set; }
		public LookupKeys LookupKeys { get; set; }
	}

	[Serializable]
	public class LookupLabel
	{
		public string LabelFieldNames { get; set; }
		public string LogicalName { get; set; }
		public string IdFieldName { get; set; }

		public LookupLabel(string labelFieldNames = null, string logicalName = null, string idFieldName = null)
		{
			LabelFieldNames = labelFieldNames;
			LogicalName = logicalName;
			IdFieldName = idFieldName;
		}
	}

	[Serializable]
	public class LookupKeys
	{
		public MappingEntity Entity { get; set; }
		public MappingField[] Fields { get; set; }

		public LookupKeys(MappingEntity entity = null, MappingField[] fields = null)
		{
			Entity = entity;
			Fields = fields;
		}
	}
}
