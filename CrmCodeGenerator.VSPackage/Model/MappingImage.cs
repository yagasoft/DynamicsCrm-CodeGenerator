#region Imports

using System;
using System.Collections.Generic;
using System.Linq;
using CrmCodeGenerator.VSPackage.Helpers;
using Microsoft.Xrm.Sdk.Metadata;

#endregion

namespace CrmCodeGenerator.VSPackage.Model
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
