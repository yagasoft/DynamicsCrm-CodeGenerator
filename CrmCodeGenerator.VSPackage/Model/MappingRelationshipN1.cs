#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk.Metadata;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class MappingRelationshipN1
	{
		public Guid? MetadataId { get; set; }
		public CrmRelationshipAttribute Attribute { get; set; }

		public string DisplayName { get; set; }
		public string SchemaName { get; set; }
		public string LogicalName { get; set; }
		public string HybridName { get; set; }
		public string FriendlyName { get; set; }
		public string ForeignKey { get; set; }
		public string PrivateName { get; set; }
		public string EntityRole { get; set; }
		public string Type { get; set; }

		public MappingEntity FromEntity { get; set; }
		public MappingEntity ToEntity { get; set; }
		public MappingField FromField { get; set; }
		public MappingField Property { get; set; }

		public static void UpdateCache(List<OneToManyRelationshipMetadata> relMetadataList, MappingEntity mappingEntity
			, MappingField[] properties)
		{
			var entityRelationshipsN1 = new List<MappingRelationshipN1>();

			if (mappingEntity.RelationshipsManyToOne != null)
			{
				entityRelationshipsN1 = mappingEntity.RelationshipsManyToOne.ToList();
			}

			// update modified
			var modifiedRelations = entityRelationshipsN1
				.Where(relation => relMetadataList.Exists(relMeta => relMeta.MetadataId == relation.MetadataId)).ToList();
			modifiedRelations.ForEach(
				rel => Parse(relMetadataList.First(relMeta => relMeta.MetadataId == rel.MetadataId), rel, properties));

			// add new
			var newRelMeta = relMetadataList
				.Where(relMeta => entityRelationshipsN1.All(relation => relation.MetadataId != relMeta.MetadataId))
				.ToList();
			entityRelationshipsN1.AddRange(newRelMeta.Select(relMeta => Parse(relMeta, null, properties)).ToList());

			mappingEntity.RelationshipsManyToOne = entityRelationshipsN1.ToArray();
		}

		public static MappingRelationshipN1 Parse(OneToManyRelationshipMetadata rel,
			MappingRelationshipN1 relationshipOneToMany,
			MappingField[] properties)
		{
			relationshipOneToMany = relationshipOneToMany
			                        ?? new MappingRelationshipN1
			                           {
				                           Attribute = new CrmRelationshipAttribute(),
			                           };

			relationshipOneToMany.Attribute.ToEntity = rel.ReferencedEntity ?? relationshipOneToMany.Attribute.FromEntity;
			relationshipOneToMany.Attribute.ToKey = rel.ReferencedAttribute ?? relationshipOneToMany.Attribute.FromKey;
			relationshipOneToMany.Attribute.FromEntity = rel.ReferencingEntity ?? relationshipOneToMany.Attribute.ToEntity;
			relationshipOneToMany.Attribute.FromKey = rel.ReferencingAttribute ?? relationshipOneToMany.Attribute.ToKey;
			relationshipOneToMany.Attribute.IntersectingEntity = "";

			if (rel.ReferencingAttribute != null)
			{
				relationshipOneToMany.Property = properties.FirstOrDefault(p => string.Equals(p.Attribute.LogicalName, rel.ReferencingAttribute,
					StringComparison.CurrentCultureIgnoreCase));

				if (relationshipOneToMany.Property != null)
				{
					relationshipOneToMany.ForeignKey = relationshipOneToMany.Property.DisplayName ?? "_MISSING_KEY";
				}
			}

			if (rel.SchemaName != null)
			{
				relationshipOneToMany.DisplayName = rel.SchemaName;
				relationshipOneToMany.SchemaName = rel.SchemaName;
				relationshipOneToMany.PrivateName = "_n1" + Naming.GetEntityPropertyPrivateName(rel.SchemaName);
				relationshipOneToMany.HybridName = Naming.GetProperVariableName(rel.SchemaName, false) + "_N1";
			}

			relationshipOneToMany.LogicalName = rel.ReferencingAttribute ?? relationshipOneToMany.LogicalName;

			relationshipOneToMany.EntityRole = "null";
			relationshipOneToMany.Type = rel.ReferencedEntity;

			relationshipOneToMany.MetadataId = rel.MetadataId;

			if (rel.ReferencedEntity == rel.ReferencingEntity)
			{
				relationshipOneToMany.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referencing";
				relationshipOneToMany.DisplayName = "Referencing_" + relationshipOneToMany.DisplayName;
			}

			return relationshipOneToMany;
		}
	}
}
