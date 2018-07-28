#region Imports

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using CrmCodeGenerator.VSPackage.Helpers;
using CrmCodeGenerator.VSPackage.Model;
using EnvDTE;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Binder = CrmCodeGenerator.VSPackage.Helpers.Binder;

#endregion

namespace CrmCodeGenerator.VSPackage
{
	public class Configuration
	{
		public static string FileName;

		public static SettingsArray LoadConfigs()
		{
			try
			{
				Status.Update("Reading settings ... ", false);

				var dte = Package.GetGlobalService(typeof (SDTE)) as DTE;
				var project = dte.GetSelectedProject();
				var file = project.GetProjectDirectory() + "\\" + FileName + ".dat";

				var newLine = false;

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

							var pending = workspace.GetPendingChanges(new[] {file});

							if (!pending.Any())
							{
								workspace.Get(new[] { file }, VersionSpec.Latest, RecursionType.Full, GetOptions.GetAll | GetOptions.Overwrite);
								Status.Update("\n\tRetrieved latest settings file from TFS' current workspace.");
								newLine = true;
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
						var bformatter = new BinaryFormatter {Binder = new Binder()};
						stream.Position = 0;
						var settingsObject = bformatter.Deserialize(stream);

						SettingsArray settings;

						if (settingsObject is SettingsArray)
						{
							settings = (SettingsArray) settingsObject;
						}
						else if (settingsObject is Settings.SerializableSettings)
						{
							Status.Update("loading old formatted settings ... ", false);

							settings = new SettingsArray();
							settings.SettingsList.Add(
								new Settings((Settings.SerializableSettings) settingsObject));
							settings.SelectedSettingsIndex = 0;
						}
						else
						{
							throw new Exception("Invalid settings format.");
						}

						Status.Update(newLine ? "Done reading settings!" : "done!");

						return settings;
					}
				}
				else
				{
					Status.Update("[ERROR] settings file does not exist.");
				}
			}
			catch (Exception ex)
			{
				Status.Update("failed to read settings => " + ex.Message);
			}

			var newSettings = new SettingsArray();
			newSettings.SettingsList.Add(new Settings());
			newSettings.SelectedSettingsIndex = 0;
			return newSettings;
		}

		public static void SaveConfigs(SettingsArray settings)
		{
			Status.Update("Writing settings ... ", false);

			var dte = Package.GetGlobalService(typeof (SDTE)) as DTE;
			var project = dte.GetSelectedProject();
			var file = project.GetProjectDirectory() + "\\" + FileName + ".dat";

			var newLine = false;

			if (!File.Exists(file))
			{
				File.Create(file).Dispose();
				project.ProjectItems.AddFromFile(file);
				Status.Update("\n\tCreated a new settings file.");
				newLine = true;
			}

			// check out file if in TFS
			try
			{
				var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(file);

				if (workspaceInfo != null)
				{
					var server = new TfsTeamProjectCollection(workspaceInfo.ServerUri);
					var workspace = workspaceInfo.GetWorkspace(server);

					var pending = workspace.GetPendingChanges(new[] {file});

					if (!pending.Any())
					{
						workspace.Get(new[] { file }, VersionSpec.Latest, RecursionType.Full, GetOptions.GetAll | GetOptions.Overwrite);
						Status.Update((newLine ? "" : "\n") + "\tRetrieved latest settings file from TFS' current workspace.");

						workspace.PendEdit(file);
						Status.Update("\tChecked out settings file from TFS' current workspace.");

						newLine = true;
					}
				}
			}
			catch (Exception)
			{
				// ignored
			}

			using (var stream = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
			{
				// clear the file to start from scratch
				stream.SetLength(0);

				var bformatter = new BinaryFormatter {Binder = new Binder()};
				bformatter.Serialize(stream, settings);

				Status.Update(newLine ? "Done writing settings!" : "done!");
			}
		}
	}
}
