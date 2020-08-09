#region Imports

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
{
	[Serializable]
	public class EntityFilterArray : INotifyPropertyChanged
	{
		private int selectedFilterIndex;

		private ObservableCollection<EntityFilter> entityFilters =
			new ObservableCollection<EntityFilter>(new [] { new EntityFilter() });

		public int SelectedFilterIndex
		{
			get { return Math.Min(Math.Max(0, selectedFilterIndex), EntityFilters.Count - 1); }
			set
			{
				selectedFilterIndex = Math.Min(Math.Max(0, value), EntityFilters.Count - 1);
				OnPropertyChanged();
			}
		}

		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<EntityFilter> EntityFilters
		{
			get { return entityFilters; }
			set
			{
				entityFilters = value;
				OnPropertyChanged();
			}
		}

		public EntityFilter GetSelectedFilter()
		{
			return EntityFilters[SelectedFilterIndex];
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
