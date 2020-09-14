using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Microsoft.Xrm.Sdk.Metadata;
using Yagasoft.CrmCodeGenerator.Models.Settings;

namespace CrmCodeGenerator.VSPackage.ViewModels
{
	public class GridRow : INotifyPropertyChanged
	{
		public string DisplayName { get; set; }

		public virtual Brush Colour => IsFiltered ? Brushes.Red : Brushes.Black;

		public bool IsFiltered => EntityProfile?.IsBasicDataFilled == true;

		public EntityProfile OriginalEntityProfile { get; set; }

		public EntityProfile EntityProfile
		{
			get => entityProfile;
			set
			{
				entityProfile = value;
				OnPropertyChanged();
				OnPropertyChanged("Colour");
			}
		}

		public EntityMetadata Entity
		{
			get => entity;
			set
			{
				entity = value;
				OnPropertyChanged();
			}
		}

		public bool IsSelected
		{
			get => isSelected;
			set
			{
				isSelected = value;
				OnPropertyChanged();
			}
		}

		public string Name { get; set; }

		public string Rename
		{
			get => rename;
			set
			{
				rename = value;
				OnPropertyChanged();
			}
		}

		public string Annotations
		{
			get => annotations;
			set
			{
				annotations = value;
				OnPropertyChanged();
			}
		}

		private bool isFiltered;
		private string rename;
		private string annotations;
		private EntityMetadata entity;
		private EntityProfile entityProfile;
		private bool isSelected;

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}