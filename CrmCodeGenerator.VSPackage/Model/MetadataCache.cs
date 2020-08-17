#region Imports

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk.Metadata;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class MetadataCache
	{
		public PlatformFeature? PlatformFeatures;

		public IDictionary<Guid, MappingEntity> EntityMetadataCache;
		public IDictionary<Guid, Context> ContextCache;

		public IDictionary<string, int> EntityCodesCache;

		public IDictionary<string, LookupMetadata> LookupKeysMetadataCache
		{
			get
			{
				if (LookupKeysMetadataCacheSerialised == null)
				{
					return new Dictionary<string, LookupMetadata>();
				}

				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));

				using (var stream = new MemoryStream(LookupKeysMetadataCacheSerialised))
				using (var reader = XmlDictionaryReader
					.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
				{
					return (IDictionary<string, LookupMetadata>)serializer.ReadObject(reader)
						?? new Dictionary<string, LookupMetadata>();
				}
			}
			set
			{
				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));
				var stream = new MemoryStream();

				using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
				{
					serializer.WriteObject(writer, value);
				}

				LookupKeysMetadataCacheSerialised = stream.ToArray();
			}
		}

		public byte[] LookupKeysMetadataCacheSerialised;

		public IDictionary<string, LookupMetadata> BasicAttributesMetadataCache
		{
			get
			{
				if (BasicAttributesMetadataCacheSerialised == null)
				{
					return new Dictionary<string, LookupMetadata>();
				}

				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));

				using (var stream = new MemoryStream(BasicAttributesMetadataCacheSerialised))
				using (var reader = XmlDictionaryReader
					.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
				{
					return (IDictionary<string, LookupMetadata>)serializer.ReadObject(reader)
						?? new Dictionary<string, LookupMetadata>();
				}
			}
			set
			{
				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));
				var stream = new MemoryStream();

				using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
				{
					serializer.WriteObject(writer, value);
				}

				BasicAttributesMetadataCacheSerialised = stream.ToArray();
			}
		}

		public byte[] BasicAttributesMetadataCacheSerialised;

		public IDictionary<string, LookupMetadata> LookupEntitiesMetadataCache
		{
			get
			{
				if (LookupEntitiesMetadataCacheSerialised == null)
				{
					return new Dictionary<string, LookupMetadata>();
				}

				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));

				using (var stream = new MemoryStream(LookupEntitiesMetadataCacheSerialised))
				using (var reader = XmlDictionaryReader
					.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
				{
					return (IDictionary<string, LookupMetadata>)serializer.ReadObject(reader)
						?? new Dictionary<string, LookupMetadata>();
				}
			}
			set
			{
				var serializer = new DataContractSerializer(typeof(IDictionary<string, LookupMetadata>));
				var stream = new MemoryStream();

				using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
				{
					serializer.WriteObject(writer, value);
				}

				LookupEntitiesMetadataCacheSerialised = stream.ToArray();
			}
		}

		public byte[] LookupEntitiesMetadataCacheSerialised;

		// credit: http://stackoverflow.com/a/12845153/1919456
		public List<EntityMetadata> ProfileEntityMetadataCache
		{
			get
			{
				if (ProfileEntityMetadataCacheSerialised == null)
				{
					return new List<EntityMetadata>();
				}

				var serializer = new DataContractSerializer(typeof(List<EntityMetadata>));

				using (var stream = new MemoryStream(ProfileEntityMetadataCacheSerialised))
				using (var reader = XmlDictionaryReader
					.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
				{
					return (List<EntityMetadata>)serializer.ReadObject(reader)
						?? new List<EntityMetadata>();
				}
			}
			set
			{
				var serializer = new DataContractSerializer(typeof(List<EntityMetadata>));
				var stream = new MemoryStream();

				using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
				{
					serializer.WriteObject(writer, value);
				}

				ProfileEntityMetadataCacheSerialised = stream.ToArray();
			}
		}

		public byte[] ProfileEntityMetadataCacheSerialised;

		public IDictionary<string, EntityMetadata> ProfileAttributeMetadataCache
		{
			get
			{
				if (ProfileAttributeMetadataCacheSerialised == null)
				{
					return new Dictionary<string, EntityMetadata>();
				}

				var serializer = new DataContractSerializer(typeof(IDictionary<string, EntityMetadata>));

				using (var stream = new MemoryStream(ProfileAttributeMetadataCacheSerialised))
				using (var reader = XmlDictionaryReader
					.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max))
				{
					return (IDictionary<string, EntityMetadata>)serializer.ReadObject(reader)
						?? new Dictionary<string, EntityMetadata>();
				}
			}
			set
			{
				var serializer = new DataContractSerializer(typeof(IDictionary<string, EntityMetadata>));
				var stream = new MemoryStream();

				using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
				{
					serializer.WriteObject(writer, value);
				}

				ProfileAttributeMetadataCacheSerialised = stream.ToArray();
			}
		}
		public byte[] ProfileAttributeMetadataCacheSerialised;

		public MetadataCache()
		{
			InitFields();
		}

		public void OnDeserialization()
		{
			InitFields();
		}

		private void InitFields()
		{
			ProfileEntityMetadataCache = ProfileEntityMetadataCache ?? new List<EntityMetadata>();
			ProfileAttributeMetadataCache = ProfileAttributeMetadataCache ?? new Dictionary<string, EntityMetadata>();

			LookupEntitiesMetadataCache = LookupEntitiesMetadataCache ?? new Dictionary<string, LookupMetadata>();

			EntityMetadataCache = EntityMetadataCache ?? new Dictionary<Guid, MappingEntity>();
			ContextCache = ContextCache ?? new Dictionary<Guid, Context>();
		}

		public Context GetCachedContext(Guid id)
		{
			ContextCache.TryGetValue(id, out var context);
			return context;
		}
	}
}
