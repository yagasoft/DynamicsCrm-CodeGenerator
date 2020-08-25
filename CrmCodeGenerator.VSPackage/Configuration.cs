#region Imports

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using CrmCodeGenerator.VSPackage.Helpers;
using EnvDTE;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Yagasoft.CrmCodeGenerator;
using Yagasoft.CrmCodeGenerator.Helpers;
using Yagasoft.CrmCodeGenerator.Models.Cache;
using Yagasoft.CrmCodeGenerator.Models.Settings;
using Yagasoft.Libraries.Common;
using Constants = CrmCodeGenerator.VSPackage.Model.Constants;
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

			try
			{
				Status.Update("Loading settings ... ");

				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var project = dte.GetSelectedProject();
				var baseFileName = $@"{project.GetPath()}\{FileName}";

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

						if (oldSettings.EntityDataFilterArray != null)
						{
							Status.Update($"\tMigrating old JSON settings ...");
							MigrateOldSettings(settings, oldSettings);
						}
					}

					settings.ConnectionString = connectionString ?? settings.ConnectionString;

					Status.Update(">>> Finished loading settings.");

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

			Status.Update(">>> Created new settings.");

			return newSettings;
		}

		private static void MigrateOldSettings(Settings settings, Model.OldSettings.Settings oldSettings)
		{
			settings.EntityProfilesHeaderSelector =
				new EntityProfilesHeaderSelector
				{
					SelectedFilterIndex = oldSettings.EntityDataFilterArray.SelectedFilterIndex,
					EntityProfilesHeaders = new ObservableCollection<EntityProfilesHeader>(oldSettings
						.EntityDataFilterArray.EntityFilters
						.Select(ef =>
						new EntityProfilesHeader
						{
							Prefix = ef.Prefix,
							Suffix = ef.Suffix,
							EntityProfiles = ef
								.EntityFilterList
								.Select(efl =>
								new EntityProfile(efl.LogicalName)
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
					var cache = CacheHelpers.GetFromMemCache<MetadataCacheArray>(Constants.CacheMemKey);

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

							CacheHelpers.AddToMemCache(Constants.CacheMemKey, cache);

							Status.Update(">>> Finished loading cache.");

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

				return CacheHelpers.AddToMemCache(Constants.CacheMemKey, new MetadataCacheArray());
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

			CleanSettings(settings);

			Status.Update("\tSerialising settings ...");
			var connectionString = settings.ConnectionString;
			settings.ConnectionString = null;
			var serialisedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented,
				new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });
			settings.ConnectionString = connectionString;

			Status.Update("\tWriting to file ...");
			File.WriteAllText(file, serialisedSettings);

			Status.Update(">>> Finished writing settings.");
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
			var isThorough = settings.IsCleanSave;

			foreach (var filter in settings.EntityProfilesHeaderSelector.EntityProfilesHeaders)
			{
				var list = filter.EntityProfiles.ToArray();

				for (var i = list.Length - 1; i >= 0; i--)
				{
					var dataFilter = list[i];

					if (dataFilter.IsExcluded && isThorough)
					{
						filter.EntityProfiles.RemoveAt(i);
						continue;
					}

					var isEntityRenameFilled = dataFilter.EntityRename.IsFilled();
					var isIsApplyToCrmFilled = dataFilter.IsApplyToCrm;
					var isIsGenerateMetaFilled = dataFilter.IsGenerateMeta;
					var isIsOptionsetLabelsFilled = dataFilter.IsOptionsetLabels;
					var isIsLookupLabelsFilled = dataFilter.IsLookupLabels;
					var isValueClearModeFilled = dataFilter.ValueClearMode != null;
					var isIsIncludeFilled = !dataFilter.IsExcluded;
					var isEnglishLabelFieldFilled = dataFilter.EnglishLabelField.IsFilled();
					var isIsFilteredFilled = dataFilter.IsFiltered;
					var isAttributesFilled = dataFilter.Attributes?.Any() == true;
					var isAttributeRenamesFilled = dataFilter.AttributeRenames?.Any() == true;
					var isAttributeLanguagesFilled = dataFilter.AttributeLanguages?.Any() == true;
					var isReadOnlyFilled = dataFilter.ReadOnly?.Any() == true;
					var isClearFlagFilled = dataFilter.ClearFlag?.Any() == true;
					var isOneToNFilled = dataFilter.OneToN?.Any() == true;
					var isOneToNRenamesFilled = dataFilter.OneToNRenames?.Any() == true;
					var isOneToNReadOnlyFilled = dataFilter.OneToNReadOnly?.Any() == true;
					var isNToOneFilled = dataFilter.NToOne?.Any() == true;
					var isNToOneRenamesFilled = dataFilter.NToOneRenames?.Any() == true;
					var isNToOneFlattenFilled = dataFilter.NToOneFlatten?.Any() == true;
					var isNToOneReadOnlyFilled = dataFilter.NToOneReadOnly?.Any() == true;
					var isNToNFilled = dataFilter.NToN?.Any() == true;
					var isNToNRenamesFilled = dataFilter.NToNRenames?.Any() == true;
					var isNToNReadOnlyFilled = dataFilter.NToNReadOnly?.Any() == true;

					var isImmediateExclude =
						isThorough && !isIsApplyToCrmFilled && !isAttributesFilled && !isOneToNFilled && !isNToOneFilled && !isNToNFilled;

					var isKeepFilter = isEntityRenameFilled || isIsApplyToCrmFilled || isIsGenerateMetaFilled
						|| isIsOptionsetLabelsFilled || isIsLookupLabelsFilled
						|| isValueClearModeFilled || isIsIncludeFilled || isEnglishLabelFieldFilled || isIsFilteredFilled;

					isKeepFilter = isKeepFilter
						|| (!isImmediateExclude
							&& (isAttributesFilled || isAttributeRenamesFilled || isAttributeLanguagesFilled || isReadOnlyFilled || isClearFlagFilled
								|| isOneToNFilled || isOneToNRenamesFilled || isOneToNReadOnlyFilled || isNToOneFilled || isNToOneRenamesFilled
								|| isNToOneFlattenFilled || isNToOneReadOnlyFilled || isNToNFilled || isNToNRenamesFilled || isNToNReadOnlyFilled));

					if (!isKeepFilter)
					{
						filter.EntityProfiles.RemoveAt(i);
					}
					else if (isThorough)
					{
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
			lock (lockObj)
			{
				Status.Update("Writing cache ... ");

				var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
				var file = $@"{dte.Solution.GetPath()}\{FileName}-Cache.dat";

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

							Status.Update(">>> Finished writing cache.");
						}
					}).Start(); 
			}
		}
	}
}
