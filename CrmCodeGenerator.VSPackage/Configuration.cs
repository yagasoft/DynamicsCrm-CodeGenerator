#region Imports

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using EnvDTE;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
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

		public static SettingsNew LoadSettings()
		{
			try
			{
				Status.Update("Loading settings ... ");

				var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
				var project = dte.GetSelectedProject();
				var file = $@"{project.GetPath()}\{FileName}-Config.json";

				if (File.Exists(file))
				{
					Status.Update($"\tFound settings file: {file}.");
					Status.Update($"\tReading content ...");

					var fileContent = File.ReadAllText(file);
					var settings = JsonConvert.DeserializeObject<SettingsNew>(fileContent,
						new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

					Status.Update(">>> Finished loading settings.");

					return settings;
				}

				file = $@"{project.GetPath()}\{FileName}.dat";

				if (File.Exists(file))
				{
					Status.Update("\tMigrating to new settings format ... ");
					return MigrateSettings(file, project);
				}

				Status.Update("\tSettings file does not exist.");
			}
			catch (Exception ex)
			{
				Status.Update("\t [ERROR] Failed to load settings.");
				Status.Update(ex.BuildExceptionMessage(isUseExStackTrace: true));
			}

			var newSettings = new SettingsNew();

			var cache = LoadCache();

			if (cache?.LatestUsedConnectionString.IsNotEmpty() == true)
			{
				newSettings.ConnectionString = cache.LatestUsedConnectionString;
			}

			Status.Update(">>> Created new settings.");

			return newSettings;
		}

		private static SettingsNew MigrateSettings(string file, Project project)
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

			SettingsArray settings;

			//Open the file written above and read values from it.
			using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var bformatter = new BinaryFormatter { Binder = new Binder() };
				stream.Position = 0;
				var settingsObject = bformatter.Deserialize(stream);

				if (settingsObject is SettingsArray array)
				{
					settings = array;
				}
				else
				{
					throw new Exception("\tInvalid settings format.");
				}

				Status.Update("\tFinished reading settings file.");
			}

			Status.Update("\tConverting settings ...");

			var selectedSettings = settings.GetSelectedSettings();
			selectedSettings.EntityMetadataCache = null;
			selectedSettings.LookupEntitiesMetadataCacheSerialised = null;
			selectedSettings.ProfileEntityMetadataCacheSerialised = null;
			selectedSettings.ProfileAttributeMetadataCacheSerialised = null;
			selectedSettings.Context = null;
			selectedSettings.Folder = "";
			selectedSettings.Template = "";

			var serialisedSettings = JsonConvert.SerializeObject(selectedSettings);
			var newSettings = JsonConvert.DeserializeObject<SettingsNew>(serialisedSettings);

			newSettings.ConnectionString = selectedSettings.GetOrganizationCrmConnectionString();

			SaveSettings(newSettings);

			Status.Update("\tDeleting old settings file ...");

			foreach (ProjectItem item in project.ProjectItems)
			{
				if (item.Name == Path.GetFileName(file))
				{
					item.Delete();
				}
			}

			project.Save();

			Status.Update("\tFinished deleting old settings file.");

			Status.Update(">>> Finished loading settings.");

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

		public static void SaveSettings(SettingsNew settings)
		{
			Status.Update("Writing settings ... ");

			var dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var file = $@"{project.GetPath()}\{FileName}-Config.json";

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

						workspace.PendEdit(file);
						Status.Update("\tChecked out settings file from TFS' current workspace.");
					}
				}
			}
			catch (Exception)
			{
				// ignored
			}

			Status.Update("\tCleaning redundant settings ...");

			CleanSettings(settings);

			Status.Update("\tSerialising settings ...");
			var serialisedSettings = JsonConvert.SerializeObject(settings, Formatting.Indented,
				new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });

			Status.Update("\tWriting to file ...");
			File.WriteAllText(file, serialisedSettings);

			Status.Update(">>> Finished writing settings.");
		}

		private static void CleanSettings(SettingsNew settings)
		{
			var isThorough = settings.IsCleanSave;

			foreach (var filter in settings.EntityDataFilterArray.EntityFilters)
			{
				var list = filter.EntityFilterList.ToArray();

				for (var i = list.Length - 1; i >= 0; i--)
				{
					var dataFilter = list[i];

					var isEntityRenameFilled = dataFilter.EntityRename.IsFilled();
					var isIsGenerateMetaFilled = dataFilter.IsGenerateMeta;
					var isIsOptionsetLabelsFilled = dataFilter.IsOptionsetLabels;
					var isIsLookupLabelsFilled = dataFilter.IsLookupLabels;
					var isValueClearModeFilled = dataFilter.ValueClearMode != null;
					var isIsExcludedFilled = !dataFilter.IsExcluded;
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
						isThorough && !filter.IsDefault && !isAttributesFilled && !isOneToNFilled && !isNToOneFilled && !isNToNFilled;

					var isKeepFilter = isEntityRenameFilled || isIsGenerateMetaFilled || isIsOptionsetLabelsFilled || isIsLookupLabelsFilled
						|| isValueClearModeFilled || isIsExcludedFilled || isEnglishLabelFieldFilled || isIsFilteredFilled;

					isKeepFilter = isKeepFilter
						|| (!isImmediateExclude
							&& (isAttributesFilled || isAttributeRenamesFilled || isAttributeLanguagesFilled || isReadOnlyFilled || isClearFlagFilled
								|| isOneToNFilled || isOneToNRenamesFilled || isOneToNReadOnlyFilled || isNToOneFilled || isNToOneRenamesFilled
								|| isNToOneFlattenFilled || isNToOneReadOnlyFilled || isNToNFilled || isNToNRenamesFilled || isNToNReadOnlyFilled));

					if (!isKeepFilter)
					{
						filter.EntityFilterList.RemoveAt(i);
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
