#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model.OldSettings2;
using EnvDTE;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Helpers.Assembly;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using CacheHelpers = Yagasoft.CrmCodeGenerator.Helpers.CacheHelpers;
using Constants = CrmCodeGenerator.VSPackage.Model.Constants;
using Settings = Yagasoft.CrmCodeGenerator.Models.Settings.Settings;
using Thread = System.Threading.Thread;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public class Configuration
	{
		public static string FileName;

		public static Settings LoadSettings()
		{
			string connectionString = null;

			string baseFileName = null;

			try
			{
				Status.Update("[Settings] Loading settings ... ");

				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var project = dte.GetSelectedProject();
				baseFileName = $@"{project.GetPath()}\{FileName}";

				connectionString = LoadConnection();

				var file = $@"{baseFileName}-Config.json";

				if (File.Exists(file))
				{
					Status.Update($"[Settings] Found settings file: {file}.");
					Status.Update($"[Settings] Reading content ...");

					var fileContent = File.ReadAllText(file);
					var settings = JsonConvert.DeserializeObject<Settings>(fileContent,
						new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

					var isLatest = settings.SettingsVersion.IsFilled()
						&& new Version(settings.SettingsVersion) >= new Version(Constants.SettingsVersion);
					Status.Update($"[Settings] Settings version:"
						+ $" {(settings.SettingsVersion.IsFilled() ? settings.SettingsVersion : "--")}"
						+ $" {(isLatest ? "(latest)" : "(old)")}");

					if (settings.EntityProfilesHeaderSelector == null)
					{
						var oldSettings = JsonConvert.DeserializeObject<Model.OldSettings.Settings>(fileContent,
							new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

						if (oldSettings.EntityDataFilterArray?.EntityFilters.Any() == true)
						{
							Status.Update($"[Settings] Migrating pre-v9 JSON settings ...");
							MigrateOldSettings(settings, oldSettings);
						}
					}

					if (settings.CrmEntityProfiles == null)
					{
						var oldSettings = JsonConvert.DeserializeObject<Model.OldSettings2.Settings>(fileContent,
							new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

						if (oldSettings.EntityProfilesHeaderSelector?.EntityProfilesHeaders?.Any() == true)
						{
							Status.Update($"[Settings] Migrating pre-v10 JSON settings ...");
							MigrateOldSettings2(settings, oldSettings);
						}
					}

					settings.ConnectionString = connectionString ?? settings.ConnectionString;
					settings.AppId = settings.AppId ?? Constants.AppId;
					settings.AppVersion = settings.AppVersion ?? Constants.AppVersion;
					settings.SettingsVersion = Constants.SettingsVersion;
					settings.BaseFileName = FileName;

					SetTemplateInfo(settings, baseFileName);

					Status.Update("[Settings] [DONE] Loading settings.");

					return settings;
				}

				Status.Update("[Settings] Settings file does not exist.");
			}
			catch (Exception ex)
			{
				Status.Update("!! [Settings] ![ERROR]! Failed to load settings.");
				Status.Update(ex.BuildExceptionMessage(isUseExStackTrace: true));
			}

			var newSettings = CreateNewSettings(connectionString);
			newSettings.AppId = Constants.AppId;
			newSettings.AppVersion = Constants.AppVersion;
			newSettings.SettingsVersion = Constants.SettingsVersion;
			newSettings.BaseFileName = FileName;

			if (baseFileName.IsFilled())
			{
				SetTemplateInfo(newSettings, baseFileName);
			}

			Status.Update("[Settings] [DONE] Creating new settings.");

			return newSettings;
		}

		private static void SetTemplateInfo(Settings settings, string baseFileName, bool isRetry = false)
		{
			if (baseFileName.IsEmpty())
			{
				return;
			}

			try
			{
				var file = $"{baseFileName}.tt";

				if (!File.Exists(file))
				{
					if (!isRetry)
					{
						Thread.Sleep(500);
						SetTemplateInfo(settings, baseFileName, true);
						return;
					}

					Status.Update("!! [Template] ![ERROR]! Could not find template file.");
					return;
				}

				Status.Update($"[Template] Found template file: {file}.");
				Status.Update($"[Template] Reading content ...");

				var fileContent = File.ReadAllText(file);

				var templateInfo = TemplateHelpers.ParseTemplateInfo(fileContent);

				if (templateInfo?.DetectedTemplateVersion.IsFilled() == true)
				{
					settings.DetectedTemplateVersion = templateInfo.DetectedTemplateVersion;
					Status.Update($"[Template] Template version: {settings.DetectedTemplateVersion}.");
				}
				else
				{
					Status.Update($"!! [Template] ![ERROR]! Could not detect template version.");
				}

				if (templateInfo?.DetectedMinAppVersion.IsFilled() == true)
				{
					settings.DetectedMinAppVersion = templateInfo.DetectedMinAppVersion;
					Status.Update($"[Template] Minimum app version: {settings.DetectedMinAppVersion}.");
				}
				else
				{
					Status.Update($"!! [Template] ![ERROR]! Could not detect minimum app version.");
				}
			}
			catch
			{
				Status.Update("!! [Template] ![ERROR]! Failed to detect template version.");
			}
		}

		private static void MigrateOldSettings(Settings settings, Model.OldSettings.Settings oldSettings)
		{
			MigrateOldSettings2(settings,
				new Model.OldSettings2.Settings
				{
					EntityProfilesHeaderSelector =
						new EntityProfilesHeaderSelector_old2
						{
							SelectedFilterIndex = oldSettings.EntityDataFilterArray.SelectedFilterIndex,
							EntityProfilesHeaders = new ObservableCollection<EntityProfilesHeader_old2>(oldSettings
								.EntityDataFilterArray.EntityFilters
								.Select(ef =>
								new EntityProfilesHeader_old2
								{
									Prefix = ef.Prefix,
									Suffix = ef.Suffix,
									EntityProfiles = ef
										.EntityFilterList?
										.Select(efl =>
										new EntityProfile_old2(efl.LogicalName)
										{
											IsApplyToCrm = ef.IsDefault,
											Attributes = efl.Attributes,
											AttributeLanguages = efl.AttributeLanguages,
											AttributeRenames = efl.AttributeRenames,
											ClearFlag = efl.ClearFlag,
											EnglishLabelField = efl.EnglishLabelField,
											EntityRename = efl.EntityRename,
											IsExcluded = efl.IsExcluded,
											IsGenerateMeta = efl.IsGenerateMeta,
											IsLookupLabels = efl.IsLookupLabels,
											IsOptionsetLabels = efl.IsOptionsetLabels,
											LogicalName = efl.LogicalName,
											NToN = efl.NToN,
											NToNReadOnly = efl.NToNReadOnly,
											NToNRenames = efl.NToNRenames,
											NToOne = efl.NToOne,
											NToOneFlatten = efl.NToOneFlatten,
											NToOneReadOnly = efl.NToOneReadOnly,
											NToOneRenames = efl.NToOneRenames,
											OneToN = efl.OneToN,
											OneToNReadOnly = efl.OneToNReadOnly,
											OneToNRenames = efl.OneToNRenames,
											ReadOnly = efl.ReadOnly,
											ValueClearMode = (ClearModeEnum_old2?)efl.ValueClearMode
										}).ToList()
								}))
						}
				});
		}

		private static void MigrateOldSettings2(Settings settings, Model.OldSettings2.Settings oldSettings)
		{
			settings.CrmEntityProfiles =
				oldSettings
					.EntityProfilesHeaderSelector.EntityProfilesHeaders
					.SelectMany(ef => ef.EntityProfiles.Where(ep => ep.IsApplyToCrm)
						.Select(ep =>
							new EntityProfile(ep.LogicalName)
							{
								Attributes = ep.Attributes,
								AttributeLanguages = ep.AttributeLanguages,
								AttributeRenames = ep.AttributeRenames,
								ClearFlag = ep.ClearFlag,
								EnglishLabelField = ep.EnglishLabelField,
								EntityRename = ep.EntityRename,
								IsExcluded = ep.IsExcluded,
								IsGenerateMeta = ep.IsGenerateMeta,
								IsLookupLabels = ep.IsLookupLabels,
								IsOptionsetLabels = ep.IsOptionsetLabels,
								LogicalName = ep.LogicalName,
								NToN = ep.NToN,
								NToNReadOnly = ep.NToNReadOnly,
								NToNRenames = ep.NToNRenames,
								NToOne = ep.NToOne,
								NToOneFlatten = ep.NToOneFlatten,
								NToOneReadOnly = ep.NToOneReadOnly,
								NToOneRenames = ep.NToOneRenames,
								OneToN = ep.OneToN,
								OneToNReadOnly = ep.OneToNReadOnly,
								OneToNRenames = ep.OneToNRenames,
								ReadOnly = ep.ReadOnly,
								ValueClearMode = (ClearModeEnum?)ep.ValueClearMode
							})).ToList();

			settings.EntityProfilesHeaderSelector =
				new EntityProfilesHeaderSelector
				{
					SelectedFilterIndex = oldSettings.EntityProfilesHeaderSelector.SelectedFilterIndex,
					EntityProfilesHeaders = new ObservableCollection<EntityProfilesHeader>(oldSettings
						.EntityProfilesHeaderSelector.EntityProfilesHeaders
						.Select(ef =>
						new EntityProfilesHeader
						{
							Prefix = ef.Prefix,
							Suffix = ef.Suffix,
							EntityProfiles = ef
								.EntityProfiles?
								.Select(efl =>
								new EntityProfile(efl.LogicalName)
								{
									Attributes = efl.Attributes,
									AttributeLanguages = efl.AttributeLanguages,
									AttributeRenames = efl.AttributeRenames,
									ClearFlag = efl.ClearFlag,
									EnglishLabelField = efl.EnglishLabelField,
									EntityRename = efl.EntityRename,
									IsExcluded = efl.IsExcluded,
									IsGenerateMeta = efl.IsGenerateMeta,
									IsLookupLabels = efl.IsLookupLabels,
									IsOptionsetLabels = efl.IsOptionsetLabels,
									LogicalName = efl.LogicalName,
									NToN = efl.NToN,
									NToNReadOnly = efl.NToNReadOnly,
									NToNRenames = efl.NToNRenames,
									NToOne = efl.NToOne,
									NToOneFlatten = efl.NToOneFlatten,
									NToOneReadOnly = efl.NToOneReadOnly,
									NToOneRenames = efl.NToOneRenames,
									OneToN = efl.OneToN,
									OneToNReadOnly = efl.OneToNReadOnly,
									OneToNRenames = efl.OneToNRenames,
									ReadOnly = efl.ReadOnly,
									ValueClearMode = (ClearModeEnum?)efl.ValueClearMode
								}).ToList()
						}))
				};
		}

		private static string LoadConnection()
		{
			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var baseFileName = $@"{project.GetPath()}\{FileName}";

			var file = $@"{baseFileName}-Connection.dat";

			if (!File.Exists(file))
			{
				return null;
			}

			Status.Update($"[Settings] Found connection file: {file}.");
			Status.Update($"[Settings] Reading content ...");

			var fileContent = File.ReadAllText(file);
			var connectionString = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent));

			Status.Update($"[Settings] Connection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");

			return connectionString;
		}

		private static Settings CreateNewSettings(string connectionString)
		{
			var newSettings = new Settings();

			if (connectionString.IsFilled())
			{
				newSettings.ConnectionString = connectionString;
			}
			else
			{
				var latest = LoadLatestConnectionString();

				if (latest.IsNotEmpty())
				{
					newSettings.ConnectionString = latest;
				}
			}

			return newSettings;
		}

		public static void SaveSettings(Settings settings)
		{
			Status.Update("[Settings] Writing settings ... ");

			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var baseFileName = $@"{project.GetPath()}\{FileName}";

			SaveConnection(settings);

			var file = $@"{baseFileName}-Config.json";

			if (!File.Exists(file))
			{
				File.Create(file).Dispose();
				project.ProjectItems.AddFromFile(file);
				project.Save();
				Status.Update("[Settings] Created a new settings file.");
			}

			// check out file if in TFS
			try
			{
				CheckoutTfs(file);
			}
			catch (Exception)
			{
				// ignored
			}

			Status.Update("[Settings] Cleaning redundant settings ...");

			if (settings.IsCleanSave)
			{
				CleanSettings(settings);
			}

			Status.Update("[Settings] Serialising settings ...");
			var serialisedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented,
				new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

			Status.Update("[Settings] Writing to file ...");
			File.WriteAllText(file, serialisedSettings);

			Status.Update("[Settings] [DONE] Writing settings.");
		}

		private static void SaveConnection(Settings settings)
		{
			SaveLatestConnectionString(settings.ConnectionString);

			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var baseFileName = $@"{project.GetPath()}\{FileName}";

			var file = $@"{baseFileName}-Connection.dat";
			var connectionString = settings.ConnectionString;

			if (!File.Exists(file) && connectionString.IsFilled())
			{
				File.Create(file).Dispose();
				Status.Update("[Settings] Created a new connection file.");
			}

			if (!File.Exists(file))
			{
				return;
			}

			Status.Update("[Settings] Writing to file ...");

			var encodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes(connectionString));
			File.WriteAllText(file, encodedString);

			Status.Update($"[Settings] Connection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");
		}

		private static void CheckoutTfs(string file)
		{
			var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(file);

			if (workspaceInfo == null)
			{
				return;
			}

			var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
			var workspace = workspaceInfo.GetWorkspace(server);

			var pending = workspace.GetPendingChanges(new[] { file });

			if (pending.Any())
			{
				return;
			}

			workspace.Get(new[] { file }, VersionSpec.Latest, RecursionType.Full, GetOptions.GetAll | GetOptions.Overwrite);
			Status.Update("[Settings] Retrieved latest settings file from TFS' current workspace.");

			workspace.PendEdit(file);
			Status.Update("[Settings] Checked out settings file from TFS' current workspace.");
		}

		private static void CleanSettings(Settings settings)
		{
			var isCleanProfiles = settings.IsRemoveUnselectedProfiles;

			foreach (var filter in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders)
			{
				var list = filter.EntityProfiles.ToArray();

				for (var i = list.Length - 1; i >= 0; i--)
				{
					var dataFilter = list[i];

					var isIncluded = !dataFilter.IsExcluded;

					if (isCleanProfiles && !isIncluded)
					{
						filter.EntityProfiles.RemoveAt(i);
						continue;
					}

					dataFilter.Attributes?.RemoveEmpty();
					dataFilter.AttributeRenames?.RemoveEmpty();
					dataFilter.AttributeLanguages?.RemoveEmpty();
					dataFilter.ReadOnly?.RemoveEmpty();
					dataFilter.ClearFlag?.RemoveEmpty();
					dataFilter.OneToN?.RemoveEmpty();
					dataFilter.OneToNRenames?.RemoveEmpty();
					dataFilter.OneToNReadOnly?.RemoveEmpty();
					dataFilter.NToOne?.RemoveEmpty();
					dataFilter.NToOneRenames?.RemoveEmpty();
					dataFilter.NToOneFlatten?.RemoveEmpty();
					dataFilter.NToOneReadOnly?.RemoveEmpty();
					dataFilter.NToN?.RemoveEmpty();
					dataFilter.NToNRenames?.RemoveEmpty();
					dataFilter.NToNReadOnly?.RemoveEmpty();

					var isEntityRenameFilled = dataFilter.EntityRename.IsFilled();
					var isIsGenerateMetaFilled = dataFilter.IsGenerateMeta;
					var isIsOptionsetLabelsFilled = dataFilter.IsOptionsetLabels;
					var isIsLookupLabelsFilled = dataFilter.IsLookupLabels;
					var isValueClearModeFilled = dataFilter.ValueClearMode != null;
					var isEnglishLabelFieldFilled = dataFilter.EnglishLabelField.IsFilled();
					var isCollectionsFilled = dataFilter.IsBasicDataFilled;

					var isKeepFilter = isIncluded || isEntityRenameFilled || isIsGenerateMetaFilled
						|| isIsOptionsetLabelsFilled || isIsLookupLabelsFilled
						|| isValueClearModeFilled || isEnglishLabelFieldFilled
						|| isCollectionsFilled;

					if (!isKeepFilter)
					{
						filter.EntityProfiles.RemoveAt(i);
					}
				}
			}
		}

		public static MetadataCache LoadCache(Guid settingsId)
		{
			return CacheHelpers.LoadCache(settingsId, GetCachePath(), entry => Status.Update(entry));
		}

		public static void SaveCache(Guid settingsId)
		{
			CacheHelpers.SaveCache(settingsId, GetCachePath(), entry => Status.Update(entry));
		}

		private static string GetCachePath()
		{
			var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
			var folder = $@"{dte.Solution.GetPath()}\.ys\gen-ext\cache\meta";

			Directory.CreateDirectory(folder);

			return folder;
		}

		private static string LoadLatestConnectionString()
		{
			try
			{
				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var folder = $@"{dte.Solution.GetPath()}\.ys\gen-ext\cache";

				Directory.CreateDirectory(folder);

				var file = $@"{folder}\conn";

				if (!File.Exists(file))
				{
					return null;
				}

				var fileContent = File.ReadAllText(file);
				return Encoding.UTF8.GetString(Convert.FromBase64String(fileContent));
			}
			catch
			{
				return null;
			}
		}

		private static void SaveLatestConnectionString(string connectionString)
		{
			try
			{
				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var folder = $@"{dte.Solution.GetPath()}\.ys\gen-ext\cache";

				Directory.CreateDirectory(folder);

				var file = $@"{folder}\conn";

				if (!File.Exists(file) && connectionString.IsFilled())
				{
					File.Create(file).Dispose();
				}

				if (!File.Exists(file))
				{
					return;
				}

				var encodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes(connectionString));
				File.WriteAllText(file, encodedString);
			}
			catch
			{
				// ignored
			}
		}
	}
}
