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
	public class MappingRelationship1N
	{
		public Guid? MetadataId { get; set; }
		public CrmRelationshipAttribute Attribute { get; set; }
		public string DisplayName { get; set; }
		public string ForeignKey { get; set; }
		public string LogicalName { get; set; }
		public string SchemaName { get; set; }
		public string HybridName { get; set; }
		public string FriendlyName { get; set; }
		public string PrivateName { get; set; }
		public string EntityRole { get; set; }
		public string Type { get; set; }

		public MappingEntity FromEntity { get; set; }
		public MappingEntity ToEntity { get; set; }
		public MappingField ToField { get; set; }

		public static void UpdateCache(List<OneToManyRelationshipMetadata> relMetadataList, MappingEntity mappingEntity
			, MappingField[] properties)
		{
			var entityRelationships1N = new List<MappingRelationship1N>();

			if (mappingEntity.RelationshipsOneToMany != null)
			{
				entityRelationships1N = mappingEntity.RelationshipsOneToMany.ToList();
			}

			// update modified
			var modifiedRelations = entityRelationships1N
				.Where(relation => relMetadataList.Exists(relMeta => relMeta.MetadataId == relation.MetadataId)).ToList();
			modifiedRelations.ForEach(
				rel => Parse(relMetadataList.First(relMeta => relMeta.MetadataId == rel.MetadataId), rel, properties));

			// add new
			var newRelMeta = relMetadataList
				.Where(relMeta => entityRelationships1N.All(relation => relation.MetadataId != relMeta.MetadataId))
				.ToList();
			entityRelationships1N.AddRange(newRelMeta.Select(relMeta => Parse(relMeta, null, properties)).ToList());

			mappingEntity.RelationshipsOneToMany = entityRelationships1N.ToArray();
		}

		public static MappingRelationship1N Parse(OneToManyRelationshipMetadata rel,
			MappingRelationship1N relationshipOneToMany,
			MappingField[] properties)
		{
			string propertyName = null;

			if (relationshipOneToMany != null)
			{
				propertyName = relationshipOneToMany.ForeignKey;
			}
			
			if (rel.ReferencedAttribute != null)
			{
				propertyName = properties.First(p => string.Equals(p.Attribute.LogicalName, rel.ReferencedAttribute,
					StringComparison.CurrentCultureIgnoreCase)).DisplayName;
			}

			relationshipOneToMany = relationshipOneToMany
			                        ?? new MappingRelationship1N
			                           {
				                           Attribute = new CrmRelationshipAttribute(),
			                           };

			relationshipOneToMany.Attribute.FromEntity = rel.ReferencedEntity ?? relationshipOneToMany.Attribute.FromEntity;
			relationshipOneToMany.Attribute.FromKey = rel.ReferencedAttribute ?? relationshipOneToMany.Attribute.FromKey;
			relationshipOneToMany.Attribute.ToEntity = rel.ReferencingEntity ?? relationshipOneToMany.Attribute.ToEntity;
			relationshipOneToMany.Attribute.ToKey = rel.ReferencingAttribute ?? relationshipOneToMany.Attribute.ToKey;
			relationshipOneToMany.Attribute.IntersectingEntity = "";
			relationshipOneToMany.ForeignKey = propertyName ?? "_MISSING_KEY";

			if (rel.SchemaName != null)
			{
				relationshipOneToMany.SchemaName = rel.SchemaName;
				relationshipOneToMany.DisplayName = rel.SchemaName;
				relationshipOneToMany.PrivateName = Naming.GetEntityPropertyPrivateName(rel.SchemaName);
				relationshipOneToMany.HybridName = Naming.GetPluralName(Naming.GetProperVariableName(rel.SchemaName, false));
			}

			relationshipOneToMany.LogicalName = rel.ReferencingAttribute ?? relationshipOneToMany.LogicalName;

			relationshipOneToMany.EntityRole = "null";
			relationshipOneToMany.Type = rel.ReferencingEntity;

			relationshipOneToMany.MetadataId = rel.MetadataId;

			if (rel.ReferencedEntity != null && rel.ReferencingEntity != null
			    && rel.ReferencedEntity == rel.ReferencingEntity)
			{
				relationshipOneToMany.DisplayName = "Referenced_" + relationshipOneToMany.DisplayName;
				relationshipOneToMany.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referenced";
			}

			return relationshipOneToMany;
		}
	}
}
