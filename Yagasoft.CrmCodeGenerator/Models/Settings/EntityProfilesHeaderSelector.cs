#region Imports

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Settings
{
	[Serializable]
	public class EntityProfilesHeaderSelector : INotifyPropertyChanged
	{
		private int selectedFilterIndex;

		private ObservableCollection<EntityProfilesHeader> entityProfilesHeaders =
			new ObservableCollection<EntityProfilesHeader>(new [] { new EntityProfilesHeader() });

		public int SelectedFilterIndex
		{
			get => Math.Min(Math.Max(0, selectedFilterIndex), EntityProfilesHeaders.Count - 1);
			set
			{
				selectedFilterIndex = Math.Min(Math.Max(0, value), EntityProfilesHeaders.Count - 1);
				OnPropertyChanged();
			}
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<EntityProfilesHeader> EntityProfilesHeaders
		{
			get => entityProfilesHeaders;
			set
			{
				entityProfilesHeaders = value;
				OnPropertyChanged();
			}
		}

		public EntityProfilesHeader GetSelectedFilter()
		{
			return EntityProfilesHeaders[SelectedFilterIndex];
		}

		#region Property events

		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion
	}
}
