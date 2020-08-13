#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using CrmPluginEntities;
using LinkDev.WebService.LogQueue;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Client.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Yagasoft.Libraries.Common;
using static CrmCodeGenerator.VSPackage.Helpers.MetadataCacheHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public delegate void MapperHandler(object sender, MapperEventArgs e);

	public class Mapper : INotifyPropertyChanged
	{
		#region Properties

		public readonly object LoggingLock = new object();

		public SettingsNew Settings { get; set; }

		public int Progress
		{
			get { return progress; }
			set
			{
				progress = value;
				OnPropertyChanged();
			}
		}

		public string ProgressMessage { get; set; }

		private List<string> workingOnEntities;

		public List<string> WorkingOnEntities
		{
			get { return workingOnEntities; }
			set
			{
				workingOnEntities = value;
				OnPropertyChanged();
			}
		}

		private string logMessage;

		public string LogMessage
		{
			get { return logMessage; }
			set
			{
				logMessage = value;
				OnPropertyChanged();
			}
		}

		private Exception error;

		public Exception Error
		{
			get { return error; }
			set
			{
				error = value;
				OnPropertyChanged();
			}
		}

		private Context context;

		public Context Context
		{
			get { return context; }
			set
			{
				context = value;
				OnPropertyChanged();
			}
		}

		private bool cancelMapping;
		private int progress;

		public bool CancelMapping
		{
			get { return cancelMapping; }
			set
			{
				cancelMapping = value;
				OnPropertyChanged();
			}
		}

		public List<MappingAction> Actions { get; set; } = new List<MappingAction>();

		public List<int> Languages { get; set; }
		
		#endregion

		private MetadataCache metadataCache;
		private Thread langThread;
		private Thread lookupKeysThread;
		private Thread actionsThread;
		
		#region event handler

		public event MapperHandler Message;

		protected void OnMessage(string message, string extendedMessage = "")
		{
			lock (LoggingLock)
			{
				LogMessage = message + (string.IsNullOrEmpty(extendedMessage) ? "" : " => " + extendedMessage);
			}

			Message?.Invoke(this, new MapperEventArgs { Message = message, MessageExtended = extendedMessage });
		}

		#endregion

		#region ctor

		public Mapper()
		{
		}

		public Mapper(SettingsNew settings)
		{
			Settings = settings;
		}

		#endregion

		public void MapContext(bool useCached = false)
		{
			try
			{
				Error = null;

				metadataCache = GetMetadataCache(Settings.ConnectionString);
				metadataCache.Require(nameof(metadataCache));

				var contextP = metadataCache.GetCachedContext(Settings.Id);

				// check if caching is going to be used
				if (useCached && contextP != null)
				{
					Context = contextP;
					return;
				}

				if (CancelMapping)
				{
					return;
				}

				var contextT = new Context();

				langThread = null;

				if (Settings.LookupLabelsEntitiesSelected.Any())
				{
					langThread = new Thread(
						() =>
						{
							Status.Update("Fetching languages ... ");

							using (var service = ConnectionHelper.GetConnection(Settings))
							{
								Languages = ((RetrieveAvailableLanguagesResponse)
									service.Execute(new RetrieveAvailableLanguagesRequest()))
									.LocaleIds.ToList();
							}

							Status.Update(">>> Finished fetching languages.");
						});
					langThread.Start();
				}
				
				if (CancelMapping)
				{
					return;
				}

				var originalSelectedEntities = Settings.EntitiesSelected
					.Where(entity => !string.IsNullOrEmpty(entity)).ToList();

				#region Actions

				actionsThread = null;

				if (Settings.GenerateGlobalActions
					|| Settings.SelectedActions?.Any(e => e.Value?.Any() == true) == true)
				{
					actionsThread = new Thread(
						() =>
						{
							Status.Update("Fetching Actions ... ");

							Actions = RetrieveActions(originalSelectedEntities
								.Select(entity => Settings.SelectedActions.FirstNotNullOrDefault(entity))
								.Where(e => e?.Any() == true)
								.SelectMany(e => e)
								.Distinct().ToArray()).ToList();

							Status.Update(">>> Finished fetching Actions.");
						});
					actionsThread.Start();
				}

				#endregion

				if (CancelMapping)
				{
					return;
				}

				contextT.Entities = GetEntities(originalSelectedEntities);

				if (CancelMapping)
				{
					return;
				}

				if (actionsThread?.IsAlive == true)
				{
					Status.Update("Waiting for Actions thread ... ");
				}

				// wait for retrieving actions to finish
				actionsThread?.Join();

				if (CancelMapping)
				{
					return;
				}

				Status.Update("Parsing Entity Actions ... ");

				foreach (var entity in originalSelectedEntities)
				{
					var mappingEntity = contextT.Entities.FirstOrDefault(e => e.LogicalName == entity);

					if (mappingEntity == null)
					{
						continue;
					}

					mappingEntity.Actions = Actions.Where(action => entity == action.TargetEntityName).ToArray();
				}

				Status.Update(">>> Finished parsing Entity Actions.");

				if (CancelMapping)
				{
					return;
				}

				// add actions
				Status.Update("Parsing Global Actions ... ");
				contextT.GlobalActions = Actions.Where(action => action.TargetEntityName == "none").ToArray();
				Status.Update(">>> Finished parsing Global Actions.");

				if (CancelMapping)
				{
					return;
				}

				if (langThread?.IsAlive == true)
				{
					Status.Update("Waiting for Languages thread ... ");
				}

				// wait for retrieving languages to finish
				langThread?.Join();

				contextT.Languages = Languages ?? new List<int> {1033};

				if (CancelMapping)
				{
					return;
				}

				if (lookupKeysThread?.IsAlive == true)
				{
					Status.Update("Waiting for Alternate Keys thread ... ");
				}

				// wait for retrieving Alternate Keys to finish
				lookupKeysThread?.Join();

				if (CancelMapping)
				{
					return;
				}

				SortEntities(contextT);

				contextT.EntityDataFilterArray = Settings.EntityDataFilterArray;

				Context = contextT;
			}
			catch (Exception ex)
			{
				Error = ex;
				throw;
			}
		}

		public void SortEntities(Context contextL)
		{
			contextL.Entities = contextL.Entities.OrderBy(e => e.DisplayName).ToArray();

			foreach (var e in contextL.Entities)
			{
				e.Enums = e.Enums.OrderBy(en => en.DisplayName).ToArray();
				e.Fields = e.Fields.OrderBy(f => f.DisplayName).ToArray();
				e.RelationshipsOneToMany = e.RelationshipsOneToMany.OrderBy(r => r.SchemaName).ToArray();
				e.RelationshipsManyToOne = e.RelationshipsManyToOne.OrderBy(r => r.SchemaName).ToArray();
				e.RelationshipsManyToMany = e.RelationshipsManyToMany.OrderBy(r => r.SchemaName).ToArray();
			}
		}

		internal MappingEntity[] GetEntities(List<string> originalSelectedEntities)
		{
			OnMessage("Gathering metadata, this may take a few minutes...");

			var threadCount = Settings.Threads;

			// group entities by their server stamp to fetch them together in bulk
			// if not stamp is found, mark it
			Status.Update("Grouping entities by last server stamp ... ");

			var groupedEntities =
				originalSelectedEntities.GroupBy(
					entity =>
					{
						try
						{
							var cachedEntity = metadataCache.EntityMetadataCache
								.FirstOrDefault(e => e.Value.LogicalName == entity);

							return cachedEntity.Value == null
									   ? "NO_CACHE"
									   : cachedEntity.Value.ServerStamp;
						}
						catch
						{
							return "NO_CACHE";
						}
					}).ToList();

			Status.Update(">>> Finished grouping entities.");

			#region Get metadata

			var totalTaken = 0;

			// go over each group of entities
			Parallel.ForEach(groupedEntities,
				new ParallelOptions
				  {
					  MaxDegreeOfParallelism = threadCount
				  },
				group =>
				  {
					  try
					  {
						  var takenCount = 0;
						  var tempGroupedEntities = group.ToList();

						  #region Counts

						  var threadCap = new int[threadCount];

						  // calculate how many entities to take per thread
						  for (var i = 0; i < tempGroupedEntities.Count; i++)
						  {
							  threadCap[i % threadCount] = Math.Min(threadCap[i % threadCount] + 1, Settings.EntitiesPerThread);
						  }

						  var takeCount = 0;
						  var remaining = tempGroupedEntities.Count;

						  // calculate how many iterations to make
						  while (remaining > 0)
						  {
							  remaining -= threadCap[takeCount % threadCount];
							  takeCount++;
						  }

						  #endregion

						  #region Fetch metadata

						  // parallelise the fetch process
						  Parallel.For(0, takeCount,
							  new ParallelOptions
								{
									MaxDegreeOfParallelism = threadCount
								},
							  (index, state) =>
								{
									if (CancelMapping || Error != null)
									{
										state.Stop();
										throw new OperationCanceledException("Mapping cancelled.");
									}

									try
									{
										List<string> tempSelectedEntities;

										// page the fetch process
										lock (tempGroupedEntities)
										{
											tempSelectedEntities = tempGroupedEntities.Skip(takenCount)
												.Take(threadCap[index % threadCount]).ToList();

											WorkingOnEntities = tempSelectedEntities;
											var joinedEntities = string.Join(", ", tempSelectedEntities);
											ProgressMessage = ("Fetching: " + joinedEntities.Substring(0,
												Math.Min(joinedEntities.Length - 1, 59)) + " ...").PadRight(80);
											Progress = Math.Max(Progress, 1);

											takenCount += tempSelectedEntities.Count;
										}

										// fetch the entities, passing the last known stamp, and null if none was found
										var entities = GetMetadata(tempSelectedEntities.ToArray(),
											group.Key == "NO_CACHE" ? null : group.Key);

										// make sure we have only the entities we selected
										var newUpdatedEntities = entities.EntityMetadata
											.Where(r => tempGroupedEntities.Contains(r.LogicalName))
											.Where(
												r =>
												{
													if (Settings.IncludeNonStandard)
													{
														return true;
													}
													else
													{
														return !EntityHelper.NonStandard.Contains(r.LogicalName);
													}
												})
											.ToList();

										var foundEntities = newUpdatedEntities.Select(entity => entity.LogicalName).ToList();

										if (foundEntities.Any())
										{
											OnMessage(string.Format("Found {0} entities: {1}", newUpdatedEntities.Count,
												foundEntities.Aggregate((entity1, entity2) => entity1 + (entity2 != null ? ", " + entity2 : ""))));
										}
										else
										{
											OnMessage(string.Format("Found 0 entities -- cached or deleted ({0})",
												tempSelectedEntities.Aggregate((entity1, entity2) => entity1 + (entity2 != null ? ", " + entity2 : ""))));
										}

										// update the cache and fix relationships
										lock (metadataCache.EntityMetadataCache)
										{
											if (CancelMapping)
											{
												state.Stop();
												throw new OperationCanceledException("Mapping cancelled.");
											}

											MappingEntity.UpdateCache(newUpdatedEntities
												, metadataCache.EntityMetadataCache, entities.ServerVersionStamp
												, entities.DeletedMetadata, Settings.TitleCaseLogicalNames);

											totalTaken += tempSelectedEntities.Count;
											Progress = (int)((totalTaken / (double)originalSelectedEntities.Count) * 100.0);
										}
									}
									catch (Exception ex)
									{
										Error = ex;
										throw;
									}
								});

						  #endregion
					  }
					  catch
					  {
						  // ignored
					  }
				  });

			#endregion

			Progress = 100;

			if (Error != null)
			{
				throw Error;
			}

			var cachedEntities = metadataCache.EntityMetadataCache.Values.ToList();

			ParseRelationshipNames(cachedEntities);

			// filter the entities to only the selected ones
			var filteredEntities =
				cachedEntities.Where(entity => originalSelectedEntities.Contains(entity.LogicalName)).ToArray();

			OnMessage(">>> Finished gathering metadata.");

			if (CancelMapping)
			{
				throw new OperationCanceledException("Mapping cancelled.");
			}

			BuildLookupKeysData(filteredEntities);

			Status.Update("Creating missing filters ... ");

			foreach (var filterList in Settings.EntityDataFilterArray.EntityFilters.Select(filter => filter))
			{
				filterList.EntityFilterList.AddRange(filteredEntities.Select(entity => entity.LogicalName)
					.Except(filterList.EntityFilterList.Select(filter => filter.LogicalName))
					.Select(unfiltered => new EntityDataFilter(unfiltered)));
			}
			
			Status.Update(">>> Finished creating missing filters.");

			if (CancelMapping)
			{
				throw new OperationCanceledException("Mapping cancelled.");
			}

			Status.Update("Building lookup labels info ... ");

			if (Settings.LookupLabelsEntitiesSelected.Any())
			{
				ProcessLookupLabels(filteredEntities, threadCount);
			}

			Status.Update(">>> Finished building lookup labels info.");

			return filteredEntities;
		}

		private void BuildLookupKeysData(MappingEntity[] filteredEntities)
		{
			lookupKeysThread = null;

			var entityGroupedLookups = filteredEntities
				.SelectMany(e => e.Fields)
				.Where(e => e?.LookupData?.LookupSingleType != null)
				.GroupBy(e => e.LookupData.LookupSingleType).ToArray();

			if (entityGroupedLookups.Any())
			{
				lookupKeysThread = new Thread(
					() =>
					{
						Status.Update("Retrieving Alternate Key information ... ");

						var entities = entityGroupedLookups.Select(e => e.Key).ToArray();

						var cacheKey = entities.StringAggregate(",");
						var result = GetKeysMetadata(entities, cacheKey);

						var keysMetadata = result.Metadata;

						foreach (var group in entityGroupedLookups
							.Where(e => keysMetadata.Any(s => s.LogicalName == e.Key)))
						{
							var lookupEntity = keysMetadata.First(e => e.LogicalName == group.Key);

							foreach (var mappingField in group)
							{
								var mappingEntity = MappingEntity.GetMappingEntity(lookupEntity, null, null, Settings.TitleCaseLogicalNames);
								var mappingFields = mappingEntity.Fields;
								mappingEntity.Fields = null;

								mappingField.LookupData.LookupKeys =
									new LookupKeys
									{
										Entity = mappingEntity,
										Fields = mappingFields
									};
							}
						}

						Status.Update(">>> Finished retrieving Alternate Key information.");
					});
				lookupKeysThread.Start();
			}
		}

		private LookupMetadata GetKeysMetadata(IEnumerable<string> entitiesParam, string cacheKey, LookupMetadata lookupMetadata = null)
		{
			if (lookupMetadata == null)
			{
				if (metadataCache.LookupKeysMetadataCache == null)
				{
					metadataCache.LookupKeysMetadataCache = new Dictionary<string, LookupMetadata>();
				}

				metadataCache.LookupKeysMetadataCache.TryGetValue(cacheKey, out lookupMetadata);
			}

			var entities = entitiesParam.ToArray();

			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.In, entities.ToArray()));

			var entityProperties = new MetadataPropertiesExpression { AllProperties = false };
			entityProperties.PropertyNames.AddRange("LogicalName", "Keys");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = entityProperties
				};

			try
			{
				var retrieveMetadataChangesRequest =
					new RetrieveMetadataChangesRequest
					{
						Query = entityQueryExpression,
						ClientVersionStamp = lookupMetadata?.Stamp,
						DeletedMetadataFilters = DeletedMetadataFilters.Attribute
					};

				RetrieveMetadataChangesResponse result;

				using (var service = ConnectionHelper.GetConnection(Settings))
				{
					result = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
				}

				var isModified = result.DeletedMetadata?.Any() != true
					|| result.EntityMetadata?
						.Any(e => e.Keys?
							.Any(s => s.KeyAttributes?
								.Any() == true) == true) == true;
				
				if (isModified)
				{
					lookupMetadata = lookupMetadata == null
						? new LookupMetadata
						  {
							  Metadata = result.EntityMetadata,
							  Stamp = result.ServerVersionStamp
						  }
						: GetKeysMetadata(entities, cacheKey);
				}
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352))
				{
					lookupMetadata = GetKeysMetadata(entities, cacheKey);
				}
				else
				{
					throw;
				}
			}
			
			metadataCache.LookupKeysMetadataCache[cacheKey] = lookupMetadata;

			if (lookupMetadata == null)
			{
				return null;
			}

			var entityFields = lookupMetadata.Metadata
				.Where(e => e.Keys?.Any(s => s.KeyAttributes?.Any() == true) == true)
				.ToDictionary(e => e.LogicalName,
					e => e.Keys.SelectMany(s => s.KeyAttributes.Select(t => t)));

			return GetBasicAttributesMetadata(entityFields, cacheKey);
		}

		private LookupMetadata GetBasicAttributesMetadata(IDictionary<string, IEnumerable<string>> entityFields, string cacheKey,
			LookupMetadata lookupMetadata = null)
		{
			if (lookupMetadata == null)
			{
				if (metadataCache.BasicAttributesMetadataCache == null)
				{
					metadataCache.BasicAttributesMetadataCache = new Dictionary<string, LookupMetadata>();
				}

				metadataCache.BasicAttributesMetadataCache.TryGetValue(cacheKey, out var basicCache);
			}

			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.In, entityFields.Keys.ToArray()));

			var entityProperties = new MetadataPropertiesExpression { AllProperties = false };
			entityProperties.PropertyNames.AddRange("LogicalName", "DisplayName", "SchemaName", "Attributes");

			var attributeFilter = new MetadataFilterExpression(LogicalOperator.And);
			attributeFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.In, entityFields.Values.SelectMany(e => e.Select(s => s)).Distinct().ToArray()));

			var attributeProperties = new MetadataPropertiesExpression { AllProperties = false };
			attributeProperties.PropertyNames.AddRange("LogicalName", "DisplayName", "SchemaName", "AttributeType");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = entityProperties,
					AttributeQuery =
						new AttributeQueryExpression
						{
							Criteria = attributeFilter,
							Properties = attributeProperties
						}
				};

			try
			{
				var retrieveMetadataChangesRequest =
					new RetrieveMetadataChangesRequest
					{
						Query = entityQueryExpression,
						ClientVersionStamp = lookupMetadata?.Stamp,
						DeletedMetadataFilters = DeletedMetadataFilters.Attribute
					};

				RetrieveMetadataChangesResponse result;

				using (var service = ConnectionHelper.GetConnection(Settings))
				{
					result = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
				}

				var isModified = result.DeletedMetadata?.Any() != true
					|| result.EntityMetadata?.Any(e => e.Attributes.Any()) == true;
				
				if (isModified)
				{
					lookupMetadata = lookupMetadata == null
						? new LookupMetadata
						  {
							  Metadata = result.EntityMetadata,
							  Stamp = result.ServerVersionStamp
						  }
						: GetBasicAttributesMetadata(entityFields, cacheKey);
				}
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352))
				{
					return GetBasicAttributesMetadata(entityFields, cacheKey);
				}

				throw;
			}
			
			return metadataCache.BasicAttributesMetadataCache[cacheKey] = lookupMetadata;
		}

		private void ProcessLookupLabels(MappingEntity[] filteredEntities, int threadCount)
		{
			var lookupEntitiesCache = metadataCache.LookupEntitiesMetadataCache;
			var lookupEntitiesSessionCache = new Dictionary<string, LookupMetadata>();

			foreach (var entity in filteredEntities
				.Where(entity => Settings.LookupLabelsEntitiesSelected.Contains(entity.LogicalName)))
			{
				Parallel.ForEach(entity.Fields
					.Where(fieldQ => !fieldQ.Attribute.IsEntityReferenceHelper && fieldQ.IsValidForRead
						&& fieldQ.TargetTypeForCrmSvcUtil.Contains("EntityReference")
						&& fieldQ.LookupData?.LookupSingleType != null),
					new ParallelOptions
					{
						MaxDegreeOfParallelism = Settings.Threads
					},
					field =>
					{
						var label =
							new LookupLabel
							{
								LogicalName = field.LookupData.LookupSingleType
							};

						var lookupData = field.LookupData;

						var lookupEntity = filteredEntities
							.FirstOrDefault(entityQ => entityQ.LogicalName == lookupData.LookupSingleType);

						if (lookupEntity == null)
						{
							LookupMetadata lookupEntitySessionCached;

							lock (lookupEntitiesCache)
							{
								lookupEntitySessionCached = lookupEntitiesSessionCache
									.FirstOrDefault(keyVal => keyVal.Key == lookupData.LookupSingleType).Value;
							}

							if (lookupEntitySessionCached == null)
							{
								LookupMetadata lookupEntityCached;
								lock (lookupEntitiesCache)
								{
									lookupEntityCached = lookupEntitiesCache
										.FirstOrDefault(keyVal => keyVal.Key == lookupData.LookupSingleType).Value;
								}

								lookupEntityCached = GetLookupEntityForLabel(lookupData.LookupSingleType, lookupEntityCached);

								lock (lookupEntitiesCache)
								{
									lookupEntitySessionCached =
										lookupEntitiesSessionCache[lookupData.LookupSingleType] =
											lookupEntitiesCache[lookupData.LookupSingleType] = lookupEntityCached;
								}
							}

							var lookupEntityFetched = lookupEntitySessionCached.Metadata.FirstOrDefault();

							if (lookupEntityFetched == null)
							{
								return;
							}

							label.IdFieldName = lookupEntityFetched.PrimaryIdAttribute;

							var attributeLangs = Settings.EntityDataFilterArray.EntityFilters
								.FirstOrDefault(filter => filter.IsDefault)?.EntityFilterList
								.FirstOrDefault(list => list.LogicalName == label.LogicalName)?.AttributeLanguages;

							if (langThread?.IsAlive == true)
							{
								Status.Update("Waiting for Languages thread ... ");
							}

							langThread?.Join();

							foreach (var language in Languages)
							{
								var labelField = attributeLangs?.FirstOrDefault(pair => int.Parse(pair.Value) == language).Key;

								if (language == 1033)
								{
									labelField = labelField ?? lookupEntityFetched.PrimaryNameAttribute;
								}

								labelField = labelField ?? lookupEntityFetched.Attributes
									.FirstOrDefault(fieldQ => Languages.Any(lang => fieldQ.LogicalName?
										.EndsWith($"_name{lang}") == true))?
									.LogicalName;

								// couldn't find field
								if (string.IsNullOrEmpty(labelField))
								{
									continue;
								}

								label.LabelFieldNames = (string.IsNullOrEmpty(label.LabelFieldNames)
									? ""
									: label.LabelFieldNames + ",") +
									language + "_" + labelField;
							}
						}
						else
						{
							label.IdFieldName = lookupEntity.PrimaryKey?.LogicalName;

							var attributeLangs = Settings.EntityDataFilterArray.EntityFilters
								.FirstOrDefault(filter => filter.IsDefault)?.EntityFilterList
								.FirstOrDefault(list => list.LogicalName == label.LogicalName)?.AttributeLanguages;

							if (langThread?.IsAlive == true)
							{
								Status.Update("Waiting for Languages thread ... ");
							}

							langThread?.Join();

							foreach (var language in Languages)
							{
								var labelField = attributeLangs?.FirstOrDefault(pair => int.Parse(pair.Value) == language).Key;

								if (language == 1033)
								{
									labelField = labelField ?? lookupEntity.PrimaryNameAttribute;
								}

								labelField = labelField ?? lookupEntity.Fields
									.FirstOrDefault(fieldQ => Languages.Any(lang => fieldQ.LogicalName?
										.EndsWith($"_name{lang}") == true))?
									.LogicalName;

								// couldn't find field
								if (string.IsNullOrEmpty(labelField))
								{
									continue;
								}

								label.LabelFieldNames = (string.IsNullOrEmpty(label.LabelFieldNames)
									? ""
									: label.LabelFieldNames + ",") +
									language + "_" + labelField;
							}
						}

						if (!string.IsNullOrEmpty(label.LabelFieldNames))
						{
							lookupData.LookupLabel = label;
						}
					});
			}

			// reserialise
			metadataCache.LookupEntitiesMetadataCache = lookupEntitiesCache;
		}

		private LookupMetadata GetLookupEntityForLabel(string lookupType, LookupMetadata lookupMetadata)
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.Equals, lookupType));

			var entityProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			entityProperties.PropertyNames
				.AddRange("LogicalName", "PrimaryIdAttribute", "Attributes", "PrimaryNameAttribute");

			var attributeFilter = new MetadataFilterExpression(LogicalOperator.And);
			attributeFilter.Conditions
				.Add(new MetadataConditionExpression("AttributeType", MetadataConditionOperator.Equals, AttributeTypeCode.String));

			var attributeProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			attributeProperties.PropertyNames.AddRange("LogicalName");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = entityProperties,
					AttributeQuery =
						new AttributeQueryExpression
						{
							Properties = attributeProperties,
							Criteria = attributeFilter
						}
				};

			try
			{
				var retrieveMetadataChangesRequest =
					new RetrieveMetadataChangesRequest
					{
						Query = entityQueryExpression,
						ClientVersionStamp = lookupMetadata?.Stamp,
						DeletedMetadataFilters = DeletedMetadataFilters.Attribute
					};

				RetrieveMetadataChangesResponse result;

				using (var service = ConnectionHelper.GetConnection(Settings))
				{
					result = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
				}

				var isModified = result.DeletedMetadata?.Any() != true
					|| result.EntityMetadata?.Any(e => e.Attributes.Any()) == true;

				if (isModified)
				{
					lookupMetadata = lookupMetadata == null
						? new LookupMetadata
						  {
							  Metadata = result.EntityMetadata,
							  Stamp = result.ServerVersionStamp
						  }
						: GetLookupEntityForLabel(lookupType, null);
				}
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352))
				{
					lookupMetadata = GetLookupEntityForLabel(lookupType, null);
				}
				else
				{
					throw;
				}
			}

			return lookupMetadata;
		}

		private static void ParseRelationshipNames(List<MappingEntity> cachedEntities)
		{
			cachedEntities.AsParallel()
				.ForAll(ent =>
				        {
					        foreach (var rel in ent.RelationshipsOneToMany)
					        {
						        var rel1 = rel;
						        rel.FromEntity =
							        cachedEntities.FirstOrDefault(
								        e => e.LogicalName.Equals(rel1.Attribute.FromEntity));
						        rel.ToEntity =
							        cachedEntities.FirstOrDefault(e => e.LogicalName.Equals(rel1.Attribute.ToEntity));

						        if (rel.ToEntity != null)
						        {
							        rel.ToField =
								        rel.ToEntity.Fields.FirstOrDefault(
									        p => string.Equals(p.Attribute.LogicalName, rel1.Attribute.ToKey,
										        StringComparison.CurrentCultureIgnoreCase));
						        }

						        if (rel.ToEntity != null && rel.ToField != null)
						        {
							        var friendlyName = Naming.Clean(
								        Naming.Clean(string.IsNullOrEmpty(rel.ToEntity.Label)
									                           ? rel.ToEntity.HybridName
									                           : rel.ToEntity.Label) + "s" + "Of"
								        + Naming.Clean(string.IsNullOrEmpty(rel.ToField.Label)
									                             ? rel.ToField.DisplayName
									                             : rel.ToField.Label));

							        var isDuplicateName = ent.RelationshipsOneToMany.Count(
								        relQ => (relQ.ToEntity != null && relQ.ToField != null)
								                && Naming.Clean(
									                Naming.Clean(string.IsNullOrEmpty(relQ.ToEntity.Label)
										                                   ? relQ.ToEntity.HybridName
										                                   : relQ.ToEntity.Label) + "s" + "Of"
									                + Naming.Clean(string.IsNullOrEmpty(relQ.ToField.Label)
										                                     ? relQ.ToField.DisplayName
										                                     : relQ.ToField.Label))
								                == friendlyName) > 1;

							        rel.FriendlyName = friendlyName + (isDuplicateName ? "_" + rel.SchemaName : "");
						        }
					        }

					        foreach (var rel in ent.RelationshipsManyToOne)
					        {
						        var rel1 = rel;
						        rel.FromEntity =
							        cachedEntities.FirstOrDefault(
								        e => e.LogicalName.Equals(rel1.Attribute.FromEntity));
						        rel.ToEntity =
							        cachedEntities.FirstOrDefault(e => e.LogicalName.Equals(rel1.Attribute.ToEntity));

						        if (rel.FromEntity != null)
						        {
							        rel.FromField = rel.FromEntity.Fields.FirstOrDefault(
								        p =>
								        string.Equals(p.Attribute.LogicalName, rel1.Attribute.FromKey,
									        StringComparison.CurrentCultureIgnoreCase));
						        }

						        if (rel.ToEntity != null && rel.FromField != null)
						        {
							        var friendlyName = Naming.Clean(
								        Naming.Clean(string.IsNullOrEmpty(rel.ToEntity.Label)
									                           ? rel.ToEntity.HybridName
									                           : rel.ToEntity.Label) + "As"
								        + Naming.Clean(string.IsNullOrEmpty(rel.FromField.Label)
									                             ? rel.FromField.DisplayName
									                             : rel.FromField.Label));

							        var isDuplicateName = ent.RelationshipsManyToOne.Count(
								        relQ => (relQ.ToEntity != null && relQ.FromField != null)
								                && Naming.Clean(
									                Naming.Clean(string.IsNullOrEmpty(relQ.ToEntity.Label)
										                                   ? relQ.ToEntity.HybridName
										                                   : relQ.ToEntity.Label) + "As"
									                + Naming.Clean(string.IsNullOrEmpty(relQ.FromField.Label)
										                                     ? relQ.FromField.DisplayName
										                                     : relQ.FromField.Label))
								                == friendlyName) > 1;

							        rel.FriendlyName = friendlyName + (isDuplicateName ? "_" + rel.SchemaName : "");
						        }
					        }
					        foreach (var rel in ent.RelationshipsManyToMany)
					        {
						        var rel1 = rel;
						        rel.FromEntity =
							        cachedEntities.FirstOrDefault(
								        e => e.LogicalName.Equals(rel1.Attribute.FromEntity));
						        rel.ToEntity =
							        cachedEntities.FirstOrDefault(e => e.LogicalName.Equals(rel1.Attribute.ToEntity));
						        rel.IntersectingEntity =
							        cachedEntities.FirstOrDefault(
								        e => e.LogicalName.Equals(rel1.Attribute.IntersectingEntity));

						        if (rel.ToEntity != null)
						        {
							        var friendlyName = Naming.Clean(
								        Naming.Clean(string.IsNullOrEmpty(rel.ToEntity.Label)
									                           ? rel.ToEntity.HybridName
									                           : rel.ToEntity.Label) + "s" + "Of"
								        +
								        Naming.Clean(string.IsNullOrEmpty(rel.SchemaName) ? "" : rel.SchemaName));

							        var isDuplicateName = ent.RelationshipsManyToMany.Count(
								        relQ => (relQ.ToEntity != null)
								                && Naming.Clean(
									                Naming.Clean(string.IsNullOrEmpty(relQ.ToEntity.Label)
										                                   ? relQ.ToEntity.HybridName
										                                   : relQ.ToEntity.Label) + "s" + "Of"
									                +
									                Naming.Clean(string.IsNullOrEmpty(relQ.SchemaName)
										                                   ? ""
										                                   : relQ.SchemaName))
								                == friendlyName) > 1;

							        rel.FriendlyName = friendlyName + (isDuplicateName ? "_" + rel.SchemaName : "");
						        }
					        }
				        });
		}

		private RetrieveMetadataChangesResponse GetMetadata(string[] entities, string clientStamp)
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(
				new MetadataConditionExpression("LogicalName", MetadataConditionOperator.In, entities));

			var entityProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			entityProperties.PropertyNames
				.AddRange("ObjectTypeCode", "LogicalName", "IsIntersect", "PrimaryIdAttribute", "DisplayName",
					"SchemaName", "Description", "Attributes", "PrimaryNameAttribute", "OneToManyRelationships",
					"ManyToOneRelationships", "ManyToManyRelationships", "Keys");

			var attributeProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			attributeProperties.PropertyNames
				.AddRange("AttributeOf", "IsValidForCreate", "IsValidForRead", "IsValidForUpdate",
					"AttributeType", "DeprecatedVersion", "Targets", "IsPrimaryId", "LogicalName", "SchemaName", "Description",
					"DisplayName", "RequiredLevel", "MaxLength", "MinValue", "MaxValue", "MaxWidth", "MaxHeight", "MaxSizeInKB",
					"OptionSet", "DateTimeBehavior");

			var relationshipProperties =
				new MetadataPropertiesExpression
				{
					AllProperties = false
				};
			relationshipProperties.PropertyNames
				.AddRange("ReferencedAttribute", "ReferencedEntity", "ReferencingEntity",
					"ReferencingAttribute", "SchemaName", "Entity1LogicalName", "Entity1IntersectAttribute", "Entity2LogicalName",
					"Entity2IntersectAttribute", "IntersectEntityName");

			var entityQueryExpression =
				new EntityQueryExpression
				{
					Criteria = entityFilter,
					Properties = entityProperties,
					AttributeQuery =
						new AttributeQueryExpression
						{
							Properties = attributeProperties
						},
					RelationshipQuery =
						new RelationshipQueryExpression
						{
							Properties = relationshipProperties
						}
				};

			try
			{
				var retrieveMetadataChangesRequest =
					new RetrieveMetadataChangesRequest
					{
						Query = entityQueryExpression,
						ClientVersionStamp = clientStamp,
						DeletedMetadataFilters = DeletedMetadataFilters.All
					};

				using (var service = ConnectionHelper.GetConnection(Settings))
				{
					return (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
				}
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352) && !CancelMapping)
				{
					return GetMetadata(entities, null);
				}
				else
				{
					throw;
				}
			}
		}

		private MappingAction[] RetrieveActions(params string[] selectedActionNames)
		{
			var actions = Array.Empty<MappingAction>();

			if (!selectedActionNames.Any() && Settings.SelectedGlobalActions?.Any() != true)
			{
				return actions;
			}

			selectedActionNames = selectedActionNames.Union(Settings.SelectedGlobalActions ?? Array.Empty<string>()).Distinct().ToArray();

			var groupedActionNames = Enumerable
				.Range(0, (int)Math.Ceiling(selectedActionNames.Length / 190.0))
				.Select(i => selectedActionNames.Skip(i * 190).Take(190));

			var threadCount = Settings.Threads;

			var rawActions = new ConcurrentBag<IEnumerable<Entity>>();

			Parallel.ForEach(groupedActionNames,
				new ParallelOptions { MaxDegreeOfParallelism = threadCount },
				group => rawActions.Add(RetrieveActions(group)));

			var messages = rawActions.SelectMany(e => e)
				.Select(e =>
					new
					{
						e.Id,
						Name = e.GetAttributeValue<string>("name"),
						PrimaryObjectTypeCode = e.GetAttributeValue<string>("primaryobjecttypecode"),
						Description = e.GetAttributeValue<string>("description"),
						SdkMessageRequestId = e.GetAttributeValue<Guid?>("sdkmessagerequestid"),
						SdkMessageResponseId = e.GetAttributeValue<Guid?>("sdkmessageresponseid"),
						InputName = (string)e.GetAttributeValue<AliasedValue>("inputname")?.Value,
						ClrParser = e.GetAttributeValue<string>("clrparser"),
						Optional = e.GetAttributeValue<bool?>("optional"),
						InputPosition = (int?)e.GetAttributeValue<AliasedValue>("inputposition")?.Value,
						OutputName = (string)e.GetAttributeValue<AliasedValue>("outputname")?.Value,
						ClrFormatter = e.GetAttributeValue<string>("clrformatter"),
						OutputPosition = (int?)e.GetAttributeValue<AliasedValue>("outputposition")?.Value,
					});
			
			#region Mapping actions

			actions = messages
				.GroupBy(m => m.Id)
				.Select(
					grp =>
					{
						var message = grp.First();
						return 
							new MappingAction
							{
								Name = message.Name,
								VarName = Naming.GetProperVariableName(message.Name, false),
								Description = Naming.XmlEscape(message.Description),
								TargetEntityName = message.PrimaryObjectTypeCode,
								InputFields = grp.GroupBy(g => g.InputName)
									.Select(g =>
									{
									   var input = g.First();
										var substring = input.ClrParser.Substring(0,
											input.ClrParser.IndexOf(",", StringComparison.Ordinal));
									   return
											new MappingAction.InputField
										   {
											   Name = input.InputName,
											   VarName = Naming.GetProperVariableName(input.InputName, false),
											   TypeName = substring,
											   Position = input.InputPosition ?? 0,
											   JavaScriptValidationType = (
												   (substring == "System.Boolean") ? "Boolean" :
												   (substring == "System.DateTime") ? "Date" :
												   (substring == "System.Decimal") ? "Number" :
												   (substring == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Entity" :
												   (substring == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.EntityCollection" :
												   (substring == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.EntityReference" :
												   (substring == "System.Double") ? "Number" :
												   (substring == "System.Int32") ? "Number" :
												   (substring == "Microsoft.Xrm.Sdk.Money") ? "Number" :
												   (substring == "Microsoft.Xrm.Sdk.OptionSetValue") ? "Number" :
												   (substring == "System.String") ? "String" : "UnexpectedType"
											   ),
											   JavaScriptValidationExpression = (
												   (substring == "System.Boolean") ? "typeof value == \"boolean\"" :
												   (substring == "System.DateTime") ? "value instanceof Date" :
												   (substring == "System.Decimal") ? "typeof value == \"number\"" :
												   (substring == "Microsoft.Xrm.Sdk.Entity") ? "value instanceof Sdk.Entity" :
												   (substring == "Microsoft.Xrm.Sdk.EntityCollection") ? "value instanceof Sdk.EntityCollection" :
												   (substring == "Microsoft.Xrm.Sdk.EntityReference") ? "value instanceof Sdk.EntityReference" :
												   (substring == "System.Double") ? "typeof value == \"number\"" :
												   (substring == "System.Int32") ? "typeof value == \"number\"" :
												   (substring == "Microsoft.Xrm.Sdk.Money") ? "typeof value == \"number\"" :
												   (substring == "Microsoft.Xrm.Sdk.OptionSetValue") ? "typeof value == \"number\"" :
												   (substring == "System.String") ? "typeof value == \"string\"" : "UnexpectedType"
											   ),
											   NamespacedType = (
												   (substring == "System.Boolean") ? "c:boolean" :
												   (substring == "System.DateTime") ? "c:dateTime" :
												   (substring == "System.Decimal") ? "c:decimal" :
												   (substring == "Microsoft.Xrm.Sdk.Entity") ? "a:Entity" :
												   (substring == "Microsoft.Xrm.Sdk.EntityCollection") ? "a:EntityCollection" :
												   (substring == "Microsoft.Xrm.Sdk.EntityReference") ? "a:EntityReference" :
												   (substring == "System.Double") ? "c:double" :
												   (substring == "System.Int32") ? "c:int" :
												   (substring == "Microsoft.Xrm.Sdk.Money") ? "a:Money" :
												   (substring == "Microsoft.Xrm.Sdk.OptionSetValue") ? "a:OptionSetValue" :
												   (substring == "System.String") ? "c:string" : "UnexpectedType"
											   ),
											   SerializeExpression = (
												   (substring == "System.DateTime") ? ".toISOString()" :
												   (substring == "Microsoft.Xrm.Sdk.Entity") ? ".toValueXml()" :
												   (substring == "Microsoft.Xrm.Sdk.EntityCollection") ? ".toValueXml()" :
												   (substring == "Microsoft.Xrm.Sdk.EntityReference") ? ".toValueXml()" : ""
											   ),
											   Optional = input.Optional == true
										   };
										}).ToArray(),
								OutputFields = grp.Where(e => e.OutputName.IsFilled()).GroupBy(g => g.OutputName)
									.Select(g =>
									{
										var output = g.First();
										var substring = output.ClrFormatter?.Substring(0,
											output.ClrFormatter.IndexOf(",", StringComparison.Ordinal));
										return
											new MappingAction.OutputField
											{
												Name = output.OutputName,
												VarName = Naming.GetProperVariableName(output.OutputName, false),
												TypeName = substring,
												Position = output.OutputPosition ?? 0,
												ValueNodeParser = (
													(substring == "System.Boolean") ? "(Sdk.Xml.getNodeText(valueNode) == \"true\") ? true : false" :
													(substring == "System.DateTime") ? "new Date(Sdk.Xml.getNodeText(valueNode))" :
													(substring == "System.Decimal") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
													(substring == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Util.createEntityFromNode(valueNode)" :
													(substring == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.Util.createEntityCollectionFromNode(valueNode)" :
													(substring == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.Util.createEntityReferenceFromNode(valueNode)" :
													(substring == "System.Double") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
													(substring == "System.Int32") ? "parseInt(Sdk.Xml.getNodeText(valueNode), 10)" :
													(substring == "Microsoft.Xrm.Sdk.Money") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
													(substring == "Microsoft.Xrm.Sdk.OptionSetValue") ? "parseInt(Sdk.Xml.getNodeText(valueNode), 10)" :
													(substring == "System.String") ? "Sdk.Xml.getNodeText(valueNode)" : "UnexpectedType"
												),
												JavaScriptType = (
													(substring == "System.Boolean") ? "Boolean" :
													(substring == "System.DateTime") ? "Date" :
													(substring == "System.Decimal") ? "Number" :
													(substring == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Entity" :
													(substring == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.EntityCollection" :
													(substring == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.EntityReference" :
													(substring == "System.Double") ? "Number" :
													(substring == "System.Int32") ? "Number" :
													(substring == "Microsoft.Xrm.Sdk.Money") ? "Number" :
													(substring == "Microsoft.Xrm.Sdk.OptionSetValue") ? "Number" :
													(substring == "System.String") ? "String" : "UnexpectedType"
												)
											};
										}).ToArray()
							};
						}).ToArray();
			#endregion

			foreach (var action in actions)
			{
				var target = action.InputFields.ToList().Find(field => field.Name.Contains("Target"));

				if (target == null)
				{
					continue;
				}

				target.Position = -1;
				action.InputFields = action.InputFields.OrderBy(i => i.Position).ToArray();
				action.OutputFields = action.OutputFields.OrderBy(o => o.Position).ToArray();
			}

			return actions;
		}

		private IEnumerable<Entity> RetrieveActions(IEnumerable<string> actionNamesParam)
		{
			var actionNames = actionNamesParam?.ToArray();

			var fetchXml =
				$@"
<fetch no-lock='true' >
  <entity name='sdkmessage' >
    <attribute name='sdkmessageid' />
    <attribute name='name' />
    <filter>
	  <condition attribute='name' operator='in' >
        <value>{actionNames.StringAggregate("</value><value>")}</value>
      </condition>
      <condition entityname='workflow' attribute='category' operator='eq' value='3' />
      <condition entityname='workflow' attribute='type' operator='neq' value='3' />
    </filter>
    <link-entity name='sdkmessagepair' from='sdkmessageid' to='sdkmessageid' >
      <link-entity name='sdkmessagerequest' from='sdkmessagepairid' to='sdkmessagepairid' >
        <attribute name='primaryobjecttypecode' alias='primaryobjecttypecode' />
        <attribute name='sdkmessagerequestid' alias='sdkmessagerequestid' />
        <link-entity name='sdkmessageresponse' from='sdkmessagerequestid' to='sdkmessagerequestid' >
          <attribute name='sdkmessageresponseid' alias='sdkmessageresponseid' />
          <link-entity name='sdkmessageresponsefield' from='sdkmessageresponseid' to='sdkmessageresponseid' link-type='outer'>
            <attribute name='clrformatter' alias='clrformatter' />
            <attribute name='name' alias='outputname' />
            <attribute name='position' alias='outputposition' />
          </link-entity>
        </link-entity>
        <link-entity name='sdkmessagerequestfield' from='sdkmessagerequestid' to='sdkmessagerequestid' >
          <attribute name='name' alias='inputname' />
          <attribute name='clrparser' alias='clrparser' />
          <attribute name='optional' alias='optional' />
          <attribute name='position' alias='inputposition' />
          <filter>
            <condition attribute='fieldmask' operator='neq' value='4' />
          </filter>
        </link-entity>
      </link-entity>
    </link-entity>
    <link-entity name='workflowdependency' from='sdkmessageid' to='sdkmessageid' >
      <link-entity name='workflow' from='workflowid' to='workflowid' >
        <attribute name='description' alias='description' />
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

			using (var service = ConnectionHelper.GetConnection(Settings))
			{
				return service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}

	[Serializable]
	public class LookupMetadata
	{
		public EntityMetadataCollection Metadata { get; set; }
		public string Stamp { get; set; }
	}
}
