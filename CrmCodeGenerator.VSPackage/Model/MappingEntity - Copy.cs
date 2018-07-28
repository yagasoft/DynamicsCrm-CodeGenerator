#region File header

// Project / File: CrmCodeGenerator.VSPackage / MappingEntity.cs
//          Authors / Contributors:
//                      Ahmed el-Sawalhy (LINK Development - MBS)
//        Created: 2015 / 06 / 09
//       Modified: 2015 / 06 / 12

#endregion

#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class MappingEntity
	{
		public Guid? MetadataId { get; set; }
		public string ServerStamp { get; set; }
		public CrmEntityAttribute Attribute { get; set; }
		public bool IsIntersect { get; set; }
		public Nullable<int> TypeCode { get; set; }
		public MappingField[] Fields { get; set; }
		public MappingEnum States { get; set; }
		public MappingEnum[] Enums { get; set; }
		public MappingRelationship1N[] RelationshipsOneToMany { get; set; }
		public MappingRelationshipN1[] RelationshipsManyToOne { get; set; }

		public MappingAction[] Actions { get; set; }

		public string LogicalName
		{
			get { return Attribute.LogicalName; }
		}

		public string SchemaName { get; set; }
		public string DisplayName { get; set; }
        public string Label { get; set; }
		public string HybridName { get; set; }
		public string StateName { get; set; }
		public MappingField PrimaryKey { get; set; }
		public string PrimaryKeyProperty { get; set; }
		public string PrimaryNameAttribute { get; set; }
		public string Description { get; set; }

		public string DescriptionXmlSafe
		{
			get { return Naming.XmlEscape(Description); }
		}

		public string Plural
		{
			get { return Naming.GetPluralName(DisplayName); }
		}

		public MappingEntity()
		{
			Description = "";
		}

		public static void UpdateCache(List<EntityMetadata> entityMetadataList
			, IDictionary<Guid, MappingEntity> mappingEntities, string serverStamp, DeletedMetadataCollection deletedMetadata)
		{
			ParseDeleted(mappingEntities, serverStamp, deletedMetadata);

			// update modified entities
			var modifiedEntities =
				entityMetadataList.Where(entity => entity.MetadataId.HasValue && mappingEntities.ContainsKey(entity.MetadataId.Value)).ToList();
			modifiedEntities.ForEach(
				entity => UpdateMappingEntity(entity, entity.MetadataId.HasValue ? mappingEntities[entity.MetadataId.Value] : null, serverStamp));

			var newEntities = entityMetadataList.Where(entity => entity.MetadataId.HasValue && !mappingEntities.ContainsKey(entity.MetadataId.Value)).ToList();
			newEntities.ForEach(entity => mappingEntities.Add(entity.MetadataId.Value, GetMappingEntity(entity, serverStamp)));
		}

		private static void ParseDeleted(IDictionary<Guid, MappingEntity> mappingEntities, string serverStamp
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
							         mappingEntities.Values.FirstOrDefault(entityQ => entityQ.Fields.ToList().Any(field => field.MetadataId == guid));

						         if (entity == null)
						         {
							         return;
						         }

						         entity.Fields = entity.Fields.ToList().Where(field => field.MetadataId != guid).ToArray();
								 entity.Enums = entity.Fields
									 .Where(a => a.EnumData != null)
									 .Select(a => a.EnumData).ToArray();
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
							         mappingEntities.Values.FirstOrDefault(entityQ => entityQ.RelationshipsOneToMany.ToList().Any(relation => relation.MetadataId == guid));

						         if (entity == null)
						         {
							         return;
						         }

						         entity.RelationshipsOneToMany = entity.RelationshipsOneToMany.ToList().Where(relation => relation.MetadataId != guid).ToArray();
								 entity.ServerStamp = serverStamp;

								 // N:1
								 entity =
							         mappingEntities.Values.FirstOrDefault(entityQ => entityQ.RelationshipsManyToOne.ToList().Any(relation => relation.MetadataId == guid));

								 if (entity == null)
								 {
									 return;
								 }

								 entity.RelationshipsManyToOne = entity.RelationshipsManyToOne.ToList().Where(relation => relation.MetadataId != guid).ToArray();
								 entity.ServerStamp = serverStamp;

								 // M:N
								 entity =
							         mappingEntities.Values.FirstOrDefault(entityQ => entityQ.RelationshipsManyToMany.ToList().Any(relation => relation.MetadataId == guid));

								 if (entity == null)
								 {
									 return;
								 }

								 entity.RelationshipsManyToMany = entity.RelationshipsManyToMany.ToList().Where(relation => relation.MetadataId != guid).ToArray();
								 entity.ServerStamp = serverStamp;
							 });
			}
		}

		private static void UpdateMappingEntity(EntityMetadata entityMetadata, MappingEntity entity, string serverStamp)
		{
			entity.ServerStamp = serverStamp;

			entity.TypeCode = entityMetadata.ObjectTypeCode ?? entity.TypeCode;
			entity.Attribute.LogicalName = entityMetadata.LogicalName ?? entity.Attribute.LogicalName;
			entity.IsIntersect = (entityMetadata.IsIntersect ?? entity.IsIntersect);
			entity.Attribute.PrimaryKey = entityMetadata.PrimaryIdAttribute ?? entity.Attribute.PrimaryKey;

			// entity.DisplayName = Helper.GetProperVariableName(entityMetadata.SchemaName);
            if (entityMetadata.DisplayName != null)
            {
                entity.Label = entityMetadata.DisplayName.UserLocalizedLabel != null
					? entityMetadata.DisplayName.UserLocalizedLabel.Label
					: entityMetadata.SchemaName;
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

			if (entityMetadata.Description != null)
			{
				if (entityMetadata.Description.UserLocalizedLabel != null)
				{
					entity.Description = entityMetadata.Description.UserLocalizedLabel.Label;
				}
			}

			>>>>>>>>> MappingField.UpdateCache(entityMetadata.Attributes.ToList(), entity);

			var fields = entity.Fields.ToList();

			fields.ForEach(f =>
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

			AddEntityImageCrm2013(fields);
			AddLookupFields(fields);

			entity.Fields = fields.ToArray();
			var states = entity.Fields.FirstOrDefault(a => a.IsStateCode);
			entity.States = (states != null) ? states.EnumData : null;
			entity.Enums = entity.Fields
				.Where(a => a.EnumData != null)
				.Select(a => a.EnumData).ToArray();

			entity.PrimaryKey = entity.Fields.First(f => f.Attribute.LogicalName == entity.Attribute.PrimaryKey);
			entity.PrimaryKeyProperty = entity.PrimaryKey.DisplayName;
			entity.PrimaryNameAttribute = entityMetadata.PrimaryNameAttribute ?? entity.PrimaryNameAttribute;

			>>>>>>>>>>>>>>>>>
			MappingRelationship1N.UpdateCache(entityMetadata.OneToManyRelationships.ToList(), entity, entity.Fields);
			MappingRelationshipN1.UpdateCache(entityMetadata.ManyToOneRelationships.ToList(), entity, entity.Fields);
			MappingRelationshipMN.UpdateCache(entityMetadata.ManyToManyRelationships.ToList(), entity, entity.LogicalName);

			>>>>>>>>>>
			var relationshipsManyToMany = entity.RelationshipsManyToMany.ToList();

			var selfReferenced = entity.RelationshipsManyToMany.Where(r => r.IsSelfReferenced).ToList();

			foreach (var referenced in selfReferenced)
			{
				var referencing = (MappingRelationshipMN)referenced.Clone();
				referencing.DisplayName = "Referencing" + Naming.GetProperVariableName(referenced.SchemaName);
				referencing.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referencing";
				relationshipsManyToMany.Add(referencing);
			}

			entity.RelationshipsManyToMany = relationshipsManyToMany.OrderBy(r => r.DisplayName).ToArray();
		}

		private static MappingEntity GetMappingEntity(EntityMetadata entityMetadata, string serverStamp)
		{
			var entity = new MappingEntity();

			entity.MetadataId = entityMetadata.MetadataId;
			entity.ServerStamp = serverStamp;

			entity.Attribute = new CrmEntityAttribute();
			entity.TypeCode = entityMetadata.ObjectTypeCode;
			entity.Attribute.LogicalName = entityMetadata.LogicalName;
			entity.IsIntersect = entityMetadata.IsIntersect ?? false;
			entity.Attribute.PrimaryKey = entityMetadata.PrimaryIdAttribute;

			// entity.DisplayName = Helper.GetProperVariableName(entityMetadata.SchemaName);
		    if (entityMetadata.DisplayName != null)
		    {
		        entity.Label = entityMetadata.DisplayName.UserLocalizedLabel != null
					? entityMetadata.DisplayName.UserLocalizedLabel.Label
					: entityMetadata.SchemaName;
		    }

			entity.SchemaName = entityMetadata.SchemaName;
			entity.DisplayName = Naming.GetProperEntityName(entityMetadata.SchemaName);
			entity.HybridName = Naming.GetProperHybridName(entityMetadata.SchemaName, entityMetadata.LogicalName);
			entity.StateName = entity.HybridName + "State";

			if (entityMetadata.Description != null)
			{
				if (entityMetadata.Description.UserLocalizedLabel != null)
				{
					entity.Description = entityMetadata.Description.UserLocalizedLabel.Label;
				}
			}

			var fields = entityMetadata.Attributes
				.Where(a => a.AttributeOf == null)
				.Select(a => MappingField.GetMappingField(a, entity)).ToList();

			fields.ForEach(f =>
			               {
				               if (f.DisplayName == entity.DisplayName)
				               {
					               f.DisplayName += "1";
				               }

				               if (f.HybridName == entity.HybridName)
				               {
					               f.HybridName += "1";
				               }
			               }
				);

			AddEntityImageCrm2013(fields);
			AddLookupFields(fields);

			entity.Fields = fields.ToArray();
			entity.States =
				entityMetadata.Attributes.Where(a => a is StateAttributeMetadata)
					.Select(a => MappingEnum.GetMappingEnum(a as EnumAttributeMetadata))
					.FirstOrDefault();
			entity.Enums = entityMetadata.Attributes
				.Where(a => a is PicklistAttributeMetadata || a is StateAttributeMetadata || a is StatusAttributeMetadata)
				.Select(a => MappingEnum.GetMappingEnum(a as EnumAttributeMetadata)).ToArray();

			entity.PrimaryKey = entity.Fields.First(f => f.Attribute.LogicalName == entity.Attribute.PrimaryKey);
			entity.PrimaryKeyProperty = entity.PrimaryKey.DisplayName;
			entity.PrimaryNameAttribute = entityMetadata.PrimaryNameAttribute;

			entity.RelationshipsOneToMany = entityMetadata.OneToManyRelationships.Select(r =>
			                                                                             MappingRelationship1N.Parse(r,
				                                                                             entity.Fields)).ToArray();

			entity.RelationshipsManyToOne = entityMetadata.ManyToOneRelationships.Select(r =>
			                                                                             MappingRelationshipN1.Parse(r,
				                                                                             entity.Fields)).ToArray();

			var relationshipsManyToMany =
				entityMetadata.ManyToManyRelationships.Select(r => MappingRelationshipMN.Parse(r, entity.LogicalName)).ToList();
			var selfReferenced = relationshipsManyToMany.Where(r => r.IsSelfReferenced).ToList();

			foreach (var referenced in selfReferenced)
			{
				var referencing = (MappingRelationshipMN) referenced.Clone();
				referencing.DisplayName = "Referencing" + Naming.GetProperVariableName(referenced.SchemaName);
				referencing.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referencing";
				relationshipsManyToMany.Add(referencing);
			}

			entity.RelationshipsManyToMany = relationshipsManyToMany.OrderBy(r => r.DisplayName).ToArray();

			return entity;
		}

		private static void AddLookupFields(List<MappingField> fields)
		{
			var fieldsIterator = fields.Where(e => e.Attribute.IsLookup).ToArray();
			foreach (var lookup in fieldsIterator)
			{
				var nameField = new MappingField
				                {
					                Attribute = new CrmPropertyAttribute
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

				if (!string.IsNullOrEmpty(lookup.LookupSingleType))
				{
					continue;
				}

				var typeField = new MappingField
				                {
					                Attribute = new CrmPropertyAttribute
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

		private static void AddEntityImageCrm2013(List<MappingField> fields)
		{
			if (!fields.Any(f => f.DisplayName.Equals("EntityImageId")))
			{
				return;
			}

			var image = new MappingField
			            {
				            Attribute = new CrmPropertyAttribute
				                        {
					                        IsLookup = false,
					                        LogicalName = "entityimage",
					                        IsEntityReferenceHelper = false
				                        },
				            SchemaName = "EntityImage",
				            DisplayName = "EntityImage",
				            HybridName = "EntityImage",
				            TargetTypeForCrmSvcUtil = "byte[]",
				            IsValidForUpdate = true,
				            Description = "",
				            // TODO there is an Description for this entityimage, Need to figure out how to read it from the server
				            GetMethod = ""
			            };
			SafeAddField(fields, image);

			var imageTimestamp = new MappingField
			                     {
				                     Attribute = new CrmPropertyAttribute
				                                 {
					                                 IsLookup = false,
					                                 LogicalName = "entityimage_timestamp",
					                                 IsEntityReferenceHelper = false
				                                 },
				                     SchemaName = "EntityImage_Timestamp",
				                     DisplayName = "EntityImage_Timestamp",
				                     HybridName = "EntityImage_Timestamp",
				                     TargetTypeForCrmSvcUtil = "System.Nullable<long>",
				                     FieldType = AttributeTypeCode.BigInt,
				                     IsValidForUpdate = false,
				                     IsValidForCreate = false,
				                     Description = " ", // CrmSvcUtil provides an empty description for this EntityImage_TimeStamp
				                     GetMethod = ""
			                     };
			SafeAddField(fields, imageTimestamp);

			var imageUrl = new MappingField
			               {
				               Attribute = new CrmPropertyAttribute
				                           {
					                           IsLookup = false,
					                           LogicalName = "entityimage_url",
					                           IsEntityReferenceHelper = false
				                           },
				               SchemaName = "EntityImage_URL",
				               DisplayName = "EntityImage_URL",
				               HybridName = "EntityImage_URL",
				               TargetTypeForCrmSvcUtil = "string",
				               FieldType = AttributeTypeCode.String,
				               IsValidForUpdate = false,
				               IsValidForCreate = false,
				               Description = " ", // CrmSvcUtil provides an empty description for this EntityImage_URL
				               GetMethod = ""
			               };
			SafeAddField(fields, imageUrl);
		}

		private static void SafeAddField(List<MappingField> fields, MappingField image)
		{
			if (!fields.Any(f => f.DisplayName == image.DisplayName))
			{
				fields.Add(image);
			}
		}

		public MappingRelationshipMN[] RelationshipsManyToMany { get; set; }
	}
}
