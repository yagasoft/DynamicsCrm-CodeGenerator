#region Imports

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

#endregion

namespace CrmCodeGenerator.VSPackage.Model.OldSettings2
{
	[Serializable]
	public class EntityProfilesHeaderSelector_old2 : INotifyPropertyChanged
	{
		private int selectedFilterIndex;

		private ObservableCollection<EntityProfilesHeader_old2> entityProfilesHeaders =
			new ObservableCollection<EntityProfilesHeader_old2>(new [] { new EntityProfilesHeader_old2() });

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
		public ObservableCollection<EntityProfilesHeader_old2> EntityProfilesHeaders
		{
			get => entityProfilesHeaders;
			set
			{
				entityProfilesHeaders = value;
				OnPropertyChanged();
			}
		}

		public EntityProfilesHeader_old2 GetSelectedFilter()
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
