using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CrmCodeGenerator.VSPackage.Model.OldSettings
{
	[Serializable]
	public class EntityFilter_old : INotifyPropertyChanged
	{
		private string prefix;
		private string suffix;

		public string Prefix
		{
			get { return prefix; }
			set
			{
				prefix = value;
				OnPropertyChanged("DisplayName");
			}
		}

		public string Suffix
		{
			get { return suffix; }
			set
			{
				suffix = value;
				OnPropertyChanged("DisplayName");
			}
		}

		public bool IsDefault { get; set; } = true;

		public string DisplayName => Prefix + "[EntityName]" + Suffix;

		public List<EntityDataFilter_old> EntityFilterList = new List<EntityDataFilter_old>();

		public EntityFilter_old(string prefix = "", string suffix = "Contract")
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
