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
using Constants = CrmCodeGenerator.VSPackage.Model.Constants;
using Settings = Yagasoft.CrmCodeGenerator.Models.Settings.Settings;
using Thread = System.Threading.Thread;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public class Configuration
	{
		private static readonly object lockObj = new object();

		public static string FileName;

		public static Settings LoadSettings()
		{
			string connectionString = null;

			string baseFileName = null;

			try
			{
				Status.Update("Loading settings ... ");

				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var project = dte.GetSelectedProject();
				baseFileName = $@"{project.GetPath()}\{FileName}";

				connectionString = LoadConnection(baseFileName);

				var file = $@"{baseFileName}-Config.json";

				if (File.Exists(file))
				{
					Status.Update($"\tFound settings file: {file}.");
					Status.Update($"\tReading content ...");

					var fileContent = File.ReadAllText(file);
					var settings = JsonConvert.DeserializeObject<Settings>(fileContent,
						new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

					if (settings.EntityProfilesHeaderSelector == null)
					{
						var oldSettings = JsonConvert.DeserializeObject<Model.OldSettings.Settings>(fileContent,
							new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

						if (oldSettings.EntityDataFilterArray?.EntityFilters.Any() == true)
						{
							Status.Update($"\tMigrating pre-v9 JSON settings ...");
							MigrateOldSettings(settings, oldSettings);
						}
					}

					if (settings.CrmEntityProfiles == null)
					{
						var oldSettings = JsonConvert.DeserializeObject<Model.OldSettings2.Settings>(fileContent,
							new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

						if (oldSettings.EntityProfilesHeaderSelector?.EntityProfilesHeaders?.Any() == true)
						{
							Status.Update($"\tMigrating pre-v10 JSON settings ...");
							MigrateOldSettings2(settings, oldSettings);
						}
					}

					settings.ConnectionString = connectionString ?? settings.ConnectionString;
					settings.AppId = settings.AppId ?? Constants.AppId;
					settings.AppVersion = settings.AppVersion ?? Constants.AppVersion;
					settings.SettingsVersion = settings.SettingsVersion ?? Constants.SettingsVersion;
					settings.BaseFileName = FileName;

					SetTemplateInfo(settings, baseFileName);

					Status.Update(">> Finished loading settings.");

					return settings;
				}

				Status.Update("\tSettings file does not exist.");
			}
			catch (Exception ex)
			{
				Status.Update("\t [ERROR] Failed to load settings.");
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

			Status.Update(">> Created new settings.");

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

					Status.Update("\t [ERROR] Could not find template file.");
					return;
				}

				Status.Update($"\tFound template file: {file}.");
				Status.Update($"\tReading content ...");

				var fileContent = File.ReadAllText(file);

				var templateInfo = TemplateHelpers.ParseTemplateInfo(fileContent);

				if (templateInfo?.DetectedTemplateVersion.IsFilled() == true)
				{
					settings.DetectedTemplateVersion = templateInfo.DetectedTemplateVersion;
					Status.Update($"\tTemplate version: {settings.DetectedTemplateVersion}.");
				}
				else
				{
					Status.Update($"\tCould not detect template version.");
				}

				if (templateInfo?.DetectedMinAppVersion.IsFilled() == true)
				{
					settings.DetectedMinAppVersion = templateInfo.DetectedMinAppVersion;
					Status.Update($"\tMinimum app version: {settings.DetectedMinAppVersion}.");
				}
				else
				{
					Status.Update($"\tCould not detect minimum app version.");
				}
			}
			catch
			{
				Status.Update("\t [ERROR] Failed to detect template version.");
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

		private static string LoadConnection(string baseFileName)
		{
			var file = $@"{baseFileName}-Connection.dat";

			if (!File.Exists(file))
			{
				return null;
			}

			Status.Update($"\tFound connection file: {file}.");
			Status.Update($"\tReading content ...");

			var fileContent = File.ReadAllText(file);
			var connectionString = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent));

			Status.Update($"\tConnection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");

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
				var cache = LoadCache();

				if (cache?.LatestUsedConnectionString.IsNotEmpty() == true)
				{
					newSettings.ConnectionString = cache.LatestUsedConnectionString;
				}
			}

			return newSettings;
		}

		public static MetadataCacheArray LoadCache()
		{
			lock (lockObj)
			{
				try
				{
					var cache = CacheHelpers.GetFromMemCache<MetadataCacheArray>(Constants.MetaCacheMemKey);

					if (cache != null)
					{
						return cache;
					}

					Status.Update("Loading cache ... ");

					var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
					var file = $@"{dte.Solution.GetPath()}\{FileName}-Cache.dat";

					if (File.Exists(file))
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
									Status.Update("\tRetrieved latest settings file from TFS' current workspace.");
								}
							}
						}
						catch (Exception)
						{
							// ignored
						}

						//Open the file written above and read values from it.
						using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
						{
							var bformatter = new BinaryFormatter { Binder = new Binder() };
							stream.Position = 0;
							cache = bformatter.Deserialize(stream) as MetadataCacheArray;

							if (cache == null)
							{
								throw new Exception("Invalid settings format.");
							}

							CacheHelpers.AddToMemCache(Constants.MetaCacheMemKey, cache);

							Status.Update(">> Finished loading cache.");

							return cache;
						}
					}
					else
					{
						Status.Update("[ERROR] cache file does not exist.");
					}
				}
				catch (Exception ex)
				{
					Status.Update("Failed to read cache => " + ex.BuildExceptionMessage(isUseExStackTrace: true));
				}

				return CacheHelpers.AddToMemCache(Constants.MetaCacheMemKey, new MetadataCacheArray());
			}
		}

		public static void SaveSettings(Settings settings)
		{
			Status.Update("Writing settings ... ");

			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var baseFileName = $@"{project.GetPath()}\{FileName}";

			SaveConnection(settings, baseFileName);

			var file = $@"{baseFileName}-Config.json";

			if (!File.Exists(file))
			{
				File.Create(file).Dispose();
				project.ProjectItems.AddFromFile(file);
				project.Save();
				Status.Update("\tCreated a new settings file.");
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

			Status.Update("\tCleaning redundant settings ...");

			if (settings.IsCleanSave)
			{
				CleanSettings(settings);
			}

			Status.Update("\tSerialising settings ...");
			var serialisedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented,
				new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

			Status.Update("\tWriting to file ...");
			File.WriteAllText(file, serialisedSettings);

			Status.Update(">> Finished writing settings.");
		}

		private static void SaveConnection(Settings settings, string baseFileName)
		{
			var file = $@"{baseFileName}-Connection.dat";
			var connectionString = settings.ConnectionString;

			if (!File.Exists(file) && connectionString.IsFilled())
			{
				File.Create(file).Dispose();
				Status.Update("\tCreated a new connection file.");
			}

			if (!File.Exists(file))
			{
				return;
			}

			Status.Update("\tWriting to file ...");

			var encodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.ConnectionString));
			File.WriteAllText(file, encodedString);

			Status.Update($"\tConnection string: {ConnectionHelpers.SecureConnectionString(connectionString)}");
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
			Status.Update("\tRetrieved latest settings file from TFS' current workspace.");

			workspace.PendEdit(file);
			Status.Update("\tChecked out settings file from TFS' current workspace.");
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

		public static void SaveCache()
		{
			SaveCache(LoadCache());
		}

		public static void SaveCache(MetadataCacheArray metadataCache)
		{
			Status.Update("Writing cache ... ");

			var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
			var file = $@"{dte.Solution.GetPath()}\{FileName}-Cache.dat";

			lock (lockObj)
			{
				if (!File.Exists(file))
				{
					File.Create(file).Dispose();
					Status.Update("\tCreated a new cache file.");
				}

				new Thread(
					() =>
					{
						Status.Update("\t Moved write operation to a new thread.");

						using (var stream = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
						{
						// clear the file to start from scratch
						stream.SetLength(0);

							var bformatter = new BinaryFormatter { Binder = new Binder() };
							bformatter.Serialize(stream, metadataCache);

							Status.Update(">> Finished writing cache.");
						}
					}).Start(); 
			}
		}
	}
}
