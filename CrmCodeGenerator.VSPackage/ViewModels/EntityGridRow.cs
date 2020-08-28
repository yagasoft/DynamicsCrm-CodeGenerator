using System.Windows.Media;

namespace CrmCodeGenerator.VSPackage.ViewModels
{
	public class EntityGridRow : GridRow
	{
		public bool IsGenerateMeta
		{
			get => isGenerateMeta;
			set
			{
				isGenerateMeta = value;
				OnPropertyChanged();
			}
		}

		public bool IsOptionsetLabels
		{
			get => isOptionsetLabels;
			set
			{
				isOptionsetLabels = value;
				OnPropertyChanged();
			}
		}

		public bool IsLookupLabels
		{
			get => isLookupLabels;
			set
			{
				isLookupLabels = value;
				OnPropertyChanged();
			}
		}

		private bool isGenerateMeta;
		private bool isOptionsetLabels;
		private bool isLookupLabels;
	}
}