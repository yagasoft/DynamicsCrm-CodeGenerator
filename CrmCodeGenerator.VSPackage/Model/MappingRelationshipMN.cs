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
	public class MappingRelationshipMN : ICloneable
	{
		public Guid? MetadataId { get; set; }
		public CrmRelationshipAttribute Attribute { get; set; }

		public string DisplayName { get; set; }
		public string SchemaName { get; set; }
		public string HybridName { get; set; }
		public string FriendlyName { get; set; }
		public string ForeignKey { get; set; }
		public string PrivateName { get; set; }
		public string EntityRole { get; set; }
		public string Type { get; set; }
		public bool IsSelfReferenced { get; set; }

		public MappingEntity FromEntity { get; set; }
		public MappingEntity ToEntity { get; set; }
		public MappingEntity IntersectingEntity { get; set; }

		public static void UpdateCache(List<ManyToManyRelationshipMetadata> relMetadataList, MappingEntity mappingEntity
			, string thisEntityLogicalName)
		{
			var entityRelationshipsNm = new List<MappingRelationshipMN>();

			if (mappingEntity.RelationshipsManyToMany != null)
			{
				entityRelationshipsNm = mappingEntity.RelationshipsManyToMany.ToList();
			}

			// update modified
			var modifiedRelations = entityRelationshipsNm
				.Where(relation => relMetadataList.Exists(relMeta => relMeta.MetadataId == relation.MetadataId)).ToList();
			modifiedRelations.ForEach(
				rel => Parse(relMetadataList.First(relMeta => relMeta.MetadataId == rel.MetadataId), rel, thisEntityLogicalName));

			// add new
			var newRelMeta = relMetadataList
				.Where(relMeta => entityRelationshipsNm.All(relation => relation.MetadataId != relMeta.MetadataId))
				.ToList();
			entityRelationshipsNm.AddRange(newRelMeta.Select(relMeta => Parse(relMeta, null, thisEntityLogicalName)).ToList());

			mappingEntity.RelationshipsManyToMany = entityRelationshipsNm.ToArray();
		}

		public static MappingRelationshipMN Parse(ManyToManyRelationshipMetadata rel,
			MappingRelationshipMN relationshipManyToMany,
			string thisEntityLogicalName)
		{
			relationshipManyToMany = relationshipManyToMany ?? new MappingRelationshipMN();

			if (rel.Entity1LogicalName != null)
			{
				if (rel.Entity1LogicalName == thisEntityLogicalName)
				{
					relationshipManyToMany.Attribute = relationshipManyToMany.Attribute ?? new CrmRelationshipAttribute();

					relationshipManyToMany.Attribute.FromEntity = rel.Entity1LogicalName ?? relationshipManyToMany.Attribute.FromEntity;
					relationshipManyToMany.Attribute.FromKey = rel.Entity1IntersectAttribute
					                                           ?? relationshipManyToMany.Attribute.FromKey;
					relationshipManyToMany.Attribute.ToEntity = rel.Entity2LogicalName ?? relationshipManyToMany.Attribute.ToEntity;
					relationshipManyToMany.Attribute.ToKey = rel.Entity2IntersectAttribute ?? relationshipManyToMany.Attribute.ToKey;
				}
				else
				{
					relationshipManyToMany.Attribute = relationshipManyToMany.Attribute ?? new CrmRelationshipAttribute();

					relationshipManyToMany.Attribute.ToEntity = rel.Entity1LogicalName ?? relationshipManyToMany.Attribute.ToEntity;
					relationshipManyToMany.Attribute.ToKey = rel.Entity1IntersectAttribute ?? relationshipManyToMany.Attribute.ToKey;
					relationshipManyToMany.Attribute.FromEntity = rel.Entity2LogicalName ?? relationshipManyToMany.Attribute.FromEntity;
					relationshipManyToMany.Attribute.FromKey = rel.Entity2IntersectAttribute
					                                           ?? relationshipManyToMany.Attribute.FromKey;
				}

				relationshipManyToMany.Attribute.IntersectingEntity = rel.IntersectEntityName
				                                                      ?? relationshipManyToMany.Attribute.IntersectingEntity;
			}

			relationshipManyToMany.EntityRole = "null";

			if (rel.SchemaName != null)
			{
				relationshipManyToMany.SchemaName = rel.SchemaName;
				relationshipManyToMany.DisplayName = rel.SchemaName;
				relationshipManyToMany.HybridName = Naming.GetProperVariableName(rel.SchemaName, false) + "_NN";
				relationshipManyToMany.PrivateName = "_nn" + Naming.GetEntityPropertyPrivateName(rel.SchemaName);
			}

			if (rel.Entity1LogicalName != null && rel.Entity2LogicalName != null
				&& rel.Entity1LogicalName == rel.Entity2LogicalName && rel.Entity1LogicalName == thisEntityLogicalName)
			{
				relationshipManyToMany.DisplayName = "Referenced_" + relationshipManyToMany.DisplayName;
				relationshipManyToMany.EntityRole = "Microsoft.Xrm.Sdk.EntityRole.Referenced";
				relationshipManyToMany.IsSelfReferenced = true;
			}

			if (relationshipManyToMany.DisplayName == thisEntityLogicalName)
			{
				relationshipManyToMany.DisplayName += "1"; // this is what CrmSvcUtil does
			}

			relationshipManyToMany.ForeignKey = Naming.GetProperVariableName(relationshipManyToMany.Attribute.ToKey, false);
			relationshipManyToMany.Type = relationshipManyToMany.Attribute.ToEntity;

			relationshipManyToMany.MetadataId = rel.MetadataId;

			return relationshipManyToMany;
		}

		public object Clone()
		{
			var newPerson = (MappingRelationshipMN) MemberwiseClone();
			newPerson.Attribute = (CrmRelationshipAttribute) Attribute.Clone();
			return newPerson;
		}
	}
}
