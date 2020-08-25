#region Imports

using System;

#endregion

namespace Yagasoft.CrmCodeGenerator.Models.Mapping
{
	[Serializable]
	public class MappingImage
	{
		public bool? CanStoreFullImage { get; set; }
		public short? MaxWidth { get; set; }
		public short? MaxHeight { get; set; }
		public int? MaxSizeInKb { get; set; }
	}
}
