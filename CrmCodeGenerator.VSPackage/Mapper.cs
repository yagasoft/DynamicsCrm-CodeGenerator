#region Imports

using System;
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
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public delegate void MapperHandler(object sender, MapperEventArgs e);

	public class Mapper : INotifyPropertyChanged
	{
		#region Properties

		public readonly object LoggingLock = new object();

		private BlockingQueue<IOrganizationService> servicesQueue = new BlockingQueue<IOrganizationService>();
		private int connectionsCreated;

		public Settings Settings { get; set; }

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

		public Mapper(Settings settings)
		{
			Settings = settings;
		}

		#endregion

		public void MapContext(bool useCached = false, Context contextP = null)
		{
			try
			{
				Error = null;

				// check if caching is going to be used
				if (useCached && contextP != null)
				{
					Context = contextP;
					return;
				}

				connectionsCreated = 0;
				servicesQueue = new BlockingQueue<IOrganizationService>();

				var contextT = new Context();

				Thread langThread = null;

				if (Settings.LookupLabelsEntitiesSelected.Any())
				{
					langThread = new Thread(() => Languages =
												((RetrieveAvailableLanguagesResponse)
														ConnectionHelper.GetConnection(Settings)
															.Execute(new RetrieveAvailableLanguagesRequest()))
													.LocaleIds.ToList());
					langThread.Start();
				}

				contextT.Entities = GetEntities();

				// wait for retrieving languages to finish
				langThread?.Join();

				contextT.Languages = Languages ?? new List<int> {1033};

				// add actions
				Status.Update("Parsing global actions ... ", false);
				contextT.GlobalActions = Actions.Where(action => action.TargetEntityName == "none").ToArray();
				Status.Update("done!");

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

		internal MappingEntity[] GetEntities()
		{
			OnMessage("Gathering metadata, this may take a few minutes...");

			var originalSelectedEntities = Settings.EntitiesSelected
				.Where(entity => !string.IsNullOrEmpty(entity)).ToList();
			if (originalSelectedEntities.All(q => q != "activityparty"))
			{
				originalSelectedEntities.Add("activityparty");
			}

			var threadCount = Settings.Threads;

			// group entities by their server stamp to fetch them together in bulk
			// if not stamp is found, mark it
			Status.Update("Grouping entities by last server stamp ... ", false);
			var groupedEntities =
				originalSelectedEntities.GroupBy(
					entity =>
					{
						try
						{
							var cachedEntity = Settings.EntityMetadataCache.FirstOrDefault(temp1 => temp1.Value.LogicalName == entity);
							return cachedEntity.Value == null
									   ? "NO_CACHE"
									   : cachedEntity.Value.ServerStamp;
						}
						catch
						{
							return "NO_CACHE";
						}
					}).ToList();
			Status.Update("done!");

			#region Get metadata

			var totalTaken = 0;

			// go over each group of entities
			Parallel.ForEach(groupedEntities
				, new ParallelOptions
				  {
					  MaxDegreeOfParallelism = threadCount
				  }
				, group =>
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
						  Parallel.For(
							  0, takeCount
							  , new ParallelOptions
								{
									MaxDegreeOfParallelism = threadCount
								}
							  , (index, state) =>
								{
									if (CancelMapping || Error != null)
									{
										state.Stop();
									}

									IOrganizationService connection = null;

									try
									{
										// if queue is empty, and connections are less than allowed, then create a new one and add it to queue
										lock (servicesQueue)
										{
											if (servicesQueue.Count <= 0 && connectionsCreated < threadCount)
											{
												servicesQueue.Enqueue(ConnectionHelper.GetConnection(Settings,
													ref connectionsCreated, Settings.Threads));
											}

											connection = servicesQueue.Dequeue();
										}

										List<string> tempSelectedEntities;

										// page the fetch process
										lock (tempGroupedEntities)
										{
											tempSelectedEntities = tempGroupedEntities.Skip(takenCount).Take(threadCap[index % threadCount]).ToList();

											WorkingOnEntities = tempSelectedEntities;
											var joinedEntities = string.Join(", ", tempSelectedEntities);
											ProgressMessage = ("Fetching: " + joinedEntities.Substring(0, Math.Min(joinedEntities.Length - 1, 59)) + " ...").PadRight(80);
											Progress = Math.Max(Progress, 1);

											takenCount += tempSelectedEntities.Count;
										}

										// fetch the entities, passing the last known stamp, and null if none was found
										var entities = GetMetadata(connection, tempSelectedEntities.ToArray(),
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
										lock (Settings.EntityMetadataCache)
										{
											MappingEntity.UpdateCache(newUpdatedEntities
												, Settings.EntityMetadataCache, entities.ServerVersionStamp
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
									finally
									{
										if (connection != null)
										{
											servicesQueue.Enqueue(connection);
										}
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

			var cachedEntities = Settings.EntityMetadataCache.Values.ToList();

			ParseRelationshipNames(cachedEntities);

			// filter the entities to only the selected ones
			var filteredEntities =
				cachedEntities.Where(entity => originalSelectedEntities.Contains(entity.LogicalName)).ToArray();

			OnMessage("Finished gathering metadata.");

			Status.Update("Creating missing filters ... ", false);

			foreach (var filterList in Settings.EntityDataFilterArray.EntityFilters.Select(filter => filter))
			{
				filterList.EntityFilterList.AddRange(filteredEntities.Select(entity => entity.LogicalName)
					.Except(filterList.EntityFilterList.Select(filter => filter.LogicalName))
					.Select(unfiltered => new EntityDataFilter(unfiltered)));
			}
			
			Status.Update("done!");

			#region Actions

			Status.Update("Fetching actions ... ", false);

			if (Settings.GenerateGlobalActions || Settings.ActionEntitiesSelected.Any())
			{
				// create a new connection if queue is empty
				if (servicesQueue.Count <= 0)
				{
					servicesQueue.Enqueue(ConnectionHelper.GetConnection(Settings,
						ref connectionsCreated, Settings.Threads));
				}

				var service = servicesQueue.Dequeue();

				Actions = GetActions(service,
					filteredEntities
						.Where(entity => Settings.ActionEntitiesSelected.Contains(entity.LogicalName))
						.Select(entity => entity.LogicalName).ToArray()).ToList();

				servicesQueue.Enqueue(service);
			}

			foreach (var entity in filteredEntities)
			{
				entity.Actions = Actions.Where(action => entity.LogicalName == action.TargetEntityName).ToArray();
			}

			Status.Update("done!");

			#endregion

			Status.Update("Building lookup labels info ... ", false);

			if (Settings.LookupLabelsEntitiesSelected.Any())
			{
				ProcessLookupLabels(filteredEntities, threadCount);
			}

			Status.Update("done!");

			return filteredEntities;
		}

		private void ProcessLookupLabels(MappingEntity[] filteredEntities, int threadCount)
		{
			var lookupEntitiesCache = Settings.LookupEntitiesMetadataCache;
			var lookupEntitiesSessionCache = new Dictionary<string, LookupMetadata>();

			foreach (var entity in filteredEntities
				.Where(entity => Settings.LookupLabelsEntitiesSelected.Contains(entity.LogicalName)))
			{
				Parallel.ForEach(entity.Fields
					.Where(fieldQ => !fieldQ.Attribute.IsEntityReferenceHelper && fieldQ.IsValidForRead
					                 && fieldQ.TargetTypeForCrmSvcUtil.Contains("EntityReference")
					                 && fieldQ.LookupSingleType != null)
					, new ParallelOptions
					  {
						  MaxDegreeOfParallelism = Settings.Threads
					  }
					, field =>
					  {
						  var label = new LookupLabel
						              {
							              LogicalName = field.LookupSingleType
						              };

						  var lookupEntity = filteredEntities.FirstOrDefault(entityQ => entityQ.LogicalName == field.LookupSingleType);

						  if (lookupEntity == null)
						  {
							  LookupMetadata lookupEntitySessionCached;

							  lock (lookupEntitiesCache)
							  {
								  lookupEntitySessionCached =
									  lookupEntitiesSessionCache.FirstOrDefault(keyVal => keyVal.Key == field.LookupSingleType).Value;
							  }

							  if (lookupEntitySessionCached == null)
							  {
								  LookupMetadata lookupEntityCached;
								  lock (lookupEntitiesCache)
								  {
									  lookupEntityCached =
										  lookupEntitiesCache.FirstOrDefault(keyVal => keyVal.Key == field.LookupSingleType).Value;
								  }

								  IOrganizationService service;

								  lock (servicesQueue)
								  {
									  // create a new connection if queue is low
									  if (servicesQueue.Count <= 0 && connectionsCreated < 5)
									  {
										  if (connectionsCreated == threadCount)
										  {
											  Status.Update("");
										  }

										  servicesQueue.Enqueue(ConnectionHelper.GetConnection(Settings,
											  ref connectionsCreated, connectionsCreated + 1));
									  }

									  service = servicesQueue.Dequeue();
								  }

								  try
								  {
									  lookupEntityCached = GetLookupEntityForLabel(service, field.LookupSingleType, lookupEntityCached);
								  }
								  finally
								  {
									  servicesQueue.Enqueue(service);
								  }


								  lock (lookupEntitiesCache)
								  {
									  lookupEntitySessionCached =
										  lookupEntitiesSessionCache[field.LookupSingleType] =
										  lookupEntitiesCache[field.LookupSingleType] = lookupEntityCached;
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
							  label.IdFieldName = lookupEntity.PrimaryKey.LogicalName;

							  var attributeLangs = Settings.EntityDataFilterArray.EntityFilters
								  .FirstOrDefault(filter => filter.IsDefault)?.EntityFilterList
								  .FirstOrDefault(list => list.LogicalName == label.LogicalName)?.AttributeLanguages;

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
							  field.LookupLabel = label;
						  }
					  });
			}

			// reserialise
			Settings.LookupEntitiesMetadataCache = lookupEntitiesCache;
		}

		private static LookupMetadata GetLookupEntityForLabel(IOrganizationService service, string lookupType, LookupMetadata lookupMetadata)
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions
				.Add(new MetadataConditionExpression("LogicalName", MetadataConditionOperator.Equals, lookupType));

			var entityProperties = new MetadataPropertiesExpression
			                       {
				                       AllProperties = false
			                       };
			entityProperties.PropertyNames
				.AddRange("LogicalName", "PrimaryIdAttribute", "Attributes", "PrimaryNameAttribute");

			var attributeFilter = new MetadataFilterExpression(LogicalOperator.And);
			attributeFilter.Conditions
				.Add(new MetadataConditionExpression("AttributeType", MetadataConditionOperator.Equals, AttributeTypeCode.String));
			
			var attributeProperties = new MetadataPropertiesExpression
			                          {
				                          AllProperties = false
			                          };
			attributeProperties.PropertyNames.AddRange("LogicalName");

			var entityQueryExpression = new EntityQueryExpression
			                            {
				                            Criteria = entityFilter,
				                            Properties = entityProperties,
				                            AttributeQuery = new AttributeQueryExpression
				                                             {
					                                             Properties = attributeProperties,
																 Criteria = attributeFilter
											}
			                            };

			try
			{
				var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
				{
					Query = entityQueryExpression,
					ClientVersionStamp = lookupMetadata?.Stamp,
					DeletedMetadataFilters = DeletedMetadataFilters.Attribute
				};

				var result = (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);

				var isUnmodified =
					(result.DeletedMetadata == null || !result.DeletedMetadata.Any())
					&& (result.EntityMetadata?.FirstOrDefault()?.Attributes == null
					|| result.EntityMetadata.First().Attributes.Length == 0);

				if (isUnmodified)
				{
					return lookupMetadata;
				}

				if (lookupMetadata != null)
				{
					return GetLookupEntityForLabel(service, lookupType, null);
				}

				return new LookupMetadata
				       {
					       Metadata = result.EntityMetadata,
					       Stamp = result.ServerVersionStamp
				       };
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352))
				{
					return GetLookupEntityForLabel(service, lookupType, null);
				}
				else
				{
					throw;
				}
			}
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

		private static RetrieveMetadataChangesResponse GetMetadata(IOrganizationService service, string[] entities,
			string clientStamp)
		{
			var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
			entityFilter.Conditions.Add(
				new MetadataConditionExpression("LogicalName", MetadataConditionOperator.In, entities));

			var entityProperties = new MetadataPropertiesExpression
								   {
									   AllProperties = false
								   };
			entityProperties.PropertyNames.AddRange(
				"ObjectTypeCode", "LogicalName", "IsIntersect", "PrimaryIdAttribute", "DisplayName"
				, "SchemaName", "Description", "Attributes", "PrimaryNameAttribute", "OneToManyRelationships"
				, "ManyToOneRelationships", "ManyToManyRelationships");

			var attributeProperties = new MetadataPropertiesExpression
									  {
										  AllProperties = false
									  };
			attributeProperties.PropertyNames.AddRange(
				"AttributeOf", "IsValidForCreate", "IsValidForRead", "IsValidForUpdate"
				, "AttributeType", "DeprecatedVersion", "Targets", "IsPrimaryId", "LogicalName", "SchemaName", "Description"
				, "DisplayName", "RequiredLevel", "MaxLength", "MinValue", "MaxValue", "OptionSet", "DateTimeBehavior");

			var relationshipProperties = new MetadataPropertiesExpression
										 {
											 AllProperties = false
										 };
			relationshipProperties.PropertyNames.AddRange(
				"ReferencedAttribute", "ReferencedEntity", "ReferencingEntity"
				, "ReferencingAttribute", "SchemaName", "Entity1LogicalName", "Entity1IntersectAttribute", "Entity2LogicalName"
				, "Entity2IntersectAttribute", "IntersectEntityName");

			var entityQueryExpression = new EntityQueryExpression
										{
											Criteria = entityFilter,
											Properties = entityProperties,
											AttributeQuery = new AttributeQueryExpression
															 {
																 Properties = attributeProperties
															 },
											RelationshipQuery = new RelationshipQueryExpression
																{
																	Properties = relationshipProperties
																}
										};

			try
			{
				var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
													 {
														 Query = entityQueryExpression,
														 ClientVersionStamp = clientStamp,
														 DeletedMetadataFilters = DeletedMetadataFilters.All
													 };

				return (RetrieveMetadataChangesResponse)service.Execute(retrieveMetadataChangesRequest);
			}
			catch (FaultException<OrganizationServiceFault> ex)
			{
				// Check for ErrorCodes.ExpiredVersionStamp (0x80044352)
				// Will occur when the timestamp exceeds the Organization.ExpireSubscriptionsInDays value, which is 90 by default.
				if (ex.Detail.ErrorCode == unchecked((int)0x80044352))
				{
					return GetMetadata(service, entities, null);
				}
				else
				{
					throw;
				}
			}
		}

		private static MappingAction[] GetActions(IOrganizationService service, params string[] typeCodes)
		{
			var xrmService = new XrmServiceContext(service);

			var messages = (from sm in xrmService.SdkMessageSet
							join smp in xrmService.SdkMessagePairSet
								on sm.SdkMessageId equals smp.SdkMessageId.Id
							join smreq in xrmService.SdkMessageRequestSet
								on smp.SdkMessagePairId equals smreq.SdkMessagePairId.Id
							join smresp in xrmService.SdkMessageResponseSet
								on smreq.SdkMessageRequestId equals smresp.SdkMessageRequestId.Id
							join wkflw in xrmService.WorkflowSet
								on sm.SdkMessageId equals wkflw.SdkMessageId.Id
							where smreq.CustomizationLevel.Equals(1)
								  && sm.Template.Equals(false)
								  && wkflw.Type.Equals(1)
							select new
								   {
									   sm.Name,
									   smreq.PrimaryObjectTypeCode,
									   wkflw.Description,
									   smreq.SdkMessageRequestId,
									   smresp.SdkMessageResponseId
								   }).ToList();

			if (typeCodes.Length > 0)
			{
				messages = messages.Where(message => typeCodes.Contains(message.PrimaryObjectTypeCode)
					|| message.PrimaryObjectTypeCode == "none").ToList();
			}

			#region Mapping actions

			var actions = messages.Select(message => new MappingAction
				{
					Name = message.Name,
					VarName = Naming.GetProperVariableName(message.Name, false),
					Description = Naming.XmlEscape(message.Description),
					TargetEntityName = message.PrimaryObjectTypeCode,
					InputFields = (from input in xrmService.SdkMessageRequestFieldSet
								   where
									   input.SdkMessageRequestId.Id.Equals(
										   message.SdkMessageRequestId)
									   && !input.FieldMask.Equals(4)
								   orderby input.Position
								   select new MappingAction.InputField
										   {
											   Name = input.Name,
											   VarName = Naming.GetProperVariableName(input.Name, false),
											   TypeName =
												   input.ClrParser.Substring(0,
													   input.ClrParser.IndexOf(",",
														   StringComparison.Ordinal)),
											   Position = input.Position ?? 0,
											   JavaScriptValidationType = (
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Boolean") ? "Boolean" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.DateTime") ? "Date" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Decimal") ? "Number" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Entity" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.EntityCollection" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.EntityReference" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Double") ? "Number" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Int32") ? "Number" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Money") ? "Number" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.OptionSetValue") ? "Number" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.String") ? "String" : "UnexpectedType"

											   ),
											   JavaScriptValidationExpression = (
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Boolean") ? "typeof value == \"boolean\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.DateTime") ? "value instanceof Date" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Decimal") ? "typeof value == \"number\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? "value instanceof Sdk.Entity" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? "value instanceof Sdk.EntityCollection" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? "value instanceof Sdk.EntityReference" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Double") ? "typeof value == \"number\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Int32") ? "typeof value == \"number\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Money") ? "typeof value == \"number\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.OptionSetValue") ? "typeof value == \"number\"" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.String") ? "typeof value == \"string\"" : "UnexpectedType"

											   ),
											   NamespacedType = (
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Boolean") ? "c:boolean" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.DateTime") ? "c:dateTime" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Decimal") ? "c:decimal" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? "a:Entity" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? "a:EntityCollection" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? "a:EntityReference" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Double") ? "c:double" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.Int32") ? "c:int" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Money") ? "a:Money" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.OptionSetValue") ? "a:OptionSetValue" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.String") ? "c:string" : "UnexpectedType"

											   ),
											   SerializeExpression = (
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "System.DateTime") ? ".toISOString()" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? ".toValueXml()" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? ".toValueXml()" :
												   (input.ClrParser.Substring(0, input.ClrParser.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? ".toValueXml()" : ""

											   ),
											   Optional =
												   !input.Optional.HasValue || input.Optional.Value
										   }).ToArray(),
					OutputFields = (from output in xrmService.SdkMessageResponseFieldSet
									where
										output.SdkMessageResponseId.Id.Equals(
											message.SdkMessageResponseId)
									orderby output.Position
									select new MappingAction.OutputField
										{
											Name = output.Name,
											VarName = Naming.GetProperVariableName(output.Name, false),
											TypeName =
												output.ClrFormatter.Substring(0,
													output.ClrFormatter.IndexOf(",",
														StringComparison.Ordinal)),
											Position = output.Position ?? 0,
											ValueNodeParser = (
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Boolean") ? "(Sdk.Xml.getNodeText(valueNode) == \"true\") ? true : false" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.DateTime") ? "new Date(Sdk.Xml.getNodeText(valueNode))" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Decimal") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Util.createEntityFromNode(valueNode)" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.Util.createEntityCollectionFromNode(valueNode)" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.Util.createEntityReferenceFromNode(valueNode)" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Double") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Int32") ? "parseInt(Sdk.Xml.getNodeText(valueNode), 10)" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.Money") ? "parseFloat(Sdk.Xml.getNodeText(valueNode))" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.OptionSetValue") ? "parseInt(Sdk.Xml.getNodeText(valueNode), 10)" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.String") ? "Sdk.Xml.getNodeText(valueNode)" : "UnexpectedType"
											),
											JavaScriptType = (
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Boolean") ? "Boolean" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.DateTime") ? "Date" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Decimal") ? "Number" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.Entity") ? "Sdk.Entity" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityCollection") ? "Sdk.EntityCollection" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.EntityReference") ? "Sdk.EntityReference" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Double") ? "Number" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.Int32") ? "Number" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.Money") ? "Number" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "Microsoft.Xrm.Sdk.OptionSetValue") ? "Number" :
											(output.ClrFormatter.Substring(0, output.ClrFormatter.IndexOf(",")) == "System.String") ? "String" : "UnexpectedType"
											)
										}).ToArray()
				}).ToArray();

			#endregion

			foreach (var action in actions)
			{
				var target = action.InputFields.ToList().Find(field => field.Name.Contains("Target"));
				if (target != null)
				{
					target.Position = -1;
					action.InputFields = action.InputFields.ToList().OrderBy(input => input.Position).ToArray();
				}
			}

			return actions;
		}

		private static void ExcludeRelationshipsNotIncluded(List<MappingEntity> mappedEntities)
		{
			foreach (var ent in mappedEntities)
			{
				ent.RelationshipsOneToMany =
					ent.RelationshipsOneToMany.ToList()
						.Where(r => mappedEntities.Select(m => m.LogicalName).Contains(r.Type))
						.ToArray();
				ent.RelationshipsManyToOne =
					ent.RelationshipsManyToOne.ToList()
						.Where(r => mappedEntities.Select(m => m.LogicalName).Contains(r.Type))
						.ToArray();
				ent.RelationshipsManyToMany =
					ent.RelationshipsManyToMany.ToList()
						.Where(r => mappedEntities.Select(m => m.LogicalName).Contains(r.Type))
						.ToArray();
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

	public class LookupMetadata
	{
		public EntityMetadataCollection Metadata { get; set; }
		public string Stamp { get; set; }
	}
}
