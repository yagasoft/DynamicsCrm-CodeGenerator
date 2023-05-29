#region Imports

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
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
using Yagasoft.CrmCodeGenerator.Models;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using CacheHelpers = Yagasoft.CrmCodeGenerator.Helpers.CacheHelpers;
using ClearModeEnum = Yagasoft.CrmCodeGenerator.Models.Settings.ClearModeEnum;
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
			try
			{
				Status.Update("[Settings] Loading settings ... ");

				var baseFileName = BuildBaseFileName();

				var file = $@"{baseFileName}-Config.json";

				if (File.Exists(file))
				{
					Status.Update($"[Settings] Found settings file: {file}.");
					Status.Update($"[Settings] Reading content ...");

					var fileContent = File.ReadAllText(file);
					var settings = JsonConvert.DeserializeObject<Settings>(fileContent,
						new JsonSerializerSettings
						{
							DefaultValueHandling = DefaultValueHandling.Ignore,
							NullValueHandling = NullValueHandling.Ignore
						});

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

					ProcessSettings(settings);

					return settings;
				}

				file = $@"{baseFileName}.dat";

				if (File.Exists(file))
				{
					Status.Update($"[Settings] Migrating pre-v7 JSON settings ...");
					var settings = MigrateOldSettings3(file);

					ProcessSettings(settings);

					return settings;
				}

				Status.Update("[Settings] Settings file does not exist.");
			}
			catch (Exception ex)
			{
				Status.Update("!! [Settings] ![ERROR]! Failed to load settings.");
				Status.Update(ex.BuildExceptionMessage(isUseExStackTrace: true));
			}

			return CreateNewSettings();
		}

		private static string BuildBaseFileName()
		{
			return $@"{GetProjectObject().GetPath()}\{FileName}";
		}

		private static Project GetProjectObject()
		{
			var project = ((DTE)Package.GetGlobalService(typeof(SDTE))).GetSelectedProject();
			return project;
		}

		public static Settings CreateNewSettings()
		{
			Status.Update("[Settings] Creating new settings ...");

			var newSettings = new Settings();
			ProcessSettings(newSettings);

			Status.Update("[Settings] [DONE] Creating new settings.");

			return newSettings;
		}

		private static void ProcessSettings(Settings settings)
		{
			settings.AppId ??= Constants.AppId;
			settings.AppVersion ??= Constants.AppVersion;
			settings.SettingsVersion = Constants.SettingsVersion;
			settings.BaseFileName = FileName;

			settings.ReplacementStrings ??= LoadReplacementChars()
				??
				new[]
				{
					new[] { "ذ", "z" }, new[] { "ض", "d" }, new[] { "ص", "s" }, new[] { "ث", "s" }, new[] { "ق", "k" },
					new[] { "ف", "f" }, new[] { "غ", "gh" }, new[] { "ع", "a" }, new[] { "ه", "h" }, new[] { "خ", "kh" },
					new[] { "ح", "h" }, new[] { "ج", "g" }, new[] { "ش", "sh" }, new[] { "س", "s" }, new[] { "ي", "y" },
					new[] { "ب", "b" }, new[] { "ل", "l" }, new[] { "ا", "a" }, new[] { "ت", "t" }, new[] { "ن", "n" },
					new[] { "م", "m" }, new[] { "ك", "k" }, new[] { "ط", "t" }, new[] { "ئ", "ea" }, new[] { "ء", "a" },
					new[] { "ؤ", "oa" }, new[] { "ر", "r" }, new[] { "لا", "la" }, new[] { "ى", "y" }, new[] { "ة", "t" },
					new[] { "و", "o" }, new[] { "ز", "th" }, new[] { "ظ", "z" }, new[] { "لإ", "la" }, new[] { "إ", "e" },
					new[] { "أ", "a" }, new[] { "لأ", "la" }, new[] { "لآ", "la" }, new[] { "آ", "a" }
				};

			LoadConnection(settings);
			SetTemplateInfo(settings);

			Status.Update("[Settings] [DONE] Loading settings.");
		}

		private static void SetTemplateInfo(Settings settings, bool isRetry = false)
		{
			var baseFileName = BuildBaseFileName();

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
						SetTemplateInfo(settings, true);
						return;
					}

					Status.Update("!! [Template] ![ERROR]! Could not find template file.");
					return;
				}

				Status.Update($"[Template] Found template file: {file}.");
				Status.Update($"[Template] Reading content ...");

				var bytes = new byte[2000];

				using (var reader = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					reader.Seek(500, SeekOrigin.Begin);
					reader.Read(bytes, 0, bytes.Length);
				}

				var templateInfo = TemplateHelpers.ParseTemplateInfo(Encoding.UTF8.GetString(bytes));

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
								IsIncluded = !ep.IsExcluded,
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
									IsIncluded = !efl.IsExcluded,
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

		private static Settings MigrateOldSettings3(string file)
		{
			// get latest file if in TFS
			try
			{
				var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(file);

				if (workspaceInfo != null)
				{
					var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
					var workspace = workspaceInfo.GetWorkspace(server);

					var pending = workspace.GetPendingChanges(new[] { file });

					if (!pending.Any())
					{
						workspace.Get(new[] { file }, VersionSpec.Latest, RecursionType.Full, GetOptions.GetAll | GetOptions.Overwrite);
						Status.Update("[Settings] Retrieved latest settings file from TFS' current workspace.");
					}
				}
			}
			catch (Exception)
			{
				// ignored
			}

			SettingsArray oldSettings3;

			//Open the file written above and read values from it.
			using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var bformatter = new BinaryFormatter { Binder = new Binder() };
				stream.Position = 0;
				var settingsObject = bformatter.Deserialize(stream);

				if (settingsObject is SettingsArray array)
				{
					oldSettings3 = array;
				}
				else
				{
					throw new Exception("[Settings] Invalid settings format.");
				}

				Status.Update("[Settings] Finished reading settings file.");
			}

			Status.Update("[Settings] Converting settings ...");

			var selectedSettings = oldSettings3.GetSelectedSettings();
			selectedSettings.EntityMetadataCache = null;
			selectedSettings.LookupEntitiesMetadataCacheSerialised = null;
			selectedSettings.ProfileEntityMetadataCacheSerialised = null;
			selectedSettings.ProfileAttributeMetadataCacheSerialised = null;
			selectedSettings.Context = null;
			selectedSettings.Folder = "";
			selectedSettings.Template = "";

			var serialisedSettings = JsonConvert.SerializeObject(selectedSettings);
			var oldSettings = JsonConvert.DeserializeObject<Model.OldSettings.Settings>(serialisedSettings);

			oldSettings.ConnectionString = selectedSettings.GetOrganizationCrmConnectionString();

			serialisedSettings = JsonConvert.SerializeObject(oldSettings);
			var newSettings = JsonConvert.DeserializeObject<Settings>(serialisedSettings);

			if (oldSettings.EntityDataFilterArray?.EntityFilters.Any() == true)
			{
				MigrateOldSettings(newSettings, oldSettings);
			}
			
			Status.Update("[Settings] Deleting old settings file ...");

			var project = GetProjectObject();

			foreach (var item in project.ProjectItems.Cast<ProjectItem>().Where(item => item.Name == Path.GetFileName(file)))
			{
				item.Delete();
			}

			project.Save();

			Status.Update("[Settings] Finished deleting old settings file.");

			Status.Update("[Settings] Finished loading settings.");

			return newSettings;
		}

		private static string LoadConnection(Settings settings)
		{
			var baseFileName = BuildBaseFileName();

			var file = $@"{baseFileName}-Connection.dat";

			string connectionString = null;

			if (File.Exists(file))
			{
				Status.Update($"[Settings] Found connection file: {file}.");
				Status.Update($"[Settings] Reading content ...");

				var fileContent = File.ReadAllText(file);
				connectionString = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent));

				Status.Update($"[Settings] Connection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");
			}

			if (connectionString.IsFilled())
			{
				settings.ConnectionString = connectionString;
			}
			else
			{
				var latest = LoadLatestConnectionString();

				if (latest.IsNotEmpty())
				{
					connectionString = settings.ConnectionString = latest;
					Status.Update($"[Settings] Connection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");
				}
			}

			return connectionString;
		}

		public static void SaveSettings(Settings settings)
		{
			Status.Update("[Settings] Writing settings ... ");

			var baseFileName = BuildBaseFileName();

			SaveConnection(settings);

			var file = $@"{baseFileName}-Config.json";

			if (!File.Exists(file))
			{
				File.Create(file).Dispose();
				var project = GetProjectObject();
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

			Status.Update("[Settings] Ordering profiles ...");

			settings.EntitiesSelected = new ObservableCollection<string>(settings.EntitiesSelected.OrderBy(e => e));
			settings.CrmEntityProfiles = new List<EntityProfile>(settings.CrmEntityProfiles
				.OrderBy(e => e.LogicalName));

			settings.EntityProfilesHeaderSelector.EntityProfilesHeaders = new ObservableCollection<EntityProfilesHeader>(settings
				.EntityProfilesHeaderSelector.EntityProfilesHeaders.OrderBy(e => e.DisplayName));

			foreach (var header in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders)
			{
				header.EntityProfiles = new List<EntityProfile>(header.EntityProfiles
					.OrderBy(e => e.LogicalName));
			}

			Status.Update("[Settings] Cleaning redundant settings ...");

			if (settings.IsCleanSave)
			{
				CleanSettings(settings);
			}

			settings.ReplacementStrings = null;

			Status.Update("[Settings] Serialising settings ...");
			var serialisedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented,
				new JsonSerializerSettings
				{
					DefaultValueHandling = DefaultValueHandling.Ignore,
					NullValueHandling = NullValueHandling.Ignore
				});

			Status.Update("[Settings] Writing to file ...");
			File.WriteAllText(file, serialisedSettings);

			Status.Update("[Settings] [DONE] Writing settings.");
		}

		private static void SaveConnection(Settings settings)
		{
			SaveLatestConnectionString(settings.ConnectionString);

			var baseFileName = BuildBaseFileName();

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
			// clean redundant profiles
			foreach (var header in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders)
			{
				var groups = header.EntityProfiles.GroupBy(e => e.LogicalName);

				foreach (var group in groups.Where(e => e.Count() > 1))
				{
					foreach (var profile in group.Skip(1))
					{
						header.EntityProfiles.Remove(profile);
					}
				}
			}

			// clean redundant profiles
			var crmProfileGroups = settings.CrmEntityProfiles.GroupBy(e => e.LogicalName);

			foreach (var group in crmProfileGroups.Where(e => e.Count() > 1))
			{
				foreach (var profile in group.Skip(1))
				{
					settings.CrmEntityProfiles.Remove(profile);
				}
			}

			// set empty collections as null to save JSON
			var profiles = settings.EntityProfilesHeaderSelector.EntityProfilesHeaders
				.SelectMany(e => e.EntityProfiles)
				.Union(settings.CrmEntityProfiles);

			var mainCollections =
				new[]
				{
					nameof(EntityProfile.Attributes),
					nameof(EntityProfile.OneToN),
					nameof(EntityProfile.NToOne),
					nameof(EntityProfile.NToN)
				};

			foreach (var profile in profiles)
			{
				profile.Attributes?.RemoveEmpty();
				profile.AttributeRenames?.RemoveEmpty();
				profile.AttributeLanguages?.RemoveEmpty();
				profile.AttributeAnnotations?.RemoveEmpty();
				profile.ReadOnly?.RemoveEmpty();
				profile.ClearFlag?.RemoveEmpty();
				profile.OneToN?.RemoveEmpty();
				profile.OneToNRenames?.RemoveEmpty();
				profile.OneToNReadOnly?.RemoveEmpty();
				profile.NToOne?.RemoveEmpty();
				profile.NToOneRenames?.RemoveEmpty();
				profile.NToOneFlatten?.RemoveEmpty();
				profile.NToOneReadOnly?.RemoveEmpty();
				profile.NToN?.RemoveEmpty();
				profile.NToNRenames?.RemoveEmpty();
				profile.NToNReadOnly?.RemoveEmpty();

				var collectionsToNull =
					profile.GetType().GetProperties()
						.Where(p => !mainCollections.Contains(p.Name) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType));

				foreach (var info in collectionsToNull)
				{
					if (info.GetValue(profile) is IEnumerable e && e.IsEmpty())
					{
						info.SetValue(profile, null);
					}
				}
			}

			var isCleanProfiles = settings.IsRemoveUnselectedProfiles;

			foreach (var filter in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders)
			{
				CleanProfiles(filter.EntityProfiles, isCleanProfiles);
			}
		}

		private static void CleanProfiles(List<EntityProfile> entityProfiles, bool isCleanProfiles)
		{
			var list = entityProfiles.ToArray();

			for (var i = list.Length - 1; i >= 0; i--)
			{
				var dataFilter = list[i];

				var isKeepFilter = IsKeepProfile(dataFilter, isCleanProfiles);

				if (!isKeepFilter)
				{
					entityProfiles.RemoveAt(i);
				}
			}
		}

		private static bool IsKeepProfile(EntityProfile dataFilter, bool isCleanProfiles)
		{
			var isIncluded = dataFilter.IsIncluded;

			if (isCleanProfiles && !isIncluded)
			{
				return false;
			}

			var isEntityRenameFilled = dataFilter.EntityRename.IsFilled();
			var isEntityAnnotationsFilled = dataFilter.EntityAnnotations.IsFilled();
			var isIsGenerateMetaFilled = dataFilter.IsGenerateMeta;
			var isIsOptionsetLabelsFilled = dataFilter.IsOptionsetLabels;
			var isIsLookupLabelsFilled = dataFilter.IsLookupLabels;
			var isValueClearModeFilled = dataFilter.ValueClearMode != null;
			var isEnglishLabelFieldFilled = dataFilter.EnglishLabelField.IsFilled();
			var isCollectionsFilled = dataFilter.IsBasicDataFilled;

			var isKeepFilter = isIncluded || isEntityRenameFilled || isEntityAnnotationsFilled || isIsGenerateMetaFilled
				|| isIsOptionsetLabelsFilled || isIsLookupLabelsFilled
				|| isValueClearModeFilled || isEnglishLabelFieldFilled
				|| isCollectionsFilled;
			return isKeepFilter;
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

		private static string[][] LoadReplacementChars()
		{
			try
			{
				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var folder = $@"{dte.Solution.GetPath()}";

				Directory.CreateDirectory(folder);

				var file = $@"{folder}\ys-replacement-chars.json";

				if (!File.Exists(file))
				{
					return null;
				}

				var fileContent = File.ReadAllText(file);

				return JsonConvert.DeserializeObject<string[][]>(fileContent,
					new JsonSerializerSettings
					{
						DefaultValueHandling = DefaultValueHandling.Ignore,
						NullValueHandling = NullValueHandling.Ignore
					});
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
