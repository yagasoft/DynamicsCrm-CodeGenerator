#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using Yagasoft.CrmCodeGenerator.Helpers;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Attributes;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Mapping
{
	[Serializable]
	public class MappingEnum
	{
		public Guid? MetadataId { get; set; }
		public bool? IsGlobal { get; set; }
		public string DisplayName { get; set; }
		public string FriendlyName { get; set; }
		public string LogicalName { get; set; }
		public string EnumName { get; set; }
		public bool IsMultiSelect { get; set; }
		public MapperEnumItem[] Items { get; set; }

		public static MappingEnum GetMappingEnum(AttributeMetadata picklist, MappingEnum mappingEnum, bool isTitleCaseLogicalName)
		{
			mappingEnum = mappingEnum
				?? new MappingEnum
				{
					MetadataId = picklist.MetadataId
				};

			if (picklist.SchemaName != null)
			{
				mappingEnum.DisplayName = Naming.GetProperVariableName(picklist, isTitleCaseLogicalName);
			}

			mappingEnum.LogicalName = picklist.LogicalName ?? mappingEnum.LogicalName;
			mappingEnum.Items = new MapperEnumItem[0];

			var attributeAsEnum = picklist as EnumAttributeMetadata;

			if (attributeAsEnum?.OptionSet != null && attributeAsEnum.OptionSet.Options.Count > 0)
			{
				var newItems = new List<MapperEnumItem>();
				mappingEnum.IsGlobal = attributeAsEnum.OptionSet.IsGlobal;
				if (mappingEnum.IsGlobal == true)
				{
					mappingEnum.EnumName = attributeAsEnum.OptionSet.Name;
				}
				else
				{
					mappingEnum.EnumName = mappingEnum.LogicalName;

				}

				newItems.AddRange(attributeAsEnum.OptionSet.Options
					.Where(o => o.Label.UserLocalizedLabel != null)
					.Select(e => GetEnumItem(e, isTitleCaseLogicalName)));

				mappingEnum.Items = newItems.ToArray();
			}
			else
			{
				var attributeAsBool = picklist as BooleanAttributeMetadata;

				if (attributeAsBool?.OptionSet != null)
				{
					var newItems = new List<MapperEnumItem>();

					var trueOption = attributeAsBool.OptionSet.TrueOption;

					if (trueOption.Label.UserLocalizedLabel != null)
					{
						newItems.Add(GetEnumItem(trueOption, isTitleCaseLogicalName));
					}

					var falseOption = attributeAsBool.OptionSet.FalseOption;

					if (falseOption.Label.UserLocalizedLabel != null)
					{
						newItems.Add(GetEnumItem(falseOption, isTitleCaseLogicalName));
					}

					mappingEnum.Items = newItems.ToArray();
				}
			}

			var duplicates = new Dictionary<string, int>();

			foreach (var item in mappingEnum.Items)
			{
				if (duplicates.ContainsKey(item.Name))
				{
					duplicates[item.Name] = duplicates[item.Name] + 1;
					item.Name += "_" + duplicates[item.Name];
				}
				else
				{
					duplicates[item.Name] = 1;
				}
			}

			mappingEnum.IsMultiSelect = picklist is MultiSelectPicklistAttributeMetadata;

			return mappingEnum;
		}

		private static MapperEnumItem GetEnumItem(OptionMetadata metadata, bool isTitleCaseLogicalName)
		{
			return
				new MapperEnumItem
				{
					Attribute =
						new CrmPicklistAttribute
						{
							DisplayName = metadata.Label.UserLocalizedLabel.Label,
							Value = metadata.Value ?? 1,
							LocalizedLabels = metadata.Label.LocalizedLabels
								.Select(label =>
								new LocalizedLabelSerialisable
								{
									LanguageCode = label.LanguageCode,
									Label = label.Label
								}).ToArray()
						},
					Name = Naming.GetProperVariableName(metadata.Label.UserLocalizedLabel.Label, isTitleCaseLogicalName)
				};
		}
	}

	[Serializable]
	public class MapperEnumItem
	{
		public CrmPicklistAttribute Attribute { get; set; }

		public string Name { get; set; }

		public int Value => Attribute.Value;

		public string DisplayName => Attribute.DisplayName;

		public LocalizedLabelSerialisable[] LocalizedLabels => Attribute.LocalizedLabels;
	}
}
