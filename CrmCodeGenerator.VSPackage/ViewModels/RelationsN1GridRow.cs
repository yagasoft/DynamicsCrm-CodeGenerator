namespace CrmCodeGenerator.VSPackage.ViewModels
{
	public class RelationsN1GridRow : EntityFilterGridRow
	{
		private bool isFlatten;

		public string ToEntity { get; set; }
		public string FromField { get; set; }

		public bool IsFlatten
		{
			get => isFlatten;
			set
			{
				isFlatten = value;
				OnPropertyChanged();
			}
		}
	}
}