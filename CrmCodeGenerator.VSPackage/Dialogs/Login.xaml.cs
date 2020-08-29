#region Imports

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Cache;
using CrmCodeGenerator.VSPackage.Connection;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using EnvDTE80;
using Xceed.Wpf.Toolkit;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Mapper;
using Yagasoft.CrmCodeGenerator.Models.Mapper;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;
using MetadataHelpers = Yagasoft.CrmCodeGenerator.Helpers.MetadataHelpers;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for Login.xaml
	/// </summary>
	public partial class Login
	{
		public Context Context;
		public bool StillOpen = true;

		private MetadataCacheManager MetadataCacheManager
		{
			get
			{
				if (cacheThread?.IsAlive == true)
				{
					cacheThread.Join();
				}

				return metadataCacheManager;
			}
		}

		private Mapper Mapper
		{
			get
			{
				if (mapperThread?.IsAlive == true)
				{
					mapperThread.Join();
				}

				return mapper;
			}
		}

		private Settings settings;

		private Mapper mapper;
		private Thread mapperThread;

		private readonly ConnectionManager connectionManager;
		private readonly MetadataCacheManager metadataCacheManager;
		private Thread cacheThread;

		#region Init

		public Login(DTE2 dte)
		{
			Assembly.Load("Xceed.Wpf.Toolkit");

			InitializeComponent();

			var main = dte.GetMainWindow();
			Owner = main;

			settings = Configuration.LoadSettings();

			connectionManager = CacheHelpers.GetFromMemCacheAdd(Constants.ConnCacheMemKey,
				() => new ConnectionManager(settings.Threads));
			metadataCacheManager = new MetadataCacheManager();

			////EventManager.RegisterClassHandler(typeof(TextBox), MouseDoubleClickEvent, new RoutedEventHandler(SelectAddress));
			////EventManager.RegisterClassHandler(typeof(TextBox), GotKeyboardFocusEvent, new RoutedEventHandler(SelectAddress));
			////EventManager.RegisterClassHandler(typeof(TextBox), PreviewMouseLeftButtonDownEvent,
			////	new MouseButtonEventHandler(SelectivelyIgnoreMouseButton));
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = $"{Constants.AppName} v{Constants.AppVersion}";
			Initialise();
		}

		private void Initialise()
		{
			WarmUp();

			DataContext = settings;

			SetInfoUiValues();

			settings.EntityProfilesHeaderSelector = settings.EntityProfilesHeaderSelector ?? new EntityProfilesHeaderSelector();

			mapperThread = new Thread(
				() =>
				{
					mapper = new Mapper(settings, connectionManager, MetadataCacheManager);
					RegisterMapperEvents();
				});
			mapperThread.Start();
		}

		private void SetInfoUiValues()
		{
			var color = (SolidColorBrush)new BrushConverter().ConvertFrom("#212121");

			foreach (var control in GridInfo.GetChildren<Label>().Where(c => c.Name.IsEmpty()))
			{
				control.Foreground = color;
			}

			var templateVersion = settings.DetectedTemplateVersion;

			if (templateVersion.IsEmpty())
			{
				LabelTemplateVersion.Content = "--";
				LabelCompatibility.Content = "--";
			}

			LabelTemplateVersion.Content = templateVersion;
			LabelTemplateVersion.Foreground = Brushes.Blue;

			var isTemplateLatest = new Version(templateVersion) >= new Version(Constants.LatestTemplateVersion);
			LabelTemplateLatest.Content = isTemplateLatest ? "(latest)" : "";
			LabelTemplateLatest.Foreground = isTemplateLatest ? Brushes.Green : Brushes.Black;

			var isTemplateCompatible = new Version(templateVersion) >= new Version(Constants.MinTemplateVersion);
			LabelCompatibility.Content = isTemplateCompatible ? "YES" : "NO";
			LabelCompatibility.Foreground = isTemplateCompatible ? Brushes.Green : Brushes.Red;
		}

		private void WarmUp()
		{
			// warm up connections
			void WarmUpConnections()
			{
				new Thread(
					() =>
					{
						try
						{
							if (settings.ConnectionString.IsFilled())
							{
								connectionManager.Get(settings.ConnectionString).Dispose();
							}
						}
						catch
						{
							// ignored
						}
					}).Start();
			}

			settings.PropertyChanged +=
				(sender, args) =>
				{
					if (args.PropertyName == nameof(settings.Threads))
					{
						connectionManager.Threads = settings.Threads;
					}

					if (args.PropertyName != nameof(settings.Threads) && args.PropertyName != nameof(settings.ConnectionString))
					{
						return;
					}

					WarmUpConnections();
				};

			if (settings.ConnectionString.IsEmpty()
				|| connectionManager == null || metadataCacheManager == null)
			{
				return;
			}

			// warm up the cache.
			cacheThread = new Thread(() => metadataCacheManager.GetCache(settings.ConnectionString));
			cacheThread.Start();

			WarmUpConnections();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.HideMinimizeAndMaximizeButtons();
			this.HideCloseButton();
		}

		private void RegisterMapperEvents()
		{
			mapper.Message
				+= (o, args) =>
				   {
					   try
					   {
						   var isOnBusy = args.MessageTarget.HasFlag(StatusMessageTarget.BusyIndicator);
						   var isOnPane = args.Status == MapperStatus.Started && args.MessageTarget.HasFlag(StatusMessageTarget.LogPane);

						   if (args.Status != MapperStatus.Started)
						   {
							   Status.HideBusy(Dispatcher, BusyIndicator);
						   }
						   else
						   {
							   if (isOnPane)
							   {
								   Status.Update(args.Message);
							   }
							   
							   if (isOnBusy)
							   {
								   return Status.ShowBusy(Dispatcher, BusyIndicator, args.Message, args.Progress);
							   }
						   }
					   }
					   catch
					   {
						   // ignored
					   }

					   return null;
				   };

			mapper.StatusUpdate
				+= (o, args) =>
				   {
					   try
					   {
						   switch (args.Status)
						   {
							   case MapperStatus.Cancelled:
								   Status.Update("Cancelled generator!");
								   StillOpen = false;
								   Dispatcher.InvokeAsync(Close);
								   break;

							   case MapperStatus.Finished:
								   Context = Mapper.Context;
								   Context.SplitFiles = settings.SplitFiles;
								   Context.UseDisplayNames = settings.UseDisplayNames;
								   Context.IsUseCustomDictionary = settings.IsUseCustomDictionary;
								   Context.IsUseCustomEntityReference = settings.IsUseCustomEntityReference;
								   Context.IsAddEntityAnnotations = settings.IsAddEntityAnnotations;
								   Context.IsAddContractAnnotations = settings.IsAddContractAnnotations;
								   Context.IsGenerateLoadPerRelation = settings.IsGenerateLoadPerRelation;
								   Context.IsGenerateEnumNames = settings.IsGenerateEnumNames;
								   Context.IsGenerateEnumLabels = settings.IsGenerateEnumLabels;
								   Context.IsGenerateFieldSchemaNames = settings.IsGenerateFieldSchemaNames;
								   Context.IsGenerateFieldLabels = settings.IsGenerateFieldLabels;
								   Context.IsGenerateRelationNames = settings.IsGenerateRelationNames;
								   Context.IsImplementINotifyProperty = settings.IsImplementINotifyProperty;
								   Context.GenerateGlobalActions = settings.GenerateGlobalActions;
								   Context.PluginMetadataEntities = settings.PluginMetadataEntitiesSelected.ToList();
								   Context.OptionsetLabelsEntities = settings.OptionsetLabelsEntitiesSelected.ToList();
								   Context.LookupLabelsEntities = settings.LookupLabelsEntitiesSelected.ToList();
								   Context.JsEarlyBoundEntities = settings.JsEarlyBoundEntitiesSelected.ToList();
								   Context.EarlyBoundFilteredSelected = settings.EarlyBoundFilteredSelected.ToList();
								   Context.SelectedActions = settings.SelectedActions;
								   Context.ClearMode = settings.ClearMode;
								   Context.SelectedEntities = settings.EntitiesSelected.ToArray();
								   Context.IsGenerateAlternateKeys = settings.IsGenerateAlternateKeys;
								   Context.IsUseCustomTypeForAltKeys = settings.IsUseCustomTypeForAltKeys;
								   Context.IsMakeCrmEntitiesJsonFriendly = settings.IsMakeCrmEntitiesJsonFriendly;
								   Context.CrmEntityProfiles = settings.CrmEntityProfiles;

								   if (settings.LockNamesOnGenerate)
								   {
									   LockNames(Context);
								   }

								   MetadataCacheManager.GetCache(settings.ConnectionString).ContextCache[settings.Id] = Context;

								   StillOpen = false;
								   Dispatcher.InvokeAsync(Close);
								   break;
						   }
					   }
					   catch
					   {
						   // ignored
					   }
				   };
		}

		#endregion

		#region CRM

		private void IncludeNonStandardEntities_Click(object sender, RoutedEventArgs e)
		{
			new Thread(
				() =>
				{
					try
					{
						Status.Update("Processing non-standard inclusion/exclusion ... ");
						MetadataHelpers.RefreshSettingsEntityMetadata(settings, connectionManager, MetadataCacheManager);
					}
					catch (Exception ex)
					{
						Status.PopException(Dispatcher, ex);
					}
					finally
					{
						Status.Update(">> Finished processing.");
					}
				}).Start();
		}

		#endregion

		#region UI events

		private void Logon_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// make sure that all entities selected in profiles are included
				var missingEntities = settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
					.SelectMany(filter => filter.EntityProfiles
					.Where(dataFilter => !dataFilter.IsExcluded || dataFilter.IsGenerateMeta)
					.Select(dataFilter => dataFilter.LogicalName))
					.Distinct().Except(settings.EntitiesSelected).ToList();

				foreach (var missingEntity in missingEntities)
				{
					settings.EntitiesSelected.Add(missingEntity);
				}

				Configuration.SaveSettings(settings);

				Status.Update("Mapping entities, this might take a while depending on CRM server/connection speed ... ");

				// check user's 'split files'
				if (settings.SplitFiles)
				{
					Status.Update("Generator will split generated code into separate entity files.");
				}

				new Thread(
					() =>
					{
						try
						{
							Mapper.MapContext();
							Configuration.SaveCache();
						}
						catch (Exception ex)
						{
							Status.PopException(Dispatcher, ex);
						}
					}).Start();
			}
			catch (Exception ex)
			{
				Status.PopException(Dispatcher, ex);
			}
		}

		private void LogonCached_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// make sure that all entities selected in profiles are included
				var missingEntities = settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
					.SelectMany(filter => filter.EntityProfiles
					.Where(dataFilter => !dataFilter.IsExcluded || dataFilter.IsGenerateMeta)
					.Select(dataFilter => dataFilter.LogicalName))
					.Distinct().Except(settings.EntitiesSelected).ToList();

				foreach (var missingEntity in missingEntities)
				{
					settings.EntitiesSelected.Add(missingEntity);
				}

				var metadataCache = MetadataCacheManager.GetCache(settings.ConnectionString);
				var context = metadataCache.GetCachedContext(settings.Id);

				var excludeEntities = new[] { "" };
				var selected = settings.EntitiesSelected.Where(s => !excludeEntities.Contains(s)).ToArray();
				var isNewModifiedEntities = context != null
					&& selected
						.Intersect(context.Entities
							.Where(s => !excludeEntities.Contains(s.LogicalName))
							.Select(entity => entity.LogicalName))
						.Count() < selected.Length;

				if (context == null || isNewModifiedEntities)
				{
					throw new Exception("There are new entities selected that need to be fetched from CRM. " +
					                    "Either deselect the new entities and use the cache; " +
					                    "connect and try again using the 'generate' button; or cancel, reopen, and then reconfigure.");
				}

				Configuration.SaveSettings(settings);

				Status.Update("Mapping entities using cache ... ");

				// check user's 'split files'
				if (settings.SplitFiles)
				{
					Status.Update("Generator will split generated code into separate entity files.");
				}

				new Thread(
					() =>
					{
						try
						{
							Mapper.MapContext(true);
							Configuration.SaveCache();
						}
						catch (Exception ex)
						{
							Status.PopException(Dispatcher, ex);
						}
					}).Start();
			}
			catch (Exception ex)
			{
				Status.PopException(Dispatcher, ex);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Mapper.CancelMapping = true;
			Configuration.SaveCache();
		}

		private void ButtonCredits_Click(object sender, RoutedEventArgs e)
		{
			new Credits(this).ShowDialog();
		}

		private void ButtonOptions_Click(object sender, RoutedEventArgs e)
		{
			new Options(this, settings, connectionManager, MetadataCacheManager).ShowDialog();
		}

		private void ButtonCancel_Click(object sender, RoutedEventArgs e)
		{
			Mapper.CancelMapping = true;
		}

		private void ButtonNewSettings_Click(object sender, RoutedEventArgs e)
		{
			settings = new Settings();
			Initialise();
		}

		private void ButtonSaveSettings_Click(object sender, RoutedEventArgs e)
		{
			Configuration.SaveSettings(settings);
			DteHelper.ShowInfo("All settings profiles have been saved to disk.", "Settings saved!");
		}

		private void EntitiesRefresh_Click(object sender, RoutedEventArgs events)
		{
			new EntitySelection(this, settings, connectionManager, MetadataCacheManager).ShowDialog();
		}

		private void EntitiesProfiling_Click(object sender, RoutedEventArgs e)
		{
			new Filter(this, settings, connectionManager, MetadataCacheManager).ShowDialog();
		}

		private void ClearCache_Click(object sender, RoutedEventArgs e)
		{
			Status.Update("Clearing cache ... ", false);
			MetadataCacheManager.Clear(settings.ConnectionString);
			Status.Update("done!");
		}

		////// credit: https://social.msdn.microsoft.com/Forums/vstudio/en-US/564b5731-af8a-49bf-b297-6d179615819f/how-to-selectall-in-textbox-when-textbox-gets-focus-by-mouse-click?forum=wpf&prof=required
		////#region Textbox selection

		////private static void SelectAddress(object sender, RoutedEventArgs e)
		////{
		////	if (sender is TextBox || sender is PasswordBox)
		////	{
		////		((dynamic)sender).SelectAll();
		////	}
		////}

		////private static void SelectivelyIgnoreMouseButton(object sender, MouseButtonEventArgs e)
		////{
		////	if (!(sender is TextBox || sender is PasswordBox))
		////	{
		////		return;
		////	}

		////	var tb = (dynamic)sender;

		////	if (tb.IsKeyboardFocusWithin)
		////	{
		////		return;
		////	}

		////	e.Handled = true;
		////	tb.Focus();
		////}

		////#endregion

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
			e.Handled = true;
		}

		#endregion

		private void LockNames(Context context)
		{
			try
			{
				Status.Update("Locking friendly names ... ", false);

				foreach (var filter in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders.Select(filterList => filterList)
					.SelectMany(filter => filter.EntityProfiles))
				{
					// if filter's entity exists in selected entities
					var entity = context.Entities.FirstOrDefault(entityQ => entityQ.LogicalName == filter.LogicalName);

					if (entity == null)
					{
						continue;
					}

					// get all non-renamed attributes to lock
					foreach (var attribute in entity.Fields.Where(field => field.LogicalName != null)
						.Select(field => field.LogicalName).Except(filter.AttributeRenames.Keys))
					{
						// if attribute exists in the entity
						var field = entity.Fields.FirstOrDefault(fieldQ => fieldQ.LogicalName == attribute);

						if (field == null)
						{
							continue;
						}

						// lock
						filter.AttributeRenames[attribute] = field.FriendlyName;
					}

					// get all non-renamed relations to lock
					foreach (var relation in entity.RelationshipsOneToMany.Where(relation => relation.SchemaName != null)
						.Select(relation => relation.SchemaName).Except(filter.OneToNRenames.Keys))
					{
						// if relation exists in the entity
						var relationInEntity = entity.RelationshipsOneToMany.FirstOrDefault(relationQ => relationQ.SchemaName == relation);

						if (relationInEntity == null)
						{
							continue;
						}

						filter.OneToNRenames[relation] = relationInEntity.FriendlyName;
					}

					foreach (var relation in entity.RelationshipsManyToOne.Where(relation => relation.SchemaName != null)
						.Select(relation => relation.SchemaName).Except(filter.NToOneRenames.Keys))
					{
						var relationInEntity = entity.RelationshipsManyToOne.FirstOrDefault(relationQ => relationQ.SchemaName == relation);

						if (relationInEntity == null)
						{
							continue;
						}

						filter.NToOneRenames[relation] = relationInEntity.FriendlyName;
					}

					foreach (var relation in entity.RelationshipsManyToMany.Where(relation => relation.SchemaName != null)
						.Select(relation => relation.SchemaName).Except(filter.NToNRenames.Keys))
					{
						var relationInEntity =
							entity.RelationshipsManyToMany.FirstOrDefault(relationQ => relationQ.SchemaName == relation);

						if (relationInEntity == null)
						{
							continue;
						}

						filter.NToNRenames[relation] = relationInEntity.FriendlyName;
					}
				}
			}
			catch (Exception ex)
			{
				Status.PopException(Dispatcher, ex);
			}
			finally
			{
				Status.Update("done!");
			}
		}
	}
}
