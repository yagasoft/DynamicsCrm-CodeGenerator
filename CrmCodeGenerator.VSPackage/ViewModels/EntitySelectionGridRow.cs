using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace CrmCodeGenerator.VSPackage.ViewModels
{
	public class EntitySelectionGridRow : EntityGridRow
	{
		public bool IsJsEarly
		{
			get => isJsEarly;
			set
			{
				isJsEarly = value;
				OnPropertyChanged();
			}
		}

		public bool IsEntityFiltered
		{
			get => isEntityFiltered;
			set
			{
				isEntityFiltered = value;
				OnPropertyChanged();

				if (!value && IsLinkToContracts)
				{
					IsLinkToContracts = false;
				}
			}
		}

		public bool IsLinkToContracts
		{
			get => isLinkToContracts;
			set
			{
				isLinkToContracts = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsApplyFilterEnabled));

				if (value && !IsEntityFiltered)
				{
					IsEntityFiltered = true;
				}
			}
		}

		public IEnumerable<string> ActionNames
		{
			get => actionNames;
			set
			{
				actionNames = value;
				OnPropertyChanged();
			}
		}

		public IEnumerable<string> SelectedActions
		{
			get => selectedActions;
			set
			{
				selectedActions = value;
				OnPropertyChanged();
				OnPropertyChanged("ActionColour");
				OnPropertyChanged("ActionCount");
			}
		}

		public bool IsApplyFilterEnabled => !IsLinkToContracts;
		public Brush ActionColour => SelectedActions?.Any() == true ? Brushes.Red : Brushes.Black;

		public string ActionCount
		{
			get
			{
				var count = SelectedActions?.Count();
				return count > 0 ? count.ToString() : "-";
			}
		}

		private bool isJsEarly;
		private bool isEntityFiltered;
		private bool isLinkToContracts;
		private IEnumerable<string> actionNames;
		private IEnumerable<string> selectedActions;
	}
}