using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrmCodeGenerator.VSPackage.Model.OldSettings2
{
	[Serializable]
	public class EntityProfilesHeader_old2 : INotifyPropertyChanged
	{
		private string prefix;
		private string suffix;

		public string Prefix
		{
			get => prefix;
			set
			{
				prefix = value;
				OnPropertyChanged("DisplayName");
			}
		}

		public string Suffix
		{
			get => suffix;
			set
			{
				suffix = value;
				OnPropertyChanged("DisplayName");
			}
		}

		public string DisplayName => Prefix + "[EntityName]" + Suffix;

		public List<EntityProfile_old2> EntityProfiles = new List<EntityProfile_old2>();

		public EntityProfilesHeader_old2(string prefix = "", string suffix = "Contract")
		{
			Prefix = prefix;
			Suffix = suffix;
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
