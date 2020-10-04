#region Imports

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Yagasoft.CrmCodeGenerator.Connection;
using Yagasoft.CrmCodeGenerator.Connection.OrgSvcs;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Mapper;
using Yagasoft.CrmCodeGenerator.Models.Mapping;
using Yagasoft.CrmCodeGenerator.Models.Messages;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using static Yagasoft.CrmCodeGenerator.Helpers.MetadataHelpers;

#endregion

namespace Yagasoft.CrmCodeGenerator.Mapper
{
	public delegate void StatusUpdateHandler(object sender, MapperEventArgs e);
	public delegate BusyMessage<Style> MessageHandler(object sender, MapperEventArgs e);

	public enum MapperStatus
	{
		Idle,
		Started,
		Error,
		Cancelled,
		Finished
	}

	public class Mapper
	{
		#region Properties

		public Settings Settings { get; set; }

		public Context Context { get; private set; }

		public List<MappingAction> Actions
		{
			get
			{
				if (actionsThread?.IsAlive == true)
				{
					var message = OnMessage("Waiting for Actions thread ... ");
					actionsThread.Join();
					OnMessage(">> Actions thread finished.", false);
					message?.FinishedProgress(progress);
				}

				return actions;
			}
			private set => actions = value;
		}

		public List<int> Languages
		{
			get
			{
				if (langThread?.IsAlive == true)
				{
					var message = OnMessage("Waiting for languages thread ... ");
					langThread.Join();
					OnMessage(">> Languages thread finished.", false);
					message?.FinishedProgress(progress);
				}

				return languages;
			}
			private set => languages = value;
		}

		public bool CancelMapping
		{
			get => cancelMapping;
			set
			{
				cancelMapping = value;

				if (cancelMapping)
				{
					Status = MapperStatus.Cancelled;
				}
			}
		}

		public MapperStatus Status
		{
			get => status;
			private set
			{
				status = value;

				lock (loggingLock)
				{
					StatusUpdate?.Invoke(this,
						new MapperEventArgs
						{
							Status = status,
							Exception = exception
						});
				}
			}
		}

		private PlatformFeature PlatformFeatures
		{
			get
			{
				if (featuresThread?.IsAlive == true)
				{
					var message = OnMessage("Waiting for features thread ... ");
					featuresThread.Join();
					OnMessage(">> Features thread finished.", false);
					message?.FinishedProgress(progress);
				}

				return platformFeatures;
			}
		}

		#endregion

		private MetadataCache metadataCache;
		private Thread langThread;
		private Thread lookupKeysThread;
		private Thread actionsThread;
		private Thread featuresThread;
		private PlatformFeature platformFeatures;
		private int progress;
		private MapperStatus status = MapperStatus.Idle;
		private bool cancelMapping;
		private readonly IConnectionManager<IDisposableOrgSvc> connectionManager;
		private readonly object loggingLock = new object();
		private Exception exception;
		private List<MappingAction> actions = new List<MappingAction>();
		private List<int> languages;

		#region event handler

		public event StatusUpdateHandler StatusUpdate;

		public event MessageHandler Message;

		protected BusyMessage<Style> OnMessage(string message, bool isUpdateBusyIndicator = true, bool isUpdateLogPane = true,
			bool isProgress = false, Exception exception = null)
		{
			lock (loggingLock)
			{
				var args =
					new MapperEventArgs
					{
						Message = message,
						Progress = isProgress ? progress : (int?)null,
						Exception = exception,
						Status = Status
					};

				if (isUpdateBusyIndicator)
				{
					args.MessageTarget |= StatusMessageTarget.BusyIndicator;
				}

				if (isUpdateLogPane)
				{
					args.MessageTarget |= StatusMessageTarget.LogPane;
				}

				return Message?.Invoke(this, args);
			}
		}

		#endregion

		#region ctor

		public Mapper(Settings settings, IConnectionManager<IDisposableOrgSvc> connectionManager, MetadataCache metadataCache)
		{
			this.connectionManager = connectionManager;
			this.metadataCache = metadataCache;
			Settings = settings;
		}

		#endregion

		public void MapContext(bool useCached = false)
		{
			try
			{
				CancelMapping = false;
				exception = null;

				Status = MapperStatus.Started;
				
				metadataCache.Require(nameof(metadataCache));

				var contextP = metadataCache.Context;

				// check if caching is going to be used
				if (useCached && contextP != null)
				{
					Context = contextP;
					Status = MapperStatus.Finished;
					return;
				}

				if (CancelMapping || exception != null)
				{
					return;
				}

				var gatheringMessage = OnMessage("Gathering metadata, this may take a few minutes...");

				var contextT = new Context();

				langThread = null;

				if (Settings.LookupLabelsEntitiesSelected.Any())
				{
					langThread = new Thread(
						() =>
						{
							try
							{
								var message = OnMessage("Fetching languages ... ");

								using (var service = connectionManager.Get(Settings.ConnectionString))
								{
									Languages = ((RetrieveAvailableLanguagesResponse)
										service.Execute(new RetrieveAvailableLanguagesRequest()))
										.LocaleIds.ToList();
								}

								OnMessage(">> Fetching languages.", false);
								message?.FinishedProgress(progress);
							}
							catch (Exception ex)
							{
								exception = ex;
								Status = MapperStatus.Error;
								OnMessage(ex.Message, false, true, false, ex);
							}
						});
					langThread.Start();
				}
				
				if (CancelMapping || exception != null)
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
							try
							{
								var message = OnMessage("Fetching Actions ... ");

								Actions = BuildActions(originalSelectedEntities
									.Select(entity => Settings.SelectedActions.FirstOrDefault(e => e.Key == entity))
									.Where(e => e.Value != null)
									.SelectMany(e => e.Value)
									.Distinct().ToArray()).ToList();

								OnMessage(">> Fetching Actions.", false);
								message?.FinishedProgress(progress);
							}
							catch (Exception ex)
							{
								exception = ex;
								Status = MapperStatus.Error;
								OnMessage(ex.Message, false, true, false, ex);
							}
						});
					actionsThread.Start();
				}

				#endregion
				
				if (CancelMapping || exception != null)
				{
					return;
				}

				#region Features

				featuresThread = null;

				if (metadataCache.PlatformFeatures == null)
				{
					metadataCache.PlatformFeatures = PlatformFeature.None;

					featuresThread = new Thread(
						() =>
						{
							try
							{
								var message = OnMessage("Fetching platform features ... ");

								platformFeatures = (metadataCache.PlatformFeatures
									|= SetImageAndFileFeaturesSupport(Settings, metadataCache.PlatformFeatures.Value,
										connectionManager)).Value;

								OnMessage(">> Fetching platform features.", false);
								message?.FinishedProgress(progress);
							}
							catch (Exception ex)
							{
								exception = ex;
								Status = MapperStatus.Error;
								OnMessage(ex.Message, false, true, false, ex);
							}
						});
					featuresThread.Start();
				}

				#endregion

				if (CancelMapping || exception != null)
				{
					return;
				}

				contextT.Entities = GetEntities(originalSelectedEntities);

				if (CancelMapping || exception != null)
				{
					return;
				}

				var parseActionMessage = OnMessage("Parsing Entity Actions ... ");

				foreach (var entity in originalSelectedEntities)
				{
					var mappingEntity = contextT.Entities.FirstOrDefault(e => e.LogicalName == entity);

					if (mappingEntity == null)
					{
						continue;
					}

					mappingEntity.Actions = Actions.Where(action => entity == action.TargetEntityName).ToArray();
				}

				OnMessage(">> Parsing Entity Actions.", false);
				parseActionMessage?.FinishedProgress(progress);

				if (CancelMapping || exception != null)
				{
					return;
				}

				// add actions
				var parseGlobalMessage = OnMessage("Parsing Global Actions ... ");
				contextT.GlobalActions = Actions.Where(action => action.TargetEntityName == "none").ToArray();
				OnMessage(">> Parsing Global Actions.", false);
				parseGlobalMessage?.FinishedProgress(progress);

				if (CancelMapping || exception != null)
				{
					return;
				}

				contextT.Languages = Languages ?? new List<int> {1033};

				if (CancelMapping || exception != null)
				{
					return;
				}

				// wait for retrieving Alternate Keys to finish
				if (lookupKeysThread?.IsAlive == true)
				{
					var lookupMessage = OnMessage("Waiting for Alternate Keys thread ... ");
					lookupKeysThread?.Join();
					OnMessage(">> Alternate Keys thread finished.", false);
					lookupMessage?.FinishedProgress(progress);
				}
				
				if (CancelMapping || exception != null)
				{
					return;
				}

				var sortMessage = OnMessage("Sorting Entities ... ");
				SortEntities(contextT);
				OnMessage(">> Sorting Entities.", false);
				sortMessage?.FinishedProgress(progress);

				contextT.EntityProfilesHeaderSelector = Settings.EntityProfilesHeaderSelector;

				Context = contextT;

				OnMessage(">> Gathering metadata.", false);
				gatheringMessage?.FinishedProgress(progress);

				Status = MapperStatus.Finished;
			}
			catch (Exception ex)
			{
				exception = ex;
				Status = MapperStatus.Error;
				OnMessage(ex.Message, false, true, false, ex);
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
			var threadCount = Math.Max(1, Settings.Threads - 1);

			// group entities by their server stamp to fetch them together in bulk
			// if not stamp is found, mark it
			var groupingMessage = OnMessage("Grouping entities by last server stamp ... ");

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

			OnMessage(">> Grouping entities.", false);
			groupingMessage?.FinishedProgress(progress);

			#region Get metadata

			var totalTaken = 0;

			Exception error = null;

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
						  var remainingThreads = Math.Max(1, threadCount - groupedEntities.Count);
						  var takenCount = 0;
						  var tempGroupedEntities = group.ToList();

						  // TODO use better grouping algorithm from other tool
						  #region Counts

						  var threadCap = new int[remainingThreads];

						  // calculate how many entities to take per thread
						  for (var i = 0; i < tempGroupedEntities.Count; i++)
						  {
							  threadCap[i % remainingThreads] = Math.Min(threadCap[i % remainingThreads] + 1, Settings.EntitiesPerThread);
						  }

						  var takeCount = 0;
						  var remaining = tempGroupedEntities.Count;

						  // calculate how many iterations to make
						  while (remaining > 0)
						  {
							  remaining -= threadCap[takeCount % remainingThreads];
							  takeCount++;
						  }

						  #endregion

						  #region Fetch metadata

						  // parallelise the fetch process
						  Parallel.For(0, takeCount,
							  new ParallelOptions
								{
									MaxDegreeOfParallelism = remainingThreads
								},
							  (index, state) =>
								{
									if (CancelMapping || error != null)
									{
										state.Stop();
										return;
									}

									try
									{
										List<string> tempSelectedEntities;

										BusyMessage<Style> message;

										// page the fetch process
										lock (tempGroupedEntities)
										{
											tempSelectedEntities = tempGroupedEntities.Skip(takenCount)
												.Take(threadCap[index % remainingThreads]).ToList();

											OnMessage($"{progress}%, fetching: {tempSelectedEntities.StringAggregate(",")} ...",
												false);
											var joinedEntities = string.Join(", ", tempSelectedEntities);
											message = OnMessage(("Fetching: " + joinedEntities.Substring(0,
												Math.Min(joinedEntities.Length - 1, 59)) + " ...").PadRight(80),
												true, false, true);
											progress = Math.Max(progress, 1);

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
														return !NonStandard.Contains(r.LogicalName);
													}
												})
											.ToList();

										var foundEntities = newUpdatedEntities.Select(entity => entity.LogicalName).ToList();

										if (foundEntities.Any())
										{
											OnMessage($"Found {newUpdatedEntities.Count}"
												+ $" entities: {foundEntities.Aggregate((entity1, entity2) => entity1 + (entity2 != null ? ", " + entity2 : ""))}",
												false);
										}
										else
										{
											OnMessage($"Found 0 entities -- cached or deleted"
												+ $" ({tempSelectedEntities.Aggregate((entity1, entity2) => entity1 + (entity2 != null ? ", " + entity2 : ""))})",
												false);
										}

										// update the cache and fix relationships
										lock (metadataCache.EntityMetadataCache)
										{
											if (CancelMapping || exception != null)
											{
												state.Stop();
												return;
											}

											MappingEntity.UpdateCache(newUpdatedEntities
												, metadataCache.EntityMetadataCache, entities.ServerVersionStamp
												, entities.DeletedMetadata, Settings.TitleCaseLogicalNames);

											totalTaken += tempSelectedEntities.Count;
											progress = (int)((totalTaken / (double)originalSelectedEntities.Count) * 100.0);
										}

										message?.FinishedProgress(progress);
									}
									catch (Exception ex)
									{
										error = ex;
										OnMessage(ex.Message, false, true, false, ex);
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

			progress = 100;

			if (error != null)
			{
				exception = error;
				Status = MapperStatus.Error;
				throw error;
			}

			var cachedEntities = metadataCache.EntityMetadataCache.Values.ToList();

			ParseRelationshipNames(cachedEntities);

			// filter the entities to only the selected ones
			var filteredEntities =
				cachedEntities.Where(entity => originalSelectedEntities.Contains(entity.LogicalName)).ToArray();

			if (CancelMapping || exception != null)
			{
				return new MappingEntity[0];
			}

			if (Settings.IsGenerateAlternateKeys)
			{
				BuildLookupKeysData(filteredEntities);
			}

			var missingMessage = OnMessage("Creating missing filters ... ");

			foreach (var filterList in Settings.EntityProfilesHeaderSelector.EntityProfilesHeaders.Select(filter => filter))
			{
				filterList.EntityProfiles.AddRange(filteredEntities.Select(entity => entity.LogicalName)
					.Except(filterList.EntityProfiles.Select(filter => filter.LogicalName))
					.Select(unfiltered => new EntityProfile(unfiltered)));
			}
			
			OnMessage(">> Creating missing filters.", false);
			missingMessage?.FinishedProgress(progress);

			if (CancelMapping || exception != null)
			{
				return new MappingEntity[0];
			}

			var lookupMessage = OnMessage("Building lookup labels info ... ");

			if (Settings.LookupLabelsEntitiesSelected.Any())
			{
				ProcessLookupLabels(filteredEntities, threadCount);
			}

			OnMessage(">> Building lookup labels info.", false);
			lookupMessage?.FinishedProgress(progress);

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
						try
						{
							var altMessage = OnMessage("Retrieving Alternate Key information ... ");

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

							OnMessage(">> Retrieving Alternate Key information.", false);
							altMessage?.FinishedProgress(progress);
						}
						catch (Exception ex)
						{
							exception = ex;
							Status = MapperStatus.Error;
							OnMessage(ex.Message, false, true, false, ex);
						}
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

				using (var service = connectionManager.Get(Settings.ConnectionString))
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

				using (var service = connectionManager.Get(Settings.ConnectionString))
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

							var attributeLangs = Settings.CrmEntityProfiles
								.FirstOrDefault(e => e.LogicalName == label.LogicalName)?.AttributeLanguages;
							
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

							var attributeLangs = Settings.CrmEntityProfiles
								.FirstOrDefault(e => e.LogicalName == label.LogicalName)?.AttributeLanguages;
							
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

				using (var service = connectionManager.Get(Settings.ConnectionString))
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
			entityFilter.Conditions.Add(new MetadataConditionExpression("LogicalName",
				MetadataConditionOperator.In, entities));

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
					"DisplayName", "RequiredLevel", "MaxLength", "MinValue", "MaxValue", "OptionSet", "DateTimeBehavior");

			if (PlatformFeatures.HasFlag(PlatformFeature.Image))
			{
				attributeProperties.PropertyNames
					.AddRange("MaxWidth", "MaxHeight", "MaxSizeInKB");
			}

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

				using (var service = connectionManager.Get(Settings.ConnectionString))
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

		private MappingAction[] BuildActions(params string[] selectedActionNames)
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
				group => rawActions.Add(RetrieveActions(group, Settings.ConnectionString, connectionManager)));

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
	}

	[Serializable]
	public class LookupMetadata
	{
		public EntityMetadataCollection Metadata { get; set; }
		public string Stamp { get; set; }
	}
}
