#region Imports

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Obsolete("Old Settings class used only for migration.", false)]
	[Serializable]
	public class SettingsArray
	{
		private int selectedSettingsIndex;
		private ObservableCollection<Settings> settingsList = new ObservableCollection<Settings>();

		public int SelectedSettingsIndex
		{
			get => selectedSettingsIndex;
			set => selectedSettingsIndex = value;
		}

		public ObservableCollection<Settings> SettingsList
		{
			get => settingsList;
			set => settingsList = value;
		}

		public Settings GetSelectedSettings()
		{
			return SettingsList[SelectedSettingsIndex];
		}
	}
}
