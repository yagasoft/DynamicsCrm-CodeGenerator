#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using EnvDTE80;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using Application = System.Windows.Forms.Application;

#endregion

namespace CrmCodeGenerator.VSPackage.Dialogs
{
	/// <summary>
	///     Interaction logic for Login.xaml
	/// </summary>
	public partial class Login
	{
		private Style originalProgressBarStyle;


		public Context Context;
		private Settings settings;
		private Mapper mapper;

		private IOrganizationService service;

		private readonly SettingsArray settingsArray;

		private bool _StillOpen = true;

		public bool StillOpen => _StillOpen;

		private IOrganizationService Service
		{
			get
			{
				//return service = new OrganizationService(CrmConnection
				//	.Parse(settings.GetOrganizationCrmConnectionString()));
				return null;
			}
		}

		#region Init

		public Login(DTE2 dte)
		{
			Assembly.Load("Xceed.Wpf.Toolkit");

			InitializeComponent();

			var main = dte.GetMainWindow();
			Owner = main;

			settingsArray = Configuration.LoadConfigs();

			EventManager.RegisterClassHandler(typeof(TextBox), MouseDoubleClickEvent, new RoutedEventHandler(SelectAddress));
			EventManager.RegisterClassHandler(typeof(TextBox), GotKeyboardFocusEvent, new RoutedEventHandler(SelectAddress));
			EventManager.RegisterClassHandler(typeof(TextBox), PreviewMouseLeftButtonDownEvent,
				new MouseButtonEventHandler(SelectivelyIgnoreMouseButton));
			EventManager.RegisterClassHandler(typeof(PasswordBox), MouseDoubleClickEvent, new RoutedEventHandler(SelectAddress));
			EventManager.RegisterClassHandler(typeof(PasswordBox), GotKeyboardFocusEvent, new RoutedEventHandler(SelectAddress));
			EventManager.RegisterClassHandler(typeof(PasswordBox), PreviewMouseLeftButtonDownEvent,
				new MouseButtonEventHandler(SelectivelyIgnoreMouseButton));
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			InitSettingsChange();
		}

		private void InitSettingsChange()
		{
			service = null;

			// make sure something valid is selected
			settingsArray.SelectedSettingsIndex = Math.Max(0, settingsArray.SelectedSettingsIndex);

			settings = settingsArray.GetSelectedSettings();

			txtPassword.Password = settings.Password; // PasswordBox doesn't allow 2 way binding
			DataContext = settings;

			ComboBoxSettings.DataContext = settingsArray;
			ComboBoxSettings.DisplayMemberPath = "DisplayName";

			if (settings.OrgList.Contains(settings.CrmOrg) == false)
			{
				settings.OrgList.Add(settings.CrmOrg);
			}
			Organization.SelectedItem = settings.CrmOrg;

			if (settings.OrgList.Contains(settings.CrmOrg) == false)
			{
				settings.OrgList.Add(settings.CrmOrg);
			}
			Organization.SelectedItem = settings.CrmOrg;

			settings.EntityDataFilterArray = settings.EntityDataFilterArray ?? new EntityFilterArray();
			settings.FiltersChanged();

			mapper = new Mapper(settings);

			RegisterMapperEvents();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.HideMinimizeAndMaximizeButtons();
			this.HideCloseButton();
		}

		private void RegisterMapperEvents()
		{
			mapper.PropertyChanged
				+= (o, args) =>
				   {
					   try
					   {
						   switch (args.PropertyName)
						   {
							   case "Progress":
								   ShowBusy(mapper.ProgressMessage, mapper.Progress);
								   break;

							   case "WorkingOnEntities":
								   UpdateStatus(mapper.Progress + "%, fetching: " + string.Join(", ", mapper.WorkingOnEntities) + "...",
									   true, mapper.Progress <= 0 || mapper.Progress >= 100);
								   break;

							   case "LogMessage":
								   lock (mapper.LoggingLock)
								   {
									   UpdateStatus(mapper.LogMessage, true, mapper.Progress <= 0 || mapper.Progress >= 100);
								   }
								   break;

							   case "CancelMapping":
								   if (mapper.CancelMapping)
								   {
									   UpdateStatus("Cancelled generator!", false);
									   _StillOpen = false;
									   Dispatcher.InvokeAsync(Close);
								   }
								   break;

							   case "Error":
								   break;

							   case "Context":
								   if (mapper.Context != null)
								   {
									   Context = mapper.Context;
									   Context.SplitFiles = settings.SplitFiles;
									   Context.UseDisplayNames = settings.UseDisplayNames;
									   Context.IsUseCustomDictionary = settings.IsUseCustomDictionary;
									   Context.IsGenerateLoadPerRelation = settings.IsGenerateLoadPerRelation;
									   Context.GenerateOptionSetLabelsInEntity = settings.GenerateOptionSetLabelsInEntity;
									   Context.GenerateLookupLabelsInEntity = settings.GenerateLookupLabelsInEntity;
									   Context.GenerateOptionSetLabelsInContract = settings.GenerateOptionSetLabelsInContract;
									   Context.GenerateLookupLabelsInContract = settings.GenerateLookupLabelsInContract;
									   Context.GenerateGlobalActions = settings.GenerateGlobalActions;
									   Context.PluginMetadataEntities = settings.PluginMetadataEntitiesSelected.ToList();
									   Context.OptionsetLabelsEntities = settings.OptionsetLabelsEntitiesSelected.ToList();
									   Context.LookupLabelsEntities = settings.LookupLabelsEntitiesSelected.ToList();
									   Context.JsEarlyBoundEntities = settings.JsEarlyBoundEntitiesSelected.ToList();
									   Context.ActionEntities = settings.ActionEntitiesSelected.ToList();
									   Context.ClearMode = settings.ClearMode;

									   settings.Context = Context;

									   if (settings.LockNamesOnGenerate)
									   {
										   LockNames();
									   }

									   Configuration.SaveConfigs(settingsArray);

									   _StillOpen = false;
									   Dispatcher.InvokeAsync(Close);
								   }
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

		private void RefreshOrgs(object sender, RoutedEventArgs e)
		{
			settings.Password = ((PasswordBox) ((Button) sender).CommandParameter).Password;
			// PasswordBox doesn't allow 2 way binding, so we have to manually read it
			new Thread(() =>
			           {
				           try
				           {
					           UpdateStatus("Refreshing organisations ...", true);
					           var newOrgs = ConnectionHelper.GetOrgList(settings);
					           settings.OrgList = newOrgs;
				           }
				           catch (Exception ex)
				           {
					           PopException(ex);
				           }
				           finally
				           {
					           UpdateStatus(">>> Finished refreshing organisations.", false);
				           }
			           }).Start();
		}

		private void IncludeNonStandardEntities_Click(object sender, RoutedEventArgs e)
		{
			new Thread(() =>
				        {
					        try
					        {
						        UpdateStatus("Processing non-standard inclusion/exclusion ... ", true);
								EntityHelper.RefreshSettingsEntityMetadata(Service, settings);
							}
							catch (Exception ex)
					        {
						        PopException(ex);
					        }
					        finally
					        {
						        UpdateStatus(">>> Finished processing.", false);
					        }
				        }).Start();
		}

		#endregion

		#region Status stuff

		private void PopException(Exception exception)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  var message = exception.Message
				                                + (exception.InnerException != null ? "\n" + exception.InnerException.Message : "");
				                  MessageBox.Show(message, exception.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);

				                  var error = "[ERROR] " + exception.Message
				                              +
				                              (exception.InnerException != null
					                               ? "\n" + "[ERROR] " + exception.InnerException.Message
					                               : "");
				                  UpdateStatus(error, false);
				                  UpdateStatus(exception.StackTrace, false);
			                  });
		}

		private void ShowBusy(string message, double? progress = null)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = true;
				                  BusyIndicator.BusyContent =
					                  string.IsNullOrEmpty(message) ? "Please wait ..." : message;

				                  if (progress == null)
				                  {
					                  BusyIndicator.ProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;
				                  }
				                  else
				                  {
					                  originalProgressBarStyle = originalProgressBarStyle ?? BusyIndicator.ProgressBarStyle;

					                  var style = new Style(typeof (ProgressBar));
					                  style.Setters.Add(new Setter(HeightProperty, 15d));
					                  style.Setters.Add(new Setter(RangeBase.ValueProperty, progress));
					                  style.Setters.Add(new Setter(RangeBase.MaximumProperty, 100d));
					                  BusyIndicator.ProgressBarStyle = style;
				                  }
			                  }, DispatcherPriority.Send);
		}

		private void HideBusy()
		{
			Dispatcher.Invoke(() =>
			                  {
				                  BusyIndicator.IsBusy = false;
				                  BusyIndicator.BusyContent = "Please wait ...";
			                  }, DispatcherPriority.Send);
		}

		internal void UpdateStatus(string message, bool working, bool allowBusy = true, bool newLine = true)
		{
			//Dispatcher.Invoke(() => SetEnabledChildren(Inputs, !working, "ButtonCancel"));

			if (allowBusy)
			{
				if (working)
				{
					ShowBusy(message);
				}
				else
				{
					HideBusy();
				}
			}

			if (!string.IsNullOrWhiteSpace(message))
			{
				Dispatcher.BeginInvoke(new Action(() => { Status.Update(message, newLine); }));
			}

			Application.DoEvents();
			// Needed to allow the output window to update (also allows the cursor wait and form disable to show up)
		}

		#endregion

		#region UI events

		private void Logon_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// make sure that all entities selected in profiles are included
				var missingEntities = settings.EntityDataFilterArray.EntityFilters
					.SelectMany(filter => filter.EntityFilterList
					.Where(dataFilter => !dataFilter.IsExcluded || dataFilter.IsGenerateMeta)
					.Select(dataFilter => dataFilter.LogicalName))
					.Distinct().Except(settings.EntitiesSelected).ToList();

				foreach (var missingEntity in missingEntities)
				{
					settings.EntitiesSelected.Add(missingEntity);
				}

				settings.Password = ((PasswordBox) ((Button) sender).CommandParameter).Password;
				// PasswordBox doesn't allow 2 way binding, so we have to manually read it
				settings.Dirty = true;

				//  TODO Because the EntitiesSelected is a collection, the Settings class can't see when an item is added or removed.  when I have more time need to get the observable to bubble up.

				// in case of a re-run (while GUI is still open), reset the metadata cache to solve the problem of corrupt data
				var settingsNew = Configuration.LoadConfigs().GetSelectedSettings();
				settings.EntityMetadataCache = settingsNew.EntityMetadataCache;

				Configuration.SaveConfigs(settingsArray);

				// if user indicated 'clear cache'
				if (CheckBoxClearCache.IsChecked == true)
				{
					UpdateStatus("Clearing cache ... ", true, true, false);
					settings.EntityMetadataCache.Clear();
					settings.LookupEntitiesMetadataCache = null;
					UpdateStatus("done!", false);
				}

				UpdateStatus("Mapping entities, this might take a while depending on CRM server/connection speed ... ", true);

				// check user's 'split files'
				if (settings.SplitFiles)
				{
					UpdateStatus("Generator will split generated code into separate entity files.", true);
				}

				new Thread(
					() =>
					{
						try
						{
							mapper.MapContext();
						}
						catch (Exception ex)
						{
							PopException(ex);
						}
					}).Start();
			}
			catch (Exception ex)
			{
				PopException(ex);
			}
		}

		private void LogonCached_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// make sure that all entities selected in profiles are included
				var missingEntities = settings.EntityDataFilterArray.EntityFilters
					.SelectMany(filter => filter.EntityFilterList
					.Where(dataFilter => !dataFilter.IsExcluded || dataFilter.IsGenerateMeta)
					.Select(dataFilter => dataFilter.LogicalName))
					.Distinct().Except(settings.EntitiesSelected).ToList();

				foreach (var missingEntity in missingEntities)
				{
					settings.EntitiesSelected.Add(missingEntity);
				}

				if (settings.EntitiesSelected
					    .Intersect(settings.Context.Entities.Select(entity => entity.LogicalName))
					    .Count() < settings.EntitiesSelected.Count)
				{
					throw new Exception("There are new entities selected that need to be fetched from CRM. " +
					                    "Either deselect the new entities and use the cache; " +
					                    "connect and try again using the 'generate' button; or cancel, reopen, and then reconfigure.");
				}

				settings.Password = ((PasswordBox)((Button)sender).CommandParameter).Password;
				// PasswordBox doesn't allow 2 way binding, so we have to manually read it
				settings.Dirty = true;

				// in case of a re-run (while GUI is still open), reset the metadata cache to solve the problem of corrupt data
				var settingsNew = Configuration.LoadConfigs().GetSelectedSettings();
				settings.EntityMetadataCache = settingsNew.EntityMetadataCache;

				Configuration.SaveConfigs(settingsArray);

				UpdateStatus("Mapping entities using cache ... ", true);

				// check user's 'split files'
				if (settings.SplitFiles)
				{
					UpdateStatus("Generator will split generated code into separate entity files.", true);
				}

				new Thread(
					() =>
					{
						try
						{
							mapper.MapContext(true, settings.Context);
						}
						catch (Exception ex)
						{
							PopException(ex);
						}
					}).Start();
			}
			catch (Exception ex)
			{
				PopException(ex);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			mapper.CancelMapping = true;
			//UpdateStatus("Cancelled generator!", false);
			//_StillOpen = false;
			//Close();
		}

		private void ButtonCredits_Click(object sender, RoutedEventArgs e)
		{
			new Credits(this).ShowDialog();
		}

		private void ButtonOptions_Click(object sender, RoutedEventArgs e)
		{
			new Options(this, settings).ShowDialog();
		}

		private void ButtonCancel_Click(object sender, RoutedEventArgs e)
		{
			mapper.CancelMapping = true;
		}

		private void ButtonNewSettings_Click(object sender, RoutedEventArgs e)
		{
			var newSettings = new Settings();
			settingsArray.SettingsList.Add(newSettings);
			settingsArray.SelectedSettingsIndex = settingsArray.SettingsList.IndexOf(newSettings);
			//InitSettingsChange();
		}

		private void ButtonDuplicateSettings_Click(object sender, RoutedEventArgs e)
		{
			var newSettings = ObjectCopier.Clone(settingsArray.GetSelectedSettings());
			settingsArray.SettingsList.Add(newSettings);
			settingsArray.SelectedSettingsIndex = settingsArray.SettingsList.IndexOf(newSettings);

			DteHelper.ShowInfo("The profile chosen has been duplicated, and the duplicate is now the " +
			                   "selected profile.", "Profile duplicated!");
		}

		private void ButtonDeleteSettings_Click(object sender, RoutedEventArgs e)
		{
			if (settingsArray.SettingsList.Count <= 1)
			{
				PopException(new Exception("Can't delete the last settings profile."));
				return;
			}

			if (DteHelper.IsConfirmed("Are you sure you want to delete this settings profile?",
				"Confirm delete action ..."))
			{
				settingsArray.SettingsList.Remove(settingsArray.GetSelectedSettings());
				settingsArray.SelectedSettingsIndex = 0; 
			}
		}

		private void ButtonSaveSettings_Click(object sender, RoutedEventArgs e)
		{
			Configuration.SaveConfigs(settingsArray);
			DteHelper.ShowInfo("All settings profiles have been saved to disk.", "Settings saved!");
		}

		private void ComboBoxSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			InitSettingsChange();
		}

		private void EntitiesRefresh_Click(object sender, RoutedEventArgs events)
		{
			settings.Password = ((PasswordBox)((Button)sender).CommandParameter).Password;
			new EntitySelection(this, settings, service).ShowDialog();
		}

		private void EntitiesProfiling_Click(object sender, RoutedEventArgs e)
		{
			settings.Password = ((PasswordBox)((Button)sender).CommandParameter).Password;
			new Filter(this, settings, service).ShowDialog();
		}

		// credit: https://social.msdn.microsoft.com/Forums/vstudio/en-US/564b5731-af8a-49bf-b297-6d179615819f/how-to-selectall-in-textbox-when-textbox-gets-focus-by-mouse-click?forum=wpf&prof=required

		#region Textbox selection

		private static void SelectAddress(object sender, RoutedEventArgs e)
		{
			if (sender is TextBox || sender is PasswordBox)
			{
				((dynamic)sender).SelectAll();
			}
		}

		private static void SelectivelyIgnoreMouseButton(object sender, MouseButtonEventArgs e)
		{
			if (!(sender is TextBox || sender is PasswordBox))
			{
				return;
			}

			var tb = (dynamic)sender;

			if (tb.IsKeyboardFocusWithin)
			{
				return;
			}

			e.Handled = true;
			tb.Focus();
		}

		#endregion

		#endregion

		private void LockNames()
		{
			try
			{
				Status.Update("Locking friendly names ... ", false);

				foreach (var filter in settings.EntityDataFilterArray.EntityFilters.Select(filterList => filterList)
					.SelectMany(filter => filter.EntityFilterList))
				{
					// if filter's entity exists in selected entities
					var entity = Context.Entities.FirstOrDefault(entityQ => entityQ.LogicalName == filter.LogicalName);

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
				PopException(ex);
			}
			finally
			{
				Status.Update("done!");
			}
		}
	}
}
