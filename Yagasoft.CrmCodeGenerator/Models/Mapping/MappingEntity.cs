#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using Yagasoft.CrmCodeGenerator.Helpers;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Yagasoft.CrmCodeGenerator.Models.Attributes;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Mapping
{
	[Serializable]
	public class MappingEntity
	{
		public Guid? MetadataId { get; set; }
		public string ServerStamp { get; set; }
		public CrmEntityAttribute Attribute { get; set; }
		public bool IsIntersect { get; set; }
		public int? TypeCode { get; set; }
		public string[] AlternateKeys { get; set; }
		public MappingField[] Fields { get; set; }
		public MappingEnum States { get; set; }
		public MappingEnum[] Enums { get; set; }
		public MappingRelationship1N[] RelationshipsOneToMany { get; set; }
		public MappingRelationshipN1[] RelationshipsManyToOne { get; set; }

		public MappingAction[] Actions { get; set; }

		public string LogicalName => Attribute.LogicalName;

		public string SchemaName { get; set; }
		public string DisplayName { get; set; }
		public string Label { get; set; }
		public string HybridName { get; set; }
		public string FriendlyName { get; set; }
		public string StateName { get; set; }
		public MappingField PrimaryKey { get; set; }
		public string PrimaryKeyProperty { get; set; }
		public string PrimaryNameAttribute { get; set; }
		public string Description { get; set; }

		public string DescriptionXmlSafe => Naming.XmlEscape(Description);

		public string Plural => Naming.GetPluralName(DisplayName);

		public MappingEntity()
		{
			Description = "";
		}

		public static void UpdateCache(List<EntityMetadata> entityMetadataList
			, IDictionary<Guid, MappingEntity> mappingEntities, string serverStamp,
			DeletedMetadataCollection deletedMetadata, bool isTitleCaseLogicalName)
		{
			if (deletedMetadata != null)
			{
				ParseDeleted(entityMetadataList, mappingEntities, serverStamp, deletedMetadata);
			}

			// update modified entities
			var modifiedEntities = entityMetadataList
				.Where(entity => entity.MetadataId.HasValue && mappingEntities.ContainsKey(entity.MetadataId.Value)).ToList();
			modifiedEntities.AsParallel().ForAll(entity =>
				GetMappingEntity(entity, serverStamp, mappingEntities[entity.MetadataId.GetValueOrDefault()], isTitleCaseLogicalName));

			var newEntities = entityMetadataList
				.Where(entity => entity.MetadataId.HasValue && !mappingEntities.ContainsKey(entity.MetadataId.Value)).ToList();
			newEntities.AsParallel().ForAll(entity => mappingEntities
				.Add(entity.MetadataId.GetValueOrDefault(), GetMappingEntity(entity, serverStamp, null, isTitleCaseLogicalName)));
		}

		private static void ParseDeleted(List<EntityMetadata> entityMetadataList,
			IDictionary<Guid, MappingEntity> mappingEntities, string serverStamp
			, DeletedMetadataCollection deletedMetadata)
		{
			if (deletedMetadata.ContainsKey(DeletedMetadataFilters.Entity))
			{
				// remove deleted entities
				deletedMetadata[DeletedMetadataFilters.Entity].ToList().ForEach(guid => mappingEntities.Remove(guid));
			}

			if (deletedMetadata.ContainsKey(DeletedMetadataFilters.Attribute))
			{
				// remove deleted fields
				deletedMetadata[DeletedMetadataFilters.Attribute].ToList()
					.ForEach(guid =>
					         {
						         var entity =
							         mappingEntities.Values.FirstOrDefault(
								         entityQ => entityQ.Fields.ToList().Any(field => field.MetadataId == guid));

						         if (entity == null)
						         {
							         return;
						         }

						         entity.Fields = entity.Fields.ToList().Where(field => field.MetadataId != guid).ToArray();
						         entity.Enums = entity.Enums.ToList().Where(field => field.MetadataId != guid).ToArray();
						         entity.ServerStamp = serverStamp;
					         });
			}

			if (deletedMetadata.ContainsKey(DeletedMetadataFilters.Relationship))
			{
				// remove deleted relationships
				deletedMetadata[DeletedMetadataFilters.Relationship].ToList()
					.ForEach(guid =>
					         {
						         // 1:N
						         var entity =
							         mappingEntities.Values.FirstOrDefault(
								         entityQ => entityQ.RelationshipsOneToMany.ToList().Any(relation => relation.MetadataId == guid));

						         if (entity != null)
						         {
							         entity.RelationshipsOneToMany =
								         entity.RelationshipsOneToMany.ToList().Where(relation => relation.MetadataId != guid).ToArray();
							         entity.ServerStamp = serverStamp;
							         return;
						         }

						         // N:1
						         entity =
							         mappingEntities.Values.FirstOrDefault(
								         entityQ => entityQ.RelationshipsManyToOne.ToList().Any(relation => relation.MetadataId == guid));

						         if (entity != null)
						         {
							         entity.RelationshipsManyToOne =
								         entity.RelationshipsManyToOne.ToList().Where(relation => relation.MetadataId != guid).ToArray();
							         entity.ServerStamp = serverStamp;
							         return;
						         }

						         // M:N
						         var entities =
							         mappingEntities.Values.Where(
								         entityQ => entityQ.RelationshipsManyToMany.ToList().Any(relation => relation.MetadataId == guid))
								         .ToList();

						         if (entities.Any())
						         {
							         entities.ForEach(entityQ =>
							                          {
								                          entityQ.RelationshipsManyToMany = entityQ.RelationshipsManyToMany
									                          .ToList()
									                          .Where(relation => relation.MetadataId != guid)
									                          .ToArray();
								                          entityQ.ServerStamp = serverStamp;
							                          });
						         }
					         });
			}
		}

		internal static MappingEntity GetMappingEntity(EntityMetadata entityMetadata, string serverStamp,
			MappingEntity entity, bool isTitleCaseLogicalName)
		{
			entity = entity ?? new MappingEntity();

			entity.MetadataId = entityMetadata.MetadataId;
			entity.ServerStamp = serverStamp;

			entity.Attribute = entity.Attribute ?? new CrmEntityAttribute();
			entity.TypeCode = entityMetadata.ObjectTypeCode ?? entity.TypeCode;
			entity.Attribute.LogicalName = entityMetadata.LogicalName ?? entity.Attribute.LogicalName;
			entity.IsIntersect = (entityMetadata.IsIntersect ?? entity.IsIntersect);
			entity.Attribute.PrimaryKey = entityMetadata.PrimaryIdAttribute ?? entity.Attribute.PrimaryKey;

			if (entityMetadata.DisplayName?.UserLocalizedLabel != null)
			{
				entity.Label = entityMetadata.DisplayName.UserLocalizedLabel.Label;
			}

			if (entityMetadata.SchemaName != null)
			{
				entity.DisplayName = Naming.GetProperEntityName(entityMetadata.SchemaName);
				entity.SchemaName = entityMetadata.SchemaName;

				if (entityMetadata.LogicalName != null)
				{
					entity.HybridName = Naming.GetProperHybridName(entityMetadata.SchemaName, entityMetadata.LogicalName);
				}
			}

			entity.StateName = entity.HybridName + "State";

			if (entityMetadata.Description?.UserLocalizedLabel != null)
			{
				entity.Description = entityMetadata.Description.UserLocalizedLabel.Label;
			}

			var fields = (entity.Fields ?? new MappingField[0]).ToList();

			////if (entityMetadata.Attributes != null)
			////{

			var validFields = entityMetadata.Attributes
				.Where(a => a.AttributeOf == null || a is ImageAttributeMetadata).ToArray();

			foreach (var field in validFields)
			{
				var existingField = fields.FirstOrDefault(fieldQ => fieldQ.MetadataId == field.MetadataId);

				// if it exists, remove it from the list
				if (existingField != null)
				{
					fields.RemoveAll(fieldQ => fieldQ.MetadataId == field.MetadataId);
				}

				// update/create and add to list
				fields.Add(MappingField.GetMappingField(field, entity, existingField, isTitleCaseLogicalName));
			}

			fields.ForEach(
				f =>
				{
					if (f.DisplayName == entity.DisplayName)
					{
						f.DisplayName += "1";
					}

					if (f.HybridName == entity.HybridName)
					{
						f.HybridName += "1";
					}
				});

			AddImages(fields);
			AddLookupFields(fields);

			entity.Fields = fields.ToArray();

			// get the states enum from the metadata
			entity.States =
				entityMetadata.Attributes
					.Where(a => a is StateAttributeMetadata && a.AttributeOf == null)
					.Select(a => MappingEnum
						.GetMappingEnum(a as EnumAttributeMetadata, null, isTitleCaseLogicalName))
					.FirstOrDefault() ?? entity.States;

			// get all optionsets from the metadata
			var newEnums = entityMetadata.Attributes
				.Where(a => (a is EnumAttributeMetadata || a is BooleanAttributeMetadata) && a.AttributeOf == null);

			// if there was never any enums previously, then just take the ones sent
			if (entity.Enums == null)
			{
				entity.Enums = newEnums
					.Select(newEnum => MappingEnum.GetMappingEnum(newEnum, null, isTitleCaseLogicalName))
					.ToArray();
			}
			else
			{
				var existingEnums = entity.Enums.ToList();

				// else, update the changed ones
				newEnums.AsParallel()
					.ForAll(newEnum =>
							{
								// has this enum been updated?
								var existingEnum = existingEnums
									.Find(existingEnumQ => existingEnumQ.MetadataId == newEnum.MetadataId);

								if (existingEnum != null)
								{
									// update it here
									entity.Enums[existingEnums.IndexOf(existingEnum)] =
										MappingEnum.GetMappingEnum(newEnum, existingEnum, isTitleCaseLogicalName);
								}
								else
								{
									// add new
									existingEnums.Add(MappingEnum.GetMappingEnum(newEnum, null, isTitleCaseLogicalName));
								}
							});

				entity.Enums = existingEnums.ToArray();
			}
			////}

			entity.PrimaryKey = entity.Fields.FirstOrDefault(f => f.Attribute.LogicalName == entity.Attribute.PrimaryKey);
			entity.PrimaryKeyProperty = entity.PrimaryKey?.DisplayName;
			entity.PrimaryNameAttribute = entityMetadata.PrimaryNameAttribute ?? entity.PrimaryNameAttribute;

			if (entityMetadata.Keys?.Any(e => e.KeyAttributes?.Any() == true) == true)
			{
				entity.AlternateKeys = entityMetadata.Keys.SelectMany(e => e.KeyAttributes).ToArray();
			}

			if (entityMetadata.OneToManyRelationships != null)
			{
				MappingRelationship1N.UpdateCache(entityMetadata.OneToManyRelationships.ToList(), entity, entity.Fields);
			}

			if (entityMetadata.OneToManyRelationships != null)
			{
				MappingRelationshipN1.UpdateCache(entityMetadata.ManyToOneRelationships.ToList(), entity, entity.Fields);
			}

			if (entityMetadata.OneToManyRelationships != null)
			{
				MappingRelationshipMN.UpdateCache(entityMetadata.ManyToManyRelationships.ToList(), entity, entity.LogicalName);

				// add a clone for self-referenced relation
				var relationshipsManyToMany = entity.RelationshipsManyToMany.ToList();
				var selfReferenced = relationshipsManyToMany.Where(r => r.IsSelfReferenced).ToList();

				foreach (var referenced in selfReferenced)
				{
					if (relationshipsManyToMany.All(rel => rel.DisplayName
						!= "Referencing" + Naming.GetProperVariableName(referenced.SchemaName, false)))
					{
						var referencing = (MappingRelationshipMN)referenced.Clone();
						referencing.DisplayName = "Referencing" + Naming.GetProperVariableName(referenced.SchemaName, false);
						referencing.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referencing";
						relationshipsManyToMany.Add(referencing);
					}
				}

				entity.RelationshipsManyToMany = relationshipsManyToMany.OrderBy(r => r.DisplayName).ToArray();
			}

			entity.FriendlyName = Naming.Clean(string.IsNullOrEmpty(entity.Label)
				? Naming.Clean(entity.HybridName)
				: Naming.Clean(entity.Label));

			// generate attribute friendly names and detect duplicates
			entity.Fields.AsParallel()
				.ForAll(field =>
						{
							var cleanFieldName =
								Naming.Clean(
									string.IsNullOrEmpty(field.Label)
										? Naming.Clean(field.DisplayName)
										: Naming.Clean(field.Label))
									+ (field == entity.PrimaryKey ? "Id" : "");

							var isDuplicateName = entity.Fields
								.Count(fieldQ => Naming.Clean(
									string.IsNullOrEmpty(fieldQ.Label)
										? Naming.Clean(fieldQ.DisplayName)
										: Naming.Clean(fieldQ.Label))
									== cleanFieldName) > 1;

							isDuplicateName =
								isDuplicateName
									|| cleanFieldName == "Attributes"
									|| cleanFieldName == entity.FriendlyName
									|| cleanFieldName == "LogicalName"
									|| cleanFieldName == "EntityLogicalName"
									|| cleanFieldName == "SchemaName"
									|| cleanFieldName == "DisplayName"
									|| cleanFieldName == "EntityTypeCode";

							field.FriendlyName = cleanFieldName +
								(isDuplicateName ? "_" + field.DisplayName : "");
						});

			// generate enum friendly names
			entity.Enums.AsParallel()
				.ForAll(enm =>
						{
							var attribute = entity.Fields.FirstOrDefault(field => field.LogicalName == enm.LogicalName);
							enm.FriendlyName = attribute == null ? enm.DisplayName : attribute.FriendlyName;
						});

			return entity;
		}

		private static void AddLookupFields(List<MappingField> fields)
		{
			var fieldsIterator = fields.Where(e => e.Attribute.IsLookup).ToArray();

			foreach (var lookup in fieldsIterator)
			{
				var nameField =
					new MappingField
					{
						Attribute =
							new CrmPropertyAttribute
							{
								IsLookup = false,
								LogicalName = lookup.Attribute.LogicalName + "Name",
								IsEntityReferenceHelper = true
							},
						SchemaName = lookup.SchemaName + "Name",
						DisplayName = lookup.DisplayName + "Name",
						HybridName = lookup.HybridName + "Name",
						FieldType = AttributeTypeCode.EntityName,
						IsValidForUpdate = false,
						GetMethod = "",
						PrivatePropertyName = lookup.PrivatePropertyName + "Name"
					};

				if (fields.Count(f => f.DisplayName == nameField.DisplayName) == 0)
				{
					fields.Add(nameField);
				}

				if (lookup.LookupData?.LookupSingleType.IsFilled() == true)
				{
					continue;
				}

				var typeField =
					new MappingField
					{
						Attribute =
							new CrmPropertyAttribute
							{
								IsLookup = false,
								LogicalName = lookup.Attribute.LogicalName + "Type",
								IsEntityReferenceHelper = true
							},
						SchemaName = lookup.SchemaName + "Type",
						DisplayName = lookup.DisplayName + "Type",
						HybridName = lookup.HybridName + "Type",
						FieldType = AttributeTypeCode.EntityName,
						IsValidForUpdate = false,
						GetMethod = "",
						PrivatePropertyName = lookup.PrivatePropertyName + "Type"
					};

				if (fields.Count(f => f.DisplayName == typeField.DisplayName) == 0)
				{
					fields.Add(typeField);
				}
			}
		}

		private static void AddImages(List<MappingField> fields)
		{
			var fieldsIterator = fields.Where(e => e.Attribute.IsImage).ToArray();

			foreach (var image in fieldsIterator)
			{
				image.TargetTypeForCrmSvcUtil = "byte[]";

				var imageTimestamp =
					new MappingField
					{
						Attribute =
							new CrmPropertyAttribute
							{
								IsLookup = false,
								LogicalName = $"{image.LogicalName}_timestamp",
								IsEntityReferenceHelper = false
							},
						SchemaName = $"{image.SchemaName}_Timestamp",
						DisplayName = $"{image.DisplayName}_Timestamp",
						HybridName = $"{image.HybridName}_Timestamp",
						Label = $"{image.Label}_Timestamp",
						LocalizedLabels = image
							.LocalizedLabels.Select(
								e =>
								{
									var copy = e.Copy();
									copy.Label = $"{copy.Label}_Timestamp";
									return copy;
								}).ToArray(),
						TargetTypeForCrmSvcUtil = "long?",
						FieldType = AttributeTypeCode.BigInt,
						IsValidForRead = true,
						IsValidForCreate = false,
						IsValidForUpdate = false,
						Description = " ", // CrmSvcUtil provides an empty description for this EntityImage_TimeStamp
						GetMethod = ""
					};
				SafeAddField(fields, imageTimestamp);

				var imageUrl =
					new MappingField
					{
						Attribute =
							new CrmPropertyAttribute
							{
								IsLookup = false,
								LogicalName = $"{image.LogicalName}_url",
								IsEntityReferenceHelper = false
							},
						SchemaName = $"{image.SchemaName}_URL",
						DisplayName = $"{image.DisplayName}_URL",
						HybridName = $"{image.HybridName}_URL",
						Label = $"{image.Label}_URL",
						LocalizedLabels = image
							.LocalizedLabels.Select(
								e =>
								{
									var copy = e.Copy();
									copy.Label = $"{copy.Label}_URL";
									return copy;
								}).ToArray(),
						TargetTypeForCrmSvcUtil = "string",
						FieldType = AttributeTypeCode.String,
						IsValidForRead = true,
						IsValidForCreate = false,
						IsValidForUpdate = false,
						Description = " ", // CrmSvcUtil provides an empty description for this EntityImage_URL
						GetMethod = ""
					};
				SafeAddField(fields, imageUrl);
			}
		}

		private static void SafeAddField(List<MappingField> fields, MappingField image)
		{
			if (fields.All(f => f.DisplayName != image.DisplayName))
			{
				fields.Add(image);
			}
		}

		public MappingRelationshipMN[] RelationshipsManyToMany { get; set; }
	}
}
